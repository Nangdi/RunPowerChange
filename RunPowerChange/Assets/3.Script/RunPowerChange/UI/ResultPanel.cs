using TMPro;
using UnityEngine;

// 09. 결과 확인 - 최종 결과(최고 속도, 누적 발전량, 송전 중 손실, 도착 전력, 판매 금액)와 오늘 판매왕 랭킹.
// 랭킹은 상위 rankingShowCount 명, 내 기록이 그 밖이면 마지막 줄에 따로 보여 준다.
public class ResultPanel : RankingPanel
{
    [SerializeField] private TMP_Text maxSpeedText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text lossText;
    [SerializeField] private TMP_Text arrivedText;
    [SerializeField] private TMP_Text saleText;
    [Tooltip("기준 단가·설비 비용 안내. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text priceText;

    protected override void Refresh()
    {
        if (Session == null) return;
        var cfg = JsonManager.instance.gameDynamicData;
        var sale = Session.Sale;

        SetText(maxSpeedText, PowerFormat.Speed(Session.MaxSpeedKmh));
        SetText(energyText, PowerFormat.Kwh(sale.sentWh));
        SetText(lossText, $"-{PowerFormat.Kwh(sale.lossWh)}");
        SetText(arrivedText, PowerFormat.Kwh(sale.arrivedWh));
        SetText(saleText, PowerFormat.Won(sale.saleWon));
        SetText(priceText, sale.facilityCostWon > 0f
            ? $"기준단가 {cfg.smpWonPerKWh:0}원/kWh | 변전 설비 비용 {PowerFormat.Won(sale.facilityCostWon)}원 차감 (가상 조건)"
            : $"기준단가 {cfg.smpWonPerKWh:0}원/kWh (가상 조건)");

        var top = Session.Ranking.GetTop(WheelRankingStore.Kind.Sale, cfg.rankingShowCount);
        SetRows(WheelRankingStore.Kind.Sale, top, Session.LastRecord, Session.SaleRank);
    }
}
