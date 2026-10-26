using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 게임씬에 미리 만들어진 Canvas/UI 위에서 동작하는 로그 패널.
// Inspector에서 텍스트/스크롤뷰/버튼을 직접 연결한다.
//
// - 컨트롤러별 COM 드롭다운: 지금 PC의 포트 목록 + "사용 안 함". 고르면 persistent port.json 에 저장하고 그 포트를 다시 연다.
//   저장된 포트가 지금 PC에 없으면 "COMx (연결 안 됨)"으로 표시한다.
// - 재연결 버튼: 포트 목록을 새로 읽고 모든 포트를 다시 연다.
// - 수동 송신: 입력한 문자열을 열린 모든 포트로 보낸다.
public class SerialPortLogPanel : MonoBehaviour
{
    // port.json 의 컨트롤러 1개와 COM 드롭다운 1개를 연결하는 항목.
    [Serializable]
    public class PortSelector
    {
        public int controllerId = 1;
        public TMP_Dropdown dropdown;

        // 드롭다운 항목별 COM 이름 (null = 사용 안 함)
        [NonSerialized] public List<string> values = new List<string>();
    }

    private const string DisabledLabel = "사용 안 함";

    [Header("연결 상태 텍스트 (인덱스 0~4 = Ctrl 1~5)")]
    [SerializeField] private TMP_Text[] connectionStatusTexts;

    [Header("로그 표시")]
    [SerializeField] private TMP_Text logText;
    [SerializeField] private ScrollRect logScrollRect;

    [Header("버튼")]
    [SerializeField] private Button clearButton;
    [Tooltip("포트 목록 새로고침 + 모든 포트 다시 열기")]
    [SerializeField] private Button reconnectButton;

    [Header("포트 선택 (컨트롤러별 COM 드롭다운)")]
    [SerializeField] private List<PortSelector> portSelectors = new List<PortSelector>();

    [Header("수동 송신 (열린 모든 포트로)")]
    [SerializeField] private TMP_InputField sendInput;
    [SerializeField] private Button sendButton;

