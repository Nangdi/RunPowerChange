using UnityEngine;

// 경주 장면 카메라. 위치와 각도는 디자이너 맵의 카메라를 그대로 쓰고, 출력 디스플레이와 보이는 레이어만 정한다.
//  - Wheel     : 쳇바퀴 32형 (레이스 카메라). 출력 디스플레이 = Setting.json wheels[].displayIndex. 이 화면 기준 캐릭터 벌(Screen{n})이 보인다.
//  - Spectator : 85형 (결과 카메라). 출력 디스플레이 = spectatorDisplayIndex. 바퀴 2개와 최고 랭킹 선이 보인다.
public class RaceCamera : MonoBehaviour
{
    public enum Target { Wheel, Spectator }

    [SerializeField] private Target screen = Target.Wheel;
    [Tooltip("Wheel: 이 쳇바퀴 번호의 디스플레이로 출력")]
    [SerializeField] private int wheelId = 1;
    [Tooltip("레이어 마스크를 코드로 정하지 않고 인스펙터 값을 그대로 쓴다")]
    [SerializeField] private bool keepCullingMask;

    public Target Screen => screen;
    public int WheelId => wheelId;

    private void Start()
    {
        var cam = GetComponent<Camera>();
        if (cam == null) return;

        cam.targetDisplay = DisplayIndex(cam);
        if (!keepCullingMask) cam.cullingMask = CullingMask();
    }

    private int DisplayIndex(Camera cam)
    {
        var cfg = JsonManager.instance.gameDynamicData;
        if (screen == Target.Spectator) return Mathf.Max(0, cfg.spectatorDisplayIndex);
        foreach (var w in cfg.wheels)
            if (w != null && w.wheelId == wheelId) return Mathf.Max(0, w.displayIndex);
        return cam.targetDisplay;
    }

    // 32형은 85형 전용 레이어와 다른 화면의 캐릭터 벌을, 85형은 캐릭터 레이어를 뺀다
    private int CullingMask() => screen == Target.Spectator ? RaceLayers.SpectatorCameraMask() : RaceLayers.WheelCameraMask(wheelId);
}
