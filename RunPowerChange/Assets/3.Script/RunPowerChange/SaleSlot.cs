using UnityEngine;

// 판매 금액 카운트 연출 계산. 판매 금액 카드가 나타나면 금액이 0 부터 얻은 금액까지 올라간다 (처음엔 빠르게, 끝에서 천천히).
// 화면(SalePanel)과 소리(PowerSoundDirector)가 같은 시간표를 쓰도록 계산만 여기 둔다.
//  - 시작: 판매 화면에 들어온 뒤 (카드 수 - 1) × saleStepSeconds (판매 금액 카드가 나타나는 때)
//  - 길이: saleSlotSeconds
public static class SaleSlot
{
    // 판매 화면 카드 수 (발전량, 화살표, 송전 방식, 화살표, 판매 금액)
    public const int SaleStepCount = 5;
    // 틱 소리 간격(초). SoundManager 의 같은 효과음 중복 방지(0.08초)보다 길게
    public const float TickSeconds = 0.09f;

    public static float StartTime(GameDynamicData cfg) => (SaleStepCount - 1) * Mathf.Max(0.05f, cfg.saleStepSeconds);
    public static float Duration(GameDynamicData cfg) => Mathf.Max(0f, cfg.saleSlotSeconds);

    public struct Frame
    {
        public float value;      // 지금 보여 줄 금액
        public int tick;         // 오르기 시작한 뒤 틱 번호 (틱 소리용)
        public bool started;     // 오르기 시작함
        public bool done;        // 얻은 금액에 도달함
    }

    // target: 얻은 금액(원). elapsed: 판매 화면에 들어온 뒤 지난 시간
    public static Frame Evaluate(float target, float elapsed, GameDynamicData cfg)
    {
        var f = new Frame();
        float t = elapsed - StartTime(cfg);
        float duration = Duration(cfg);
        f.started = t >= 0f;
        if (!f.started) return f;
        if (duration <= 0f || t >= duration)
        {
            f.value = target;
            f.done = true;
            return f;
        }

        float p = t / duration;
        float ease = 1f - (1f - p) * (1f - p) * (1f - p);   // 처음엔 빠르게, 끝에서 천천히
        f.value = Mathf.Floor(target * ease);
        f.tick = Mathf.FloorToInt(t / TickSeconds);
        return f;
    }
}
