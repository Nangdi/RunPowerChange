using System;
using System.Collections.Generic;
using UnityEngine;

// 쳇바퀴 1대(체험자 1명)의 체험 흐름. 달리기까지는 GameRound 가 두 쳇바퀴를 같은 회차로 묶어 진행하고,
// 달리기가 끝난 뒤 전압 선택·판매·랭킹은 각자 진행한다.
//
// Idle ─시작─→ Join ─(회차)→ Guide → Countdown → Run → SelectVoltage → Sale → Ranking → Idle
// 옆 쳇바퀴의 회차가 진행 중이면(합류 시간이 지남) 시작할 수 없다. 대기화면에 "체험 중" 안내가 나온다.
//
// - 캐릭터는 쳇바퀴마다 정해져 있다 (1 = 왼쪽 파랑 레인 사람, 2 = 오른쪽 분홍 레인 공룡).
// - 화면(패널)은 OnStateChanged 를 구독하고 아래 공개 값을 읽어 그린다. 버튼은 공개 메서드를 호출한다.
// - 관전 화면(85형)도 같은 값을 읽는다.
// - 랭킹(발전왕·판매왕)은 전압을 고를 때 한 번에 기록한다. 중간에 끊긴 체험은 랭킹에 기록하지 않는다.
//   순위는 쳇바퀴별이 아니라 두 쳇바퀴 기록을 합친 게임 전체 종합순위다 (WheelRankingStore.TotalRank).
// - 달리기 기록(고스트·85형 최고 랭킹 선용)은 달리기가 끝날 때 RaceRecordStore 에 넘긴다.
public class WheelSession : MonoBehaviour
{
    public enum State { Idle, Join, Guide, Countdown, Run, SelectVoltage, Sale, Ranking }

    [Tooltip("Setting.json wheels[].wheelId")]
    [SerializeField] private int wheelId = 1;

    [Header("개발자용")]
    [Tooltip("체험 중 남은 시간을 바로 끝내는 키 (에디터 시험용)")]
    [SerializeField] private KeyCode debugFinishRunKey = KeyCode.None;

    // 디버그 시작 키: 쳇바퀴 번호와 같은 숫자 키(1 → 쳇바퀴 1, 2 → 쳇바퀴 2, 키패드 숫자도 됨).
    // 대기화면의 체험시작 버튼(옆 쳇바퀴가 참가 대기 중이면 참여하기)과 같은 동작이다.
    public KeyCode DebugStartKey => wheelId >= 1 && wheelId <= 9 ? KeyCode.Alpha0 + wheelId : KeyCode.None;
    private KeyCode DebugStartKeypad => wheelId >= 1 && wheelId <= 9 ? KeyCode.Keypad0 + wheelId : KeyCode.None;

    public int WheelId => wheelId;
    public State Current { get; private set; } = State.Idle;
    // 현재 상태에 들어온 뒤 지난 시간(초)
    public float StateElapsed => Time.unscaledTime - stateEnterTime;

    public GameRound Round => GameRound.Instance;
    public int CountdownNumber => Round != null ? Round.CountdownNumber : 0;

    // 레인과 캐릭터 번호 (0 = 사람, 1 = 공룡)
    public int CharacterIndex => Mathf.Max(0, wheelId - 1);

    // 발전 체험 값
    public float RunDuration => Mathf.Max(1f, Config.runSeconds);
    public float RunElapsed { get; private set; }
    public float RunRemaining => Mathf.Max(0f, RunDuration - RunElapsed);
    public float SpeedKmh => sensor != null && IsRunning ? sensor.SpeedKmh : 0f;
    public float Watt => IsRunning ? PowerModel.WattFromSpeed(SpeedKmh, Config) : 0f;
    public float EnergyWh { get; private set; }
    public float DistanceM { get; private set; }
    public float MaxSpeedKmh { get; private set; }
    public float AverageWatt => PowerModel.AverageWatt(EnergyWh, RunElapsed);
    public bool IsRunning => Current == State.Run;
    // 시안의 "누적 환산 발전량" (Wh). 달리는 동안 늘어나고, 끝나는 순간 판매에 쓰는 환산 발전량(Sale.sentWh)과 같아진다.
    public float ScaledEnergyWh => PowerModel.ScaledEnergyWh(EnergyWh, Config);
    // 게이지 최대값: 최대 출력으로 saleScaleMinutes 동안 달렸을 때 (Wh)
    public float MaxScaledEnergyWh => PowerModel.ScaledWh(Config.MaxWatt, Config);

