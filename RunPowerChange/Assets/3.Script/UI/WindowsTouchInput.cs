using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.EventSystems;

// Unity 2022.3.0~2022.3.60 Windows 버그(UUM-99077) 우회.
// 모니터를 여러 대 쓸 때 주모니터가 아닌 터치모니터의 터치가 주모니터 좌표로 들어가, 터치한 곳이 아닌 엉뚱한 곳이 눌린다.
// 마우스는 보조 모니터에서도 위치가 맞는다 (Input.mousePosition 은 주모니터 창 기준으로 화면 밖까지 이어지는 좌표이고,
// UGUI 가 Display.RelativeMouseAt 으로 어느 모니터인지 찾는다).
// 그래서 Windows 가 보내는 터치(WM_POINTER) 메시지를 직접 받아 실제 화면 좌표로 터치를 만들고, 마우스와 같은 좌표 방식으로 바꿔 UI 에 넘긴다.
//
// EventSystem 오브젝트에 붙이면 StandaloneInputModule.inputOverride 로 끼워 들어간다. 에디터에서는 Unity 터치를 그대로 쓴다.
// gameSettingData.json useWindowsTouchFix(설정창 토글)로 끄면 Unity 터치를 그대로 쓴다.
// Unity 를 2022.3.61f1 이상으로 올리면 필요 없다.
[RequireComponent(typeof(StandaloneInputModule))]
public class WindowsTouchInput : BaseInput
{
    private const string SettingField = "useWindowsTouchFix";

    // ── Windows API ─────────────────────────────────────
    private const int WH_GETMESSAGE = 3;
    private const int PM_REMOVE = 1;
    private const uint WM_POINTERUPDATE = 0x0245;
    private const uint WM_POINTERDOWN = 0x0246;
    private const uint WM_POINTERUP = 0x0247;
    private const uint WM_POINTERCAPTURECHANGED = 0x024C;
    private const int PT_TOUCH = 2;
    private const uint MONITORINFOF_PRIMARY = 1;

    [StructLayout(LayoutKind.Sequential)] private struct POINT { public int x, y; }
    [StructLayout(LayoutKind.Sequential)] private struct RECT { public int left, top, right, bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MSG
    {
        public IntPtr hwnd; public uint message; public IntPtr wParam; public IntPtr lParam; public uint time; public POINT pt; public uint lPrivate;
    }
    [StructLayout(LayoutKind.Sequential)] private struct MONITORINFO { public int cbSize; public RECT rcMonitor; public RECT rcWork; public uint dwFlags; }

    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool GetPointerType(uint pointerId, out int pointerType);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint flags);
    [DllImport("user32.dll")] private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

    // ── 훅에서 받은 터치 (훅은 창 스레드에서 불리므로 잠가서 넘긴다) ──
    private enum Kind { Down, Move, Up }
    private struct PointerEvent { public Kind kind; public uint id; public int x, y; }
    private const int NoPosition = int.MinValue;

    private static readonly List<PointerEvent> queue = new List<PointerEvent>();
    private static readonly HookProc hookProc = OnGetMessage;   // GC 에 수거되지 않게 붙잡아 둔다
    private static readonly List<IntPtr> hooks = new List<IntPtr>();
    private static int instances;

    // 지금 눌려 있는 터치 (pointerId 별 화면 좌표)
    private class Contact { public uint id; public Vector2 screen; public Vector2 lastPosition; public bool isNew; public bool upPending; }
    private readonly Dictionary<uint, Contact> contacts = new Dictionary<uint, Contact>();
    private readonly List<Touch> touches = new List<Touch>();
    private readonly List<PointerEvent> drained = new List<PointerEvent>();
    private int builtFrame = -1;
    private IntPtr mainWindow;
    private bool settingEnabled = true;

    // 우회가 실제로 동작 중인지 (빌드 + 훅 설치 + 설정 켜짐)
    public bool Active => hooks.Count > 0 && settingEnabled;

    protected override void Awake()
    {
        base.Awake();
        GetComponent<StandaloneInputModule>().inputOverride = this;
    }

