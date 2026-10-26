using UnityEngine;

// 경주 장면 카메라. 따라갈 러너가 있으면 코스 방향 기준 뒤쪽 위에서 따라간다.
//  - Wheel     : 쳇바퀴 32형. 출력 디스플레이 = Setting.json wheels[].displayIndex.
//                자기 캐릭터는 본체, 상대 캐릭터는 반투명 복제 레이어를 본다.
//  - Spectator : 85형. 출력 디스플레이 = spectatorDisplayIndex. 두 캐릭터 본체와 레코드 라인을 본다.
// viewport 를 연결하면 그 UI 영역(Overlay 캔버스의 RectTransform)에만 그린다 (85형 Player 칸).
public class RaceCamera : MonoBehaviour
{
    public enum Target { Wheel, Spectator }

    [SerializeField] private Target screen = Target.Wheel;
    [Tooltip("Wheel: 이 쳇바퀴 번호의 디스플레이로 출력")]
    [SerializeField] private int wheelId = 1;
    [Tooltip("따라갈 러너. 비워두면 움직이지 않는다 (배경 지우기용 카메라 등).")]
    [SerializeField] private RaceRunner follow;
    [Tooltip("러너 기준 카메라 위치 (코스 방향 기준: x 오른쪽, y 위, z 앞)")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 2.6f, -5.5f);
    [Tooltip("러너 기준 바라볼 지점")]
    [SerializeField] private Vector3 lookOffset = new Vector3(0f, 1.2f, 6f);
    [SerializeField] private float followSharpness = 5f;
    [Tooltip("이 UI 영역에만 그린다. 비워두면 화면 전체.")]
    [SerializeField] private RectTransform viewport;
    [Tooltip("레이어 마스크를 코드로 정하지 않고 인스펙터 값을 그대로 쓴다")]
    [SerializeField] private bool keepCullingMask;

    private Camera cam;

    private void Start()
    {
        cam = GetComponent<Camera>();
        if (cam == null) return;

        cam.targetDisplay = DisplayIndex();
        if (!keepCullingMask) cam.cullingMask = CullingMask();
        Snap();
    }

    private int DisplayIndex()
    {
        var cfg = JsonManager.instance.gameDynamicData;
        if (screen == Target.Spectator) return Mathf.Max(0, cfg.spectatorDisplayIndex);
        foreach (var w in cfg.wheels)
            if (w != null && w.wheelId == wheelId) return Mathf.Max(0, w.displayIndex);
        return cam.targetDisplay;
    }

    // 경주 레이어를 모두 뺀 기본 장면 + 이 화면에 보여 줄 경주 레이어
    private int CullingMask()
    {
        int all = ~RaceLayers.Mask(RaceLayers.Runner(1), RaceLayers.Runner(2), RaceLayers.RunnerGhost(1), RaceLayers.RunnerGhost(2),
            RaceLayers.RecordLine, "UI");
        if (screen == Target.Spectator)
            return all | RaceLayers.Mask(RaceLayers.Runner(1), RaceLayers.Runner(2), RaceLayers.RecordLine);

        int other = wheelId == 1 ? 2 : 1;
        return all | RaceLayers.Mask(RaceLayers.Runner(wheelId), RaceLayers.RunnerGhost(other));
    }

    private void LateUpdate()
    {
        if (cam == null) return;
        if (viewport != null) cam.rect = ViewportRect();
        if (follow == null) return;

        GetPose(out Vector3 pos, out Vector3 look);
        float t = 1f - Mathf.Exp(-followSharpness * Time.unscaledDeltaTime);
        transform.position = Vector3.Lerp(transform.position, pos, t);
        transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
    }

    private void Snap()
    {
        if (follow == null) return;
        GetPose(out Vector3 pos, out Vector3 look);
        transform.position = pos;
        transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
    }

    private void GetPose(out Vector3 pos, out Vector3 look)
    {
        Quaternion axis = RaceCourse.Instance != null ? Quaternion.LookRotation(RaceCourse.Instance.Forward, Vector3.up) : Quaternion.identity;
        Vector3 basePos = follow.transform.position;
        pos = basePos + axis * offset;
        look = basePos + axis * lookOffset;
    }

    // Overlay 캔버스의 RectTransform 은 월드 좌표가 곧 화면 픽셀 좌표다
    private Rect ViewportRect()
    {
        var corners = new Vector3[4];
        viewport.GetWorldCorners(corners);
        float w = ScreenWidth(), h = ScreenHeight();
        if (w <= 0f || h <= 0f) return new Rect(0, 0, 1, 1);
        return Rect.MinMaxRect(corners[0].x / w, corners[0].y / h, corners[2].x / w, corners[2].y / h);
    }

    private float ScreenWidth()
    {
        int d = cam.targetDisplay;
        return d > 0 && d < Display.displays.Length ? Display.displays[d].renderingWidth : Screen.width;
    }

    private float ScreenHeight()
    {
        int d = cam.targetDisplay;
        return d > 0 && d < Display.displays.Length ? Display.displays[d].renderingHeight : Screen.height;
    }
}
