using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// ESC 키로 열고 닫는 런타임 설정창.
// gameSettingData.json 의 bool 값을 토글로 노출하고, 바뀌면 즉시 적용 + 저장한다.
// UI는 런타임에 생성하지 않고 씬에 오브젝트로 만들어 인스펙터에서 연결한다.
//
// [bool 설정을 추가하는 방법]
//  1) JsonManager.GameSettingData 에 public bool 필드를 추가한다.
//  2) 씬의 설정창에 Toggle 오브젝트를 하나 만든다.
//  3) 아래 boolSettings 목록에 (필드 이름, Toggle)을 연결한다.
// 저장/복원은 리플렉션으로 처리되므로 이 스크립트를 고칠 필요가 없다.
//
// [숫자 설정을 추가하는 방법] (Setting.json = GameDynamicData 의 float/int 필드)
//  1) 씬의 설정창에 TMP_InputField 를 하나 만든다.
//  2) 아래 numberSettings 목록에 (필드 이름, InputField, 최소/최대)를 연결한다.
//     목록 안의 값은 경로로 적는다 (예: voltages[1].facilityCostWon, wheels[0].displayIndex).
// 입력을 마치면 범위를 맞춰 persistentDataPath/Setting.json 에 바로 저장한다.
public class SettingsPanelUI : MonoBehaviour
{
    // Setting.json 의 숫자 필드 1개와 씬의 InputField 1개를 연결하는 항목.
    [Serializable]
    public class NumberSetting
    {
        [Tooltip("GameDynamicData 안의 float/int 필드 이름 또는 경로 (예: runSeconds, voltages[1].lossRate)")]
        public string fieldName;
        public TMP_InputField input;
        public float min = 0f;
        public float max = 9999f;

        [NonSerialized] public FieldInfo field;
        [NonSerialized] public UnityAction<string> handler;
    }

    // gameSettingData.json 의 bool 필드 1개와 씬의 Toggle 1개를 연결하는 항목.
    [Serializable]
    public class BoolSetting
    {
        [Tooltip("GameSettingData 안의 bool 필드 이름 (예: useUnityOnTop)")]
        public string fieldName;
        public Toggle toggle;

        // 런타임에만 쓰는 값들 (직렬화하지 않는다)
        [NonSerialized] public FieldInfo field;
        [NonSerialized] public UnityAction<bool> handler;
    }

    // 스타트팩이 기본으로 즉시 반영해 주는 설정 필드 이름
    private const string UseUnityOnTopField = "useUnityOnTop";
    private const string ShowMouseCursorField = "showMouseCursor";

