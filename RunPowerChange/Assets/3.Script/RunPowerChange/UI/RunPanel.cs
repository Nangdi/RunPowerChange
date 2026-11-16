using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 05. 전기생산 체험 - 왼쪽 위 속도계(바늘 + km/h), 오른쪽 위 남은 시간(원형), 하단 누적 환산 발전량 바.
// 캐릭터 위 표시: ME 는 끝까지 내 캐릭터 머리 위를 따라다니고(어느 쪽이 나인지 알 수 있게),
// 상대의 PLAYER 표시는 시안대로 시작 후 tagSeconds 동안만 보인다.
// 위쪽 가운데에 상대방(혼자면 오늘 1위 기록)보다 몇 m 앞서는지/뒤처지는지 보여 준다.
// 그 아래에 "지금 기록이면 오늘 N명 중 M등"(발전왕 종합순위 기준, WheelSession.LiveEnergyRank)을 보여 준다.
// 순위는 오늘 저장된 랭킹 기록과 같이 달리는 상대만 센다. 1위 기록 선은 오늘 기록이 없으면 전날 1위라 "전날 최고 랭킹"으로 바꿔 보여 준다.
// 85형처럼 1위 기록 선(RaceStage, 32형 화면별 한 벌) 위에 "오늘 최고 랭킹" 표시를 붙인다. 선은 실제 거리 차 자리에 있다.
// 손잡이를 놓으면(FREE, 다음 HOLD 까지) 손잡이 경고(빨간 테두리 점멸)를 띄운다. 그동안 발전량은 늘지 않는다 (WheelSession.Watt).
public class RunPanel : WheelPanel
{
    [Header("속도계")]
    [SerializeField] private TMP_Text speedText;
    [Tooltip("바늘 그림 (가운데를 축으로 돈다)")]
    [SerializeField] private RectTransform needle;
    [Tooltip("속도 0 → 최고(speedometerMaxKmh) 사이에 바늘이 도는 각도 (시계 방향)")]
    [SerializeField] private float needleSweepDegrees = 270f;
    [SerializeField] private float needleSharpness = 8f;

    [Header("남은 시간")]
    [SerializeField] private TMP_Text remainText;
    [Tooltip("남은 시간 원형 (Image Filled, 반시계 방향으로 줄어듦)")]
    [SerializeField] private Image remainRing;

    [Header("누적 환산 발전량")]
    [SerializeField] private TMP_Text energyText;
    [Tooltip("최대 = 최고 출력으로 달렸을 때")]
    [SerializeField] private GaugeView energyGauge;

    [Header("캐릭터 위 표시 (인덱스 = 레인: 0 왼쪽 쳇바퀴 1, 1 오른쪽 쳇바퀴 2)")]
    [SerializeField] private GameObject[] meTags;
    [SerializeField] private GameObject[] otherTags;
    [SerializeField] private float tagSeconds = 2f;

    [Header("상대와 거리 차")]
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private GameObject compareGroup;
    [SerializeField] private TMP_Text compareText;
    [Tooltip("이 거리(m)보다 차이가 작으면 나란히 달리는 중")]
    [SerializeField] private float evenMeters = 1f;
    [Tooltip("시작 후 이 시간(초)이 지나야 표시 (처음엔 둘 다 0m)")]
    [SerializeField] private float compareDelaySeconds = 2f;
    [Tooltip("거리 숫자 색 (프리팹 생성 도구가 PowerUIKit 색으로 채운다)")]
    [SerializeField] private Color aheadColor = new Color32(87, 181, 136, 255);
    [SerializeField] private Color behindColor = new Color32(250, 114, 85, 255);
    [SerializeField] private Color evenColor = new Color32(251, 176, 59, 255);

    [Header("지금 순위")]
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private GameObject rankGroup;
    [SerializeField] private TMP_Text rankText;
    [Tooltip("순위 숫자 색 (프리팹 생성 도구가 PowerUIKit 색으로 채운다)")]
    [SerializeField] private Color rankColor = new Color32(251, 176, 59, 255);

    [Header("1위 기록 선 표시")]
    [Tooltip("\"오늘 최고 랭킹\" 표시 (아래 끝 가운데 피벗). 비워두면 표시하지 않음")]
    [SerializeField] private RectTransform recordTag;
    [Tooltip("오늘/전날 그림 바꾸기. 비워두면 바꾸지 않음")]
    [SerializeField] private RecordTagView recordTagView;

    [Header("손잡이 경고")]
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private HandWarningView handWarning;

    private float needleAngle;

    protected override void Refresh()
    {
        needleAngle = 0f;
        if (handWarning != null) handWarning.ResetEffect();
        if (recordTag != null) recordTag.gameObject.SetActive(false);
        Update();
    }

    private void Update()
    {
        if (Session == null) return;
        var cfg = JsonManager.instance.gameDynamicData;

        SetText(speedText, PowerFormat.TwoDigits(Session.SpeedKmh));
        if (needle != null)
        {
            float target = -needleSweepDegrees * Mathf.Clamp01(Session.SpeedKmh / Mathf.Max(1f, cfg.speedometerMaxKmh));
            needleAngle = Mathf.Lerp(needleAngle, target, 1f - Mathf.Exp(-needleSharpness * Time.unscaledDeltaTime));
            needle.localEulerAngles = new Vector3(0f, 0f, needleAngle);
        }

        SetText(remainText, PowerFormat.TwoDigits(Mathf.Ceil(Session.RunRemaining)));
        if (remainRing != null) remainRing.fillAmount = Session.RunRemaining / Session.RunDuration;

        SetText(energyText, PowerFormat.Wh(Session.ScaledEnergyWh));
        if (energyGauge != null)
            energyGauge.Set(Session.MaxScaledEnergyWh > 0f ? Session.ScaledEnergyWh / Session.MaxScaledEnergyWh : 0f);

        UpdateTags();
        UpdateCompare();
        UpdateRank();
        UpdateRecordTag();
        if (handWarning != null) handWarning.Set(Session.HandsReleased);
    }

