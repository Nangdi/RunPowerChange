using TMPro;
using UnityEngine;

// 04. 발전체험 - 속도, 출력, 누적 발전량, 거리, 남은 시간
public class RunPanel : WheelPanel
{
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private TMP_Text wattText;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text distanceText;
    [SerializeField] private TMP_Text remainText;
    [Tooltip("남은 시간 게이지")]
    [SerializeField] private GaugeView timeGauge;
    [Tooltip("현재 출력 게이지 (최대 = Setting.json maxWatt)")]
    [SerializeField] private GaugeView powerGauge;

    protected override void Refresh() => Update();

    private void Update()
    {
        if (Session == null) return;
        var cfg = JsonManager.instance.gameDynamicData;

        SetText(speedText, PowerFormat.Speed(Session.SpeedKmh));
        SetText(wattText, PowerFormat.Watt(Session.Watt));
        SetText(energyText, PowerFormat.Energy(Session.EnergyWh));
        SetText(distanceText, PowerFormat.Distance(Session.DistanceM));
        SetText(remainText, PowerFormat.Time(Session.RunRemaining));
        if (timeGauge != null) timeGauge.Set(Session.RunRemaining / Session.RunDuration);
        if (powerGauge != null) powerGauge.Set(cfg.maxWatt > 0f ? Session.Watt / cfg.maxWatt : 0f);
    }
}
