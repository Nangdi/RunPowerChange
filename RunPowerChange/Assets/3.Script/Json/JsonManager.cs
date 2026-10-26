using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 설정창(SettingsPanelUI)에 노출되는 런타임 설정.
// public bool 필드를 추가하면 설정창에서 토글로 다룰 수 있다.
[Serializable]
public class GameSettingData
{
    // Unity 빌드 창을 항상 맨 위로 띄울지 여부.
    public bool useUnityOnTop;
    // 마우스 커서 표시 여부. false면 커서를 숨긴다(키오스크/전시용).
    public bool showMouseCursor;
}

// 쳇바퀴 1대의 장치·화면 연결.
[Serializable]
public class WheelConfig
{
    // 쳇바퀴 번호 (1, 2). 랭킹 파일과 화면 표시에 쓴다.
    public int wheelId = 1;
    // 회전 센서 컨트롤러 id (port.json 의 controllerId)
    public int controllerId = 1;
    // 32형 터치모니터 디스플레이 번호 (0 = Display 1)
    public int displayIndex;
    // 장치 없이 시험할 때 누르고 있으면 달리는 키 (KeyCode 이름)
    public string simulateKey = "UpArrow";
}

// 송전전압 1종의 계수. 실제 값이 아니라 체험용 가상 계수.
[Serializable]
public class VoltageOption
{
    public string id = "high";
    // 화면 표시 이름
    public string label = "높은 전압";
    // 송전 중 손실 비율 (0~1). 도착 전력 = 출발 전력 × (1 - lossRate)
    public float lossRate = 0.05f;
    // 승압·강압 설비 비용(원). 판매금액에서 뺀다. 적게 보내면 고전압이 오히려 불리해지도록 둔다.
    public float facilityCostWon;
}

// 프로젝트별로 실행 중에 바뀌는 데이터를 담는 곳 (Setting.json).
// 달려라! 전력 챌린지 운영값. 숫자는 모두 임시값이며 현장·한전 의견에 맞춰 바꾼다.
[Serializable]
public class GameDynamicData
{
    // 발전 체험 시간(초)
    public float runSeconds = 50f;
    // 시작 카운트다운(초)
    public int countdownSeconds = 3;
    // 체험 안내 화면 자동 넘김(초)
    public float guideSeconds = 10f;
    // 한 명이 시작한 뒤 다른 쳇바퀴가 같은 회차로 합류할 수 있는 시간(초). 참가 대기 화면에 남은 초가 나온다.
    public float joinWaitSeconds = 9f;
    // 참가가 마감된 뒤 "준비하세요!" 화면(초). 끝나면 체험 안내로 넘어간다.
    public float readySeconds = 3f;

    // 쳇바퀴 1회전 이동거리(m). 컨트롤러가 1회전마다 신호를 보내면 이만큼 거리를 더한다. (직경 2m → 6.283)
    public float wheelCircumferenceM = 6.283f;
    // 이 시간(초) 동안 회전 신호가 없으면 멈춘 것으로 본다. 길수록 천천히 걸어도 속도가 0으로 떨어지지 않는다.
    public float rotationStopSeconds = 5f;
    // 속도 표시 평활 시간(초)
    public float speedSmoothingSeconds = 0.3f;
    // 직전 회전과의 간격이 이 속도(km/h)보다 빠르게 환산되면 센서 튐(잡음)으로 보고 무시
    public float maxValidSpeedKmh = 40f;
    // 정지 후 첫 회전 신호는 속도를 아직 모르므로, 화면 캐릭터가 그 1회전 거리를 이 시간(초)에 걸쳐 걸어간다
    public float firstRotationSeconds = 2f;

    // 환산 발전출력(W) = min(maxWatt, 속도(km/h) × wattPerKmh)
    public float wattPerKmh = 15f;
    public float maxWatt = 300f;

