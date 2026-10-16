using UnityEngine;
using UnityEngine.UI;

// 01. 대기화면 - 화면 어디를 눌러도 시작
public class IdlePanel : WheelPanel
{
    [Tooltip("화면 전체를 덮는 투명 버튼")]
    [SerializeField] private Button startButton;

    private void Awake() => OnClick(startButton, () => Session?.StartExperience());
}
