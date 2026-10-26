using TMPro;
using UnityEngine;

// 다음 게임 대기 - 옆 쳇바퀴의 체험이 진행 중이라 이번 회차에 합류하지 못한 경우.
// 체험 안내와 같은 3단계 카드를 미리 보여 주고, 지금 회차가 끝나면 자동으로 시작한다.
public class WaitNextPanel : GuidePanel
{
    [SerializeField] private TMP_Text remainText;

    private void Update()
    {
        var round = Session != null ? Session.Round : null;
        if (round == null) return;
        SetText(remainText, $"약 {PowerFormat.Seconds(round.EstimatedWaitSeconds)}초 후 시작");
    }
}