    // 판매 환산: "이 속도로 saleScaleMinutes 분 달렸을 때" 의 발전량으로 계산 (시안: 100시간)
    public float saleScaleMinutes = 6000f;
    // 판매단가 기준: 이 속도(km/h, 15세 평균 최고속도)로 체험 시간 내내 달리고 가장 유리한 전압을 고르면
    // 판매 금액이 saleReferenceWon 이 되도록 판매단가(원/kWh)를 자동으로 정한다. (50m 평균 약 8.7초 ≈ 20km/h)
    public float saleReferenceKmh = 20f;
    public float saleReferenceWon = 5000f;
    public List<VoltageOption> voltages = new List<VoltageOption>
    {
        new VoltageOption { id = "low", label = "낮은 전압", lossRate = 0.30f, facilityCostWon = 0f },
        new VoltageOption { id = "high", label = "높은 전압", lossRate = 0.05f, facilityCostWon = 300f },
    };

    // 최종 전력 판매 금액 화면(초). 끝나면 랭킹 화면으로 넘어간다.
    public float saleSeconds = 10f;
    // 판매 금액 화면에서 카드가 하나씩 나타나는 간격(초)
    public float saleStepSeconds = 0.6f;

    // 송전 전압 선택 화면에서 무입력 시 대기화면 복귀(초). 0 이하이면 사용 안 함. (그 밖의 화면은 자동으로 넘어간다)
    public float idleTimeoutSeconds = 30f;
    // 랭킹 화면 자동 넘김(초)
    public float resultAutoCloseSeconds = 10f;

    // 속도계 바늘이 끝까지 가는 속도(km/h)
    public float speedometerMaxKmh = 20f;
    // 결승선이 체험 끝나기 몇 초 전부터 다가오는지
    public float finishApproachSeconds = 10f;
    // 3D 배경(기둥·레일)이 달린 1m 마다 흘러가는 거리(유닛). 클수록 같은 속도에서 배경이 빨리 지나간다.
    public float raceScrollPerMeter = 1.5f;
    // 두 체험자의 거리 차 1m 를 화면에서 앞뒤로 몇 유닛 벌릴지. 클수록 조금만 앞서도 크게 벌어진다.
    public float raceGapPerMeter = 0.1f;
    // 85형 아래 칸(랭킹 → 왜 그럴까요? → 우리와 어떤 관련이?)이 바뀌는 간격(초)
    public float spectatorInfoSeconds = 10f;

    // 쳇바퀴별 연결 (PC 1대에 쳇바퀴 2대)
    public List<WheelConfig> wheels = new List<WheelConfig>
    {
        new WheelConfig { wheelId = 1, controllerId = 1, displayIndex = 0, simulateKey = "UpArrow" },
        new WheelConfig { wheelId = 2, controllerId = 2, displayIndex = 1, simulateKey = "W" },
    };
    // 85형 관전 화면 디스플레이 번호
    public int spectatorDisplayIndex = 2;
}

[Serializable]
public class PortConfig
{
    public int controllerId;
    public string com;
    public int baudLate;
    // RS232 사용 여부. false면 해당 컨트롤러는 포트를 열지 않는다.
    public bool enabled = true;
}

[Serializable]
public class PortJson
{
    // 컨트롤러별 전용 포트 목록. 필요한 만큼 port.json 에서 늘려 쓴다.
    public List<PortConfig> ports = new List<PortConfig>
    {
        new PortConfig { controllerId = 1, com = "COM1", baudLate = 19200, enabled = false },
    };
}

[Serializable]
public class TcpJson
{
    // 메인 서버 접속 주소와 포트
    public string host = "127.0.0.1";
    public int port = 5000;
    // 끊겼을 때 재시도 간격(초). 0 이하이면 재연결 시도하지 않음.
    public float reconnectIntervalSeconds = 3f;
    // 송신 메시지 끝에 줄바꿈을 자동으로 붙일지 여부
    public bool appendOutgoingLineEnding = true;
}

public class JsonManager : MonoBehaviour
{
    public static JsonManager instance;
    public GameSettingData gameSettingData = new GameSettingData();
    public PortJson portJson = new PortJson();
    public TcpJson tcpJson = new TcpJson();
    public GameDynamicData gameDynamicData = new GameDynamicData();

    private string gameDataPath;
    private string gameDynamicDataPath;
    private string portPath;
    private string tcpPath;

    public string GameDataPath => gameDataPath;
    // 설정 파일이 모여 있는 폴더 (설정창에 표시용)
    public static string ConfigFolder => Application.persistentDataPath;