    private void Start()
    {
        var data = JsonManager.instance != null ? JsonManager.instance.gameSettingData : null;
        if (data != null) settingEnabled = data.useWindowsTouchFix;
        var panel = FindObjectOfType<SettingsPanelUI>(true);
        if (panel != null) panel.OnSettingChanged += HandleSettingChanged;

        if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer) return;
        instances++;
        InstallHooks();
        Debug.Log($"[Touch] Windows 터치 보정 {(Active ? "켜짐" : settingEnabled ? "훅 설치 실패 - Unity 터치 사용" : "꺼짐 (설정)")}");
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        var panel = FindObjectOfType<SettingsPanelUI>(true);
        if (panel != null) panel.OnSettingChanged -= HandleSettingChanged;
        if (Application.isEditor || Application.platform != RuntimePlatform.WindowsPlayer) return;
        if (--instances <= 0) RemoveHooks();
    }

    private void HandleSettingChanged(string field, bool value)
    {
        if (field != SettingField) return;
        settingEnabled = value;
        contacts.Clear();
        Debug.Log($"[Touch] Windows 터치 보정 {(Active ? "켜짐" : "꺼짐")}");
    }

    // ── BaseInput: 우회가 켜져 있으면 Windows 에서 받은 터치를 넘긴다 ──
    public override bool touchSupported => Active || base.touchSupported;

    // 터치하면 Unity 가 같은 위치(틀린 좌표)에 마우스 클릭도 만든다. 그대로 두면 보조 모니터를 터치했을 때
    // 주모니터의 같은 자리 버튼이 눌리므로, 터치 중과 터치 직후 잠깐은 마우스 버튼을 UI 에 넘기지 않는다.
    private const float MouseAfterTouchSeconds = 0.5f;
    private float lastTouchTime = -10f;

    private bool SuppressMouse
    {
        get
        {
            if (!Active) return false;
            Build();
            return contacts.Count > 0 || Time.unscaledTime - lastTouchTime < MouseAfterTouchSeconds;
        }
    }

    public override bool GetMouseButtonDown(int button) => !SuppressMouse && base.GetMouseButtonDown(button);
    public override bool GetMouseButtonUp(int button) => !SuppressMouse && base.GetMouseButtonUp(button);
    public override bool GetMouseButton(int button) => !SuppressMouse && base.GetMouseButton(button);

    public override int touchCount
    {
        get
        {
            if (!Active) return base.touchCount;
            Build();
            return touches.Count;
        }
    }

    public override Touch GetTouch(int index)
    {
        if (!Active) return base.GetTouch(index);
        Build();
        return touches[index];
    }

    // 프레임마다 한 번, 훅에서 쌓인 터치 신호로 이번 프레임 터치 목록을 만든다.
    // 누르고 떼는 신호가 한 프레임에 같이 와도 Began 을 먼저 보내고 다음 프레임에 Ended 를 보내 클릭이 빠지지 않게 한다.
    private void Build()
    {
        if (builtFrame == Time.frameCount) return;
        builtFrame = Time.frameCount;

        drained.Clear();
        lock (queue)
        {
            drained.AddRange(queue);
            queue.Clear();
        }

        if (drained.Count > 0 || contacts.Count > 0) lastTouchTime = Time.unscaledTime;
        foreach (var e in drained)
        {
            contacts.TryGetValue(e.id, out Contact c);
            var screen = new Vector2(e.x, e.y);
            switch (e.kind)
            {
                case Kind.Down:
                    contacts[e.id] = new Contact { id = e.id, screen = screen, isNew = true };
                    break;
                case Kind.Move:
                    if (c != null && !c.upPending) c.screen = screen;
                    break;
                case Kind.Up:
                    if (c == null) break;
                    if (e.x != NoPosition) c.screen = screen;
                    c.upPending = true;
                    break;
            }
        }

        touches.Clear();
        var finished = new List<uint>();
        foreach (var c in contacts.Values)
        {
            Vector2 position = ToMousePosition(c.screen);
            TouchPhase phase;
            if (c.isNew) { phase = TouchPhase.Began; c.isNew = false; c.lastPosition = position; }
            else if (c.upPending) { phase = TouchPhase.Ended; finished.Add(c.id); }
            else phase = (position - c.lastPosition).sqrMagnitude > 0.01f ? TouchPhase.Moved : TouchPhase.Stationary;

            touches.Add(new Touch
            {
                fingerId = (int)c.id,
                position = position,
                rawPosition = position,
                deltaPosition = position - c.lastPosition,
                phase = phase,
                tapCount = 1,
                type = TouchType.Direct,
                pressure = 1f,
                maximumPossiblePressure = 1f,
            });
            c.lastPosition = position;
        }
        foreach (var id in finished) contacts.Remove(id);
    }

    // 화면 좌표(픽셀, 왼쪽 위 기준) → Input.mousePosition 과 같은 좌표 (주모니터 창 왼쪽 아래 기준, 다른 모니터는 창 밖으로 이어짐)
    private Vector2 ToMousePosition(Vector2 screen)
    {
        if (mainWindow == IntPtr.Zero || !GetClientRect(mainWindow, out RECT rect)) return screen;
        var origin = new POINT();
        ClientToScreen(mainWindow, ref origin);
        float height = rect.bottom - rect.top;
        // Unity 마우스 좌표와 같게 맨 아래 픽셀이 y=0 (빌드에서 마우스 좌표와 비교해 확인)
        return new Vector2(screen.x - origin.x, height - 1 - (screen.y - origin.y));
    }

    // ── 훅 ──────────────────────────────────────────────

    private void InstallHooks()
    {
        if (hooks.Count > 0) return;
        var windows = UnityAlwaysOnTop.GetProcessTopLevelWindows();
        mainWindow = FindMainWindow(windows);

        var threads = new HashSet<uint>();
        foreach (var w in windows) threads.Add(GetWindowThreadProcessId(w, out _));
        foreach (uint thread in threads)
        {
            IntPtr hook = SetWindowsHookEx(WH_GETMESSAGE, hookProc, IntPtr.Zero, thread);
            if (hook != IntPtr.Zero) hooks.Add(hook);
            else Debug.LogWarning($"[Touch] 터치 훅 설치 실패 (스레드 {thread}, 오류 {Marshal.GetLastWin32Error()})");
        }
    }

    private static void RemoveHooks()
    {
        foreach (var h in hooks) UnhookWindowsHookEx(h);
        hooks.Clear();
    }

    // Unity Display 1 은 Windows 주모니터다. 주모니터에 있는 창을 기준 창으로 삼는다.
    private static IntPtr FindMainWindow(List<IntPtr> windows)
    {
        foreach (var w in windows)
        {
            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            if (GetMonitorInfo(MonitorFromWindow(w, 2), ref info) && (info.dwFlags & MONITORINFOF_PRIMARY) != 0) return w;
        }
        return windows.Count > 0 ? windows[0] : IntPtr.Zero;
    }

    [AOT.MonoPInvokeCallback(typeof(HookProc))]
    private static IntPtr OnGetMessage(int code, IntPtr wParam, IntPtr lParam)
    {
        // 메시지를 큐에서 꺼낼 때(PM_REMOVE)만 본다. 미리 보기(PM_NOREMOVE)까지 보면 같은 터치가 두 번 들어온다.
        if (code >= 0 && wParam.ToInt64() == PM_REMOVE)
        {
            var msg = Marshal.PtrToStructure<MSG>(lParam);
            if (msg.message >= WM_POINTERUPDATE && msg.message <= WM_POINTERUP || msg.message == WM_POINTERCAPTURECHANGED)
            {
                uint id = (uint)(msg.wParam.ToInt64() & 0xFFFF);
                if (GetPointerType(id, out int type) && type == PT_TOUCH || msg.message == WM_POINTERCAPTURECHANGED)
                {
                    long lp = msg.lParam.ToInt64();
                    bool cancel = msg.message == WM_POINTERCAPTURECHANGED;   // 이 메시지의 lParam 은 좌표가 아니다
                    var e = new PointerEvent
                    {
                        id = id,
                        x = cancel ? NoPosition : (short)(lp & 0xFFFF),
                        y = cancel ? NoPosition : (short)((lp >> 16) & 0xFFFF),
                        kind = msg.message == WM_POINTERDOWN ? Kind.Down : msg.message == WM_POINTERUPDATE ? Kind.Move : Kind.Up,
                    };
                    lock (queue) queue.Add(e);
                }
            }
        }
        return CallNextHookEx(IntPtr.Zero, code, wParam, lParam);
    }
}
