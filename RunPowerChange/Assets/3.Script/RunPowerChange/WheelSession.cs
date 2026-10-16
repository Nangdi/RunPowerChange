using System;
using UnityEngine;

// 쳇바퀴 1대의 체험 흐름. 쳇바퀴마다 하나씩 두고 서로 독립적으로 진행한다.
// Idle → Guide → Countdown → Run → RunResult → SelectVoltage → Transmit → SaleResult → Summary → End → Idle
//
// - 화면(패널)은 OnStateChanged 를 구독하고 아래 공개 값을 읽어 그린다. 버튼은 공개 메서드를 호출한다.
// - 관전 화면(85형)도 같은 값을 읽는다.
// - 안내·선택·결과 화면에서 idleTimeoutSeconds 동안 입력이 없으면 대기화면으로 돌아간다.
// - 랭킹(발전왕·판매왕)은 판매 결과로 넘어갈 때 한 번에 기록한다. 중간에 끊긴 체험은 기록하지 않는다.
public class WheelSession : MonoBehaviour
{
    public enum State { Idle, Guide, Countdown, Run, RunResult, SelectVoltage, Transmit, SaleResult, Summary, End }

    [Tooltip("Setting.json wheels[].wheelId")]
    [SerializeField] private int wheelId = 1;

    [Header("개발자용")]
    [Tooltip("체험 중 남은 시간을 바로 끝내는 키 (에디터 시험용)")]
    [SerializeField] private KeyCode debugFinishRunKey = KeyCode.None;

    public int WheelId => wheelId;
    public State Current { get; private set; } = State.Idle;
    // 현재 상태에 들어온 뒤 지난 시간(초)
    public float StateElapsed => Time.unscaledTime - stateEnterTime;

    // 카운트다운 남은 숫자 (3, 2, 1, 0 = START)
    public int CountdownNumber => Mathf.Max(0, Config.countdownSeconds - Mathf.FloorToInt(StateElapsed));

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

    // 발전 결과 (RunResult 부터 유효). 순위는 아직 기록 전이므로 미리 계산한 값.
    public int EnergyRank { get; private set; }
    // 판매 결과 (Transmit 부터 유효)
    public int SelectedVoltageIndex { get; private set; } = -1;
    public SaleResult Sale { get; private set; }
    public int SaleRank { get; private set; }
    // 오늘 체험자 수 (이번 체험 포함)
    public int TodayCount { get; private set; }
    public int EnergyTopPercent => WheelRankingStore.TopPercent(EnergyRank, TodayCount);
    public int SaleTopPercent => WheelRankingStore.TopPercent(SaleRank, TodayCount);
    // 송전 연출 진행률 0~1
    public float TransmitProgress => Mathf.Clamp01(StateElapsed / Mathf.Max(0.1f, Config.transmitSeconds));

    public WheelRankingStore Ranking { get; private set; }
    public PowerRecord LastRecord { get; private set; }

    public event Action<WheelSession, State> OnStateChanged;
    // 랭킹이 갱신됨 (관전 화면 팝업용)
    public event Action<WheelSession, PowerRecord> OnRecordAdded;

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    private WheelSensor sensor;
    private double runStartDistance;
    private float stateEnterTime;
    private float lastInputTime;

    private void Start()
    {
        Ranking = new WheelRankingStore(wheelId);
        sensor = WheelSensorLink.Link != null ? WheelSensorLink.Link.GetSensor(wheelId) : null;
        if (sensor == null) Debug.LogWarning($"[Wheel {wheelId}] 센서 없음 (WheelSensorLink 또는 Setting.json wheels 확인) - 속도 0으로 진행");

        GoTo(State.Idle);
    }

    private void Update()
    {
        switch (Current)
        {
            case State.Countdown:
                if (StateElapsed >= Config.countdownSeconds) BeginRun();
                break;

            case State.Run:
                TickRun(Time.unscaledDeltaTime);
                if (RunElapsed >= RunDuration || (debugFinishRunKey != KeyCode.None && Input.GetKeyDown(debugFinishRunKey)))
                    FinishRun();
                break;

            case State.Transmit:
                if (TransmitProgress >= 1f) GoTo(State.SaleResult);
                break;

            case State.RunResult:
            case State.SaleResult:
            case State.Summary:
                if (Config.resultAutoCloseSeconds > 0f && StateElapsed >= Config.resultAutoCloseSeconds)
                    Next();
                break;

            case State.End:
                if (StateElapsed >= Mathf.Max(3f, Config.resultAutoCloseSeconds * 0.5f)) GoTo(State.Idle);
                break;
        }

        // 선택이 필요한 화면은 무입력 시 대기화면 복귀 (결과 화면은 자동 넘김이 먼저 처리)
        if ((Current == State.Guide || Current == State.SelectVoltage)
            && Config.idleTimeoutSeconds > 0f && Time.unscaledTime - lastInputTime >= Config.idleTimeoutSeconds)
        {
            Debug.Log($"[Wheel {wheelId}] 무입력 {Config.idleTimeoutSeconds}s - 대기화면 복귀");
            GoTo(State.Idle);
        }
    }

