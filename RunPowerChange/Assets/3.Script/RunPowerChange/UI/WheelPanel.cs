using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 쳇바퀴 화면(32형) 패널 베이스. WheelScreen 이 자기 세션을 넘겨준다.
// 버튼은 세션 메서드를 직접 호출하고, 표시 값은 세션에서 읽는다.
public abstract class WheelPanel : PanelBase
{
    [Tooltip("자동 넘김 화면 하단의 남은 시간 바. 비워두면 표시하지 않음")]
    [SerializeField] private GaugeView autoGauge;

    protected WheelSession Session { get; private set; }
    // 이 화면(32형)에 3D 장면을 그리는 카메라. 캐릭터 위 표시 등 3D 위치를 화면 좌표로 바꿀 때 쓴다.
    protected Camera RaceCamera { get; private set; }

    public void Bind(WheelSession session, Camera raceCamera)
    {
        Session = session;
        RaceCamera = raceCamera;
    }

    public override void Show()
    {
        base.Show();
        Refresh();
    }

    // 화면이 열릴 때 한 번 값을 채운다. 실시간 값은 Update 에서 갱신한다.
    protected virtual void Refresh() { }

    private void LateUpdate()
    {
        if (autoGauge != null && Session != null) autoGauge.Set(Session.AutoAdvanceRemaining01);
    }

    protected static void SetText(TMP_Text text, string value)
    {
        if (text != null) text.text = value;
    }

    protected static void OnClick(Button button, UnityAction action)
    {
        if (button != null) button.onClick.AddListener(action);
    }
}

// 표시 형식 모음 (패널과 관전 화면이 같이 쓴다)
public static class PowerFormat
{
    // 누적 환산 발전량 (Wh). 천 단위가 넘으면 소수점 없이 자릿수 구분.
    public static string Wh(float wh) => wh >= 1000f ? $"{wh:#,0}" : $"{wh:0.0}";
    public static string Won(float won) => $"{won:#,0}";
    // 속도계·남은 시간은 두 자리 (시안: 00)
    public static string TwoDigits(float value) => $"{Mathf.Clamp(Mathf.FloorToInt(value), 0, 99):00}";
    public static string Seconds(float seconds) => $"{Mathf.Max(0, Mathf.CeilToInt(seconds))}";
    // 랭킹 표의 기록 시각 (시안: 26.10.22 1:22:43)
    public static string RecordTime(System.DateTime time) => time.ToString("yy.MM.dd H:mm:ss");
}
