using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using static PowerUIKit;

// 임시 3D 경주 장면을 만든다. 디자이너 3D 맵·캐릭터를 받으면 Race/Track 아래를 맵으로, 캐릭터 프리팹을 교체한다.
// 메뉴: Tools > 달려라 전력 챌린지 > 5. 3D 경주 장면 만들기
//
//  Race (RaceCourse)            12시 방향(+Z) 직선 코스. 1 유닛 = 1 m
//   ├ Track                     임시 트랙·난간·옆 설비·바닥 (맵으로 교체할 부분)
//   ├ Runner_1, Runner_2        RaceRunner (쳇바퀴별 레인)
//   └ RecordLines               85형 레코드 라인 (RecordLine 레이어)
//  Camera_Wheel_1, _2           32형 (RaceCamera Wheel)
//  Camera_Spectator_Bg, _1, _2  85형 바탕 지우기 + Player 칸 2개 (RaceCamera Spectator, viewport = 관전 캔버스 Pane_n)
//
// 이미 있는 오브젝트·재질·캐릭터 프리팹은 그대로 두고 연결만 다시 한다. 트랙을 다시 만들려면 Build(rebuildTrack: true).
public static class RaceSceneBuilder
{
    private const string MaterialDir = "Assets/6.Materials/Race";
    private const string CharacterDir = PrefabRoot + "/Characters";
    private static readonly string[] CharacterNames = { "Character_Ssadori", "Character_Dino" };

    private const float TrackLength = 1040f;
    private const float TrackHalfWidth = 3.5f;

    [MenuItem("Tools/달려라 전력 챌린지/5. 3D 경주 장면 만들기", priority = 5)]
    public static void BuildMenu() => Build(false);

    public static void Build(bool rebuildTrack)
    {
        EnsureLayers();
        EnsureFolder(MaterialDir);
        EnsureFolder(CharacterDir);

        var characters = new List<GameObject>
        {
            GetOrCreateCharacter(CharacterNames[0], BuildSsadori),
            GetOrCreateCharacter(CharacterNames[1], BuildDino),
        };
        Material ghost = GhostMaterial();

        GameObject race = PowerPrefabBuilder.FindRoot("Race");
        if (race == null)
        {
            race = new GameObject("Race");
            race.AddComponent<RaceCourse>();
            Undo.RegisterCreatedObjectUndo(race, "Create Race");
        }

        Transform track = race.transform.Find("Track");
        if (track != null && rebuildTrack)
        {
            Object.DestroyImmediate(track.gameObject);
            track = null;
        }
        if (track == null) BuildTrack(race.transform);

        WheelSession[] sessions = PowerPrefabBuilder.SortedSessions();
        var runners = new List<RaceRunner>();
        foreach (var session in sessions)
        {
            RaceRunner runner = GetOrAdd<RaceRunner>(GetOrCreateChild(race.transform, $"Runner_{session.WheelId}"));
            var so = new SerializedObject(runner);
            so.FindProperty("wheelId").intValue = session.WheelId;
            so.ApplyModifiedPropertiesWithoutUndo();
            Wire(runner, ("session", session), ("ghostMaterial", ghost));
            WireList(runner, "characterPrefabs", characters.ConvertAll(c => (Object)c));
            runners.Add(runner);

            // 두 쳇바퀴의 기본 캐릭터를 다르게
            var sso = new SerializedObject(session);
            sso.FindProperty("defaultCharacter").intValue = (session.WheelId - 1) % characters.Count;
            sso.ApplyModifiedPropertiesWithoutUndo();
        }

        // 공용 게임 회차
        GameObject wheels = PowerPrefabBuilder.FindRoot("Wheels");
        if (wheels != null && wheels.GetComponent<GameRound>() == null) Undo.AddComponent<GameRound>(wheels);

        BuildCameras(runners);
        BuildRecordLines(race.transform);

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("3D 경주 장면 적용 완료. 씬을 저장하세요.");
    }

    #region 레이어

