using System;
using System.Collections.Generic;
using UnityEngine;

// 쳇바퀴 1대의 센서 상태. 속도와 앱 시작 후 누적 이동거리를 들고 있다.
// 세션은 시작 시점의 TotalDistanceM 을 기억해 두고 차이로 체험 거리를 계산한다.
//
// 센서는 쳇바퀴 안쪽 기어에 붙어 있어, 쳇바퀴 1회전에 신호(R1)가 pulsesPerRotation 개(현장 20개) 온다.
// 신호 1개 = DistancePerPulseM (둘레 / pulsesPerRotation). 속도·거리는 여기에 속도 배율(speedMultiplier)을 곱한
// 게임 값(GameDistancePerPulseM)으로 계산한다 (쳇바퀴가 무거워 실제 속도가 낮게 나오므로). 센서 튐 판정만 실제 값 기준.
//  - 속도: 최근 speedWindowSeconds 동안 들어온 신호 수로 계산한다. 신호가 잦아(15km/h 에 초당 약 13개)
//          직전 한 간격만 보면 프레임 단위 수신 시각 오차로 숫자가 크게 흔들리고, 한 프레임에 몰려 들어온
//          신호가 센서 튐으로 버려지기 때문이다. 다음 신호가 예상보다 늦어지면 그만큼 줄여 간다.
//  - 센서 튐: 구간 안 신호 수가 maxValidSpeedKmh 로 낼 수 있는 수보다 많으면 넘치는 신호만 버린다.
//  - 거리: 신호마다 한 번에 뛰지 않도록 속도로 적분해 보여 주되,
//          확인된 신호 수(CountedDistanceM)를 넘지 않게 하고, 신호 1개 넘게 뒤처지면 순간이동 없이 서서히 따라잡는다.
public class WheelSensor
{
    public int WheelId { get; }
    public int ControllerId { get; }
    public KeyCode SimulateKey { get; }

    // 표시용으로 평활한 속도 (m/s)
    public float SpeedMps { get; private set; }
    public float SpeedKmh => SpeedMps * 3.6f;
    // 앱 시작 후 누적 이동거리 (m, 화면용으로 보간). 줄어들지 않는다.
    public double TotalDistanceM { get; private set; }
    // 회전 신호로 확인된 누적 이동거리 (m) = 신호 수 × 신호 1개 거리
    public double CountedDistanceM { get; private set; }
    // 앱 시작 후 인식한 회전 신호(R1) 수 (쳇바퀴 회전 수가 아니라 기어 회전 수)
    public long PulseCount { get; private set; }
    // 마지막으로 센서 메시지를 받은 시각 (Time.unscaledTime). 받은 적 없으면 -1.
    public float LastMessageTime { get; private set; } = -1f;

    private float lastPulseTime = -1f;
    // 속도 계산 구간 안에 들어온 신호 시각들 (오래된 것부터)
    private readonly Queue<float> recentPulses = new Queue<float>();
    // 구간 안 신호로 계산한 속도 (m/s). 정지 후 첫 신호처럼 신호가 1개뿐이면 0.
    private float windowSpeedMps;
    // 화면 거리를 움직이는 속도 (m/s). 속도를 아직 모르는 첫 신호에도 캐릭터가 걷게 하려고 SpeedMps 와 따로 둔다.
    private float displayMps;

    // 키 시뮬레이션: 한 번 누르면 쳇바퀴 1회전분 신호를 직전 누른 간격에 나눠 보낸다
    private float simLastTapTime = -1f;
    private int simPending;
    private float simSpacing;
    private float simNextTime;

    // 신호 1개 넘게 뒤처졌을 때 넘친 거리를 따라잡는 시간(초)
    private const float LagRecoverSeconds = 0.5f;
    // 다음 신호가 예상 간격의 이 배수보다 늦어지면 그때부터 속도를 줄인다 (수신 시각 오차로 흔들리지 않게)
    private const float LateTolerance = 1.5f;

    public WheelSensor(int wheelId, int controllerId, KeyCode simulateKey)
    {
        WheelId = wheelId;
        ControllerId = controllerId;
        SimulateKey = simulateKey;
    }

