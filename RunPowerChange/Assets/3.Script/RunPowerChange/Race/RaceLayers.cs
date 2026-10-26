using UnityEngine;

// 경주 장면의 레이어 이름. 화면마다 보이는 것이 달라서 카메라 Culling Mask 로 나눈다.
//  - Screen{n}      : 32형 쳇바퀴 n 화면에만 보이는 캐릭터 한 벌. 화면마다 자기 캐릭터를 기준으로 상대 위치가 달라서
//                     캐릭터를 화면 수만큼 두고 각 카메라가 자기 벌만 본다.
//  - SpectatorOnly  : 85형 바퀴 2개와 "오늘 최고 랭킹" 선 (85형에만 보임. 32형 카메라 앞을 가리지 않게)
// 레이어는 ProjectSettings/TagManager 에 같은 이름으로 만들어 둔다 (RaceSceneBuilder 가 만든다).
public static class RaceLayers
{
    public const string SpectatorOnly = "SpectatorOnly";

    public static string Screen(int wheelId) => $"Screen{wheelId}";

    public static int Layer(string name)
    {
        int layer = LayerMask.NameToLayer(name);
        if (layer < 0) Debug.LogError($"레이어 '{name}' 가 없습니다. Tools > 달려라 전력 챌린지 > 3D 경주 장면 만들기를 실행하세요.");
        return Mathf.Max(0, layer);
    }

    public static int Mask(params string[] names)
    {
        int mask = 0;
        foreach (var n in names)
        {
            int layer = LayerMask.NameToLayer(n);
            if (layer >= 0) mask |= 1 << layer;
        }
        return mask;
    }

    // 32형 쳇바퀴 wheelId 화면 카메라: 85형 전용과 다른 화면의 캐릭터 벌을 뺀다
    public static int WheelCameraMask(int wheelId) =>
        ~Mask("UI", SpectatorOnly, Screen(wheelId == 1 ? 2 : 1));

    // 85형 카메라: 캐릭터는 보지 않는다 (바퀴로 보여 줌)
    public static int SpectatorCameraMask() => ~Mask("UI", Screen(1), Screen(2));

    public static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }
}
