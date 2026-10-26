using UnityEngine;

// 디자이너 3D 맵(RaceMap 프리팹 루트)의 연출. 캐릭터는 제자리에서 뛰고 바닥·기둥·결승선이 다가온다 (연출 참조 PPT).
//  - 기둥·유리벽 세트(scrollItems)는 화면 쪽(-앞)으로 흘러가고, 카메라 뒤로 지나가면 맨 안쪽으로 돌아간다 (루핑).
//  - 레일·바닥 화살표의 텍스처 흐름 애니메이션(scrollAnimators)은 같은 빠르기로 재생한다.
//  - 흐르는 빠르기 = 지금 달리는 캐릭터 중 가장 빠른 속도. 아무도 달리지 않으면 멈춘다.
//  - 결승선은 체험 끝나기 finishApproachSeconds 전부터 다가와 끝나는 순간 캐릭터를 지나간다.
//  - 32형은 화면마다 자기 캐릭터가 제자리이고 상대가 거리 차만큼 앞/뒤로 간다 (ScreenGapZ, 앞설 때 상대는 화면 밖으로 나가도 됨).
//  - 85형 바퀴는 더 많이 달린 쪽만 앞으로 나간다 (GapZ). 뒤처진 쪽은 제자리라 두 바퀴가 다 보인다.
//  - 85형 "오늘 최고 랭킹" 선은 뒤처진 레인 기준으로 1위 기록이 지금 시간에 있던 자리에 둔다 (앞뒤 모두 갈 수 있음).
public class RaceStage : MonoBehaviour
{
    public static RaceStage Instance { get; private set; }

    [Tooltip("레인 순서 (쳇바퀴 1, 2)")]
    [SerializeField] private RaceRunner[] runners;

    [Header("흐르는 배경")]
    [SerializeField] private Transform[] scrollItems;
    [Tooltip("이 로컬 z 보다 뒤(카메라 쪽)로 가면 loopLength 만큼 안쪽으로 옮긴다")]
    [SerializeField] private float recycleBehindZ = -40f;
    [SerializeField] private float loopLength = 108f;
    [SerializeField] private Animator[] scrollAnimators;
    [Tooltip("이 속도(m/s)일 때 레일 흐름 애니메이션을 1배속으로 재생")]
    [SerializeField] private float scrollAnimationMps = 3f;
    [SerializeField] private float speedSharpness = 3f;

    [Header("결승선")]
    [SerializeField] private Transform finishLine;
    [Tooltip("다가오기 시작할 때 캐릭터보다 얼마나 안쪽(유닛)에 있는지")]
    [SerializeField] private float finishStartDistance = 150f;
    [Tooltip("캐릭터가 서 있는 로컬 z (결승선이 지나가는 기준)")]
    [SerializeField] private float runnerZ = -24f;

    [Header("레인 앞뒤 간격")]
    [Tooltip("앞선 레인이 최대 몇 유닛까지 앞으로 나갈지 (카메라 화면 안에 보이는 범위)")]
    [SerializeField] private float maxGap = 6f;
    [Tooltip("32형에서 내가 앞설 때 상대가 최대 몇 유닛까지 뒤로 갈지 (카메라 뒤로 지나가 안 보여도 됨)")]
    [SerializeField] private float maxBehind = 30f;

    [Header("85형 오늘 최고 랭킹 선")]
    [SerializeField] private Transform recordLine;
    [Tooltip("표시(오늘 최고 랭킹)가 붙을 선 위의 점")]
    [SerializeField] private Transform recordTagPoint;
    [Tooltip("두 레인 거리가 같을 때 선이 놓일 로컬 z (85형 바퀴 기준 위치)")]
    [SerializeField] private float recordZeroZ = 0.5f;
    [Tooltip("선이 바퀴보다 최대 몇 유닛 앞뒤로 갈지")]
    [SerializeField] private float recordMaxGap = 12f;

    public float WorldSpeedMps { get; private set; }
    public Vector3 LocalForward => Vector3.forward;
    public bool RecordLineVisible => recordLine != null && recordLine.gameObject.activeSelf;
    public Vector3 RecordLineTagPoint => recordTagPoint != null ? recordTagPoint.position : recordLine != null ? recordLine.position : Vector3.zero;

    // 흐르는 빠르기·앞뒤 간격 배율은 현장에서 바꿀 수 있게 Setting.json(설정창)에서 읽는다
    private static float UnitsPerMeter => JsonManager.instance.gameDynamicData.raceScrollPerMeter;
    private static float GapPerMeter => JsonManager.instance.gameDynamicData.raceGapPerMeter;

