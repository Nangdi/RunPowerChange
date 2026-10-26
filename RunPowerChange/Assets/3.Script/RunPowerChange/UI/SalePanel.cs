using TMPro;
using UnityEngine;

// 07. 최종 전력 판매 금액 - 누적 환산 발전량 > 송전 방식 > 판매 금액 카드가 saleStepSeconds 간격으로 차례로 나타난다.
// 판매 금액은 0 부터 얻은 금액까지 올라가고(SaleSlot, saleSlotSeconds), 다 오르면 살짝 커졌다 돌아온다.
// 소리(틱·동전)는 PowerSoundDirector 가 같은 시간표로 낸다. saleSeconds 후 랭킹 화면으로 넘어간다.
public class SalePanel : WheelPanel
{
    [Tooltip("나타나는 순서대로 (발전량 카드, 화살표, 송전 방식 카드, 화살표, 판매 금액 카드)")]
    [SerializeField] private CanvasGroup[] steps;
    [Tooltip("송전 방식 카드 (Setting.json voltages 순서: 0 저전압, 1 고전압). steps 의 세 번째 칸 안에 둔다")]
    [SerializeField] private GameObject[] voltageCards;
    [SerializeField] private TMP_Text energyText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private float popSeconds = 0.35f;
    [SerializeField] private float popScale = 0.85f;
    [Tooltip("금액이 다 오를 때 커지는 배율")]
    [SerializeField] private float doneScale = 1.06f;
    [SerializeField] private float doneSeconds = 0.4f;

    private float moneyTarget;

    protected override void Refresh()
    {
        if (Session == null) return;
        var sale = Session.Sale;
        SetText(energyText, PowerFormat.Wh(sale.sentWh));
        moneyTarget = sale.saleWon;
        for (int i = 0; voltageCards != null && i < voltageCards.Length; i++)
            if (voltageCards[i] != null) voltageCards[i].SetActive(i == Session.SelectedVoltageIndex);
        Update();
    }

    private void Update()
    {
        if (Session == null || steps == null) return;
        float interval = Mathf.Max(0.05f, JsonManager.instance.gameDynamicData.saleStepSeconds);
        for (int i = 0; i < steps.Length; i++)
        {
            if (steps[i] == null) continue;
            float t = Mathf.Clamp01((Session.StateElapsed - i * interval) / Mathf.Max(0.01f, popSeconds));
            steps[i].alpha = t;
            float ease = 1f - (1f - t) * (1f - t);
            steps[i].transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, ease);
        }
        UpdateMoney();
    }

    private void UpdateMoney()
    {
        if (moneyText == null) return;
        var cfg = JsonManager.instance.gameDynamicData;
        SaleSlot.Frame f = SaleSlot.Evaluate(moneyTarget, Session.StateElapsed, cfg);
        SetText(moneyText, PowerFormat.Won(f.value));

        // 다 오른 순간 살짝 커졌다 돌아온다
        float since = Session.StateElapsed - SaleSlot.StartTime(cfg) - SaleSlot.Duration(cfg);
        float k = f.done && since < doneSeconds ? Mathf.Sin(Mathf.Clamp01(since / Mathf.Max(0.01f, doneSeconds)) * Mathf.PI) : 0f;
        moneyText.transform.localScale = Vector3.one * Mathf.Lerp(1f, doneScale, k);
    }

    public override void Hide()
    {
        for (int i = 0; steps != null && i < steps.Length; i++)
        {
            if (steps[i] == null) continue;
            steps[i].alpha = 1f;
            steps[i].transform.localScale = Vector3.one;
        }
        if (moneyText != null) moneyText.transform.localScale = Vector3.one;
        base.Hide();
    }
}
