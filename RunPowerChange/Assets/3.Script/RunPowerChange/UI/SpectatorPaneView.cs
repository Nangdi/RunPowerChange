using TMPro;
using UnityEngine;

// 85형 관전 화면의 Player 칸 1개. 3D 장면은 RaceCamera 가 이 칸(viewport)에 그리고,
// 이 뷰는 그 위의 상태 배지·기록 줄·대기 안내만 맡는다.
public class SpectatorPaneView : MonoBehaviour
{
    [SerializeField] private WheelSession session;
    [Tooltip("체험 중 / 준비 중 / 대기 중")]
    [SerializeField] private TMP_Text badgeText;
    [SerializeField] private TMP_Text maxSpeedText;
    [SerializeField] private TMP_Text wattText;
    [SerializeField] private TMP_Text energyText;
    [Tooltip("1위 기록과의 거리 차. 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text compareText;
    [Tooltip("비교 문구 배경 묶음 (문구가 없으면 숨김). 비워두면 표시하지 않음")]
    [SerializeField] private GameObject compareGroup;
    [Tooltip("체험자가 없을 때 칸 위에 보이는 안내 묶음")]
    [SerializeField] private GameObject idleGroup;

    private void Update()
    {
        if (session == null) return;
        var state = session.Current;
        bool idle = state == WheelSession.State.Idle || state == WheelSession.State.WaitNext;
        bool running = state == WheelSession.State.Run;

        if (idleGroup != null && idleGroup.activeSelf != idle) idleGroup.SetActive(idle);
        SetText(badgeText, idle ? "체험 대기 중" : running ? "체험 중..." : state < WheelSession.State.Run ? "준비 중..." : "결과 확인 중");

        SetText(maxSpeedText, $"{PowerFormat.Speed(session.MaxSpeedKmh)} <size=55%>km/h</size>");
        SetText(wattText, $"{PowerFormat.Watt(session.Watt)} <size=55%>W</size>");
        SetText(energyText, $"{PowerFormat.Kwh(session.ScaledEnergyWh)} <size=55%>kWh</size>");
        string compare = running ? Compare() : "";
        SetText(compareText, compare);
        bool showCompare = compare.Length > 0;
        if (compareGroup != null && compareGroup.activeSelf != showCompare) compareGroup.SetActive(showCompare);
    }

    // 1위 기록이 같은 시간에 있던 위치와 비교
    private string Compare()
    {
        var round = session.Round;
        if (round == null || round.Leaders.Count == 0) return "";
        float diff = session.DistanceM - round.Leaders[0].DistanceAt(round.RunElapsed);
        string who = round.LeadersFromPreviousDay ? "전날 1위" : "오늘 1위";
        return diff >= 0f
            ? $"{who}보다 <color=#2DB36F><b>{diff:0}m</b></color> 앞서는 중"
            : $"{who}보다 <color=#F26A3D><b>{-diff:0}m</b></color> 뒤지는 중";
    }

    private static void SetText(TMP_Text t, string value)
    {
        if (t != null && t.text != value) t.text = value;
    }
}
