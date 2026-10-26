using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 07. 송전전압 선택 (버튼 순서 = Setting.json voltages 순서: 0 낮은 전압, 1 높은 전압)
public class VoltagePanel : WheelPanel
{
    [SerializeField] private Button lowButton;
    [SerializeField] private Button highButton;
    [Tooltip("보낼 전력. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text sendText;

    private void Awake()
    {
        OnClick(lowButton, () => Session?.SelectVoltage(0));
        OnClick(highButton, () => Session?.SelectVoltage(1));
    }

    protected override void Refresh()
    {
        if (Session == null) return;
        SetText(sendText, $"보낼 전력  <b>{PowerFormat.Kwh(Session.ScaledEnergyWh)}</b> kWh");
    }
}
