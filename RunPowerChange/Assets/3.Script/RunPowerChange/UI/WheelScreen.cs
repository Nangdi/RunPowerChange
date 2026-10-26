using UnityEngine;

// 쳇바퀴 1대의 32형 터치모니터 화면. Canvas_Wheel 프리팹 루트에 붙는다.
// 연결된 WheelSession 의 상태에 맞춰 패널을 켜고 끈다.
// 캔버스 출력 디스플레이는 Setting.json wheels[].displayIndex 를 따른다.
public class WheelScreen : MonoBehaviour
{
    [Tooltip("이 화면이 보여줄 쳇바퀴 세션 (씬 인스턴스에서 연결)")]
    [SerializeField] private WheelSession session;
    [SerializeField] private Canvas canvas;

    [Header("패널")]
    [SerializeField] private IdlePanel idlePanel;
    [SerializeField] private WaitNextPanel waitNextPanel;
    [SerializeField] private GuidePanel guidePanel;
    [SerializeField] private CharacterSelectPanel characterSelectPanel;
    [SerializeField] private CountdownPanel countdownPanel;
    [SerializeField] private RunPanel runPanel;
    [SerializeField] private RunResultPanel runResultPanel;
    [SerializeField] private VoltagePanel voltagePanel;
    [SerializeField] private TransmitPanel transmitPanel;
    [SerializeField] private ResultPanel resultPanel;

    // WheelSession.State 순서. Ready(선택 후 대기)는 캐릭터 선택 화면을 그대로 쓴다.
    private WheelPanel[] panels;
    private WheelPanel current;

    private void Awake()
    {
        panels = new WheelPanel[]
        {
            idlePanel, waitNextPanel, guidePanel, characterSelectPanel, characterSelectPanel, countdownPanel,
            runPanel, runResultPanel, voltagePanel, transmitPanel, resultPanel,
        };
        foreach (var panel in panels)
        {
            if (panel == null) continue;
            panel.Bind(session);
            panel.Hide();
        }
    }

    private void Start()
    {
        if (session == null)
        {
            Debug.LogError($"{name}: WheelSession 연결이 비어 있습니다.", this);
            return;
        }

        ApplyDisplay();
        session.OnStateChanged += HandleStateChanged;
        HandleStateChanged(session, session.Current);
    }

    private void OnDestroy()
    {
        if (session != null) session.OnStateChanged -= HandleStateChanged;
    }

    private void ApplyDisplay()
    {
        if (canvas == null) return;
        foreach (var w in JsonManager.instance.gameDynamicData.wheels)
        {
            if (w == null || w.wheelId != session.WheelId) continue;
            canvas.targetDisplay = Mathf.Max(0, w.displayIndex);
            return;
        }
        Debug.LogWarning($"{name}: Setting.json 에 wheelId {session.WheelId} 가 없어 Display {canvas.targetDisplay + 1} 그대로 사용");
    }

    private void HandleStateChanged(WheelSession s, WheelSession.State state)
    {
        int index = (int)state;
        WheelPanel next = index >= 0 && index < panels.Length ? panels[index] : null;
        if (next == null)
        {
            Debug.LogWarning($"{name}: {state} 패널이 연결되지 않았습니다.", this);
            return;
        }

        if (current != null && current != next) current.Hide();
        current = next;
        current.Show();
    }
}