    // RaceLayers 의 레이어를 빈 칸(8번부터)에 만든다
    private static void EnsureLayers()
    {
        var names = new[] { RaceLayers.Runner(1), RaceLayers.Runner(2), RaceLayers.RunnerGhost(1), RaceLayers.RunnerGhost(2), RaceLayers.RecordLine };
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        foreach (string name in names)
        {
            if (LayerMask.NameToLayer(name) >= 0) continue;
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"레이어 추가: {i} {name}");
                break;
            }
        }
    }

    #endregion

    #region 재질

    private static Material Mat(string name, Color color, float smoothness = 0.3f, Color? emission = null)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetColor("_BaseColor", color);
        mat.SetFloat("_Smoothness", smoothness);
        if (emission.HasValue)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission.Value);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    private static Material UnlitMat(string name, Color color)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // 반투명 캐릭터용 (URP Lit, Transparent, Alpha 블렌드)
    private static Material GhostMaterial()
    {
        string path = $"{MaterialDir}/Ghost.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        mat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.35f));
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    #endregion

    #region 임시 캐릭터

    private static GameObject GetOrCreateCharacter(string name, System.Action<Transform> build)
    {
        string path = $"{CharacterDir}/{name}.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        var root = new GameObject(name);
        build(root.transform);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        Debug.Log($"임시 캐릭터 생성: {path}");
        return prefab;
    }

    private static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.On;
        return go;
    }

    // 싸돌이: 파란 후드, 빨간 머리 (임시)
    private static void BuildSsadori(Transform root)
    {
        Material skin = Mat("Char_Skin", new Color32(255, 214, 170, 255));
        Material hoodie = Mat("Char_Blue", new Color32(40, 80, 220, 255));
        Material pants = Mat("Char_Black", new Color32(35, 35, 40, 255));
        Material hair = Mat("Char_Red", new Color32(210, 40, 30, 255));
        Material shoe = Mat("Char_White", new Color32(240, 240, 245, 255));

        Part(root, PrimitiveType.Capsule, "LegL", new Vector3(-0.13f, 0.32f, 0f), new Vector3(0.2f, 0.3f, 0.2f), pants);
        Part(root, PrimitiveType.Capsule, "LegR", new Vector3(0.13f, 0.32f, 0f), new Vector3(0.2f, 0.3f, 0.2f), pants);
        Part(root, PrimitiveType.Cube, "ShoeL", new Vector3(-0.13f, 0.05f, 0.05f), new Vector3(0.18f, 0.1f, 0.3f), shoe);
        Part(root, PrimitiveType.Cube, "ShoeR", new Vector3(0.13f, 0.05f, 0.05f), new Vector3(0.18f, 0.1f, 0.3f), shoe);
        Part(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.55f, 0.4f, 0.42f), hoodie);
        Part(root, PrimitiveType.Capsule, "ArmL", new Vector3(-0.36f, 0.88f, 0.05f), new Vector3(0.16f, 0.25f, 0.16f), hoodie, new Vector3(-30, 0, 15));
        Part(root, PrimitiveType.Capsule, "ArmR", new Vector3(0.36f, 0.88f, -0.05f), new Vector3(0.16f, 0.25f, 0.16f), hoodie, new Vector3(30, 0, -15));
        Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.45f, 0f), new Vector3(0.6f, 0.6f, 0.6f), skin);
        Part(root, PrimitiveType.Sphere, "Hair", new Vector3(0f, 1.62f, -0.06f), new Vector3(0.62f, 0.42f, 0.6f), hair);
        for (int i = 0; i < 4; i++)
            Part(root, PrimitiveType.Cube, $"Spike_{i + 1}", new Vector3(-0.18f + i * 0.12f, 1.82f, -0.05f), new Vector3(0.1f, 0.22f, 0.1f), hair,
                new Vector3(-20, 0, -20 + i * 13));
    }

    // 로봇공룡 디노: 초록 공룡 옷, 노란 등 가시, 꼬리 (임시)
    private static void BuildDino(Transform root)
    {
        Material green = Mat("Char_Green", new Color32(60, 190, 70, 255));
        Material yellow = Mat("Char_Yellow", new Color32(250, 210, 40, 255));
        Material skin = Mat("Char_Skin", new Color32(255, 214, 170, 255));
        Material shoe = Mat("Char_White", new Color32(240, 240, 245, 255));

        Part(root, PrimitiveType.Capsule, "LegL", new Vector3(-0.15f, 0.3f, 0f), new Vector3(0.24f, 0.3f, 0.24f), green);
        Part(root, PrimitiveType.Capsule, "LegR", new Vector3(0.15f, 0.3f, 0f), new Vector3(0.24f, 0.3f, 0.24f), green);
        Part(root, PrimitiveType.Cube, "ShoeL", new Vector3(-0.15f, 0.05f, 0.05f), new Vector3(0.2f, 0.1f, 0.3f), shoe);
        Part(root, PrimitiveType.Cube, "ShoeR", new Vector3(0.15f, 0.05f, 0.05f), new Vector3(0.2f, 0.1f, 0.3f), shoe);
        Part(root, PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0f), new Vector3(0.62f, 0.45f, 0.5f), green);
        Part(root, PrimitiveType.Sphere, "Belly", new Vector3(0f, 0.8f, 0.17f), new Vector3(0.42f, 0.55f, 0.25f), yellow);
        Part(root, PrimitiveType.Capsule, "ArmL", new Vector3(-0.38f, 0.9f, 0.05f), new Vector3(0.18f, 0.24f, 0.18f), green, new Vector3(-30, 0, 15));
        Part(root, PrimitiveType.Capsule, "ArmR", new Vector3(0.38f, 0.9f, -0.05f), new Vector3(0.18f, 0.24f, 0.18f), green, new Vector3(30, 0, -15));
        Part(root, PrimitiveType.Sphere, "Head", new Vector3(0f, 1.5f, 0f), new Vector3(0.72f, 0.7f, 0.72f), green);
        Part(root, PrimitiveType.Sphere, "Face", new Vector3(0f, 1.45f, 0.2f), new Vector3(0.45f, 0.45f, 0.4f), skin);
        Part(root, PrimitiveType.Capsule, "Tail", new Vector3(0f, 0.55f, -0.5f), new Vector3(0.22f, 0.35f, 0.22f), green, new Vector3(-60, 0, 0));
        for (int i = 0; i < 5; i++)
            Part(root, PrimitiveType.Cube, $"Spike_{i + 1}", new Vector3(0f, 1.85f - i * 0.28f, -0.28f - i * 0.04f), new Vector3(0.06f, 0.16f, 0.16f),
                yellow, new Vector3(45, 0, 0));
    }

    #endregion

    #region 트랙

    private static void BuildTrack(Transform race)
    {
        Material road = Mat("Track_Road", new Color32(36, 38, 46, 255), 0.5f);
        Material line = Mat("Track_Line", new Color32(255, 200, 60, 255), 0.2f, new Color(1f, 0.75f, 0.2f) * 1.5f);
        Material marker = Mat("Track_Marker", new Color32(255, 250, 230, 255), 0.2f, new Color(1f, 0.95f, 0.8f) * 1.2f);
        Material rail = Mat("Track_Rail", new Color32(225, 228, 235, 255), 0.6f);
        Material machine = Mat("Track_Machine", new Color32(240, 242, 246, 255), 0.4f);
        Material machineDark = Mat("Track_MachineDark", new Color32(80, 86, 100, 255), 0.4f);
        Material ground = Mat("Track_Ground", new Color32(120, 176, 110, 255), 0.1f);
        Material tower = Mat("Track_Tower", new Color32(245, 247, 250, 255), 0.5f);
        Material glow = Mat("Track_TowerGlow", new Color32(140, 220, 255, 255), 0.5f, new Color(0.5f, 0.9f, 1f) * 2f);

        var track = new GameObject("Track").transform;
        track.SetParent(race, false);
        Undo.RegisterCreatedObjectUndo(track.gameObject, "Create Track");

        float mid = TrackLength * 0.5f - 20f;
        Part(track, PrimitiveType.Cube, "Road", new Vector3(0, -0.1f, mid), new Vector3(TrackHalfWidth * 2f, 0.2f, TrackLength), road);
        Part(track, PrimitiveType.Plane, "Ground", new Vector3(0, -0.6f, mid), new Vector3(40f, 1f, TrackLength / 10f), ground);
        for (int s = -1; s <= 1; s += 2)
        {
            Part(track, PrimitiveType.Cube, $"EdgeLine_{(s < 0 ? "L" : "R")}", new Vector3(s * (TrackHalfWidth - 0.15f), 0.01f, mid),
                new Vector3(0.08f, 0.02f, TrackLength), line);
            Part(track, PrimitiveType.Cube, $"Rail_{(s < 0 ? "L" : "R")}", new Vector3(s * (TrackHalfWidth + 0.15f), 0.35f, mid),
                new Vector3(0.12f, 0.7f, TrackLength), rail);
        }

        // 가운데 점선, 레인 위 진행 표시(움직임이 잘 보이도록 일정 간격)
        var markers = new GameObject("Markers").transform;
        markers.SetParent(track, false);
        for (float z = 0f; z < TrackLength - 20f; z += 6f)
            Part(markers, PrimitiveType.Cube, "Dash", new Vector3(0, 0.01f, z), new Vector3(0.08f, 0.02f, 3f), line);
        for (float z = 4f; z < TrackLength - 20f; z += 12f)
            for (int s = -1; s <= 1; s += 2)
                Part(markers, PrimitiveType.Cube, "Marker", new Vector3(s * 1.6f, 0.01f, z), new Vector3(1.2f, 0.02f, 0.35f), marker);

        // 옆 설비 (시안의 트랙 양옆 기계 줄)
        var side = new GameObject("SideMachines").transform;
        side.SetParent(track, false);
        for (float z = -10f; z < TrackLength - 20f; z += 8f)
            for (int s = -1; s <= 1; s += 2)
            {
                Part(side, PrimitiveType.Cube, "Machine", new Vector3(s * 5.2f, 0.7f, z), new Vector3(2.2f, 1.6f, 6.6f), machine);
                Part(side, PrimitiveType.Cube, "Vent", new Vector3(s * 4.08f, 0.8f, z), new Vector3(0.05f, 0.5f, 4.5f), machineDark);
            }

        // 멀리 보이는 탑 (발전 설비 느낌)
        var towers = new GameObject("Towers").transform;
        towers.SetParent(track, false);
        for (float z = 40f; z < TrackLength; z += 70f)
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * (22f + (z % 3f) * 4f);
                Part(towers, PrimitiveType.Cylinder, "Tower", new Vector3(x, 8f, z), new Vector3(5f, 8f, 5f), tower);
                Part(towers, PrimitiveType.Cylinder, "TowerGlow", new Vector3(x, 12f, z), new Vector3(5.2f, 0.8f, 5.2f), glow);
            }

        foreach (var r in track.GetComponentsInChildren<Renderer>())
            GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
    }

    #endregion

    #region 카메라·레코드 라인

    private static void BuildCameras(List<RaceRunner> runners)
    {
        // 예전 카메라 정리 (경주 카메라로 대체)
        foreach (string old in new[] { "Main Camera", "Camera_Display2" })
        {
            GameObject go = PowerPrefabBuilder.FindRoot(old);
            if (go != null) Undo.DestroyObjectImmediate(go);
        }

        foreach (var runner in runners)
        {
            Camera cam = GetOrCreateCamera($"Camera_Wheel_{runner.WheelId}");
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 55f;
            cam.depth = 0;
            cam.targetDisplay = runner.WheelId - 1;
            if (runner.WheelId == 1 && cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            SetupRaceCamera(cam, RaceCamera.Target.Wheel, runner.WheelId, runner, new Vector3(0f, 2.6f, -5.5f), new Vector3(0f, 1.2f, 6f), null);
        }

        GameObject spectator = PowerPrefabBuilder.FindRoot("Canvas_Spectator");
        Camera bg = GetOrCreateCamera("Camera_Spectator_Bg");
        bg.clearFlags = CameraClearFlags.SolidColor;
        bg.backgroundColor = BgColor;
        bg.cullingMask = 0;
        bg.depth = -10;
        bg.targetDisplay = 2;
        SetupRaceCamera(bg, RaceCamera.Target.Spectator, 0, null, Vector3.zero, Vector3.zero, null, keepMask: true);

        foreach (var runner in runners)
        {
            Camera cam = GetOrCreateCamera($"Camera_Spectator_{runner.WheelId}");
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 40f;
            cam.depth = 0;
            cam.targetDisplay = 2;
            RectTransform pane = spectator != null
                ? spectator.transform.Find($"Frame/Panel_{SpectatorName}/Pane_{runner.WheelId}") as RectTransform
                : null;
            if (pane == null) Debug.LogWarning("Canvas_Spectator 의 Player 칸을 찾지 못했습니다. 먼저 '1. 프리팹 생성 및 씬 적용'을 실행하세요.");
            SetupRaceCamera(cam, RaceCamera.Target.Spectator, runner.WheelId, runner, new Vector3(6.5f, 6f, -4f), new Vector3(-0.5f, 0.6f, 4f), pane);
        }
    }

    private static Camera GetOrCreateCamera(string name)
    {
        GameObject go = PowerPrefabBuilder.FindRoot(name);
        if (go == null)
        {
            go = new GameObject(name, typeof(Camera));
            Undo.RegisterCreatedObjectUndo(go, "Create Camera");
        }
        var cam = go.GetComponent<Camera>();
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 600f;
        return cam;
    }

    private static void SetupRaceCamera(Camera cam, RaceCamera.Target target, int wheelId, RaceRunner follow, Vector3 offset, Vector3 look,
        RectTransform viewport, bool keepMask = false)
    {
        var rc = GetOrAdd<RaceCamera>(cam.gameObject);
        var so = new SerializedObject(rc);
        so.FindProperty("screen").enumValueIndex = (int)target;
        so.FindProperty("wheelId").intValue = wheelId;
        so.FindProperty("offset").vector3Value = offset;
        so.FindProperty("lookOffset").vector3Value = look;
        so.FindProperty("keepCullingMask").boolValue = keepMask;
        so.ApplyModifiedPropertiesWithoutUndo();
        Wire(rc, ("follow", follow), ("viewport", viewport));

        if (follow != null)
        {
            Quaternion axis = Quaternion.identity;
            cam.transform.position = follow.transform.position + axis * offset;
            cam.transform.rotation = Quaternion.LookRotation(follow.transform.position + look - cam.transform.position);
        }
    }

    private static void BuildRecordLines(Transform race)
    {
        GameObject root = GetOrCreateChild(race, "RecordLines");
        int layer = LayerMask.NameToLayer(RaceLayers.RecordLine);
        Color[] colors = { new Color32(230, 50, 50, 255), new Color32(245, 160, 40, 255), new Color32(50, 120, 220, 255) };
        TMP_FontAsset font = BoldFont;

        var lines = new List<Object>();
        var labels = new List<Object>();
        for (int i = 0; i < colors.Length; i++)
        {
            GameObject lineGo = GetOrCreateChild(root.transform, $"Line_{i + 1}");
            lineGo.layer = layer;
            var lr = GetOrAdd<LineRenderer>(lineGo);
            lr.sharedMaterial = UnlitMat($"RecordLine_{i + 1}", colors[i]);
            lr.widthMultiplier = 0.14f;
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lines.Add(lr);

            GameObject labelGo = GetOrCreateChild(root.transform, $"Label_{i + 1}");
            labelGo.layer = layer;
            var tmp = GetOrAdd<TextMeshPro>(labelGo);
            tmp.font = font;
            tmp.text = $"랭킹 {i + 1}위";
            tmp.fontSize = 6f;
            tmp.color = colors[i];
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
            labels.Add(tmp);
        }

        var view = GetOrAdd<RecordLines>(root);
        WireList(view, "lines", lines);
        WireList(view, "labels", labels);
        GameObject faceCam = PowerPrefabBuilder.FindRoot("Camera_Spectator_1");
        Wire(view, ("faceCamera", faceCam != null ? faceCam.transform : null));
    }

    // 에디터의 GetComponent 는 없을 때 가짜 null 을 돌려줄 수 있어 ?? 대신 TryGetComponent 를 쓴다
    private static T GetOrAdd<T>(GameObject go) where T : Component => go.TryGetComponent(out T c) ? c : go.AddComponent<T>();

    private static GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t != null) return t.gameObject;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        return go;
    }

    #endregion
}
