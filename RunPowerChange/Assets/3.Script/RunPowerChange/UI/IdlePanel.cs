using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 01. 대기화면 - 체험시작 버튼을 눌러야 시작한다. 버튼 그림은 살짝 커졌다 작아지며 터치를 유도한다.
// 옆 쳇바퀴의 체험이 진행 중이라 합류할 수 없으면 버튼을 흐리게 막고 "체험 중" 안내와 남은 시간을 보여 준다.
public class IdlePanel : WheelPanel
{
    [Tooltip("체험시작 버튼")]
    [SerializeField] private Button startButton;
    [Tooltip("체험시작 버튼 그림 (숨쉬기 효과, 보통 startButton 과 같은 오브젝트). 비워두면 효과 없음")]
    [SerializeField] private Image startBadge;
    [SerializeField] private float pulseScale = 0.04f;
    [SerializeField] private float pulseSeconds = 1.6f;

    [Header("체험 진행 중 안내")]
    [SerializeField] private GameObject busyGroup;
    [SerializeField] private TMP_Text busyText;
    [SerializeField, Range(0f, 1f)] private float busyBadgeAlpha = 0.35f;
    [Tooltip("안내 문구 강조 색 (프리팹 생성 도구가 PowerUIKit 색으로 채운다)")]
    [SerializeField] private Color highlightColor = new Color32(251, 176, 59, 255);

    private void Awake() => OnClick(startButton, () => Session?.StartExperience());

    // 회차가 진행 중이면(합류 시간이 지남) 시작할 수 없다
    private bool Busy
    {
        get
        {
            var round = Session != null ? Session.Round : null;
            return round != null && !round.CanJoin;
        }
    }

    private void Update()
    {
        bool busy = Busy;
        if (startButton != null && startButton.interactable == busy) startButton.interactable = !busy;
        if (busyGroup != null && busyGroup.activeSelf != busy) busyGroup.SetActive(busy);
        if (busy && Session != null)
            SetText(busyText, $"지금 <color=#{ColorUtility.ToHtmlStringRGB(highlightColor)}>다른 체험</color>이 진행 중이에요. 약 {PowerFormat.Seconds(Session.Round.EstimatedWaitSeconds)}초 후 체험할 수 있어요.");

        if (startBadge == null) return;
        startBadge.color = new Color(1f, 1f, 1f, busy ? busyBadgeAlpha : 1f);
        float t = busy ? 0f : Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.1f, pulseSeconds)) * 0.5f + 0.5f;
        startBadge.rectTransform.localScale = Vector3.one * (1f + pulseScale * t);
    }

    public override void Hide()
    {
        if (startBadge != null) startBadge.rectTransform.localScale = Vector3.one;
        base.Hide();
    }
}
