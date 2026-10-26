using UnityEngine;

// 경주 코스: 거리(m) → 3D 위치 변환만 맡는다. 코스는 이 오브젝트의 앞(+Z, 12시 방향)으로 뻗은 직선이다.
// 디자이너 3D 맵을 받으면 맵을 이 오브젝트 아래에 두고 레인 위치와 배율만 맞추면 된다 (러너·카메라 코드는 그대로).
public class RaceCourse : MonoBehaviour
{
    public static RaceCourse Instance { get; private set; }

    [Tooltip("쳇바퀴별 레인의 좌우 위치 (인덱스 = wheelId - 1)")]
    [SerializeField] private float[] laneX = { -1.6f, 1.6f };
    [Tooltip("실제 1m 를 코스에서 몇 유닛으로 그릴지")]
    [SerializeField] private float unitsPerMeter = 1f;
    [Tooltip("코스 길이(유닛). 이보다 멀리 달리면 끝에 멈춘다.")]
    [SerializeField] private float length = 1000f;
    [Tooltip("레코드 라인 등 코스를 가로지르는 표시의 폭(유닛)")]
    [SerializeField] private float width = 7f;

    public float Width => width;
    public Vector3 Forward => transform.forward;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public float LaneX(int wheelId)
    {
        int i = Mathf.Clamp(wheelId - 1, 0, laneX.Length - 1);
        return laneX.Length > 0 ? laneX[i] : 0f;
    }

    // wheelId 레인의 distanceM 지점 (월드 좌표)
    public Vector3 PositionAt(int wheelId, float distanceM) => PointAt(LaneX(wheelId), distanceM);

    // 코스 가운데에서 좌우 x 만큼 떨어진 distanceM 지점
    public Vector3 PointAt(float x, float distanceM)
    {
        float z = Mathf.Clamp(distanceM * unitsPerMeter, 0f, length);
        return transform.TransformPoint(new Vector3(x, 0f, z));
    }
}
