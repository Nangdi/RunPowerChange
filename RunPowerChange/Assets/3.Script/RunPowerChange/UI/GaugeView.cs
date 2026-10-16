using UnityEngine;

// 가로 게이지. Fill 의 오른쪽 앵커를 0~1 로 바꾼다 (게이지는 크기 애니메이션 예외).
public class GaugeView : MonoBehaviour
{
    [SerializeField] private RectTransform fill;

    public void Set(float value)
    {
        if (fill == null) return;
        Vector2 max = fill.anchorMax;
        max.x = Mathf.Clamp01(value);
        fill.anchorMax = max;
    }
}
