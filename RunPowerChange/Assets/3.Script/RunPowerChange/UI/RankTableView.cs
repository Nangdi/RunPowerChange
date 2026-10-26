using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 랭킹 표 하나 (발전왕 또는 판매왕). 디자인 그림의 줄 칸에 맞춰 둔 글자들에 기록 시각과 값을 채운다.
// 32형 랭킹 화면과 85형 관전 화면이 같이 쓴다. 나의 순위 줄은 비워두면 표시하지 않는다.
public class RankTableView : MonoBehaviour
{
    [Tooltip("1위부터 순서대로 줄의 기록 시각")]
    [SerializeField] private TMP_Text[] timeTexts;
    [Tooltip("1위부터 순서대로 줄의 값 (발전량 Wh 또는 판매 금액)")]
    [SerializeField] private TMP_Text[] valueTexts;

    [Header("나의 순위")]
    [Tooltip("비워두면 표시하지 않음 (85형)")]
    [SerializeField] private TMP_Text myRankText;
    [Tooltip("비워두면 표시하지 않음 (85형)")]
    [SerializeField] private TMP_Text myTimeText;
    [Tooltip("비워두면 표시하지 않음 (85형)")]
    [SerializeField] private TMP_Text myValueText;

    public int RowCount => timeTexts != null ? timeTexts.Length : 0;

    public void Set(WheelRankingStore.Kind kind, List<PowerRecord> top, PowerRecord mine, int myRank)
    {
        for (int i = 0; i < RowCount; i++)
        {
            PowerRecord r = top != null && i < top.Count ? top[i] : null;
            SetText(timeTexts[i], r != null ? PowerFormat.RecordTime(r.Time) : "-");
            if (valueTexts != null && i < valueTexts.Length) SetText(valueTexts[i], r != null ? Value(kind, r) : "");
        }

        SetText(myRankText, mine != null ? myRank.ToString() : "");
        SetText(myTimeText, mine != null ? PowerFormat.RecordTime(mine.Time) : "");
        SetText(myValueText, mine != null ? Value(kind, mine) : "");
    }

    // 발전왕은 화면에 보이는 누적 환산 발전량(Wh), 판매왕은 판매 금액(원)
    private static string Value(WheelRankingStore.Kind kind, PowerRecord r) =>
        kind == WheelRankingStore.Kind.Energy ? PowerFormat.Wh(r.scaledEnergyWh) : PowerFormat.Won(r.saleWon);

    private static void SetText(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