    // 싱글톤을 초기화하고 JSON 파일에서 런타임 설정 데이터를 불러옵니다.
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        // 설정 파일은 모두 persistentDataPath 에 둔다 (빌드를 덮어써도 현장 값 유지).
        // StreamingAssets 의 같은 이름 파일은 첫 실행 때 복사해 오는 기본값 원본이다.
        portPath = Path.Combine(ConfigFolder, "port.json");
        tcpPath = Path.Combine(ConfigFolder, "tcp.json");
        gameDynamicDataPath = Path.Combine(ConfigFolder, "Setting.json");
        gameDataPath = Path.Combine(ConfigFolder, "gameSettingData.json");

        gameSettingData ??= new GameSettingData();
        gameDynamicData ??= new GameDynamicData();
        portJson ??= new PortJson();
        tcpJson ??= new TcpJson();

        gameSettingData = LoadConfig(gameDataPath, gameSettingData);
        gameDynamicData = LoadConfig(gameDynamicDataPath, gameDynamicData);
        portJson = LoadConfig(portPath, portJson);
        tcpJson = LoadConfig(tcpPath, tcpJson);
        Debug.Log($"설정 폴더: {ConfigFolder}");
    }

    // persistentDataPath 설정 파일을 읽는다.
    // 없으면 StreamingAssets 의 같은 이름 파일을 복사해 오고, 그것도 없으면 코드 기본값으로 만든다.
    // 읽은 뒤 다시 저장해서 새로 추가된 필드도 파일에 기본값으로 채운다.
    // 읽기에 실패하면 기본값으로 실행한다 (파일은 덮어쓰지 않음).
    public static T LoadConfig<T>(string path, T data) where T : new()
    {
        if (data == null) data = new T();

        try
        {
            if (!File.Exists(path))
            {
                string source = Path.Combine(Application.streamingAssetsPath, Path.GetFileName(path));
                if (File.Exists(source))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.Copy(source, path);
                    Debug.Log($"기본 설정 복사: {source} -> {path}");
                }
            }

            data = LoadData(path, data);
        }
        catch (Exception ex)
        {
            Debug.LogError($"설정 파일 읽기 실패 - 기본값으로 실행: {path}\n{ex.Message}");
            return data;
        }

        try { SaveData(data, path); }
        catch (Exception ex) { Debug.LogError($"설정 파일 저장 실패: {path}\n{ex.Message}"); }
        return data;
    }

    // 현재 운영값(Setting.json)을 저장한다. 설정창에서 숫자 값을 바꿨을 때 호출.
    public void SaveGameDynamicData()
    {
        SaveData(gameDynamicData, gameDynamicDataPath);
    }

    // 포트 설정(port.json)을 저장한다. 디버그 패널에서 COM 포트를 바꿨을 때 호출.
    public void SavePortJson()
    {
        SaveData(portJson, portPath);
    }

    // 현재 게임 설정 데이터를 gameSettingData.json 파일에 저장합니다.
    public void SaveGameSettingData()
    {
        SaveData(gameSettingData, gameDataPath);
    }

    // 지정한 경로에 JSON 파일을 생성하거나 덮어씁니다.
    public static void SaveData<T>(T jsonObject, string path) where T : new()
    {
        if (jsonObject == null)
            jsonObject = new T();

        string directoryPath = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            Directory.CreateDirectory(directoryPath);

        string json = JsonUtility.ToJson(jsonObject, true);
        File.WriteAllText(path, json);
        Debug.Log($"Saved JSON: {path}");
    }

    // JSON 파일을 읽고, 파일이 없으면 기본값으로 새 파일을 만듭니다.
    // FromJsonOverwrite를 사용해 json에 없는 필드는 인스턴스 기본값을 유지한다.
    // (예: 기존 port.json에 enabled 필드가 없어도 PortConfig.enabled = true 가 보존됨)
    public static T LoadData<T>(string path, T data) where T : new()
    {
        if (data == null) data = new T();

        if (!File.Exists(path))
        {
            Debug.LogWarning($"JSON file does not exist. Creating a new file: {path}");
            SaveData(data, path);
            return data;
        }

        Debug.Log($"Loaded JSON: {path}");
        string json = File.ReadAllText(path);
        JsonUtility.FromJsonOverwrite(json, data);
        return data;
    }
}
