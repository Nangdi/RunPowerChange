using System.Collections.Generic;
using UnityEngine;

// 랭킹 목록이 있는 결과 패널 공통
public abstract class RankingPanel : WheelPanel
{
    [Header("랭킹 목록 (표시 인원은 Setting.json rankingShowCount)")]
    [SerializeField] private RankRowView rowPrefab;
    [SerializeField] private Transform rowRoot;

    private readonly List<RankRowView> rows = new List<RankRowView>();

    protected void SetRows(WheelRankingStore.Kind kind, List<PowerRecord> top, PowerRecord mine)
    {
        if (rowPrefab == null || rowRoot == null) return;

        int count = top != null ? top.Count : 0;
        while (rows.Count < count)
            rows.Add(Instantiate(rowPrefab, rowRoot));

        for (int i = 0; i < rows.Count; i++)
        {
            bool on = i < count;
            rows[i].gameObject.SetActive(on);
            if (on) rows[i].Set(RankOf(kind, top, i), top[i], kind, top[i] == mine);
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
