using UnityEngine;

// 04. 카운트다운 - 3, 2, 1, GO (디자이너 숫자 그림). 숫자가 바뀔 때마다 크게 나타났다가 제 크기로 줄어든다.
public class CountdownPanel : WheelPanel
{
    [Tooltip("1, 2, 3 … 순서의 숫자 그림 (인덱스 0 = 1)")]
    [SerializeField] private RectTransform[] numbers;
    [SerializeField] private RectTransform go;
    [SerializeField] private float popScale = 1.35f;
    [SerializeField] private float popSeconds = 0.35f;

    private int shown = -1;
    private float shownTime;

    protected override void Refresh() => shown = -1;

    private void Update()
    {
        if (Session == null) return;

        int n = Session.CountdownNumber;
        if (n != shown)
        {
            shown = n;
            shownTime = Time.unscaledTime;
            for (int i = 0; numbers != null && i < numbers.Length; i++)
                if (numbers[i] != null) numbers[i].gameObject.SetActive(i == n - 1);
            if (go != null) go.gameObject.SetActive(n <= 0);
        }

        RectTransform current = n <= 0 ? go : numbers != null && n - 1 < numbers.Length ? numbers[n - 1] : null;
        if (current == null) return;
        float t = Mathf.Clamp01((Time.unscaledTime - shownTime) / Mathf.Max(0.01f, popSeconds));
        current.localScale = Vector3.one * Mathf.Lerp(popScale, 1f, 1f - (1f - t) * (1f - t));
    }
}
