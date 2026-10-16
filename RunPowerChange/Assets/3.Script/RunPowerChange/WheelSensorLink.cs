using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

// 쳇바퀴 1대의 센서 상태. 속도와 앱 시작 후 누적 이동거리를 들고 있다.
// 세션은 시작 시점의 TotalDistanceM 을 기억해 두고 차이로 체험 거리를 계산한다.
public class WheelSensor
{
    public int WheelId { get; }
    public int ControllerId { get; }
    public KeyCode SimulateKey { get; }

    // 표시용으로 평활한 속도 (m/s)
    public float SpeedMps { get; private set; }
    public float SpeedKmh => SpeedMps * 3.6f;
    // 앱 시작 후 누적 이동거리 (m). 줄어들지 않는다.
    public double TotalDistanceM { get; private set; }
    // 마지막으로 센서 메시지를 받은 시각 (Time.unscaledTime). 받은 적 없으면 -1.
    public float LastMessageTime { get; private set; } = -1f;

    // 속도는 이 시간 동안의 누적거리 변화로 계산한다.
    // 메시지마다 계산하면 한 프레임에 여러 줄이 몰려 들어올 때 간격이 0에 가까워 값이 튄다.
    private const float SpeedWindowSeconds = 0.5f;
    // 잡음 판정 시 최소 시간 간격. 몰려 들어온 메시지를 잡음으로 버리지 않게 한다.
    private const float MinCheckSeconds = 0.25f;

    private uint lastPulse;
    private bool hasPulseBase;
    private float lastPulseTime;
    private float rpmSpeedMps;
    private float lastRpmTime = -1f;
    private float windowStartTime = -1f;
    private double windowStartDistance;
    private float windowSpeedMps;

    public WheelSensor(int wheelId, int controllerId, KeyCode simulateKey)
    {
        WheelId = wheelId;
        ControllerId = controllerId;
        SimulateKey = simulateKey;
    }

    // 누적 펄스 수신. 증가분을 거리로 더한다. 처리 결과를 문자열로 돌려준다 (null = 정상, 로그 생략).
    public string ApplyPulse(uint pulse, float now, GameDynamicData cfg)
    {
        LastMessageTime = now;

        if (!hasPulseBase || pulse < lastPulse)
        {
            string note = hasPulseBase ? $"펄스 값 감소({lastPulse}→{pulse}) - 컨트롤러 재시작으로 보고 기준값만 다시 잡음" : $"펄스 기준값 설정: {pulse}";
            hasPulseBase = true;
            lastPulse = pulse;
            lastPulseTime = now;
            return note;
        }

        uint delta = pulse - lastPulse;
        float dt = Mathf.Max(MinCheckSeconds, now - lastPulseTime);
        lastPulse = pulse;
        lastPulseTime = now;

        float meters = delta * MetersPerPulse(cfg);
        float speedKmh = meters / dt * 3.6f;
        if (speedKmh > cfg.maxValidSpeedKmh)
            return $"무시됨: 증가 {delta}펄스/{dt:0.00}s = {speedKmh:0.0}km/h (> {cfg.maxValidSpeedKmh}km/h)";

        TotalDistanceM += meters;
        return null;
    }

    // RPM 수신. 거리는 Tick 에서 시간 적분한다.
    public void ApplyRpm(float rpm, float now, GameDynamicData cfg)
    {
        LastMessageTime = now;
        lastRpmTime = now;
        rpmSpeedMps = Mathf.Max(0f, rpm) / 60f * Mathf.Max(0f, cfg.rollerCircumferenceM);
    }

