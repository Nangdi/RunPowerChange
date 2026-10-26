using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 06. 전기 발전 완료 - 누적 환산 발전량, 오늘의 발전왕 순위(기록 전 미리보기), 송전 체험 시작 버튼
public class RunResultPanel : WheelPanel
{
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Button nextButton;

    private void Awake() => OnClick(nextButton, () => Session?.Next());

    protected override void Refresh()
    {
        if (Session == null) return;
        SetText(energyText, PowerFormat.Kwh(Session.ScaledEnergyWh));
        SetText(rankText, PowerFormat.Rank(Session.EnergyRank));
    }
}
