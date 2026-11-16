using UnityEngine;
using UnityEngine.UI;

// 1위 기록 선 위 표시 그림. 오늘 기록이 없어 선이 전날 1위일 때는 "전날 최고 랭킹" 그림으로 바꾼다.
// 85형(SpectatorScreen)과 32형 발전 화면(RunPanel)이 같이 쓴다. 전날 그림은 Tools/make_record_tag_prev.py 로 만든다.
public class RecordTagView : MonoBehaviour
{
    [SerializeField] private Image image;
    [Tooltip("오늘 최고 랭킹")]
    [SerializeField] private Sprite todaySprite;
    [Tooltip("전날 최고 랭킹. 비워두면 오늘 그림을 그대로 쓴다")]
    [SerializeField] private Sprite previousDaySprite;

    public void Set(bool previousDay)
    {
        if (image == null) return;
        Sprite sprite = previousDay && previousDaySprite != null ? previousDaySprite : todaySprite;
        if (sprite != null && image.sprite != sprite) image.sprite = sprite;
    }
}