    private Vector3 finishBase;
    private Vector3 recordBase;
    // 지금 달리는 레인 중 가장 뒤처진 거리 (앞뒤 간격의 기준)
    private float trailingDistance;

    private void Awake()
    {
        Instance = this;
        if (finishLine != null) finishBase = finishLine.localPosition;
        if (recordLine != null) recordBase = recordLine.localPosition;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 가장 뒤처진 레인과의 거리 차를 앞쪽 간격(유닛)으로
    public float GapZ(float distanceM) => Mathf.Clamp((distanceM - trailingDistance) * GapPerMeter, 0f, maxGap);

    // 32형 쳇바퀴 screenWheelId 화면에서 runner 레인의 앞뒤 간격. 그 화면의 레인(체험자 또는 1위 고스트)이 기준(0)이다.
    // 그 화면 레인이 비어 있으면(대기 중 등) 85형과 같이 가장 뒤처진 레인을 기준으로 한다.
    public float ScreenGapZ(int screenWheelId, RaceRunner runner)
    {
        RaceRunner own = Runner(screenWheelId);
        if (runner == own) return 0f;
        float reference = own != null && own.IsActive ? own.DistanceM : trailingDistance;
        return Mathf.Clamp((runner.DistanceM - reference) * GapPerMeter, -maxBehind, maxGap);
    }

    public RaceRunner Runner(int wheelId)
    {
        foreach (var r in runners)
            if (r != null && r.WheelId == wheelId) return r;
        return null;
    }

    private void LateUpdate()
    {
        if (JsonManager.instance == null) return; // 앱 종료 중
        float dt = Time.unscaledDeltaTime;
        float fastest = 0f;
        float trailing = float.MaxValue;
        foreach (var r in runners)
        {
            if (r == null || !r.IsActive) continue;
            fastest = Mathf.Max(fastest, r.SpeedMps);
            trailing = Mathf.Min(trailing, r.DistanceM);
        }
        trailingDistance = trailing == float.MaxValue ? 0f : trailing;
        WorldSpeedMps = Mathf.Lerp(WorldSpeedMps, fastest, 1f - Mathf.Exp(-speedSharpness * dt));

        foreach (var r in runners)
            if (r != null) r.ApplyStage(this, dt);

        Scroll(dt);
        UpdateFinish();
        UpdateRecordLine();
    }

    private void Scroll(float dt)
    {
        float move = WorldSpeedMps * UnitsPerMeter * dt;
        if (scrollItems != null)
        {
            foreach (var item in scrollItems)
            {
                if (item == null) continue;
                Vector3 p = item.localPosition;
                p.z -= move;
                if (p.z < recycleBehindZ) p.z += loopLength;
                item.localPosition = p;
            }
        }

        float animSpeed = WorldSpeedMps / Mathf.Max(0.1f, scrollAnimationMps);
        if (scrollAnimators != null)
            foreach (var a in scrollAnimators)
                if (a != null) a.speed = animSpeed;
    }

    private void UpdateFinish()
    {
        if (finishLine == null) return;
        var round = GameRound.Instance;
        float approach = JsonManager.instance.gameDynamicData.finishApproachSeconds;
        bool show = round != null && round.Current == GameRound.Phase.Run && approach > 0f && round.RunRemaining <= approach;
        if (finishLine.gameObject.activeSelf != show) finishLine.gameObject.SetActive(show);
        if (!show) return;

        // 남은 시간에 비례해 다가와 0초에 캐릭터 위치를 지난다
        float t = round.RunRemaining / approach;
        Vector3 p = finishBase;
        p.z = runnerZ + finishStartDistance * t;
        finishLine.localPosition = p;
    }

    private void UpdateRecordLine()
    {
        if (recordLine == null) return;
        var round = GameRound.Instance;
        bool show = round != null && round.RaceActive && round.Leader != null && round.Current >= GameRound.Phase.Countdown;
        if (recordLine.gameObject.activeSelf != show) recordLine.gameObject.SetActive(show);
        if (!show) return;

        float leader = round.Current >= GameRound.Phase.Run ? round.Leader.DistanceAt(round.RunElapsed) : 0f;
        float z = recordZeroZ + Mathf.Clamp((leader - trailingDistance) * GapPerMeter, -recordMaxGap, recordMaxGap);
        recordLine.localPosition = new Vector3(recordBase.x, recordBase.y, z);
    }
}
