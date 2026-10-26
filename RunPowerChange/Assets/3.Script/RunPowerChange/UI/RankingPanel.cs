using System.Collections.Generic;
using UnityEngine;

// 랭킹 표가 있는 패널 공통. 줄은 RankRowView 프리팹으로 필요한 만큼 만든다.
public abstract class RankingPanel : WheelPanel
{
    [Header("랭킹 표 (표시 인원은 Setting.json rankingShowCount)")]
    [SerializeField] private RankRowView rowPrefab;
    [SerializeField] private Transform rowRoot;
    [Tooltip("내 기록이 상위 밖일 때 상위 목록과 내 줄 사이에 보이는 표시. 비워두면 표시하지 않음")]
    [SerializeField] private GameObject gapMark;

    private readonly List<RankRowView> rows = new List<RankRowView>();

    // top 은 순위순 목록. mine 이 top 밖이면 myRank 와 함께 마지막 줄로 붙인다.
    protected void SetRows(WheelRankingStore.Kind kind, List<PowerRecord> top, PowerRecord mine, int myRank)
    {
        RankTable.Fill(rows, rowPrefab, rowRoot, gapMark, kind, top, mine, myRank);
    }
}

// 랭킹 표 채우기 (32형 결과 화면과 85형 관전 화면이 같이 쓴다)
public static class RankTable
{
    public static void Fill(List<RankRowView> rows, RankRowView rowPrefab, Transform rowRoot, GameObject gapMark,
        WheelRankingStore.Kind kind, List<PowerRecord> top, PowerRecord mine, int myRank)
    {
        if (rowPrefab == null || rowRoot == null) return;

        int count = top != null ? top.Count : 0;
        bool mineOutside = mine != null && (top == null || !top.Contains(mine));
        int total = count + (mineOutside ? 1 : 0);
        while (rows.Count < total)
            rows.Add(Object.Instantiate(rowPrefab, rowRoot));

        for (int i = 0; i < rows.Count; i++)
        {
            bool on = i < total;
            rows[i].gameObject.SetActive(on);
            if (!on) continue;

            if (i < count) rows[i].Set(RankOf(kind, top, i), top[i], top[i] == mine);
            else rows[i].Set(myRank, mine, true);
        }

        if (gapMark != null)
        {
            gapMark.SetActive(mineOutside);
            if (mineOutside) gapMark.transform.SetSiblingIndex(count);
        }
    }

    // 같은 값은 같은 순위
    private static int RankOf(WheelRankingStore.Kind kind, List<PowerRecord> list, int index)
    {
        while (index > 0 && Value(kind, list[index - 1]) == Value(kind, list[index])) index--;
        return index + 1;
    }

    private static float Value(WheelRankingStore.Kind kind, PowerRecord r) => kind == WheelRankingStore.Kind.Energy ? r.energyWh : r.saleWon;
}
