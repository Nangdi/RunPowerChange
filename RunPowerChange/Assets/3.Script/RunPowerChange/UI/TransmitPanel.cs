using TMPro;
using UnityEngine;

// 07. 송전 시뮬레이션 - 발전소 → 송전선 → 도시, 출발/손실/도착 전력
public class TransmitPanel : WheelPanel
{
    [SerializeField] private TMP_Text voltageText;
    [SerializeField] private TMP_Text sentText;
    [SerializeField] private TMP_Text lossText;
    [SerializeField] private TMP_Text arrivedText;
    [Tooltip("송전선 진행 게이지")]
    [SerializeField] private GaugeView lineGauge;

    protected override void Refresh()
    {
        if (Session == null) return;
        SetText(voltageText, Session.Sale.voltageLabel);
        SetText(sentText, PowerFormat.Energy(Session.Sale.sentWh));
        Update();
    }

    private void Update()
    {
        if (Session == null) return;

        // 진행에 따라 손실이 쌓이고, 끝나면 도착 전력이 나타난다
        float t = Session.TransmitProgress;
        var sale = Session.Sale;
        if (lineGauge != null) lineGauge.Set(t);
        SetText(lossText, PowerFormat.Energy(sale.lossWh * t));
        SetText(arrivedText, t >= 1f ? PowerFormat.Energy(sale.arrivedWh) : "-");
    }
}