    // 발전 종합순위 (두 쳇바퀴 기록 합산. SelectVoltage 부터 유효, 그 전에는 0).
    // 기록을 저장한 뒤에는 저장된 기록 기준이라, 같은 회차의 다른 체험자가 나중에 저장해도 순위가 맞게 바뀐다.
    // 저장 전(전압 선택 화면)에는 같은 회차에서 아직 저장하지 않은 다른 체험자의 발전량도 끼워 미리 계산한다.
    public int EnergyRank
    {
        get
        {
            if (LastRecord != null) return WheelRankingStore.TotalRank(WheelRankingStore.Kind.Energy, LastRecord.energyWh);
            if (Current != State.SelectVoltage) return 0;
            int rank = WheelRankingStore.TotalRank(WheelRankingStore.Kind.Energy, EnergyWh);
            foreach (var other in PendingOthers())
                if (other.EnergyWh > EnergyWh) rank++;
            return rank;
        }
    }
    // 판매 결과 (Sale 부터 유효)
    public int SelectedVoltageIndex { get; private set; } = -1;
    public SaleResult Sale { get; private set; }
    // 판매 종합순위 (Sale 부터 유효, 그 전에는 0)
    public int SaleRank => LastRecord != null ? WheelRankingStore.TotalRank(WheelRankingStore.Kind.Sale, LastRecord.saleWon) : 0;
    // 오늘 전체 체험자 수 (두 쳇바퀴 합산. 같은 회차에서 아직 기록을 저장하지 않은 체험자 포함)
    public int TodayCount
    {
        get
        {
            int n = WheelRankingStore.TotalCount;
            if (LastRecord == null && Current == State.SelectVoltage) n++;
            foreach (var _ in PendingOthers()) n++;
            return n;
        }
    }

    // 같은 회차에서 달리기를 끝내고 아직 기록을 저장하지 않은 다른 체험자
    private IEnumerable<WheelSession> PendingOthers()
    {
        if (Round == null) yield break;
        foreach (var p in Round.Participants)
            if (p != null && p != this && p.Current == State.SelectVoltage && p.LastRecord == null) yield return p;
    }
    // 자동 넘김 화면의 남은 시간 비율 1→0 (하단 진행 바). 자동 넘김이 없는 화면은 1.
    public float AutoAdvanceRemaining01
    {
        get
        {
            if (Current == State.Guide) return Round != null ? Round.GuideRemaining01 : 0f;
            if (Current == State.SelectVoltage)
            {
                float idle = Config.idleTimeoutSeconds;
                return idle > 0f ? Mathf.Clamp01(1f - (Time.unscaledTime - lastInputTime) / idle) : 1f;
            }
            float limit = AutoAdvanceSeconds;
            return limit > 0f ? Mathf.Clamp01(1f - StateElapsed / limit) : 1f;
        }
    }

    public WheelRankingStore Ranking { get; private set; }
    public PowerRecord LastRecord { get; private set; }

    public event Action<WheelSession, State> OnStateChanged;
    // 랭킹이 갱신됨 (관전 화면 랭킹표 갱신용)
    public event Action<WheelSession, PowerRecord> OnRecordAdded;

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    private WheelSensor sensor;
    private double runStartDistance;
    private float stateEnterTime;
    private float lastInputTime;
    private RaceTrack track;
    private float nextSampleTime;

    private void Start()
    {
        Ranking = new WheelRankingStore(wheelId);
        sensor = WheelSensorLink.Link != null ? WheelSensorLink.Link.GetSensor(wheelId) : null;
        if (sensor == null) Debug.LogWarning($"[Wheel {wheelId}] 센서 없음 (WheelSensorLink 또는 Setting.json wheels 확인) - 속도 0으로 진행");
        if (Round == null) Debug.LogError($"[Wheel {wheelId}] 씬에 GameRound 가 없습니다.");

        GoTo(State.Idle);
        ShortcutHelp.Register(this, debugFinishRunKey, "발전 바로 끝내기 (시험용)", "발전 중");
        ShortcutHelp.Register(this, DebugStartKey, $"쳇바퀴 {wheelId} 체험 시작 (체험시작·참여하기 버튼과 같음, 키패드 {wheelId}도 됨)", $"쳇바퀴 {wheelId} 대기화면, 숫자 입력칸 입력 중 제외");
    }

