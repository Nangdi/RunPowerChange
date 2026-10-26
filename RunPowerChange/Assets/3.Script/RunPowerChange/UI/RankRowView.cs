using TMPro;
using UnityEngine;

// 랭킹 표 한 줄: 순위 / 최고 속도 / 누적 발전량 / 송전 중 손실 / 도착한 전력 / 전기 판매 금액.
// Highlight 는 방금 체험한 기록 표시.
public class RankRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text lossText;
    [SerializeField] private TMP_Text arrivedText;
    [SerializeField] private TMP_Text saleText;
    [Tooltip("1위 순위 배지. 비워두면 표시하지 않음")]
    [SerializeField] private GameObject firstBadge;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private GameObject highlight;

    public void Set(int rank, PowerRecord record, bool isMine)
    {
        bool has = record != null;
        Text(rankText, has ? rank.ToString() : "");
        Text(speedText, has ? PowerFormat.Speed(record.maxSpeedKmh) : "");
        Text(energyText, has ? PowerFormat.Kwh(record.scaledEnergyWh) : "");
        Text(lossText, has ? $"-{PowerFormat.Kwh(record.lossWh)}" : "");
        Text(arrivedText, has ? PowerFormat.Kwh(record.arrivedWh) : "");
        Text(saleText, has ? PowerFormat.Won(record.saleWon) : "");
        if (firstBadge != null) firstBadge.SetActive(has && rank == 1);
        if (highlight != null) highlight.SetActive(has && isMine);
    }

    private static void Text(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