    // 회전 신호(R1) 1개 수신. 처리 결과를 문자열로 돌려준다 ("무시됨"으로 시작하면 버린 신호).
    public string ApplyPulse(float now, GameDynamicData cfg)
    {
        LastMessageTime = now;
        float d = cfg.DistancePerPulseM;          // 실제 거리 (센서 튐 판정용)
        float gameD = cfg.GameDistancePerPulseM;  // 게임 거리 (속도 배율 적용, 거리·속도용)
        float mult = cfg.SpeedMultiplier;
        float window = WindowSeconds(cfg);
        float maxMps = Mathf.Max(0.1f, cfg.maxValidSpeedKmh / 3.6f);

        bool afterStop = lastPulseTime >= 0f && now - lastPulseTime > StopSeconds(cfg);
        if (afterStop) recentPulses.Clear();
        while (recentPulses.Count > 0 && now - recentPulses.Peek() > window) recentPulses.Dequeue();

        // 센서 튐: 구간 안 신호 수가 사람이 낼 수 없는 속도만큼 많아지면 넘치는 신호는 버린다.
        // 한 간격만 보지 않으므로 한 프레임에 몰려 들어온 정상 신호는 버리지 않는다.
        int maxPulses = Mathf.FloorToInt(maxMps * window / d) + 1;
        if (recentPulses.Count + 1 > maxPulses)
            return $"무시됨: 최근 {window:0.0}s 신호 {recentPulses.Count + 1}개 = {(recentPulses.Count + 1) * d / window * 3.6f:0.0}km/h 이상 (> {cfg.maxValidSpeedKmh}km/h)";

        recentPulses.Enqueue(now);
        lastPulseTime = now;
        PulseCount++;
        CountedDistanceM += gameD;

        string speedNote;
        float span = now - recentPulses.Peek();
        if (recentPulses.Count >= 2 && span > 0.001f)
        {
            windowSpeedMps = Mathf.Min(maxMps * mult, (recentPulses.Count - 1) * gameD / span);
            speedNote = $"최근 {span:0.00}s 신호 {recentPulses.Count}개, 실제 {windowSpeedMps / mult * 3.6f:0.0}km/h × {mult:0.##} = {windowSpeedMps * 3.6f:0.0}km/h";
        }
        else if (recentPulses.Count >= 2)
            speedNote = "같은 순간 신호";   // 속도는 직전 값 유지
        else
        {
            windowSpeedMps = 0f;
            speedNote = afterStop ? "정지 후 첫 신호" : PulseCount == 1 ? "첫 신호" : "구간 첫 신호";
        }

        float rotations = (float)(PulseCount / Math.Max(1d, cfg.pulsesPerRotation));
        return $"신호 #{PulseCount} 인식 (쳇바퀴 {rotations:0.00}바퀴) - {speedNote}";
    }

    // 장치 없이 시험: 키를 한 번 누를 때마다 쳇바퀴 1회전분 신호(pulsesPerRotation 개)를 직전 누른 간격에 나눠 만든다.
    // 실제 신호처럼 한 프레임에 1개씩, 같은 ApplyPulse 경로를 탄다. 누른 간격으로 속도가 정해진다.
    // 신호가 생기면 처리 결과, 아니면 null.
    public string TickSimulation(float now, GameDynamicData cfg)
    {
        if (SimulateKey != KeyCode.None && Input.GetKeyDown(SimulateKey)) SimulateTap(now, cfg);
        return TickSimulatedPulses(now, cfg);
    }

    // 시뮬레이션 키 1번 = 쳇바퀴 1회전 (자동 플레이 시험에서도 쓴다)
    public void SimulateTap(float now, GameDynamicData cfg)
    {
        int n = Mathf.Max(1, Mathf.RoundToInt(cfg.pulsesPerRotation));
        float interval = simLastTapTime >= 0f && now - simLastTapTime <= StopSeconds(cfg) ? now - simLastTapTime : FirstRotationSeconds(cfg);
        simLastTapTime = now;
        if (simPending <= 0) simNextTime = now;
        simPending += n;
        simSpacing = interval / n;
    }

