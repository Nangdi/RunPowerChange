using UnityEngine;
using UnityEngine.UI;

// 디자이너 숫자 그림(0~9)으로 정수를 그린다. 자릿수 칸은 미리 만들어 두고(오른쪽부터 1의 자리), 필요한 칸만 켠다.
// 칸들은 가운데 정렬된 HorizontalLayoutGroup 안에 있어 자릿수가 바뀌어도 가운데에 놓인다.
public class DigitView : MonoBehaviour
{
    [Tooltip("숫자 0~9 그림")]
    [SerializeField] private Sprite[] digitSprites;
    [Tooltip("자릿수 칸 (왼쪽부터 높은 자리)")]
    [SerializeField] private Image[] cells;

    private int shown = int.MinValue;

    public void Set(int value)
    {
        if (value == shown || cells == null || digitSprites == null || digitSprites.Length < 10) return;
        shown = value;

        string text = Mathf.Max(0, value).ToString();
        int start = cells.Length - text.Length;
        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null) continue;
            bool on = i >= start;
            cells[i].gameObject.SetActive(on);
            if (on) cells[i].sprite = digitSprites[text[i - start] - '0'];
        }
    }
}
