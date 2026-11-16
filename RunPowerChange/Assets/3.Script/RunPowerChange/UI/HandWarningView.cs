using UnityEngine;
using UnityEngine.UI;

// 05. 전기생산 체험 중 손잡이를 놓았을 때(컨트롤러가 FREE 를 보냄, HOLD 가 오면 사라짐) 띄우는 경고 (PSD 05-게임화면 / 경고문구(추가)).
//  - 화면 가장자리 빨간 테두리가 맥박 치듯 밝아졌다 어두워진다 (게임에서 체력이 없을 때처럼)
//  - 가운데: 어두운 바탕 + 손잡이 그림 + "양쪽 손잡이를 모두 잡아야 전력이 생산됩니다!" (PSD 그대로)
//  - 경고 아이콘은 일정 간격으로 깜빡인다 (PSD 메모 "일정간격 깜빡이게")
// RunPanel 이 매 프레임 Set(손잡이 놓음 여부)를 부른다. 효과는 알파와 스케일만 바꾼다.
[RequireComponent(typeof(CanvasGroup))]
public class HandWarningView : MonoBehaviour
{
    [Tooltip("빨간 테두리 (흰 그림을 Image.color 로 칠한다)")]
    [SerializeField] private Image redEdge;
    [Tooltip("깜빡이는 경고 아이콘")]
    [SerializeField] private Graphic icon;
    [Tooltip("살짝 커졌다 작아지는 손잡이 그림·문구 묶음 (비워두면 크기 효과 없음)")]
    [SerializeField] private RectTransform pulseTarget;

    [Header("효과")]
    [Tooltip("나타나고 사라지는 시간(초)")]
    [SerializeField] private float fadeSeconds = 0.15f;
    [Tooltip("빨간 테두리 맥박 한 번 시간(초)")]
    [SerializeField] private float pulseSeconds = 1.2f;
    [SerializeField, Range(0f, 1f)] private float edgeMinAlpha = 0.15f;
    [SerializeField, Range(0f, 1f)] private float edgeMaxAlpha = 0.45f;
    [Tooltip("손잡이 그림·문구가 맥박에 맞춰 커지는 배율")]
    [SerializeField] private float pulseScale = 1.04f;
    [Tooltip("경고 아이콘 깜빡임 한 번(켜짐+꺼짐) 시간(초)")]
    [SerializeField] private float blinkSeconds = 0.6f;

    private CanvasGroup group;
    private float shown;      // 0~1 나타난 정도
    private float startTime;  // 이번 경고가 시작된 시각 (맥박·깜빡임을 처음부터 시작하려고)

    public bool Visible => shown > 0f;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        ResetEffect();
    }

    // 패널에 다시 들어올 때 이전 경고가 남지 않게
    public void ResetEffect()
    {
        if (group == null) group = GetComponent<CanvasGroup>();
        shown = 0f;
        group.alpha = 0f;
        if (pulseTarget != null) pulseTarget.localScale = Vector3.one;
        gameObject.SetActive(true);
    }

    public void Set(bool on)
    {
        float now = Time.unscaledTime;
        if (on && shown <= 0f) startTime = now;

        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        shown = Mathf.Clamp01(shown + (on ? step : -step));
        group.alpha = shown;
        if (shown <= 0f) return;

        float t = now - startTime;
        // 맥박: 0 → 1 → 0 (시작할 때 가장 밝게)
        float pulse = 0.5f + 0.5f * Mathf.Cos(2f * Mathf.PI * t / Mathf.Max(0.05f, pulseSeconds));
        if (redEdge != null)
        {
            Color c = redEdge.color;
            c.a = Mathf.Lerp(edgeMinAlpha, edgeMaxAlpha, pulse);
            redEdge.color = c;
        }
        if (pulseTarget != null) pulseTarget.localScale = Vector3.one * Mathf.Lerp(1f, pulseScale, pulse);
        if (icon != null)
        {
            bool iconOn = Mathf.Repeat(t, Mathf.Max(0.05f, blinkSeconds)) < blinkSeconds * 0.5f;
            icon.canvasRenderer.SetAlpha(iconOn ? 1f : 0f);
        }
    }
}
