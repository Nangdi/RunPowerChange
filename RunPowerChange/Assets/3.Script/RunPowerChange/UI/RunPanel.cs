using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 05. 전기생산 체험 - 현재 속도·환산 출력(작게), 누적 환산 발전량(하단 바, 크게), 남은 시간(우상단 원형)
public class RunPanel : WheelPanel
{
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text wattText;
    [Tooltip("누적 환산 발전량 (바 위 숫자)")]
    [SerializeField] private TMP_Text energyText;
    [Tooltip("누적 환산 발전량 바 (최대 = 최대 출력으로 달렸을 때)")]
    [SerializeField] private GaugeView energyGauge;
    [Tooltip("바 아래 설명. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text scaleText;
    [SerializeField] private TMP_Text remainText;
    [Tooltip("남은 시간 원형 게이지 (Image Filled)")]
    [SerializeField] private Image remainRing;

    protected override void Refresh()
    {
        var cfg = JsonManager.instance.gameDynamicData;
        SetText(scaleText, $"현재 속도로 {cfg.saleScaleMinutes / 60f:0.#}시간 동안 달려 생산한 전력량");
        Update();
    }

    private void Update()
    {
        if (Session == null) return;

        SetText(speedText, PowerFormat.Speed(Session.SpeedKmh));
        SetText(wattText, PowerFormat.Watt(Session.Watt));
        SetText(energyText, $"{PowerFormat.Kwh(Session.ScaledEnergyWh)} <size=60%>kWh</size>");
        if (energyGauge != null)
            energyGauge.Set(Session.MaxScaledEnergyWh > 0f ? Session.ScaledEnergyWh / Session.MaxScaledEnergyWh : 0f);
        SetText(remainText, PowerFormat.Seconds(Session.RunRemaining));
        if (remainRing != null) remainRing.fillAmount = Session.RunRemaining / Session.RunDuration;
    }
}
