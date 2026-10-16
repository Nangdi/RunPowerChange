using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 08. 판매 결과 & 판매왕 순위
public class SaleResultPanel : RankingPanel
{
    [SerializeField] private TMP_Text arrivedText;
    [SerializeField] private TMP_Text saleText;
    [Tooltip("기준단가·설비비 안내. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private Button nextButton;

    private void Awake() => OnClick(nextButton, () => Session?.Next());

    protected override void Refresh()
    {
        if (Session == null) return;
        var cfg = JsonManager.instance.gameDynamicData;
        var sale = Session.Sale;

        SetText(arrivedText, PowerFormat.Energy(sale.arrivedWh));
        SetText(saleText, PowerFormat.Won(sale.saleWon));
        SetText(priceText, sale.facilityCostWon > 0f
            ? $"기준단가 {cfg.smpWonPerKWh:0}원/kWh | 변전 설비 비용 {sale.facilityCostWon:0.#}원 차감"
            : $"기준단가 {cfg.smpWonPerKWh:0}원/kWh");
        SetText(rankText, PowerFormat.Rank(Session.SaleRank, Session.TodayCount));
        SetText(percentText, PowerFormat.TopPercent(Session.SaleTopPercent));

        SetRows(WheelRankingStore.Kind.Sale, Session.Ranking.GetTop(WheelRankingStore.Kind.Sale, cfg.rankingShowCount), Session.LastRecord);
    }
}