    private void OnDestroy() => ShortcutHelp.Unregister(this);

    // 화면별 자동 넘김 시간 (0 = 없음)
    private float AutoAdvanceSeconds
    {
        get
        {
            switch (Current)
            {
                case State.Sale: return Config.saleSeconds;
                case State.Ranking: return Config.resultAutoCloseSeconds;
                default: return 0f;
            }
        }
    }

    private void Update()
    {
        if (JsonManager.instance == null) return; // 앱 종료 중
        if (Current == State.Idle && DebugStartKeyDown())
        {
            Debug.Log($"[Wheel {wheelId}] 디버그 키로 체험 시작");
            StartExperience();
        }

        switch (Current)
        {
            case State.Run:
                TickRun(Time.unscaledDeltaTime);
                if (debugFinishRunKey != KeyCode.None && Input.GetKeyDown(debugFinishRunKey)) Round?.DebugFinishRun();
                break;

            case State.SelectVoltage:
                // 전압을 고르지 않고 떠나면 랭킹에 기록하지 않고 대기화면으로
                if (Config.idleTimeoutSeconds > 0f && Time.unscaledTime - lastInputTime >= Config.idleTimeoutSeconds)
                {
                    Debug.Log($"[Wheel {wheelId}] 무입력 {Config.idleTimeoutSeconds}s - 기록 없이 대기화면 복귀");
                    GoTo(State.Idle);
                }
                break;

            case State.Sale:
                if (StateElapsed >= Config.saleSeconds) GoTo(State.Ranking);
                break;

            case State.Ranking:
                if (StateElapsed >= Config.resultAutoCloseSeconds) GoTo(State.Idle);
                break;
        }
    }

    // ── 화면 버튼에서 호출 ────────────────────────────────

    // 아무 터치나 들어왔을 때 (무입력 타이머 초기화)
    public void NotifyInput() => lastInputTime = Time.unscaledTime;

    // 대기화면의 체험 시작, 참가 대기화면의 참여하기
    public void StartExperience()
    {
        NotifyInput();
        if (Current != State.Idle || Round == null) return;
        if (Round.RequestJoin(this)) GoTo(State.Join);
    }

    // 송전전압 선택 (Setting.json voltages 의 순서)
    public void SelectVoltage(int index)
    {
        NotifyInput();
        if (Current != State.SelectVoltage) return;

        var voltage = PowerModel.GetVoltage(Config, index);
        if (voltage == null)
        {
            Debug.LogError($"[Wheel {wheelId}] 잘못된 전압 번호: {index}");
            return;
        }

        SelectedVoltageIndex = index;
        Sale = PowerModel.Sell(AverageWatt, voltage, Config);
        CommitRecord();
        GoTo(State.Sale);
    }

    // ── GameRound 에서 호출 ─────────────────────────────

    public void BeginGuide() => GoTo(State.Guide);

    public void BeginCountdown()
    {
        ResetRunValues();
        GoTo(State.Countdown);
    }

    public void BeginRun()
    {
        ResetRunValues();
        runStartDistance = sensor != null ? sensor.TotalDistanceM : 0d;
        track = new RaceTrack { wheelId = wheelId, characterIndex = CharacterIndex };
        track.samples.Add(0f);
        nextSampleTime = RaceTrack.SampleSeconds;
        GoTo(State.Run);
    }

    public void FinishRun()
    {
        if (Current != State.Run) return;
        // 이번 프레임에 이 세션이 GameRound 보다 늦게 업데이트돼 마지막 구간을 아직 적산하지 않았으면 체험 끝 시각까지 채운다
        TickRun(0f);
        RunElapsed = RunDuration;

        if (track != null)
        {
            track.ticks = DateTime.Now.Ticks;
            track.distanceM = DistanceM;
            track.energyWh = EnergyWh;
            track.samples.Add(DistanceM);
            RaceRecordStore.Add(track);
            track = null;
        }

        GoTo(State.SelectVoltage);
        Debug.Log($"[Wheel {wheelId}] 발전 종료: {EnergyWh:0.00}Wh, {DistanceM:0}m, 평균 {AverageWatt:0}W, 발전 종합 {EnergyRank}/{TodayCount}위");
    }

