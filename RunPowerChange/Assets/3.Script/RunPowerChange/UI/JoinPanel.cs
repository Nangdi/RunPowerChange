using UnityEngine;
using UnityEngine.UI;

// 02. 참가 대기 - 세 가지 모습을 한 패널에서 바꿔 보여 준다.
//  - Waiting : 먼저 시작한 체험자. "함께 할 참가자를 기다리고 있습니다." + 합류 마감까지 남은 초
//  - Invite  : 옆 쳇바퀴가 먼저 시작해 합류할 수 있는 대기화면. "당신의 참가를 기다리고 있습니다!" + 참여하기 버튼
//  - Ready   : 참가 마감. "준비하세요!" + 체험 안내까지 남은 초
// 아래 PLAYER 01/02 칸은 이번 회차 참가자면 READY, 아니면 WAIT... 를 보여 준다.
public class JoinPanel : WheelPanel
{
    public enum Mode { Waiting, Invite, Ready }

    [Header("문구 (모드 순서: Waiting, Invite, Ready)")]
    [SerializeField] private GameObject[] messages;
    [SerializeField] private GameObject[] subMessages;

    [Header("남은 초")]
    [Tooltip("카운터 묶음 (원 + 숫자). Invite 모드에서는 숨긴다")]
    [SerializeField] private GameObject counterGroup;
    [SerializeField] private RectTransform ring;
    [SerializeField] private float ringDegreesPerSecond = 90f;
    [SerializeField] private DigitView digits;

    [Header("참여하기")]
    [SerializeField] private Button joinButton;

    [Header("PLAYER 01/02 상태 (인덱스 = wheelId - 1)")]
    [SerializeField] private GameObject[] readyBadges;
    [SerializeField] private GameObject[] waitBadges;

    private void Awake() => OnClick(joinButton, () => Session?.StartExperience());

    // 지금 보여 줄 모습. 세션이 Idle 인데 이 패널이 보이면 합류 초대다.
    private Mode CurrentMode
    {
        get
        {
            var round = Session != null ? Session.Round : null;
            if (Session == null || Session.Current != WheelSession.State.Join) return Mode.Invite;
            return round != null && round.Current == GameRound.Phase.Ready ? Mode.Ready : Mode.Waiting;
        }
    }

    private void Update()
    {
        if (Session == null) return;
        var round = Session.Round;
        Mode mode = CurrentMode;

        for (int i = 0; i < 3; i++)
        {
            SetActive(messages, i, i == (int)mode);
            // Ready 는 Waiting 과 같은 아래 문구("잠시 후 체험이 시작됩니다.")를 쓴다
            SetActive(subMessages, i, i == (mode == Mode.Ready ? (int)Mode.Waiting : (int)mode));
        }

        bool counting = mode != Mode.Invite;
        if (counterGroup != null && counterGroup.activeSelf != counting) counterGroup.SetActive(counting);
        if (joinButton != null && joinButton.gameObject.activeSelf == counting) joinButton.gameObject.SetActive(!counting);
        if (counting && round != null)
        {
            float remain = mode == Mode.Ready ? round.ReadyRemaining : round.JoinRemaining;
            if (digits != null) digits.Set(Mathf.CeilToInt(remain));
            if (ring != null) ring.localEulerAngles = new Vector3(0f, 0f, -Time.unscaledTime * ringDegreesPerSecond);
        }

        int count = Mathf.Max(readyBadges != null ? readyBadges.Length : 0, waitBadges != null ? waitBadges.Length : 0);
        for (int i = 0; i < count; i++)
        {
            bool joined = round != null && IsParticipant(round, i + 1);
            SetActive(readyBadges, i, joined);
            SetActive(waitBadges, i, !joined);
        }
    }

    private static bool IsParticipant(GameRound round, int wheelId)
    {
        foreach (var p in round.Participants)
            if (p != null && p.WheelId == wheelId) return true;
        return false;
    }

    private static void SetActive(GameObject[] list, int index, bool on)
    {
        if (list == null || index >= list.Length || list[index] == null) return;
        if (list[index].activeSelf != on) list[index].SetActive(on);
    }
}
