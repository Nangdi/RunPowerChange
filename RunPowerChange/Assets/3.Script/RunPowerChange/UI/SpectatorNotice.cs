using TMPro;
using UnityEngine;

// 85형 관전 화면 가운데 아래의 안내 카드와 가운데 카운트다운. 무엇을 보여 줄지는 SpectatorScreen 이 정하고 여기는 그리기만 한다.
//  - 카드: 남색 배지(지금 단계) + 제목 + 설명 + 남은 초. 체험 안내 화면의 반투명 흰 카드와 같은 모양.
//    내용(배지·제목)이 바뀌면 살짝 작게 시작해 커지며 나타나고, 숨길 때는 흐려지며 사라진다.
//  - 카운트다운: 32형과 같은 디자이너 숫자 그림(3, 2, 1, GO). 숫자가 바뀔 때마다 크게 나타났다가 제 크기로 줄어든다.
public class SpectatorNotice : MonoBehaviour
{
    [Header("안내 카드")]
    [SerializeField] private CanvasGroup card;
    [SerializeField] private TMP_Text tagText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subText;
    [Tooltip("남은 초 등. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text countText;
    [SerializeField] private float fadeSeconds = 0.3f;
    [SerializeField] private float popScale = 0.9f;
    [SerializeField] private float popSeconds = 0.35f;

    [Header("카운트다운")]
    [Tooltip("1, 2, 3 … 순서의 숫자 그림 (인덱스 0 = 1)")]
    [SerializeField] private RectTransform[] numbers;
    [SerializeField] private RectTransform go;
    [SerializeField] private float countPopScale = 1.35f;
    [SerializeField] private float countPopSeconds = 0.35f;

    [Header("PLAYER 이름 색 (인덱스 = wheelId - 1, 레인 색)")]
    [SerializeField] private Color[] playerColors = { new Color32(47, 128, 237, 255), new Color32(236, 56, 140, 255) };

    private bool cardVisible;
    private string cardKey;
    private float cardShownTime;
    private int countShown = int.MinValue;
    private float countShownTime;

    private void Awake()
    {
        if (card != null) card.alpha = 0f;
        ShowCountdown(-1);
    }

    // "PLAYER 01" 을 레인 색으로
    public string Player(int wheelId)
    {
        string label = $"PLAYER {wheelId:00}";
        int i = wheelId - 1;
        if (playerColors == null || i < 0 || i >= playerColors.Length) return label;
        return $"<color=#{ColorUtility.ToHtmlStringRGB(playerColors[i])}>{label}</color>";
    }

    public void ShowCard(string tag, string title, string sub, string count = "")
    {
        string key = tag + "|" + title;
        if (!cardVisible || key != cardKey)
        {
            cardKey = key;
            cardShownTime = Time.unscaledTime;
            if (!cardVisible && card != null) card.alpha = 0f;
        }
        cardVisible = true;
        SetText(tagText, tag);
        SetText(titleText, title);
        SetText(subText, sub);
        SetText(countText, count);
        if (card != null && !card.gameObject.activeSelf) card.gameObject.SetActive(true);
    }

    public void HideCard()
    {
        cardVisible = false;
        cardKey = null;
    }

    // n > 0 숫자, 0 = GO, 음수 = 숨김
    public void ShowCountdown(int n)
    {
        if (n == countShown) return;
        countShown = n;
        countShownTime = Time.unscaledTime;
        for (int i = 0; numbers != null && i < numbers.Length; i++)
            if (numbers[i] != null) numbers[i].gameObject.SetActive(i == n - 1);
        if (go != null) go.gameObject.SetActive(n == 0);
    }

    private void Update()
    {
        UpdateCard();
        UpdateCountdown();
    }

    private void UpdateCard()
    {
        if (card == null) return;
        float target = cardVisible ? 1f : 0f;
        card.alpha = Mathf.MoveTowards(card.alpha, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds));
        if (!cardVisible)
        {
            if (card.alpha <= 0f && card.gameObject.activeSelf) card.gameObject.SetActive(false);
            return;
        }
        float t = Mathf.Clamp01((Time.unscaledTime - cardShownTime) / Mathf.Max(0.01f, popSeconds));
        float ease = 1f - (1f - t) * (1f - t);
        card.transform.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, ease);
    }

    private void UpdateCountdown()
    {
        RectTransform current = countShown == 0 ? go
            : countShown > 0 && numbers != null && countShown - 1 < numbers.Length ? numbers[countShown - 1] : null;
        if (current == null) return;
        float t = Mathf.Clamp01((Time.unscaledTime - countShownTime) / Mathf.Max(0.01f, countPopSeconds));
        current.localScale = Vector3.one * Mathf.Lerp(countPopScale, 1f, 1f - (1f - t) * (1f - t));
    }

    private static void SetText(TMP_Text t, string value)
    {
        if (t == null) return;
        value ??= "";
        if (t.text != value) t.text = value;
    }
}