    // ── 내부 ───────────────────────────────────────────

    // 설정창 숫자 입력칸에 숫자를 치는 중이면 시작 키로 보지 않는다
    private bool DebugStartKeyDown()
    {
        if (DebugStartKey == KeyCode.None) return false;
        if (!Input.GetKeyDown(DebugStartKey) && !Input.GetKeyDown(DebugStartKeypad)) return false;
        var selected = UnityEngine.EventSystems.EventSystem.current != null ? UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject : null;
        if (selected == null) return true;
        var tmp = selected.GetComponent<TMPro.TMP_InputField>();
        if (tmp != null && tmp.isFocused) return false;
        var legacy = selected.GetComponent<UnityEngine.UI.InputField>();
        return legacy == null || !legacy.isFocused;
    }

    // 이전 체험자의 발전·결과 값을 지운다. 대기화면으로 돌아갈 때와 카운트다운·발전 시작 시 호출한다.
    // 센서의 누적거리는 지우지 않는다 (세션은 시작 시점과의 차이만 쓴다).
    private void ResetRunValues()
    {
        RunElapsed = 0f;
        EnergyWh = 0f;
        DistanceM = 0f;
        MaxSpeedKmh = 0f;
        SelectedVoltageIndex = -1;
        Sale = default;
        LastRecord = null;
        track = null;
    }

    // 발전량은 프레임 시간(dt)이 아니라 체험 시계(RunElapsed)가 흐른 만큼만 적산한다.
    // 프레임 시간을 그대로 쓰면, PC 가 잠깐 멈춘 뒤의 긴 프레임에서 체험 시간이 끝났는데도 멈춘 시간 전체만큼 발전량이 더해지고,
    // 그 프레임에 GameRound 가 달리기를 끝내는 순서(스크립트 실행 순서)에 따라 한 쳇바퀴에만 더해져 승자·순위가 뒤바뀐다.
    private void TickRun(float dt)
    {
        float elapsed = Round != null ? Round.RunElapsed : Mathf.Min(RunDuration, RunElapsed + dt);
        float runDt = Mathf.Max(0f, elapsed - RunElapsed);
        RunElapsed = elapsed;
        if (sensor != null) DistanceM = (float)(sensor.TotalDistanceM - runStartDistance);
        EnergyWh += Watt * runDt / 3600f;
        MaxSpeedKmh = Mathf.Max(MaxSpeedKmh, SpeedKmh);

        // 고스트·최고 랭킹 선용 시간별 거리
        while (track != null && RunElapsed >= nextSampleTime)
        {
            track.samples.Add(DistanceM);
            nextSampleTime += RaceTrack.SampleSeconds;
        }
    }

    private void CommitRecord()
    {
        LastRecord = Ranking.Add(new PowerRecord
        {
            ticks = DateTime.Now.Ticks,
            wheelId = wheelId,
            energyWh = EnergyWh,
            scaledEnergyWh = Sale.sentWh,
            maxSpeedKmh = MaxSpeedKmh,
            lossWh = Sale.lossWh,
            saleWon = Sale.saleWon,
            arrivedWh = Sale.arrivedWh,
            distanceM = DistanceM,
            voltageId = Sale.voltageId,
        });
        Debug.Log($"[Wheel {wheelId}] 기록 저장: {Sale.voltageLabel}, 도착 {Sale.arrivedWh:0.0}Wh, {Sale.saleWon:0.0}원, 종합 순위 발전 {EnergyRank}위·판매 {SaleRank}위/{TodayCount}명");
        OnRecordAdded?.Invoke(this, LastRecord);
    }

    private void GoTo(State next)
    {
        if (next == State.Idle)
        {
            ResetRunValues();
            Round?.Leave(this);
        }

        Current = next;
        stateEnterTime = Time.unscaledTime;
        NotifyInput();
        Debug.Log($"[Wheel {wheelId}] 상태: {next}");
        OnStateChanged?.Invoke(this, next);
    }
}