    // 매 프레임 호출. RPM 적분, 시뮬레이션 키, 속도 계산과 평활을 처리한다.
    public void Tick(float now, float dt, GameDynamicData cfg)
    {
        // RPM 방식: 마지막 값이 sensorStopSeconds 안에 들어온 동안 속도 × 시간으로 거리 적분
        if (lastRpmTime >= 0f && now - lastRpmTime <= Mathf.Max(0.1f, cfg.sensorStopSeconds))
            TotalDistanceM += rpmSpeedMps * dt;

        // 장치 없이 시험: 키를 누르는 동안 정해진 속도로 달린다
        if (SimulateKey != KeyCode.None && Input.GetKey(SimulateKey))
            TotalDistanceM += cfg.simulateSpeedKmh / 3.6f * dt;

        // 누적거리 변화로 구간 속도 계산 (펄스·RPM·시뮬레이션 공통). 거리 변화가 없으면 자연히 0.
        if (windowStartTime < 0f)
        {
            windowStartTime = now;
            windowStartDistance = TotalDistanceM;
        }
        else if (now - windowStartTime >= SpeedWindowSeconds)
        {
            windowSpeedMps = (float)((TotalDistanceM - windowStartDistance) / (now - windowStartTime));
            windowStartTime = now;
            windowStartDistance = TotalDistanceM;
        }

        float smoothing = Mathf.Max(0.01f, cfg.speedSmoothingSeconds);
        SpeedMps = Mathf.Lerp(SpeedMps, windowSpeedMps, 1f - Mathf.Exp(-dt / smoothing));
        if (windowSpeedMps <= 0f && SpeedMps < 0.05f) SpeedMps = 0f;
    }

    private static float MetersPerPulse(GameDynamicData cfg)
    {
        return Mathf.Max(0f, cfg.rollerCircumferenceM) / Mathf.Max(1, cfg.pulsesPerRevolution);
    }
}

// 쳇바퀴 RPM 센서 컨트롤러와의 RS232 통신. 쳇바퀴 2대를 컨트롤러 id 로 구분한다.
// SerialPortManager 대신 이 컴포넌트를 씬에 둔다. 규약은 Docs/ControllerProtocol.md 참고.
//
// 컨트롤러 → PC : P,누적펄스 / RPM,값 / ERR,내용 / PONG
// PC → 컨트롤러 : PING (연결 확인용)
//
// 포트가 닫혀 있어도 Setting.json 의 simulateKey 로 달리기를 흉내 낼 수 있다.
public class WheelSensorLink : SerialPortManager
{
    public const string MsgPulse = "P";
    public const string MsgRpm = "RPM";
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

    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        EnsureSensors();
        foreach (var s in sensors) s.Tick(now, dt, Config);

        if (pingIntervalSeconds > 0f && now >= nextPingTime)
        {
            nextPingTime = now + pingIntervalSeconds;
            foreach (var s in sensors)
                if (IsControllerOpen(s.ControllerId)) SendData(s.ControllerId, CmdPing);
        }
    }

    protected override void ReceivedData(int controllerId, string data)
    {
        if (string.IsNullOrEmpty(data)) return;

        var sensor = GetSensorByController(controllerId);
        if (sensor == null)
        {
            ReportInterpretation(controllerId, $"무시됨: 이 컨트롤러에 연결된 쳇바퀴 없음 - {data}", true);
            return;
        }

        string[] parts = data.Trim().Split(',');
        string head = parts[0].Trim().ToUpperInvariant();
        float now = Time.unscaledTime;

        switch (head)
        {
            case MsgPulse:
                if (parts.Length > 1 && uint.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out uint pulse))
                {
                    string note = sensor.ApplyPulse(pulse, now, Config);
                    if (note != null) ReportInterpretation(controllerId, $"쳇바퀴 {sensor.WheelId} {note}", note.StartsWith("무시됨"));
                }
                else ReportInterpretation(controllerId, $"무시됨(형식 오류): {data}", true);
                break;
            case MsgRpm:
                if (parts.Length > 1 && float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float rpm))
                    sensor.ApplyRpm(rpm, now, Config);
                else ReportInterpretation(controllerId, $"무시됨(형식 오류): {data}", true);
                break;
            case MsgPong:
                break;
            case MsgError:
                ReportInterpretation(controllerId, $"장치 오류: {data}", true);
                break;
            default:
                ReportInterpretation(controllerId, $"무시됨(알 수 없는 메시지): {data}", true);
                break;
        }
    }

    private static KeyCode ParseKey(string name)
    {
        if (string.IsNullOrEmpty(name)) return KeyCode.None;
        if (Enum.TryParse(name, true, out KeyCode key)) return key;
        Debug.LogWarning($"알 수 없는 simulateKey '{name}' - 시뮬레이션 끔");
        return KeyCode.None;
    }
}
