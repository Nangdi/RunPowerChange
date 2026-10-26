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

    public void Bind(WheelSession session) => Session = session;

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
    // 환산 발전량은 수천 Wh 단위라 kWh 로 보여 준다
    public static string Kwh(float wh) => $"{wh / 1000f:#,0.0}";
    public static string Energy(float wh) => $"{wh:0.00}";
    public static string Won(float won) => $"{won:#,0}";
    public static string Watt(float w) => $"{w:0}";
    public static string Speed(float kmh) => $"{kmh:0.0}";
    public static string Distance(float m) => $"{m:#,0}";
    public static string Seconds(float seconds) => $"{Mathf.Max(0, Mathf.CeilToInt(seconds))}";

    public static string Time(float seconds)
    {
        int s = Mathf.Max(0, Mathf.CeilToInt(seconds));
        return $"{s / 60:00}:{s % 60:00}";
    }

    public static string Rank(int rank) => $"{rank}<size=50%>위</size>";
}
