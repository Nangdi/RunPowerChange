using System;
using System.Collections.Generic;
using UnityEngine;

public class SerialPortManager : MonoBehaviour
{
    public static SerialPortManager Instance { get; private set; }

    private readonly List<SerialPortChannel> channels = new List<SerialPortChannel>();

    // RS232 송수신/연결 변화를 외부(UI 등)에 알리기 위한 이벤트
    public event Action<int, string> OnDataReceived;
    public event Action<int, string> OnDataSent;
    public event Action<int, bool> OnConnectionChanged;
    // 수신 데이터 해석 결과(인식됨/무시됨과 이유). 로그 패널이 구독한다.
    public event Action<int, string> OnInterpreted;

    public bool IsControllerOpen(int controllerId)
    {
        var ch = FindChannel(controllerId);
        return ch != null && ch.IsOpen;
    }

    protected virtual void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    protected virtual void Start()
    {
        var portJson = JsonManager.instance.portJson;
        if (portJson?.ports == null || portJson.ports.Count == 0)
        {
            Debug.LogError("port.json에 포트 설정이 없습니다.");
            return;
        }

        foreach (var cfg in portJson.ports)
        {
            Debug.Log($"포트 설정 로드됨: id={cfg.controllerId}, COM={cfg.com}, Baud={cfg.baudLate}, enabled={cfg.enabled}");

            // 동일 controllerId 중복 시 첫 번째만 사용
            if (FindChannel(cfg.controllerId) != null)
            {
                Debug.LogWarning($"중복된 controllerId 무시: {cfg.controllerId}");
                continue;
            }
            OpenChannel(cfg);
        }
    }

    // port.json 항목 1개로 채널을 만들어 연다. 비활성/포트 없음이면 열지 않는다.
    private void OpenChannel(PortConfig cfg)
    {
        // 컨트롤러별 RS232 사용 여부 - 비활성 시 채널 생성/포트 오픈 자체를 건너뜀
        if (!cfg.enabled || string.IsNullOrEmpty(cfg.com))
        {
            Debug.Log($"[Ctrl {cfg.controllerId}] RS232 사용 안 함 - 포트 열지 않음 ({cfg.com})");
            OnConnectionChanged?.Invoke(cfg.controllerId, false);
            return;
        }

        var channel = new SerialPortChannel(
            cfg.controllerId, cfg.com, cfg.baudLate,
            OnChannelMessage,
            OnChannelSent,
            OnChannelConnectionChanged);
        channels.Add(channel);
        channel.Open();
    }

    // 컨트롤러 1개의 포트를 닫고 port.json 의 현재 값으로 다시 연다 (디버그 패널에서 COM 을 바꿨을 때).
    public void ReopenController(int controllerId)
    {
        var old = FindChannel(controllerId);
        if (old != null)
        {
            old.Close();
            channels.Remove(old);
        }

        var cfg = FindPortConfig(controllerId);
        if (cfg == null)
        {
            Debug.LogWarning($"[Ctrl {controllerId}] port.json 에 설정 없음 - 다시 열지 않음");
            return;
        }
        OpenChannel(cfg);
    }

    // port.json 의 모든 컨트롤러 포트를 닫고 다시 연다 (재연결 버튼).
    public void ReconnectAll()
    {
        Cleanup();
        var portJson = JsonManager.instance.portJson;
        if (portJson?.ports == null) return;
        foreach (var cfg in portJson.ports)
        {
            if (FindChannel(cfg.controllerId) != null) continue;
            OpenChannel(cfg);
        }
    }

    private static PortConfig FindPortConfig(int controllerId)
    {
        var portJson = JsonManager.instance != null ? JsonManager.instance.portJson : null;
        if (portJson?.ports == null) return null;
        foreach (var cfg in portJson.ports)
            if (cfg.controllerId == controllerId) return cfg;
        return null;
    }

    private void OnChannelMessage(int controllerId, string data)
    {
        OnDataReceived?.Invoke(controllerId, data);
        ReceivedData(controllerId, data);
    }

    private void OnChannelSent(int controllerId, string data)
    {
        OnDataSent?.Invoke(controllerId, data);
    }

    private void OnChannelConnectionChanged(int controllerId, bool isOpen)
    {
        OnConnectionChanged?.Invoke(controllerId, isOpen);
    }

    // 어느 포트(컨트롤러)에서 들어왔는지를 알 수 있도록 controllerId를 함께 전달한다.
    protected virtual void ReceivedData(int controllerId, string data)
    {
        // 상속받은 클래스에서 프로젝트별 처리 구현
    }

    // 상속 클래스가 수신 해석 결과를 로그 패널과 콘솔에 남길 때 쓴다.
    protected void ReportInterpretation(int controllerId, string message, bool warning = false)
    {
        if (warning) Debug.LogWarning($"[Ctrl {controllerId}] {message}");
        else Debug.Log($"[Ctrl {controllerId}] {message}");
        OnInterpreted?.Invoke(controllerId, message);
    }

    // 특정 컨트롤러의 포트로 송신
    public void SendData(int controllerId, string message)
    {
        var channel = FindChannel(controllerId);
        if (channel == null)
        {
            Debug.LogWarning($"[Ctrl {controllerId}] 채널 미존재 - 송신 실패");
            return;
        }
        channel.Send(message);
    }

    // 열린 모든 포트로 송신
    public void SendDataToAll(string message)
    {
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i].IsOpen) channels[i].Send(message);
        }
    }

    protected SerialPortChannel FindChannel(int controllerId)
    {
        for (int i = 0; i < channels.Count; i++)
        {
            if (channels[i].ControllerId == controllerId) return channels[i];
        }
        return null;
    }

    private void Cleanup()
    {
        for (int i = 0; i < channels.Count; i++)
        {
            channels[i].Close();
        }
        channels.Clear();
    }

    void OnApplicationQuit()
    {
        Debug.Log("Task 종료");
        Cleanup();
    }

    protected virtual void OnDestroy()
    {
        Cleanup();
    }
}
