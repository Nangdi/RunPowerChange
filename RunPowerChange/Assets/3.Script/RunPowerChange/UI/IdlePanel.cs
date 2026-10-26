using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 01. 대기화면 - 화면 어디를 눌러도 시작. 다른 쳇바퀴가 시작해서 합류할 수 있으면 안내 문구를 띄운다.
public class IdlePanel : WheelPanel
{
    [Tooltip("화면 전체를 덮는 투명 버튼")]
    [SerializeField] private Button startButton;
    [Tooltip("합류 안내 묶음. 비워두면 표시하지 않음")]
    [SerializeField] private GameObject joinGroup;
    [SerializeField] private TMP_Text joinText;

    private void Awake() => OnClick(startButton, () => Session?.StartExperience());

    private void Update()
    {
        var round = Session != null ? Session.Round : null;
        bool canJoin = round != null && round.Current == GameRound.Phase.Open && round.CanJoin;
        if (joinGroup != null && joinGroup.activeSelf != canJoin) joinGroup.SetActive(canJoin);
        if (canJoin)
            SetText(joinText, $"옆 쳇바퀴에서 체험을 시작했어요!\n<b>{PowerFormat.Seconds(round.JoinRemaining)}초</b> 안에 시작하면 함께 달려요");
    }
}
