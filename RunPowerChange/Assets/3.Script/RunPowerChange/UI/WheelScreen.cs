using UnityEngine;

// 쳇바퀴 1대의 32형 터치모니터 화면. Canvas_Wheel 프리팹 루트에 붙는다.
// 연결된 WheelSession 의 상태에 맞춰 패널을 켜고 끈다.
// 대기 중(Idle)인데 옆 쳇바퀴가 먼저 시작해 합류할 수 있으면 대기화면 대신 참가 대기(참여하기) 화면을 보여 준다.
// 캔버스 출력 디스플레이는 Setting.json wheels[].displayIndex 를 따른다. 설정창에서 바꾸면 바로 옮긴다 (DisplayRouting).
public class WheelScreen : MonoBehaviour
{
    [Tooltip("이 화면이 보여줄 쳇바퀴 세션 (씬 인스턴스에서 연결)")]
    [SerializeField] private WheelSession session;
    [SerializeField] private Canvas canvas;
    [Tooltip("이 화면에 3D 장면을 그리는 카메라 (씬 인스턴스에서 연결)")]
    [SerializeField] private Camera raceCamera;

    [Header("패널")]
    [SerializeField] private IdlePanel idlePanel;
    [SerializeField] private JoinPanel joinPanel;
    [SerializeField] private GuidePanel guidePanel;
    [SerializeField] private CountdownPanel countdownPanel;
    [SerializeField] private RunPanel runPanel;
    [SerializeField] private VoltagePanel voltagePanel;
    [SerializeField] private SalePanel salePanel;
    [SerializeField] private RankingPanel rankingPanel;

    // WheelSession.State 순서
    private WheelPanel[] panels;
    private WheelPanel current;

    private void Awake()
    {
        panels = new WheelPanel[]
        {
            idlePanel, joinPanel, guidePanel, countdownPanel, runPanel, voltagePanel, salePanel, rankingPanel,
        };
        foreach (var panel in panels)
        {
            if (panel == null) continue;
            panel.Bind(session, raceCamera);
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
        DisplayRouting.Changed += ApplyDisplay;
        Refresh();
    }

    private void OnDestroy() => DisplayRouting.Changed -= ApplyDisplay;

    private void Update()
    {
        if (session != null) Refresh();
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

    private WheelPanel PanelFor(WheelSession.State state)
    {
        if (state == WheelSession.State.Idle)
        {
            var round = session.Round;
            if (round != null && round.Current == GameRound.Phase.Open && round.CanJoin) return joinPanel;
        }
        int index = (int)state;
        return index >= 0 && index < panels.Length ? panels[index] : null;
    }

    private void Refresh()
    {
        WheelPanel next = PanelFor(session.Current);
        if (next == current) return;
        if (next == null)
        {
            Debug.LogWarning($"{name}: {session.Current} 패널이 연결되지 않았습니다.", this);
            return;
        }

        if (current != null) current.Hide();
        current = next;
        current.Show();
    }
}