    [Header("패널")]
    [Tooltip("ESC로 켜고 끌 설정창 루트 오브젝트. 이 스크립트는 항상 켜져 있는 오브젝트(캔버스 등)에 둔다.")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;
    [Tooltip("시작할 때 설정창을 숨긴 상태로 둘지 여부.")]
    [SerializeField] private bool hideOnStart = true;

    [Header("설정 토글 (gameSettingData.json 의 bool 필드)")]
    [SerializeField] private List<BoolSetting> boolSettings = new List<BoolSetting>();

    [Header("숫자 설정 (Setting.json 의 float/int 필드)")]
    [SerializeField] private List<NumberSetting> numberSettings = new List<NumberSetting>();

    [Header("적용 대상 (비워두면 자동 탐색)")]
    [SerializeField] private UnityAlwaysOnTop alwaysOnTop;

    // 설정이 바뀔 때 (필드 이름, 새 값)을 알린다. 프로젝트별 반영 처리는 여기에 붙인다.
    public event Action<string, bool> OnSettingChanged;
    // 숫자 설정이 바뀔 때 (필드 이름, 새 값)
    public event Action<string, float> OnNumberChanged;

    // 설정창이 열리고 닫힐 때 알린다. 디버그 패널(SerialPortLogPanel)이 같은 키로 함께 열리고 닫히도록 따라온다.
    public static event Action<bool> PanelVisibilityChanged;
    // 씬에 설정창이 있으면 디버그 패널은 자기 키를 쓰지 않고 설정창을 따른다.
    public static bool Exists => instanceCount > 0;
    public static bool IsVisible { get; private set; }
    private static int instanceCount;

    // 데이터 -> UI 반영 중에는 onValueChanged 콜백이 저장을 유발하지 않도록 막는다.
    private bool _syncing;

    private void Start()
    {
        if (alwaysOnTop == null)
            alwaysOnTop = FindObjectOfType<UnityAlwaysOnTop>();

        BindFields();
        BindNumberFields();
        SyncFromData();

        foreach (var setting in numberSettings)
        {
            if (setting == null || setting.field == null || setting.input == null) continue;

            var captured = setting;
            captured.handler = text => OnNumberEdited(captured, text);
            captured.input.onEndEdit.AddListener(captured.handler);
        }

        foreach (var setting in boolSettings)
        {
            if (setting == null || setting.field == null || setting.toggle == null) continue;

            var captured = setting;                 // 클로저가 항목별로 따로 잡히도록 지역 변수에 담는다
            captured.handler = value => OnToggleChanged(captured, value);
            captured.toggle.onValueChanged.AddListener(captured.handler);
        }

        if (hideOnStart)
            SetPanelVisible(false);

        ShortcutHelp.Register(this, toggleKey, "설정창 + RS232 디버그 패널 열기/닫기", $"모든 화면, {OperatorDisplay.DisplayName}에 표시");
    }

    private void OnEnable() => instanceCount++;
    private void OnDisable() => instanceCount--;

    private void OnDestroy()
    {
        ShortcutHelp.Unregister(this);

        foreach (var setting in boolSettings)
        {
            if (setting == null || setting.toggle == null || setting.handler == null) continue;

            setting.toggle.onValueChanged.RemoveListener(setting.handler);
            setting.handler = null;
        }

        foreach (var setting in numberSettings)
        {
            if (setting == null || setting.input == null || setting.handler == null) continue;

            setting.input.onEndEdit.RemoveListener(setting.handler);
            setting.handler = null;
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && panelRoot != null)
            SetPanelVisible(!panelRoot.activeSelf);
    }

    // 인스펙터에 적은 필드 이름을 GameSettingData 의 bool 필드와 연결한다.
    // 오타·타입 불일치·연결 누락은 시작 시점에 로그로 알려준다.
    private void BindFields()
    {
        var type = typeof(GameSettingData);
        var bound = new HashSet<string>();

        foreach (var setting in boolSettings)
        {
            if (setting == null) continue;
            setting.field = null;

            if (string.IsNullOrEmpty(setting.fieldName))
            {
                Debug.LogWarning("[Settings] 필드 이름이 비어 있는 항목이 있습니다.", this);
                continue;
            }

            var field = type.GetField(setting.fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"[Settings] GameSettingData 에 '{setting.fieldName}' 필드가 없습니다.", this);
                continue;
            }
            if (field.FieldType != typeof(bool))
            {
                Debug.LogError($"[Settings] '{setting.fieldName}' 은 bool 이 아니라 {field.FieldType.Name} 입니다. 토글로 다룰 수 없습니다.", this);
                continue;
            }
            if (setting.toggle == null)
            {
                Debug.LogWarning($"[Settings] '{setting.fieldName}' 에 연결된 Toggle 이 없습니다.", this);
                continue;
            }

            setting.field = field;
            bound.Add(field.Name);
        }

        // json 에는 있는데 설정창에 안 나오는 bool 필드를 알려준다.
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.FieldType == typeof(bool) && !bound.Contains(field.Name))
                Debug.LogWarning($"[Settings] bool 필드 '{field.Name}' 에 연결된 토글이 없습니다. 씬에 Toggle 을 만들고 목록에 추가하세요.", this);
        }
    }

    // 설정창 표시/숨김 + 마우스 커서 연동.
    // 창이 보일 때는 조작할 수 있도록 커서를 강제로 표시하고,
    // 창이 닫히면 커서를 설정값(showMouseCursor)대로 되돌린다(숨김이면 같이 숨김).
    public void SetPanelVisible(bool show)
    {
        if (panelRoot != null)
            panelRoot.SetActive(show);
        IsVisible = show;
        PanelVisibilityChanged?.Invoke(show);

        if (show)
        {
            SyncFromData();          // 열 때 최신 json 값 반영
            Cursor.visible = true;   // 조작을 위해 커서 강제 표시
        }
        else
        {
            RestoreCursor();         // 설정값대로 복원
        }
    }

    // 현재 showMouseCursor 설정값대로 커서 표시/숨김을 되돌린다.
    private void RestoreCursor()
    {
        bool show = GetValue(ShowMouseCursorField, true);
        if (alwaysOnTop != null) alwaysOnTop.ApplyMouseCursor(show);
        else Cursor.visible = show;
    }

    // 현재 gameSettingData / gameDynamicData -> 토글·입력칸 UI 반영
    public void SyncFromData()
    {
        var jm = JsonManager.instance;
        if (jm == null) return;

        _syncing = true;
        foreach (var setting in boolSettings)
        {
            if (setting == null || setting.field == null || setting.toggle == null || jm.gameSettingData == null) continue;
            setting.toggle.isOn = (bool)setting.field.GetValue(jm.gameSettingData);
        }
        foreach (var setting in numberSettings)
        {
            if (setting == null || setting.field == null || setting.input == null || jm.gameDynamicData == null) continue;
            object owner = ResolveOwner(jm.gameDynamicData, setting.fieldName);
            if (owner != null) setting.input.SetTextWithoutNotify(FormatNumber(setting.field.GetValue(owner)));
        }
        _syncing = false;
    }

    // 인스펙터에 적은 필드 이름을 GameDynamicData 의 float/int 필드와 연결한다.
    private void BindNumberFields()
    {
        var data = JsonManager.instance != null ? JsonManager.instance.gameDynamicData : new GameDynamicData();
        foreach (var setting in numberSettings)
        {
            if (setting == null) continue;
            setting.field = null;

            object owner = string.IsNullOrEmpty(setting.fieldName) ? null : ResolveOwner(data, setting.fieldName);
            var field = owner == null ? null : owner.GetType().GetField(LastName(setting.fieldName), BindingFlags.Public | BindingFlags.Instance);
            if (field == null)
            {
                Debug.LogError($"[Settings] GameDynamicData 에 '{setting.fieldName}' 필드가 없습니다.", this);
                continue;
            }
            if (field.FieldType != typeof(float) && field.FieldType != typeof(int))
            {
                Debug.LogError($"[Settings] '{setting.fieldName}' 은 float/int 가 아니라 {field.FieldType.Name} 입니다.", this);
                continue;
            }
            if (setting.input == null)
            {
                Debug.LogWarning($"[Settings] '{setting.fieldName}' 에 연결된 InputField 가 없습니다.", this);
                continue;
            }

            setting.field = field;
        }
    }

    private void OnNumberEdited(NumberSetting setting, string text)
    {
        if (_syncing) return;

        var jm = JsonManager.instance;
        if (jm == null || setting.field == null) return;

        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
        {
            Debug.LogWarning($"[Settings] '{setting.fieldName}' 숫자가 아님: '{text}' - 이전 값 유지", this);
            SyncFromData();
            return;
        }

        object target = ResolveOwner(jm.gameDynamicData, setting.fieldName);
        if (target == null)
        {
            Debug.LogWarning($"[Settings] '{setting.fieldName}' 항목을 찾지 못함 (목록 길이 확인)", this);
            return;
        }
        value = Mathf.Clamp(value, setting.min, setting.max);
        object boxed = setting.field.FieldType == typeof(int) ? (object)Mathf.RoundToInt(value) : value;
        setting.field.SetValue(target, boxed);
        jm.SaveGameDynamicData();               // persistentDataPath/Setting.json 에 바로 저장
        SyncFromData();                         // 범위 보정된 값 표시

        Debug.Log($"[Settings] {setting.fieldName} = {boxed}");
        OnNumberChanged?.Invoke(setting.fieldName, value);
    }

    // 경로 "a[1].b.c" 에서 마지막 필드(c)를 가진 객체를 찾는다. 없으면 null.
    private static object ResolveOwner(object root, string path)
    {
        object current = root;
        string[] parts = path.Split('.');
        for (int i = 0; i < parts.Length - 1 && current != null; i++)
            current = Step(current, parts[i]);
        return current;
    }

    // "name" 또는 "name[index]" 한 칸 내려간다
    private static object Step(object obj, string part)
    {
        int bracket = part.IndexOf('[');
        string name = bracket >= 0 ? part.Substring(0, bracket) : part;
        var field = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
        object value = field != null ? field.GetValue(obj) : null;
        if (bracket < 0 || value == null) return value;
        if (!int.TryParse(part.Substring(bracket + 1).TrimEnd(']'), out int index)) return null;
        return value is System.Collections.IList list && index >= 0 && index < list.Count ? list[index] : null;
    }

    private static string LastName(string path)
    {
        string last = path.Substring(path.LastIndexOf('.') + 1);
        int bracket = last.IndexOf('[');
        return bracket >= 0 ? last.Substring(0, bracket) : last;
    }

    private static string FormatNumber(object value)
    {
        return value is float f ? f.ToString("0.##", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture);
    }

    // gameSettingData 의 bool 값 하나를 읽는다. 필드가 없거나 데이터가 없으면 fallback.
    public bool GetValue(string fieldName, bool fallback = false)
    {
        var data = JsonManager.instance != null ? JsonManager.instance.gameSettingData : null;
        if (data == null) return fallback;

        var field = typeof(GameSettingData).GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
        if (field == null || field.FieldType != typeof(bool)) return fallback;

        return (bool)field.GetValue(data);
    }

    private void OnToggleChanged(BoolSetting setting, bool value)
    {
        if (_syncing) return;

        var jm = JsonManager.instance;
        if (jm == null || setting.field == null) return;

        setting.field.SetValue(jm.gameSettingData, value);
        jm.SaveGameSettingData();               // json 파일에 실시간 저장

        Apply(setting.fieldName, value);
        OnSettingChanged?.Invoke(setting.fieldName, value);
    }

    // 스타트팩이 기본으로 제공하는 설정의 즉시 반영.
    // 프로젝트별 설정은 OnSettingChanged 이벤트를 구독해 처리한다.
    private void Apply(string fieldName, bool value)
    {
        if (fieldName == UseUnityOnTopField && alwaysOnTop != null)
            alwaysOnTop.ApplyAlwaysOnTop(value);

        // showMouseCursor 는 창이 열려 있는 동안 조작을 위해 커서를 계속 표시해야 하므로
        // 실제 반영은 창을 닫을 때 RestoreCursor() 가 설정값대로 처리한다.
    }
}
