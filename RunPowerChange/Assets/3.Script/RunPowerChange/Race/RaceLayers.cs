using UnityEngine;

// 경주 장면의 레이어 이름. 화면마다 보이는 것이 달라서 카메라 Culling Mask 로 나눈다.
//  - Runner{n}      : 쳇바퀴 n 캐릭터 (자기 32형과 85형에 보임)
//  - Runner{n}Ghost : 쳇바퀴 n 캐릭터의 반투명 복제 (상대 32형에 보임 - 2인 경주 때 상대를 반투명으로)
//  - RecordLine     : 오늘 1~3위 레코드 라인 (85형에만 보임)
// 레이어는 ProjectSettings/TagManager 에 같은 이름으로 만들어 둔다 (RaceSceneBuilder 가 만든다).
public static class RaceLayers
{
    public const string RecordLine = "RecordLine";

    public static string Runner(int wheelId) => $"Runner{wheelId}";
    public static string RunnerGhost(int wheelId) => $"Runner{wheelId}Ghost";

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

    public static void SetLayerRecursively(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
    }
}
