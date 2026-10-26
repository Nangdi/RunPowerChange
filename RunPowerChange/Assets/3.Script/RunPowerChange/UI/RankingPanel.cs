using System.Collections.Generic;
using UnityEngine;

// 08. 랭킹 확인 - 이 쳇바퀴의 오늘 발전왕·판매왕 1~5위와 맨 아래 나의 순위 줄. resultAutoCloseSeconds 후 대기 화면.
// 순위 숫자 1~5, 줄 구분선, 단위(wh, ₩)는 정적 레이어 그림에 있다.
public class RankingPanel : WheelPanel
{
    [SerializeField] private RankTableView energyTable;
    [SerializeField] private RankTableView saleTable;

    protected override void Refresh()
    {
        if (Session == null) return;
        Fill(energyTable, WheelRankingStore.Kind.Energy, Session.EnergyRank);
        Fill(saleTable, WheelRankingStore.Kind.Sale, Session.SaleRank);
    }

    private void Fill(RankTableView table, WheelRankingStore.Kind kind, int myRank)
    {
        if (table == null) return;
        List<PowerRecord> top = Session.Ranking.GetTop(kind, table.RowCount);
        table.Set(kind, top, Session.LastRecord, myRank);
    }
}
