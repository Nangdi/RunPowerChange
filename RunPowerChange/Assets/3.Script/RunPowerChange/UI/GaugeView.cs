using UnityEngine;
using UnityEngine.UI;

// 가로 게이지. 디자이너 게이지 이미지(Image Filled, Horizontal)의 채움 비율을 0~1 로 바꾼다.
public class GaugeView : MonoBehaviour
{
    [SerializeField] private Image fill;
    [Tooltip("같은 비율로 채울 덧그림 (게이지 위 하이라이트 줄 등). 비워두면 표시하지 않음")]
    [SerializeField] private Image[] followers;

    public void Set(float value)
    {
        value = Mathf.Clamp01(value);
        if (fill != null) fill.fillAmount = value;
        if (followers == null) return;
        foreach (var f in followers)
            if (f != null) f.fillAmount = value;
    }
}
