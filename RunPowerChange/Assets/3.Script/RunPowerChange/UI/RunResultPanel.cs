using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 05. 발전 결과 & 발전왕 순위 (기록은 아직 저장 전이라 미리보기 기록을 끼워 넣는다)
public class RunResultPanel : RankingPanel
{
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text distanceText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private Button nextButton;

    private void Awake() => OnClick(nextButton, () => Session?.Next());

    protected override void Refresh()
    {
        if (Session == null) return;

        SetText(energyText, PowerFormat.Energy(Session.EnergyWh));
        SetText(distanceText, PowerFormat.Distance(Session.DistanceM));
        SetText(rankText, PowerFormat.Rank(Session.EnergyRank, Session.TodayCount));
        SetText(percentText, PowerFormat.TopPercent(Session.EnergyTopPercent));

        var mine = new PowerRecord { ticks = DateTime.Now.Ticks, energyWh = Session.EnergyWh };
        var top = Session.Ranking.GetTopWith(WheelRankingStore.Kind.Energy, mine, JsonManager.instance.gameDynamicData.rankingShowCount);
        SetRows(WheelRankingStore.Kind.Energy, top, mine);
    }
}
