using UnityEngine;
using UnityEngine.UI;

// 02. 체험안내 - 발전 → 송전 → 판매 흐름, 시작 버튼
public class GuidePanel : WheelPanel
{
    [SerializeField] private Button confirmButton;

    private void Awake() => OnClick(confirmButton, () => Session?.ConfirmGuide());
}