    [Header("토글 (ESC 키로 표시/숨김)")]
    [SerializeField] private GameObject togglePanel;
    [Tooltip("씬에 설정창(SettingsPanelUI)이 있으면 이 키 대신 설정창과 함께 열리고 닫힌다.")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [Tooltip("시작할 때 로그 창을 숨긴 상태로 둘지 여부.")]
    [SerializeField] private bool hideOnStart = true;

    [Header("동작 설정")]
    [SerializeField] private int maxLogLines = 200;

    [Header("상태 색상")]
    [SerializeField] private Color openColor = new Color(0.4f, 1f, 0.4f);
    [SerializeField] private Color closeColor = new Color(1f, 0.4f, 0.4f);
    [SerializeField] private Color disabledColor = new Color(0.7f, 0.7f, 0.7f);

    private readonly Queue<string> logLines = new Queue<string>();
    private readonly StringBuilder builder = new StringBuilder();

    private SerialPortManager manager;
    private bool subscribed;

    private void Start()
    {
        if (clearButton != null) clearButton.onClick.AddListener(ClearLog);
        if (reconnectButton != null) reconnectButton.onClick.AddListener(Reconnect);
        if (sendButton != null) sendButton.onClick.AddListener(SendManual);
        if (sendInput != null) sendInput.onSubmit.AddListener(_ => SendManual());
        foreach (var selector in portSelectors)
        {
            if (selector?.dropdown == null) continue;
            var captured = selector;
            selector.dropdown.onValueChanged.AddListener(index => OnPortSelected(captured, index));
        }
        RefreshPortOptions();
        TrySubscribe();
        RefreshAllStatus();

        if (!SettingsPanelUI.Exists)
            ShortcutHelp.Register(this, toggleKey, "RS232 디버그 패널 열기/닫기", $"모든 화면, {OperatorDisplay.DisplayName}에 표시");

        if (hideOnStart && togglePanel != null)
            togglePanel.SetActive(false);

        // 설정창과 같은 키로 함께 열리고 닫힌다
        SettingsPanelUI.PanelVisibilityChanged += HandleSettingsVisibility;
        if (SettingsPanelUI.Exists) HandleSettingsVisibility(SettingsPanelUI.IsVisible);
    }

    private void HandleSettingsVisibility(bool show)
    {
        if (togglePanel != null) togglePanel.SetActive(show);
    }

    private void Update()
    {
        // SerialPortManager.Instance가 늦게 만들어지는 경우 대비
        if (!subscribed) TrySubscribe();

        if (!SettingsPanelUI.Exists && Input.GetKeyDown(toggleKey) && togglePanel != null)
        {
            togglePanel.SetActive(!togglePanel.activeSelf);
        }
    }

    private void OnDestroy()
    {
        if (clearButton != null) clearButton.onClick.RemoveListener(ClearLog);
        if (reconnectButton != null) reconnectButton.onClick.RemoveListener(Reconnect);
        if (sendButton != null) sendButton.onClick.RemoveListener(SendManual);
        if (sendInput != null) sendInput.onSubmit.RemoveAllListeners();
        foreach (var selector in portSelectors)
            if (selector?.dropdown != null) selector.dropdown.onValueChanged.RemoveAllListeners();
        ShortcutHelp.Unregister(this);
        SettingsPanelUI.PanelVisibilityChanged -= HandleSettingsVisibility;
        if (!subscribed || manager == null) return;
        manager.OnDataReceived -= HandleReceived;
        manager.OnDataSent -= HandleSent;
        manager.OnConnectionChanged -= HandleConnectionChanged;
        manager.OnInterpreted -= HandleInterpreted;
        subscribed = false;
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        manager = SerialPortManager.Instance;
        if (manager == null) return;

        manager.OnDataReceived += HandleReceived;
        manager.OnDataSent += HandleSent;
        manager.OnConnectionChanged += HandleConnectionChanged;
        manager.OnInterpreted += HandleInterpreted;
        subscribed = true;
        RefreshAllStatus();
    }

    private void HandleReceived(int id, string data) => AppendLog($"[Receive][Ctrl {id}] {data}");
    private void HandleSent(int id, string data) => AppendLog($"[Send][Ctrl {id}] {data}");
    private void HandleInterpreted(int id, string message) => AppendLog($"[Parse][Ctrl {id}] {message}");

    private void HandleConnectionChanged(int id, bool isOpen)
    {
        UpdateStatus(id, isOpen, IsEnabled(id));
        AppendLog($"[Connect][Ctrl {id}] {(isOpen ? "OPEN" : "CLOSE")}");
    }

    private bool IsEnabled(int controllerId)
    {
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson == null || portJson.ports == null) return true;
        foreach (var cfg in portJson.ports)
        {
            if (cfg.controllerId == controllerId) return cfg.enabled;
        }
        return true;
    }

    private void RefreshAllStatus()
    {
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson == null || portJson.ports == null) return;

