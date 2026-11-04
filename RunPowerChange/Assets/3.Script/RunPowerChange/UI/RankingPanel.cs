using System.Collections.Generic;
using UnityEngine;

// 08. 랭킹 확인 - 오늘 발전왕·판매왕 종합 1~5위(두 쳇바퀴 기록 합산)와 맨 아래 나의 종합 순위 줄. resultAutoCloseSeconds 후 대기 화면.
// 순위 숫자 1~5, 줄 구분선, 단위(wh, ₩)는 정적 레이어 그림에 있다.
// 같은 회차의 옆 쳇바퀴 체험자가 이 화면을 보는 중에 기록을 저장하면 순위가 바뀌므로 다시 그린다.
public class RankingPanel : WheelPanel
{
    [SerializeField] private RankTableView energyTable;
    [SerializeField] private RankTableView saleTable;

    private int shownVersion = -1;

    protected override void Refresh()
    {
        if (Session == null) return;
        shownVersion = WheelRankingStore.Version;
        Fill(energyTable, WheelRankingStore.Kind.Energy, Session.EnergyRank);
        Fill(saleTable, WheelRankingStore.Kind.Sale, Session.SaleRank);
    }

    private void Update()
    {
        if (Session != null && shownVersion != WheelRankingStore.Version) Refresh();
    }

    private void Fill(RankTableView table, WheelRankingStore.Kind kind, int myRank)
    {
        if (table == null) return;
        List<PowerRecord> top = WheelRankingStore.TotalTop(kind, table.RowCount);
        table.Set(kind, top, Session.LastRecord, myRank);
    }
}
