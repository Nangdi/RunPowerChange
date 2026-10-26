using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 08. 송전 체험 - 출발 발전량에서 선택한 전압의 손실만큼 줄어든다 (달리지 않음).
// 하단 바: 전체(출발 발전량) 위에 도착 전력이 줄어드는 모습.
public class TransmitPanel : WheelPanel
{
    [SerializeField] private TMP_Text voltageText;
    [SerializeField] private TMP_Text sentText;
    [SerializeField] private TMP_Text lossText;
    [SerializeField] private TMP_Text arrivedText;
    [Tooltip("하단 바: 도착 전력 (출발 발전량 대비)")]
    [SerializeField] private GaugeView arrivedGauge;
    [Tooltip("하단 바 위 도착 전력 숫자")]
    [SerializeField] private TMP_Text arrivedBarText;
    [Tooltip("하단 바 오른쪽 출발 발전량 숫자")]
    [SerializeField] private TMP_Text sentBarText;
    [SerializeField] private TMP_Text remainText;
    [SerializeField] private Image remainRing;

    protected override void Refresh()
    {
        if (Session == null) return;
        SetText(voltageText, $"{Session.Sale.voltageLabel}으로 송전 중");
        SetText(sentText, PowerFormat.Kwh(Session.Sale.sentWh));
        SetText(sentBarText, $"누적 환산 발전량 <b>{PowerFormat.Kwh(Session.Sale.sentWh)}</b> kWh");
        Update();
    }

    private void Update()
    {
        if (Session == null) return;

        // 진행에 따라 손실이 쌓이고 도착 전력이 줄어든다
        float t = Session.TransmitProgress;
        var sale = Session.Sale;
        float loss = sale.lossWh * t;
        float arrived = sale.sentWh - loss;
        SetText(lossText, $"-{PowerFormat.Kwh(loss)}");
        SetText(arrivedText, PowerFormat.Kwh(arrived));
        SetText(arrivedBarText, $"{PowerFormat.Kwh(arrived)} <size=60%>kWh</size>");
        if (arrivedGauge != null) arrivedGauge.Set(sale.sentWh > 0f ? arrived / sale.sentWh : 0f);

        float total = Mathf.Max(0.1f, JsonManager.instance.gameDynamicData.transmitSeconds);
        float remain = Mathf.Max(0f, total - Session.StateElapsed);
        SetText(remainText, PowerFormat.Seconds(remain));
        if (remainRing != null) remainRing.fillAmount = remain / total;
    }
}
