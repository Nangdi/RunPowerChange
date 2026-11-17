using UnityEngine;

// 디자이너 3D 맵(RaceMap 프리팹 루트)의 연출. 캐릭터는 제자리에서 뛰고 바닥·기둥·결승선이 다가온다 (연출 참조 PPT).
//  - 기둥·유리벽 세트(scrollItems)는 화면 쪽(-앞)으로 흘러가고, 카메라 뒤로 지나가면 맨 안쪽으로 돌아간다 (루핑).
//  - 레일·바닥 화살표의 텍스처 흐름 애니메이션(scrollAnimators)은 같은 빠르기로 재생한다.
//  - 흐르는 빠르기 = 지금 달리는 캐릭터 중 가장 빠른 속도. 아무도 달리지 않으면 멈춘다.
//  - 결승선은 체험 끝나기 finishApproachSeconds 전부터 다가와 끝나는 순간 캐릭터를 지나간다.
//  - 32형은 화면마다 자기 캐릭터가 제자리이고 상대가 거리 차만큼 앞/뒤로 간다 (ScreenGapZ, 앞설 때 상대는 화면 밖으로 나가도 됨).
//    배경이 루프라도 앞뒤 간격은 실제 거리 차 그대로다: 거리 차 1m = 바닥이 1m 흐르는 만큼(raceScrollPerMeter 유닛).
//    상대가 raceScreenMaxAheadM 보다 더 앞서면 그 거리에 머문다 (너무 멀어 점처럼 작아지는 것 방지).
//  - 32형에도 1위 기록 선(화면마다 한 벌, Screen{n} 레이어)을 내 캐릭터 기준 실제 거리 차 자리에 둔다. 표시는 RunPanel 이 붙인다.
//  - 85형 바퀴는 화면 기준 거리(SpectatorBase)와의 실제 거리 차만큼 앞뒤로 간다 (GapZ). 기준은 평소 뒤처진 레인이라
//    뒤처진 쪽은 제자리, 더 많이 달린 쪽만 앞으로 나간다.
//    2인 경주에서 아래 경우에는 기준을 앞선 쪽으로 바꿔 앞선 사람 시점으로 바로 끊어 바꾼다 (UpdateSpectatorFollow):
//      · 거리 차가 spectatorFollowGapM 이상 벌어지면
//      · 뒤처진 사람이 spectatorQuitSeconds 동안 멈춰 있으면(중도포기)
//    거리 차 때문에 바뀐 앞선 사람 시점은 거리 차가 spectatorReturnGapM 밑으로 다시 좁혀지면 뒤처진 사람 시점으로 바로 끊어 돌아온다.
//    포기 때문에 바뀐 앞선 사람 시점은 포기한 사람이 다시 달리면 바로 끝난다 (거리 차가 기준보다 작으면 뒤처진 사람 시점).
//    반대로 앞선 사람이 멈춰 있으면 거리 차와 관계없이 뒤처진(계속 달리는) 사람 시점이다. 둘 다 멈추면 지금 시점 그대로.
//    그래서 화면이 끊겨 바뀌는 것은 거리 차가 벌어지거나 다시 좁혀질 때와 한 사람이 포기할 때뿐이고, 역전은 끊김 없이 이어진다.
//    앞선 사람 시점에서는 앞선 바퀴가 원래 자리, 뒤처진 바퀴는 화면 아래로 빠진다. 85형 카메라는 원근이 커서
//    약 10m 만 앞서도 앞 바퀴가 화면 위쪽 발전량 글자 뒤로 작아지므로, 앞 바퀴를 그 거리에 붙여 두지 않고 원래 자리로 데려온다.
//    혼자 달릴 때(1위 고스트)는 쓰지 않는다.
//    앞선 바퀴가 화면 위로 나가면 SpectatorScreen 이 맨 위에 "PLAYER 01 63m 앞서는 중", 뒤처진 바퀴가 화면 아래로 나가면
//    아래에 "PLAYER 02 60m 추격 중" 표시를 띄운다. (달리는 동안 85형 제목은 숨겨 앞 바퀴를 가리지 않는다)
//  - 85형 "오늘 최고 랭킹" 선은 화면 기준 거리에서 1위 기록이 지금 시간에 있던 자리(실제 거리 차)에 둔다 (앞뒤 모두 갈 수 있음).
//  - 32형·85형 모두 raceScreenMaxAheadM 보다 더 앞서면 그 거리에 머문다.
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
    [Tooltip("32형에서 내가 앞설 때 상대가 최대 몇 유닛까지 뒤로 갈지 (카메라 뒤로 지나가 안 보여도 됨)")]
    [SerializeField] private float maxBehind = 30f;
    [Tooltip("85형에서 앞선 사람을 따라갈 때 뒤처진 바퀴가 최대 몇 유닛까지 뒤로 갈지 (카메라 뒤로 지나가 안 보여도 됨)")]
    [SerializeField] private float spectatorMaxBehind = 30f;

    [Header("85형 오늘 최고 랭킹 선")]
    [SerializeField] private Transform recordLine;
    [Tooltip("표시(오늘 최고 랭킹)가 붙을 선 위의 점")]
    [SerializeField] private Transform recordTagPoint;
    [Tooltip("두 레인 거리가 같을 때 선이 놓일 로컬 z (85형 바퀴 기준 위치)")]
    [SerializeField] private float recordZeroZ = 0.5f;
    [Tooltip("32형 1위 기록 선을 캐릭터 발 높이에서 얼마나 띄울지 (바닥에 묻히지 않게)")]
    [SerializeField] private float screenLineLift = 0.03f;
    [Tooltip("1위 기록 선이 기록 거리를 따라가는 빠르기 (레인 RaceRunner.followSharpness 와 같게)")]
    [SerializeField] private float leaderSharpness = 6f;
    private float leaderShown;

    public float WorldSpeedMps { get; private set; }
    public Vector3 LocalForward => Vector3.forward;
    public Vector3 RecordLineCenter => recordLine != null ? recordLine.position : Vector3.zero;
    public bool RecordLineVisible => recordLine != null && recordLine.gameObject.activeSelf;
    // 85형 "오늘 최고 랭킹" 표시 자리: 1위 기록 선 위, 가로는 두 쳇바퀴 레인의 정중앙 (선 오브젝트 기준점이 레인 중앙과 다를 수 있어서)
    public Vector3 RecordLineLaneCenter
    {
        get
        {
            if (recordLine == null) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (var r in runners)
            {
                if (r == null) continue;
                sum += r.WheelCenterPoint;
                n++;
            }
            if (n == 0) return recordLine.position;
            Transform space = recordLine.parent;
            Vector3 mid = space != null ? space.InverseTransformPoint(sum / n) : sum / n;
            Vector3 line = recordLine.localPosition;
            Vector3 local = new Vector3(mid.x, line.y, line.z);
            return space != null ? space.TransformPoint(local) : local;
        }
    }
    public Vector3 RecordLineTagPoint => recordTagPoint != null ? recordTagPoint.position : recordLine != null ? recordLine.position : Vector3.zero;

    // 32형 쳇바퀴 screenWheelId 화면의 1위 기록 선
    public bool ScreenRecordVisible(int screenWheelId)
    {
        int i = screenWheelId - 1;
        return i >= 0 && i < ScreenCount && screenLines[i] != null && screenLines[i].gameObject.activeSelf;
    }

    // 선 가운데 (두 레인 사이). 32형 "오늘 최고 랭킹" 표시의 아래 끝이 놓일 자리.
    public Vector3 ScreenRecordPoint(int screenWheelId)
    {
        int i = screenWheelId - 1;
        return i >= 0 && i < ScreenCount && screenLines[i] != null ? screenLines[i].position : Vector3.zero;
    }

    // 흐르는 빠르기·앞뒤 간격 배율은 현장에서 바꿀 수 있게 Setting.json(설정창)에서 읽는다
    private static float UnitsPerMeter => JsonManager.instance.gameDynamicData.raceScrollPerMeter;
    private static float ScreenMaxAheadM => Mathf.Max(0f, JsonManager.instance.gameDynamicData.raceScreenMaxAheadM);

    private const int ScreenCount = 2;
    private readonly Transform[] screenLines = new Transform[ScreenCount];

    private Vector3 finishBase;
    private Vector3 recordBase;
    // 지금 달리는 레인 중 가장 뒤처진 거리 (앞뒤 간격의 기준)
    private float trailingDistance;
    // 85형 화면 기준이 뒤처진 레인보다 몇 m 앞인지 (앞선 사람 시점이면 거리 차, 아니면 0)
    private float spectatorShift;
    // 85형이 앞선 사람 시점인지
    private bool followLead;
    // 2인 경주에서 앞선 / 뒤처진 체험자 레인 (아니면 null)
    private RaceRunner leadPlayer, trailPlayer;
    // 앞선 사람 시점이 거리 차 때문인지 (아니면 뒤처진 사람 포기 때문)
    private bool followByGap;
    // 이 거리 차(m) 밑이면 나란히 선 것으로 본다 (앞선 사람 시점을 끝내도 바퀴가 튀지 않는 거리)
    private const float SideBySideM = 0.5f;
    // 레인별로 멈춰 있던 시간(초) (중도포기 판단)
    private readonly float[] stoppedSeconds = new float[2];
    // 이 속도(m/s) 밑이면 멈춘 것으로 본다
    private const float StoppedMps = 0.2f;

    private void Awake()
    {
        Instance = this;
        if (finishLine != null) finishBase = finishLine.localPosition;
        if (recordLine != null)
        {
            recordBase = recordLine.localPosition;
            // 32형 화면마다 1위 기록 선 한 벌 (화면마다 내 캐릭터 기준 위치가 달라서)
            for (int i = 0; i < ScreenCount; i++)
            {
                Transform line = Instantiate(recordLine, recordLine.parent);
                line.name = $"{recordLine.name}_Screen{i + 1}";
                RaceLayers.SetLayerRecursively(line.gameObject, RaceLayers.Layer(RaceLayers.Screen(i + 1)));
                line.gameObject.SetActive(false);
                screenLines[i] = line;
            }
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // 지금 달리는 레인 중 가장 뒤처진 거리(m). "몇 m 앞서는 중" 표시의 기준.
    public float TrailingDistance => trailingDistance;
    // 지금 달리는 레인 중 가장 앞선 거리(m). "몇 m 추격 중" 표시의 기준.
    public float LeadingDistance { get; private set; }

    // 85형 화면 기준 거리(m): 이 거리에 있는 바퀴가 원래 자리에 보인다. 평소 뒤처진 레인, 앞선 사람을 따라갈 때는 그만큼 앞.
    public float SpectatorBase => trailingDistance + spectatorShift;
    // 85형이 앞선 사람 시점인지 (거리 차가 벌어졌거나 뒤처진 사람이 멈춤)
    public bool SpectatorFollowingLeader => followLead;
    // 뒤처진 / 앞선 체험자가 멈춰 중도포기로 본 상태인지
    public bool TrailingQuit { get; private set; }
    public bool LeadingQuit { get; private set; }
    // 이번 프레임에 85형 시점이 바뀌었는지 (바퀴를 부드럽게 옮기지 않고 바로 새 자리에 둔다)
    public bool SpectatorCut { get; private set; }

    // 85형: 화면 기준 거리와의 거리 차를 앞뒤 간격(유닛)으로 (실제 거리 차). 기준보다 뒤면 화면 아래(카메라 쪽)로 빠진다.
    public float GapZ(float distanceM) => SpectatorOffset(distanceM - SpectatorBase);

    // 85형: 1위 기록 선이 보이는 쪽 레인(평소 뒤처진 레인, 앞선 사람을 따라가면 앞선 레인)보다 몇 m 앞인지 (선이 꺼져 있으면 0)
    public float RecordAheadM { get; private set; }

    // 32형 쳇바퀴 screenWheelId 화면에서 runner 레인의 앞뒤 간격. 그 화면의 레인(체험자 또는 1위 고스트)이 기준(0)이다.
    // 그 화면 레인이 비어 있으면(대기 중 등) 85형과 같이 가장 뒤처진 레인을 기준으로 한다.
    public float ScreenGapZ(int screenWheelId, RaceRunner runner)
    {
        RaceRunner own = Runner(screenWheelId);
        if (runner == own) return 0f;
        float reference = own != null && own.IsActive ? own.DistanceM : trailingDistance;
        return ScreenOffset(runner.DistanceM - reference);
    }

    // 거리 차(m) → 앞뒤 간격(유닛). 바닥이 흐르는 것과 같은 비율이라 실제 거리 차 그대로 보인다 (32형·85형 공통).
    private float ScreenOffset(float diffM) => Mathf.Clamp(diffM, -maxBehind / Mathf.Max(0.01f, UnitsPerMeter), ScreenMaxAheadM) * UnitsPerMeter;
    // 85형용: 뒤로는 카메라 뒤까지 빠지면 충분하므로 spectatorMaxBehind 유닛까지만 간다
    private float SpectatorOffset(float diffM) => Mathf.Clamp(diffM, -spectatorMaxBehind / Mathf.Max(0.01f, UnitsPerMeter), ScreenMaxAheadM) * UnitsPerMeter;

    // 85형 시점(누구를 원래 자리에 둘지). 2인 경주 달리기 중에만 정한다. 바뀔 때는 부드럽게 옮기지 않고 그 프레임에 바로 끊어 바꾼다.
    //  1) 한 사람만 멈춰 있으면(spectatorQuitSeconds 이상, 중도포기) 계속 달리는 사람 시점
    //     - 뒤처진 사람이 포기: 앞선 사람 시점 / 앞선 사람이 포기: 뒤처진 사람 시점 (거리 차와 관계없이)
    //  2) 둘 다 멈췄으면 지금 시점 그대로
    //  3) 둘 다 달리면 거리 차가 spectatorFollowGapM 이상일 때 앞선 사람 시점, spectatorReturnGapM 밑으로 좁혀지면 뒤처진 사람 시점.
    //     포기 때문에 앞선 사람 시점이었으면 포기한 사람이 다시 달릴 때 바로 뒤처진 사람 시점
    private void UpdateSpectatorFollow(float dt)
    {
        var cfg = JsonManager.instance.gameDynamicData;
        var round = GameRound.Instance;
        bool running = round != null && round.Current == GameRound.Phase.Run;

        leadPlayer = trailPlayer = null;
        float lead = float.MinValue;
        int players = 0;
        for (int i = 0; i < runners.Length; i++)
        {
            RaceRunner r = runners[i];
            bool player = r != null && r.CurrentMode == RaceRunner.Mode.Player && round != null && round.IsParticipant(r.Session);
            if (i < stoppedSeconds.Length)
                stoppedSeconds[i] = running && player && r.SpeedMps < StoppedMps ? stoppedSeconds[i] + dt : 0f;
            if (r != null && r.IsActive) lead = Mathf.Max(lead, r.DistanceM);
            if (!player) continue;
            players++;
            if (leadPlayer == null || r.DistanceM > leadPlayer.DistanceM) leadPlayer = r;
            if (trailPlayer == null || r.DistanceM <= trailPlayer.DistanceM) trailPlayer = r;
        }
        LeadingDistance = lead == float.MinValue ? 0f : lead;
        if (players < 2 || leadPlayer == trailPlayer) leadPlayer = trailPlayer = null;

        bool wasFollowing = followLead;
        float gap = 0f;
        TrailingQuit = LeadingQuit = false;
        if (running && leadPlayer != null)
        {
            gap = Mathf.Max(0f, leadPlayer.DistanceM - trailPlayer.DistanceM);
            TrailingQuit = Quit(trailPlayer, cfg);
            LeadingQuit = Quit(leadPlayer, cfg);

            if (TrailingQuit && LeadingQuit) { }                         // 둘 다 멈춤: 지금 시점 그대로
            else if (TrailingQuit) { if (gap >= 1f) { followLead = true; followByGap = false; } }  // 뒤처진 사람 포기: 앞선 사람 시점
            else if (LeadingQuit) followLead = false;                     // 앞선 사람 포기: 뒤처진(달리는) 사람 시점
            else if (cfg.spectatorFollowGapM > 0f && gap >= cfg.spectatorFollowGapM) { followLead = true; followByGap = true; }
            // 포기 때문에 바뀐 앞선 사람 시점은 포기한 사람이 다시 달리면 바로 끝낸다 (거리 차가 기준보다 작으면 뒤처진 사람 시점이 맞으므로)
            else if (followLead && !followByGap) followLead = false;
            // 거리 차 때문에 바뀐 앞선 사람 시점은 뒤처진 사람이 따라와 거리 차가 spectatorReturnGapM 밑으로 좁혀지면 끝낸다 (바로 끊어 바꿈).
            // 바꾸는 기준(spectatorFollowGapM)보다 낮은 값에서 돌아와야 그 근처에서 화면이 왔다 갔다 하지 않는다.
            else if (cfg.spectatorFollowGapM <= 0f || gap < ReturnGapM(cfg)) followLead = false;
        }
        else if (round == null || !round.RaceActive || round.Current < GameRound.Phase.Run)
        {
            followLead = false;
        }

        // 대기·준비 중에는 원래 기준, 달리기가 끝난 뒤(발표)에는 마지막 화면 그대로 둔다
        if (round == null || !round.RaceActive || round.Current < GameRound.Phase.Run) spectatorShift = 0f;
        else if (running) spectatorShift = followLead ? gap : 0f;
        SpectatorCut = followLead != wasFollowing && gap >= SideBySideM;
    }

    // 앞선 사람 시점에서 뒤처진 사람 시점으로 돌아오는 거리 차(m). 바꾸는 기준보다 1m 이상 작게, 나란히(SideBySideM) 이상으로.
    private static float ReturnGapM(GameDynamicData cfg) =>
        Mathf.Max(SideBySideM, Mathf.Min(cfg.spectatorReturnGapM, cfg.spectatorFollowGapM - 1f));

    // 이 레인 체험자가 spectatorQuitSeconds 이상 멈춰 있는지 (0 이면 끔)
    private bool Quit(RaceRunner r, GameDynamicData cfg)
    {
        int i = System.Array.IndexOf(runners, r);
        return cfg.spectatorQuitSeconds > 0f && i >= 0 && i < stoppedSeconds.Length && stoppedSeconds[i] >= cfg.spectatorQuitSeconds;
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
        UpdateSpectatorFollow(dt);
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
        if (!show) RecordAheadM = 0f;
        // 1위 기록 거리도 레인(RaceRunner.followSharpness)처럼 부드럽게 따라간다. 그대로 쓰면 PC 가 잠깐 멈춘 뒤 기록은 한 번에 건너뛰고
        // 비교 기준인 레인 거리는 천천히 따라와 그 사이 선·표시가 튄다.
        float leaderTarget = show && round.Current >= GameRound.Phase.Run ? round.Leader.DistanceAt(round.RunElapsed) : 0f;
        leaderShown = !show || leaderTarget < leaderShown - 0.5f ? leaderTarget
            : Mathf.Lerp(leaderShown, leaderTarget, 1f - Mathf.Exp(-leaderSharpness * Time.unscaledDeltaTime));
        float leader = leaderShown;
        if (show)
        {
            // 따라가는 중이면 앞선 레인 기준 거리 차, 아니면 뒤처진 레인 기준
            float reference = SpectatorFollowingLeader && leadPlayer != null ? leadPlayer.DistanceM : trailingDistance;
            RecordAheadM = leader - reference;
            float z = recordZeroZ + SpectatorOffset(leader - SpectatorBase);
            recordLine.localPosition = new Vector3(recordBase.x, recordBase.y, z);
        }

        // 32형: 그 화면 체험자가 이번 회차에 달리고 있을 때만, 내 캐릭터 기준 실제 거리 차 자리에 (바닥 높이)
        for (int i = 0; i < ScreenCount; i++)
        {
            Transform line = screenLines[i];
            if (line == null) continue;
            RaceRunner own = Runner(i + 1);
            bool on = show && own != null && own.CurrentMode == RaceRunner.Mode.Player && round.IsParticipant(own.Session);
            if (line.gameObject.activeSelf != on) line.gameObject.SetActive(on);
            if (!on) continue;
            float z = own.CharacterBase.z + ScreenOffset(leader - own.DistanceM);
            line.localPosition = new Vector3(recordBase.x, own.CharacterBase.y + screenLineLift, z);
        }
    }
}
