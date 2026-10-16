using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 09. 종합 정산 - 발전 기록과 판매 기록을 나란히 (종합 랭킹 아님)
public class SummaryPanel : WheelPanel
{
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text energyRankText;
    [SerializeField] private TMP_Text saleText;
    [SerializeField] private TMP_Text saleRankText;
    [SerializeField] private TMP_Text conditionText;
    [SerializeField] private Button againButton;
    [SerializeField] private Button homeButton;

    private void Awake()
    {
        OnClick(againButton, () => Session?.Replay());
        OnClick(homeButton, () => Session?.Next());
    }

    protected override void Refresh()
    {
        if (Session == null) return;

        SetText(energyText, PowerFormat.Energy(Session.EnergyWh));
        SetText(energyRankText, $"{PowerFormat.Rank(Session.EnergyRank, Session.TodayCount)}  {PowerFormat.TopPercent(Session.EnergyTopPercent)}");
        SetText(saleText, PowerFormat.Won(Session.Sale.saleWon));
        SetText(saleRankText, $"{PowerFormat.Rank(Session.SaleRank, Session.TodayCount)}  {PowerFormat.TopPercent(Session.SaleTopPercent)}");
        SetText(conditionText, $"{Session.Sale.voltageLabel}으로 송전 | {PowerFormat.Distance(Session.DistanceM)}m 달림");
    }
}