        foreach (var cfg in portJson.ports)
        {
            bool open = manager != null && manager.IsControllerOpen(cfg.controllerId);
            UpdateStatus(cfg.controllerId, open, cfg.enabled);
        }
    }

    private void UpdateStatus(int controllerId, bool isOpen, bool enabled)
    {
        if (connectionStatusTexts == null) return;
        int idx = controllerId - 1;
        if (idx < 0 || idx >= connectionStatusTexts.Length) return;
        var t = connectionStatusTexts[idx];
        if (t == null) return;

        string com = "";
        int baud = 0;
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson != null && portJson.ports != null)
        {
            foreach (var cfg in portJson.ports)
            {
                if (cfg.controllerId == controllerId)
                {
                    com = cfg.com;
                    baud = cfg.baudLate;
                    break;
                }
            }
        }

        string state;
        Color color;
        if (!enabled) { state = "DISABLED"; color = disabledColor; }
        else if (isOpen) { state = "OPEN"; color = openColor; }
        else { state = "CLOSE"; color = closeColor; }

        t.text = $"Ctrl {controllerId}  {com}  {baud}bps  [{state}]";
        t.color = color;
    }

    private void AppendLog(string line)
    {
        string formatted = $"{DateTime.Now:HH:mm:ss} {line}";
        logLines.Enqueue(formatted);
        while (logLines.Count > maxLogLines) logLines.Dequeue();

        builder.Clear();
        foreach (var l in logLines) builder.AppendLine(l);
        if (logText != null) logText.text = builder.ToString();

        // ContentSizeFitter 재계산 후, Content가 Viewport를 넘었을 때만 끝으로 스크롤.
        // 항상 0으로 강제하면 로그가 적어 Content가 Viewport보다 짧을 때 위로 튕겨 올라가는 현상이 생긴다.
        if (logScrollRect != null && logScrollRect.content != null && logScrollRect.viewport != null)
        {
            Canvas.ForceUpdateCanvases();
            if (logScrollRect.content.rect.height > logScrollRect.viewport.rect.height)
            {
                logScrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }

    // ── 포트 선택 ───────────────────────────────────────

    private static PortConfig FindPortConfig(int controllerId)
    {
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson?.ports == null) return null;
        foreach (var cfg in portJson.ports)
            if (cfg.controllerId == controllerId) return cfg;
        return null;
    }

    // 지금 PC의 COM 목록으로 드롭다운을 다시 채우고 현재 저장값을 선택해 둔다.
    private void RefreshPortOptions()
    {
        string[] names;
        try { names = SerialPort.GetPortNames(); }
        catch (Exception ex)
        {
            Debug.LogWarning($"COM 포트 목록 읽기 실패: {ex.Message}");
            names = new string[0];
        }
        Array.Sort(names, StringComparer.OrdinalIgnoreCase);

        foreach (var selector in portSelectors)
        {
            if (selector?.dropdown == null) continue;
            var cfg = FindPortConfig(selector.controllerId);

            selector.values.Clear();
            var labels = new List<string>();
            selector.values.Add(null);
            labels.Add(DisabledLabel);
            foreach (var n in names)
            {
                selector.values.Add(n);
                labels.Add(n);
            }

            int selected = 0;
            if (cfg != null && cfg.enabled && !string.IsNullOrEmpty(cfg.com))
            {
                selected = selector.values.FindIndex(v => v != null && string.Equals(v, cfg.com, StringComparison.OrdinalIgnoreCase));
                if (selected < 0)
                {
                    selector.values.Add(cfg.com);
                    labels.Add($"{cfg.com} (연결 안 됨)");
                    selected = selector.values.Count - 1;
                }
            }

            selector.dropdown.ClearOptions();
            selector.dropdown.AddOptions(labels);
            selector.dropdown.SetValueWithoutNotify(selected);
            selector.dropdown.RefreshShownValue();
        }
    }

    private void OnPortSelected(PortSelector selector, int index)
    {
        if (index < 0 || index >= selector.values.Count) return;
        var jm = JsonManager.instance;
        if (jm == null) return;

        var cfg = FindPortConfig(selector.controllerId);
        if (cfg == null)
        {
            cfg = new PortConfig { controllerId = selector.controllerId, baudLate = 19200 };
            jm.portJson.ports.Add(cfg);
        }

        string com = selector.values[index];
        cfg.enabled = com != null;
        if (com != null) cfg.com = com;
        jm.SavePortJson();
        AppendLog($"[Port][Ctrl {selector.controllerId}] {(com ?? DisabledLabel)} 저장 - 다시 연결");

        if (manager != null) manager.ReopenController(selector.controllerId);
        RefreshPortOptions();
        RefreshAllStatus();
    }

    // 포트 목록 새로고침 + 모든 포트 다시 열기
    public void Reconnect()
    {
        AppendLog("[Port] 포트 목록 새로고침, 모든 포트 다시 연결");
        RefreshPortOptions();
        if (manager != null) manager.ReconnectAll();
        RefreshAllStatus();
    }

    // ── 수동 송신 ───────────────────────────────────────

    public void SendManual()
    {
        if (sendInput == null) return;
        string message = sendInput.text.Trim();
        if (message.Length == 0) return;

        if (manager == null || !AnyPortOpen())
        {
            AppendLog($"[Send] 열린 포트 없음 - 보내지 않음: {message}");
            return;
        }
        manager.SendDataToAll(message);
        sendInput.text = string.Empty;
        sendInput.ActivateInputField();
    }

    private bool AnyPortOpen()
    {
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson?.ports == null) return false;
        foreach (var cfg in portJson.ports)
            if (manager.IsControllerOpen(cfg.controllerId)) return true;
        return false;
    }

    public void ClearLog()
    {
        logLines.Clear();
        if (logText != null) logText.text = string.Empty;
    }
}
