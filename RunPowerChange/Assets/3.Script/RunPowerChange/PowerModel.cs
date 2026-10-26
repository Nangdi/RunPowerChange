using UnityEngine;

// 송전·판매 계산 결과 1건.
public struct SaleResult
{
    public string voltageId;
    public string voltageLabel;
    // 판매 환산 발전량 (Wh): 평균 출력으로 saleScaleMinutes 동안 달렸을 때
    public float sentWh;
    // 송전 중 손실 (Wh)
    public float lossWh;
    // 도착 전력 (Wh)
    public float arrivedWh;
    // 설비 비용 차감 전 금액 (원)
    public float grossWon;
    // 승압·강압 설비 비용 (원)
    public float facilityCostWon;
    // 최종 판매금액 (원, 0 이상)
    public float saleWon;
}

// 체험용 환산 계산. 실제 발전·송전 값이 아니라 Setting.json 의 임시 계수로 계산한다.
public static class PowerModel
{
    // 속도(km/h) → 환산 발전출력(W)
    public static float WattFromSpeed(float speedKmh, GameDynamicData cfg)
    {
        return Mathf.Clamp(speedKmh * cfg.wattPerKmh, 0f, Mathf.Max(0f, cfg.maxWatt));
    }

    // 시안의 "누적 환산 발전량"(Wh): 지금까지의 평균 출력으로 saleScaleMinutes 동안 달렸을 때의 발전량.
    // 달리는 동안 0에서 시작해 늘어나고, 끝나는 순간 송전 출발 전력(Sell 의 sentWh)과 같아진다.
    public static float ScaledEnergyWh(float energyWh, float runSeconds, GameDynamicData cfg)
    {
        float duration = Mathf.Max(1f, cfg.runSeconds);
        return Mathf.Max(0f, energyWh) * Mathf.Max(0f, cfg.saleScaleMinutes) * 60f / duration;
    }

    // 체험 중 평균 출력(W) = 누적 발전량 / 경과 시간
    public static float AverageWatt(float energyWh, float seconds)
    {
        return seconds > 0f ? energyWh * 3600f / seconds : 0f;
    }

    // 판매단가(원/kWh). 기준 속도(saleReferenceKmh)로 끝까지 달리고 가장 유리한 전압을 고르면 판매 금액이 saleReferenceWon 이 되는 값.
    // 출력·환산 시간·손실률·설비 비용을 바꿔도 기준 체험자의 판매 금액은 그대로 유지된다.
    public static float WonPerKWh(GameDynamicData cfg)
    {
        float sentWh = WattFromSpeed(Mathf.Max(0f, cfg.saleReferenceKmh), cfg) * Mathf.Max(0f, cfg.saleScaleMinutes) / 60f;
        float best = float.MaxValue;
        if (cfg.voltages != null)
        {
            foreach (var v in cfg.voltages)
            {
                if (v == null) continue;
                float arrivedKWh = sentWh * (1f - Mathf.Clamp01(v.lossRate)) / 1000f;
                if (arrivedKWh <= 0f) continue;
                best = Mathf.Min(best, (Mathf.Max(0f, cfg.saleReferenceWon) + Mathf.Max(0f, v.facilityCostWon)) / arrivedKWh);
            }
        }
        return best == float.MaxValue ? 0f : best;
    }

    public static VoltageOption GetVoltage(GameDynamicData cfg, int index)
    {
        if (cfg.voltages == null || index < 0 || index >= cfg.voltages.Count) return null;
        return cfg.voltages[index];
    }

    // 평균 출력과 선택한 전압으로 송전·판매 결과를 계산한다.
    public static SaleResult Sell(float averageWatt, VoltageOption voltage, GameDynamicData cfg)
    {
        voltage ??= new VoltageOption();
        float loss = Mathf.Clamp01(voltage.lossRate);

        var r = new SaleResult
        {
            voltageId = voltage.id,
            voltageLabel = voltage.label,
            sentWh = Mathf.Max(0f, averageWatt) * Mathf.Max(0f, cfg.saleScaleMinutes) / 60f,
        };
        r.lossWh = r.sentWh * loss;
        r.arrivedWh = r.sentWh - r.lossWh;
        r.grossWon = r.arrivedWh / 1000f * WonPerKWh(cfg);
        r.facilityCostWon = Mathf.Max(0f, voltage.facilityCostWon);
        r.saleWon = Mathf.Max(0f, r.grossWon - r.facilityCostWon);
        return r;
    }
}