    // ── 화면 버튼에서 호출 ────────────────────────────────

    // 아무 터치나 들어왔을 때 (무입력 타이머 초기화)
    public void NotifyInput() => lastInputTime = Time.unscaledTime;

    // 대기화면 터치
    public void StartExperience()
    {
        NotifyInput();
        if (Current == State.Idle) GoTo(State.Guide);
    }

    // 안내 화면 시작 버튼
    public void ConfirmGuide()
    {
        NotifyInput();
        if (Current == State.Guide) GoTo(State.Countdown);
    }

    // 결과 화면의 다음 버튼 (자동 넘김과 같은 동작)
    public void Next()
    {
        NotifyInput();
        switch (Current)
        {
            case State.RunResult: GoTo(State.SelectVoltage); break;
            case State.SaleResult: GoTo(State.Summary); break;
            case State.Summary: GoTo(State.End); break;
            case State.End: GoTo(State.Idle); break;
        }
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
        GoTo(State.Transmit);
    }

    // 정산 화면의 다시 체험하기
    public void Replay()
    {
        NotifyInput();
        if (Current == State.Summary || Current == State.End) GoTo(State.Guide);
    }

    // ── 내부 ───────────────────────────────────────────

    private void BeginRun()
    {
        RunElapsed = 0f;
        EnergyWh = 0f;
        DistanceM = 0f;
        MaxSpeedKmh = 0f;
        runStartDistance = sensor != null ? sensor.TotalDistanceM : 0d;
        GoTo(State.Run);
    }

    private void TickRun(float dt)
    {
        RunElapsed = Mathf.Min(RunDuration, RunElapsed + dt);
        if (sensor != null) DistanceM = (float)(sensor.TotalDistanceM - runStartDistance);
        EnergyWh += Watt * dt / 3600f;
        MaxSpeedKmh = Mathf.Max(MaxSpeedKmh, SpeedKmh);
    }

    private void FinishRun()
    {
        RunElapsed = Mathf.Min(RunElapsed, RunDuration);
        TodayCount = Ranking.Count + 1;
        EnergyRank = Ranking.GetRank(WheelRankingStore.Kind.Energy, EnergyWh);
        Debug.Log($"[Wheel {wheelId}] 발전 종료: {EnergyWh:0.00}Wh, {DistanceM:0}m, 평균 {AverageWatt:0}W, 발전 {EnergyRank}/{TodayCount}위");
        GoTo(State.RunResult);
    }

    private void CommitRecord()
    {
        TodayCount = Ranking.Count + 1;
        EnergyRank = Ranking.GetRank(WheelRankingStore.Kind.Energy, EnergyWh);
        SaleRank = Ranking.GetRank(WheelRankingStore.Kind.Sale, Sale.saleWon);

        LastRecord = Ranking.Add(new PowerRecord
        {
            ticks = DateTime.Now.Ticks,
            energyWh = EnergyWh,
            saleWon = Sale.saleWon,
            arrivedWh = Sale.arrivedWh,
            distanceM = DistanceM,
            voltageId = Sale.voltageId,
        });
        Debug.Log($"[Wheel {wheelId}] 기록 저장: {Sale.voltageLabel}, 도착 {Sale.arrivedWh:0.0}Wh, {Sale.saleWon:0.0}원, 판매 {SaleRank}/{TodayCount}위");
        OnRecordAdded?.Invoke(this, LastRecord);
    }

    private void GoTo(State next)
    {
        if (next == State.Idle || next == State.Guide)
        {
            SelectedVoltageIndex = -1;
            Sale = default;
            LastRecord = null;
        }

        Current = next;
        stateEnterTime = Time.unscaledTime;
        NotifyInput();
        Debug.Log($"[Wheel {wheelId}] 상태: {next}");
        OnStateChanged?.Invoke(this, next);
    }
}
