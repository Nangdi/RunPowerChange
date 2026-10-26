using TMPro;
using UnityEngine;

// 85형 버추얼 레코드 라인 (올림픽 중계 방식). 오늘(없으면 전날) 1~3위 기록이 지금 이 시간에 있던 위치에
// 코스를 가로지르는 선과 "랭킹 n위" 표시를 둔다. 체험자가 선보다 앞서는지 뒤지는지 바로 보인다.
// RecordLine 레이어라 85형 카메라에만 보인다.
public class RecordLines : MonoBehaviour
{
    [Tooltip("순위 순서 (1위, 2위, 3위 …)")]
    [SerializeField] private LineRenderer[] lines;
    [Tooltip("lines 와 같은 순서의 순위 표시")]
    [SerializeField] private TMP_Text[] labels;
    [Tooltip("표시가 바라볼 카메라. 비워두면 기본 방향")]
    [SerializeField] private Transform faceCamera;
    [SerializeField] private float labelHeight = 1.6f;

    private void LateUpdate()
    {
        var round = GameRound.Instance;
        var course = RaceCourse.Instance;
        bool active = round != null && course != null && round.Current >= GameRound.Phase.Countdown;
        int count = Mathf.Min(lines != null ? lines.Length : 0, Mathf.Max(0, JsonManager.instance.gameDynamicData.recordLineCount));

        for (int i = 0; lines != null && i < lines.Length; i++)
        {
            bool show = active && i < count && i < round.Leaders.Count;
            if (lines[i] != null) lines[i].gameObject.SetActive(show);
            TMP_Text label = labels != null && i < labels.Length ? labels[i] : null;
            if (label != null) label.gameObject.SetActive(show);
            if (!show) continue;

            float d = round.Leaders[i].DistanceAt(round.RunElapsed);
            float half = course.Width * 0.5f;
            Vector3 left = course.PointAt(-half, d) + Vector3.up * 0.03f;
            Vector3 right = course.PointAt(half, d) + Vector3.up * 0.03f;
            lines[i].positionCount = 2;
            lines[i].SetPosition(0, left);
            lines[i].SetPosition(1, right);

            if (label != null)
            {
                label.text = $"{(round.LeadersFromPreviousDay ? "전날" : "랭킹")} {i + 1}위";
                label.transform.position = right + Vector3.up * (labelHeight + i * 0.45f);
                if (faceCamera != null)
                    label.transform.rotation = Quaternion.LookRotation(faceCamera.forward, Vector3.up); // 두 Player 칸 카메라는 같은 각도라 방향만 맞춘다
            }
        }
    }
}