    private string TickSimulatedPulses(float now, GameDynamicData cfg)
    {
        if (simPending <= 0 || now < simNextTime) return null;
        simPending--;
        simNextTime += simSpacing;
        if (simNextTime < now) simNextTime = now;   // 프레임이 늦어 밀린 만큼은 버리지 않고 다음 프레임부터 이어 간다
        return ApplyPulse(now, cfg);
    }

    // 매 프레임 호출. 속도 계산·평활과 표시 거리 보간을 처리한다.
    public void Tick(float now, float dt, GameDynamicData cfg)
    {
        float d = cfg.GameDistancePerPulseM;

        float targetMps = 0f;
        if (lastPulseTime >= 0f && windowSpeedMps > 0f)
        {
            float elapsed = now - lastPulseTime;
            // 다음 신호가 예상 간격(신호 1개 거리 / 속도)보다 한참 늦으면 그만큼 느려진 것으로 본다
            if (elapsed <= StopSeconds(cfg))
                targetMps = Mathf.Min(windowSpeedMps, d * LateTolerance / Mathf.Max(0.01f, elapsed));
            else
            {
                windowSpeedMps = 0f;
                recentPulses.Clear();
            }
        }

        float smoothing = Mathf.Max(0.01f, cfg.speedSmoothingSeconds);
        float blend = 1f - Mathf.Exp(-dt / smoothing);
        SpeedMps = Mathf.Lerp(SpeedMps, targetMps, blend);
        if (targetMps <= 0f && SpeedMps < 0.05f) SpeedMps = 0f;

        // 화면 거리: 속도를 알면 그 속도로 간다. 정지 후 첫 신호처럼 속도를 아직 모르면
        // 확인된 신호 거리를 firstRotationSeconds 에 걸쳐 일정한 빠르기로 걸어간다 (속도·발전량은 그대로 0).
        // 남은 거리에 비례해 따라가면 신호 직후 순간적으로 튀어 나갔다가 멈춰 보인다.
        float gap = (float)(CountedDistanceM - TotalDistanceM);
        float displayTarget = targetMps > 0f ? targetMps : gap > 0.01f ? d / FirstRotationSeconds(cfg) : 0f;
        displayMps = Mathf.Lerp(displayMps, displayTarget, blend);
        if (displayTarget <= 0f && displayMps < 0.05f) displayMps = 0f;

        // 빨라져서 신호 1개 넘게 뒤처지면 넘친 거리만큼만 짧게 나눠 따라잡는다 (한 번에 옮기면 캐릭터가 튄다)
        float lagFix = gap > d ? (gap - d) / LagRecoverSeconds : 0f;
        double next = TotalDistanceM + (displayMps + lagFix) * dt;
        TotalDistanceM = Math.Max(TotalDistanceM, Math.Min(next, CountedDistanceM));
    }

    private static float WindowSeconds(GameDynamicData cfg) => Mathf.Max(0.2f, cfg.speedWindowSeconds);
    private static float StopSeconds(GameDynamicData cfg) => Mathf.Max(0.5f, cfg.rotationStopSeconds);
    private static float FirstRotationSeconds(GameDynamicData cfg) => Mathf.Max(0.3f, cfg.firstRotationSeconds);
}

// 쳇바퀴 회전 센서 컨트롤러와의 RS232 통신. 규약은 Docs/ControllerProtocol.md 참고.
// SerialPortManager 대신 이 컴포넌트를 씬에 둔다.
//
// 컨트롤러 → PC : R1 (기어 1회전 = 쳇바퀴 1/pulsesPerRotation 회전), ERR,내용 / PONG
// PC → 컨트롤러 : PING (연결 확인용, 열린 모든 포트로)
//
// 컨트롤러 id 로 쳇바퀴를 구분한다: 쳇바퀴마다 컨트롤러가 1대씩 붙고 체험자가 따로 달리므로,
// 같은 R1 신호라도 어느 포트로 들어왔는지에 따라 다른 쳇바퀴의 거리·속도로 더해야 한다.
// 포트(port.json controllerId) → 쳇바퀴 연결은 Setting.json 의 wheels[].controllerId.
//
// 포트가 닫혀 있어도 Setting.json 의 simulateKey 로 달리기를 흉내 낼 수 있다.
public class WheelSensorLink : SerialPortManager
{
    public const string MsgRotation = "R1";
    public const string MsgError = "ERR";
    public const string MsgPong = "PONG";
    public const string CmdPing = "PING";