    // 자기 레인에는 ME(계속), 상대 레인에는 PLAYER 0n(처음 tagSeconds 동안). 혼자 달리면 상대 레인(1위 고스트)에는 표시하지 않는다.
    private void UpdateTags()
    {
        var round = Session.Round;
        bool showOther = Session.StateElapsed < tagSeconds;
        int mine = Session.WheelId - 1;
        for (int lane = 0; lane < 2; lane++)
        {
            bool isMine = lane == mine;
            bool otherRunning = !isMine && round != null && !round.IsSolo;
            SetActive(meTags, lane, isMine);
            SetActive(otherTags, lane, showOther && otherRunning);
            Follow(Get(meTags, lane), lane + 1);
            Follow(Get(otherTags, lane), lane + 1);
        }
    }

    // 표시의 아래 끝(피벗)을 그 레인 캐릭터 머리 위 화면 위치에 둔다 (앞서 나가면 표시도 같이 안쪽으로 간다)
    private void Follow(GameObject tag, int wheelId)
    {
        if (tag == null || !tag.activeSelf || RaceCamera == null || RaceStage.Instance == null) return;
        RaceRunner runner = RaceStage.Instance.Runner(wheelId);
        if (runner == null) return;
        var rt = (RectTransform)tag.transform;
        var parent = (RectTransform)rt.parent;
        Vector3 screen = RaceCamera.WorldToScreenPoint(runner.HeadPoint(Session.WheelId));
        if (screen.z <= 0f || !RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out Vector2 local)) return;
        Vector2 pivotOffset = (Vector2.one * 0.5f - parent.pivot) * parent.rect.size;
        rt.anchoredPosition = local + pivotOffset;
    }

    private void UpdateCompare()
    {
        if (compareGroup == null) return;
        string text = CompareText();
        bool show = text != null;
        if (compareGroup.activeSelf != show) compareGroup.SetActive(show);
        if (show) SetText(compareText, text);
    }

    // 상대(2인) 또는 오늘(없으면 전날) 1위 기록(1인)과 지금 거리 차
    private string CompareText()
    {
        var round = Session.Round;
        if (round == null || Session.StateElapsed < compareDelaySeconds) return null;

        string who;
        string whoObject; // 목적어 조사(을/를)를 붙인 이름
        float other;
        if (!round.IsSolo)
        {
            WheelSession rival = null;
            foreach (var p in round.Participants)
                if (p != null && p != Session) rival = p;
            if (rival == null) return null;
            who = "상대방";
            whoObject = "상대방을";
            other = rival.DistanceM;
        }
        else if (round.Ghost != null)
        {
            who = round.LeaderFromPreviousDay ? "전날 1위" : "오늘 1위";
            whoObject = who + "를";
            other = round.Ghost.DistanceAt(round.RunElapsed);
        }
        else return null;

        float diff = Session.DistanceM - other;
        if (Mathf.Abs(diff) < evenMeters) return $"{who}와 {Paint("나란히", evenColor)} 달리는 중!";
        return diff > 0f
            ? $"{who}보다 {Paint($"{diff:0}m", aheadColor)} 앞서는 중!"
            : $"{whoObject} {Paint($"{-diff:0}m", behindColor)} 차이로 추격 중!";
    }

    private void UpdateRank()
    {
        if (rankGroup == null) return;
        bool show = Session.Round != null && Session.StateElapsed >= compareDelaySeconds;
        if (rankGroup.activeSelf != show) rankGroup.SetActive(show);
        if (!show) return;

        int total = Session.LiveTotal;
        SetText(rankText, total <= 1
            ? $"오늘의 {Paint("첫 기록", rankColor)}에 도전 중!"
            : $"지금 기록이면 오늘 {total}명 중 {Paint($"{Session.LiveEnergyRank}등", rankColor)}이에요!");
    }

    // 1위 기록 선 가운데 위에 표시의 아래 끝을 둔다. 선이 화면 밖(카메라 뒤 등)이면 숨긴다.
    private void UpdateRecordTag()
    {
        if (recordTag == null) return;
        var stage = RaceStage.Instance;
        bool show = stage != null && RaceCamera != null && stage.ScreenRecordVisible(Session.WheelId);
        Vector2 local = default;
        if (show)
        {
            var parent = (RectTransform)recordTag.parent;
            Vector3 screen = RaceCamera.WorldToScreenPoint(stage.ScreenRecordPoint(Session.WheelId));
            show = screen.z > 0f && RaceCamera.pixelRect.Contains(screen)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, null, out local);
            if (show) recordTag.anchoredPosition = local + (Vector2.one * 0.5f - parent.pivot) * parent.rect.size;
        }
        if (show && recordTagView != null) recordTagView.Set(Session.Round != null && Session.Round.LeaderFromPreviousDay);
        if (recordTag.gameObject.activeSelf != show) recordTag.gameObject.SetActive(show);
    }

    private static string Paint(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{text}</color>";

    private static GameObject Get(GameObject[] list, int index) => list != null && index < list.Length ? list[index] : null;

    private static void SetActive(GameObject[] list, int index, bool on)
    {
        if (list == null || index >= list.Length || list[index] == null) return;
        if (list[index].activeSelf != on) list[index].SetActive(on);
    }
}
