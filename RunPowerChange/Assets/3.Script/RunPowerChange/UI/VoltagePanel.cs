using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 06. 송전 전압 선택 - 누적 환산 발전량과 오늘의 발전왕 순위를 보여 주고 고/저전압 중 하나를 고른다.
// 버튼 순서 = Setting.json voltages 순서 (0 낮은 전압, 1 높은 전압).
// 하단 바는 무입력 복귀(idleTimeoutSeconds)까지 남은 시간이다.
public class VoltagePanel : WheelPanel
{
    [SerializeField] private Button lowButton;
    [SerializeField] private Button highButton;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text rankText;

    private void Awake()
    {
        OnClick(lowButton, () => Session?.SelectVoltage(0));
        OnClick(highButton, () => Session?.SelectVoltage(1));
    }

    protected override void Refresh()
    {
        if (Session == null) return;
        SetText(energyText, PowerFormat.Wh(Session.ScaledEnergyWh));
        SetText(rankText, Session.EnergyRank.ToString());
    }
}
