using TMPro;
using UnityEngine;

// 03. 카운트다운 - 3, 2, 1, START
public class CountdownPanel : WheelPanel
{
    [SerializeField] private TMP_Text countText;

    private int shown = -1;

    protected override void Refresh() => shown = -1;

    private void Update()
    {
        if (Session == null || countText == null) return;

        int n = Session.CountdownNumber;
        if (n == shown) return;
        shown = n;
        countText.text = n > 0 ? n.ToString() : "START";
    }
}
