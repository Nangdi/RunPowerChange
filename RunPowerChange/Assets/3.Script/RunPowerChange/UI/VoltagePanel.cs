using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 06. 송전전압 선택 (버튼 순서 = Setting.json voltages 순서: 0 낮은 전압, 1 높은 전압)
public class VoltagePanel : WheelPanel
{
    [Tooltip("보낼 전력 (판매 환산 발전량)")]
    [SerializeField] private TMP_Text sendText;
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text scaleText;
    [SerializeField] private Button lowButton;
    [SerializeField] private Button highButton;

    private void Awake()
    {
        OnClick(lowButton, () => Session?.SelectVoltage(0));
        OnClick(highButton, () => Session?.SelectVoltage(1));
    }

    protected override void Refresh()
    {
        if (Session == null) return;
        var cfg = JsonManager.instance.gameDynamicData;

        float sendWh = Session.AverageWatt * cfg.saleScaleMinutes / 60f;
        SetText(sendText, PowerFormat.Energy(sendWh));
        SetText(scaleText, $"지금 속도로 {cfg.saleScaleMinutes:0}분 동안 달렸다면 만들 수 있는 전기");
    }
}