    public static WheelSensorLink Link => Instance as WheelSensorLink;

    [Tooltip("PING 송신 간격(초). 0 이하이면 보내지 않는다.")]
    [SerializeField] private float pingIntervalSeconds = 5f;

    private readonly List<WheelSensor> sensors = new List<WheelSensor>();
    private float nextPingTime;

    public IReadOnlyList<WheelSensor> Sensors
    {
        get { EnsureSensors(); return sensors; }
    }

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    // JsonManager 와 Awake 순서가 보장되지 않으므로 센서 목록은 처음 쓸 때 만든다.
    private bool sensorsBuilt;

    private void EnsureSensors()
    {
        if (sensorsBuilt) return;
        sensorsBuilt = true;

        foreach (var w in Config.wheels)
        {
            if (w == null) continue;
            if (GetSensor(w.wheelId) != null)
            {
                Debug.LogWarning($"중복된 wheelId 무시: {w.wheelId}");
                continue;
            }
            sensors.Add(new WheelSensor(w.wheelId, w.controllerId, ParseKey(w.simulateKey)));
        }
        if (sensors.Count == 0) Debug.LogError("Setting.json 에 쳇바퀴(wheels) 설정이 없습니다.");
    }

    public WheelSensor GetSensor(int wheelId)
    {
        EnsureSensors();
        foreach (var s in sensors)
            if (s.WheelId == wheelId) return s;
        return null;
    }

    private WheelSensor GetSensorByController(int controllerId)
    {
        EnsureSensors();
        foreach (var s in sensors)
            if (s.ControllerId == controllerId) return s;
        return null;
    }

    protected override void Start()
    {
        base.Start();
        foreach (var s in Sensors)
            ShortcutHelp.Register(this, s.SimulateKey, $"쳇바퀴 {s.WheelId} 1바퀴 돌리기 (회전 신호 R1 을 1바퀴분 보냄, 누른 간격으로 속도 계산)", "모든 화면, 발전 중에만 기록됨");
    }

    protected override void OnDestroy()
    {
        ShortcutHelp.Unregister(this);
        base.OnDestroy();
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        EnsureSensors();
        foreach (var s in sensors)
        {
            string simNote = s.TickSimulation(now, Config);
            if (simNote != null) ReportInterpretation(s.ControllerId, $"[시뮬레이션] 쳇바퀴 {s.WheelId} {simNote}", simNote.StartsWith("무시됨"));
            s.Tick(now, dt, Config);
        }

        if (pingIntervalSeconds > 0f && now >= nextPingTime)
        {
            nextPingTime = now + pingIntervalSeconds;
            SendDataToAll(CmdPing);
        }
    }

    protected override void ReceivedData(int controllerId, string data)
    {
        if (string.IsNullOrEmpty(data)) return;

        string text = data.Trim().ToUpperInvariant();
        string head = text.Split(',')[0].Trim();

        if (head == MsgPong) return;
        if (head == MsgError)
        {
            ReportInterpretation(controllerId, $"장치 오류: {data}", true);
            return;
        }

        if (head == MsgRotation)
        {
            var sensor = GetSensorByController(controllerId);
            if (sensor == null)
            {
                ReportInterpretation(controllerId, $"무시됨: 이 컨트롤러(id {controllerId})에 연결된 쳇바퀴 없음 (Setting.json wheels[].controllerId 확인) - {data}", true);
                return;
            }

            string note = sensor.ApplyPulse(Time.unscaledTime, Config);
            ReportInterpretation(controllerId, $"쳇바퀴 {sensor.WheelId} {note}", note.StartsWith("무시됨"));
            return;
        }

        ReportInterpretation(controllerId, $"무시됨(알 수 없는 메시지): {data}", true);
    }

    private static KeyCode ParseKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return KeyCode.None;
        if (Enum.TryParse(name, true, out KeyCode key)) return key;
        Debug.LogWarning($"알 수 없는 simulateKey '{name}' - 시뮬레이션 끔");
        return KeyCode.None;
    }
}
