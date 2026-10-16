using System;
using TMPro;
using UnityEngine;

// 랭킹 목록 한 줄: 순위 / 기록 시각 / 값. Highlight 는 방금 체험한 기록 표시.
public class RankRowView : MonoBehaviour
{
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text valueText;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private GameObject highlight;

    public void Set(int rank, PowerRecord record, WheelRankingStore.Kind kind, bool isMine)
    {
        if (record == null)
        {
            if (rankText != null) rankText.text = "";
            if (nameText != null) nameText.text = "";
            if (valueText != null) valueText.text = "";
            if (highlight != null) highlight.SetActive(false);
            return;
        }

        // 이름 입력 대신 체험 종료 시각으로 구분한다
        if (rankText != null) rankText.text = $"{rank}위";
        if (nameText != null) nameText.text = isMine ? "나의 기록" : new DateTime(record.ticks).ToString("HH:mm:ss");
        if (valueText != null)
            valueText.text = kind == WheelRankingStore.Kind.Energy
                ? $"{PowerFormat.Energy(record.energyWh)} <size=60%>Wh</size>"
                : $"{PowerFormat.Won(record.saleWon)} <size=60%>원</size>";
        if (highlight != null) highlight.SetActive(isMine);
    }
}
