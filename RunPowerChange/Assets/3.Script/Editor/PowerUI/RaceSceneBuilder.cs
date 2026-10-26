using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;
using static PowerUIKit;

// 디자이너 3D 맵(Assets/runing/save/running_R_007.unity, HDRP)으로 경주 장면을 만든다.
// 메뉴: Tools > 달려라 전력 챌린지 > 5. 3D 경주 장면 만들기
//
//  1) 디자이너 씬에서 맵·조명·반사·후처리 볼륨·캐릭터·바퀴를 골라 RaceMap.prefab 으로 저장한다 (디자이너 씬은 저장하지 않는다).
//  2) 현재 씬에 RaceMap 을 두고 RaceStage(흐르는 배경·결승선·최고 랭킹 선)와 RaceRunner(레인 2개)를 연결한다.
//  3) 카메라: 디자이너 카메라를 그대로 복사한다. 32형 2대 = "레이스카메라", 85형 1대 = "결과 카메라".
//
//  RaceMap (RaceStage)
//   ├ 트랙 (레일 + 흐름 애니메이션, 평면 반사), 가로등SET (n) (흐르는 기둥·유리벽), 결승선, 결과 결승라인 (85형 최고 랭킹 선)
//   ├ 조명, PP Volume (디자이너 후처리: 블룸·노출·하늘), Environment VFX (반딧불)
//   ├ 사람 (쳇바퀴 1, 왼쪽 파랑 레인), 공룡 (쳇바퀴 2, 오른쪽 분홍 레인)   ← Screen1/2 레이어 (32형 화면별 한 벌)
//   ├ 바퀴_블루, 바퀴_핑크 (85형 바퀴)                                    ← SpectatorOnly 레이어 (85형에만 보임)
//   └ Runner_1, Runner_2 (RaceRunner)
// 디자이너가 맵을 새로 주면: 패키지를 같은 경로에 덮어쓰고 이 메뉴를 다시 실행한다 (RaceMap.prefab 을 새로 만든다).
public static class RaceSceneBuilder
{
    private const string DesignerScene = "Assets/runing/save/running_R_007.unity";
    private const string MapPrefabPath = PrefabRoot + "/RaceMap.prefab";
    private const string GhostMaterialPath = "Assets/6.Materials/Race/Ghost.mat";

    // 디자이너 씬 루트 이름
    private const string RaceCameraName = "레이스카메라";
    private const string SpectatorCameraName = "결과 카메라";
    // 맵에 넣지 않는 루트 (카메라는 따로 복사, 나머지는 구분용 빈 오브젝트·꺼 둔 캐릭터 선택 모델·베이크 데이터)
    private static readonly string[] SkipRoots = { RaceCameraName, SpectatorCameraName, "사람 초이스", "공룡 초이스", "StaticLightingSky", "SceneIDMap" };
    private const string LampPrefix = "가로등SET";
    // 레인 (인덱스 = wheelId - 1)
    private static readonly string[] Characters = { "사람", "공룡" };
    private static readonly string[] CharacterRunStates = { "달리기", "공룡달리기" };
    private static readonly string[] CharacterIdleStates = { "아이들", "공룡아이들" };
    private static readonly string[] Wheels = { "바퀴_블루", "바퀴_핑크" };

    [MenuItem("Tools/달려라 전력 챌린지/5. 3D 경주 장면 만들기", priority = 5)]
    public static void BuildMenu() => Build();

    public static void Build()
    {
        Scene target = SceneManager.GetActiveScene();
        if (target.path == DesignerScene)
        {
            Debug.LogError("StartScene 을 연 상태에서 실행하세요.");
            return;
        }

        EnsureLayers();
        FixDesignerAssets();
        RemoveOldRace();

        Scene designer = EditorSceneManager.OpenScene(DesignerScene, OpenSceneMode.Additive);
        GameObject mapPrefab;
        try
        {
            // 카메라는 디자이너 설정(HDRP 카메라 데이터)을 그대로 복사한다
            SceneManager.SetActiveScene(target);
            BuildCameras(designer);
            mapPrefab = BuildMapPrefab(designer);
        }
        finally
        {
            EditorSceneManager.CloseScene(designer, true);
            SceneManager.SetActiveScene(target);
        }

        var map = (GameObject)PrefabUtility.InstantiatePrefab(mapPrefab, target);
        map.name = "RaceMap";
        Undo.RegisterCreatedObjectUndo(map, "Create RaceMap");
        SetupStage(map);

        // 공용 게임 회차
        GameObject wheels = PowerPrefabBuilder.FindRoot("Wheels");
        if (wheels != null && wheels.GetComponent<GameRound>() == null) Undo.AddComponent<GameRound>(wheels);

        // 씬 기본 조명은 맵의 조명으로 대신한다
        GameObject oldLight = PowerPrefabBuilder.FindRoot("Directional Light");
        if (oldLight != null && oldLight.activeSelf)
        {
            Undo.RecordObject(oldLight, "Disable Light");
            oldLight.SetActive(false);
        }

        // 85형 최고 랭킹 표시가 따라갈 카메라
        GameObject spectator = PowerPrefabBuilder.FindRoot("Canvas_Spectator");
        Camera spectatorCam = PowerPrefabBuilder.FindRoot("Camera_Spectator")?.GetComponent<Camera>();
        if (spectator != null && spectatorCam != null) Wire(spectator.GetComponent<SpectatorScreen>(), ("raceCamera", spectatorCam));

        EditorSceneManager.MarkSceneDirty(target);
        Debug.Log("3D 경주 장면 적용 완료. 씬을 저장하세요.");
    }

    #region 레이어

    // RaceLayers 의 레이어를 빈 칸(8번부터)에 만든다. 예전 임시 장면의 레이어는 지운다.
    private static void EnsureLayers()
    {
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        var obsolete = new[] { "Runner1Ghost", "Runner2Ghost", "RecordLine", "Runner1", "Runner2" };
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (obsolete.Contains(slot.stringValue)) slot.stringValue = "";
        }
        foreach (string name in new[] { RaceLayers.Screen(1), RaceLayers.Screen(2), RaceLayers.SpectatorOnly })
        {
            bool exists = false;
            for (int i = 8; i < layers.arraySize; i++) exists |= layers.GetArrayElementAtIndex(i).stringValue == name;
            if (exists) continue;
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(slot.stringValue)) continue;
                slot.stringValue = name;
                Debug.Log($"레이어 추가: {i} {name}");
                break;
            }
        }
        tagManager.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion

    #region 디자이너 에셋 보정

    // 디자이너 패키지에서 확인된 문제를 고친다 (패키지를 새로 덮어써도 이 메뉴가 다시 고친다)
    //  - 공룡 달리기, 두 캐릭터의 아이들 클립이 반복 재생(loop)이 꺼져 있어 한 번 재생 후 멈춘다
    private static readonly string[] CharacterModels = { "Assets/runing/model/사람.fbx", "Assets/runing/model/공룡.fbx" };

    private static void FixDesignerAssets()
    {
        foreach (string path in CharacterModels)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) continue;
            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0 ? importer.clipAnimations : importer.defaultClipAnimations;
            bool changed = false;
            foreach (var clip in clips)
            {
                if (clip.loopTime) continue;
                clip.loopTime = true;
                changed = true;
                Debug.Log($"애니메이션 반복 켬: {path} {clip.name}");
            }
            if (!changed) continue;
            importer.clipAnimations = clips;
            importer.SaveAndReimport();
        }
    }

    #endregion

    #region 맵 프리팹

    // 디자이너 씬의 오브젝트를 RaceMap 아래로 모아 프리팹으로 저장한다
    private static GameObject BuildMapPrefab(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        var map = new GameObject("RaceMap");
        SceneManager.MoveGameObjectToScene(map, scene);
        foreach (var r in roots)
        {
            if (SkipRoots.Contains(r.name) || r.name.StartsWith("---")) continue;
            r.transform.SetParent(map.transform, true);
        }

        // 디자이너 씬에서 빠진 파티클 프리팹 등 빈 오브젝트 정리
        foreach (var t in map.GetComponentsInChildren<Transform>(true).ToArray())
            if (t != null && !PrefabUtility.IsPartOfPrefabInstance(t.gameObject))
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);

        // 레이어: 캐릭터는 32형(쳇바퀴 1 화면용. 쳇바퀴 2 화면용은 RaceRunner 가 실행할 때 복제한다), 바퀴와 최고 랭킹 선은 85형에만
        for (int i = 0; i < 2; i++)
        {
            SetLayer(map.transform.Find(Characters[i]), RaceLayers.Screen(1));
            SetLayer(map.transform.Find(Wheels[i]), RaceLayers.SpectatorOnly);
        }
        Transform line = map.transform.Find("결과 결승라인");
        SetLayer(line, RaceLayers.SpectatorOnly);

        // 분홍 바퀴의 바닥 띠에 어두운 기본 재질(0000)이 들어가 있어 파랑 바퀴(휠블루001)와 같은 짝 재질(휠핑크001)로 바꾼다
        Transform pinkStrip = map.transform.Find($"{Wheels[1]}/Rectangle002");
        var pinkMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/runing/model/Materials/휠핑크001.mat");
        if (pinkStrip != null && pinkMat != null && pinkStrip.TryGetComponent(out MeshRenderer stripRenderer)
            && stripRenderer.sharedMaterial != null && stripRenderer.sharedMaterial.name == "0000")
            stripRenderer.sharedMaterial = pinkMat;

        // 두 바퀴를 같은 출발선에 둔다 (디자이너 시안은 앞뒤로 엇갈려 있어 같은 거리여도 한쪽이 앞서 보인다)
        Transform blue = map.transform.Find(Wheels[0]), pink = map.transform.Find(Wheels[1]);
        if (blue != null && pink != null)
        {
            float z = (blue.localPosition.z + pink.localPosition.z) * 0.5f;
            blue.localPosition = new Vector3(blue.localPosition.x, blue.localPosition.y, z);
            pink.localPosition = new Vector3(pink.localPosition.x, pink.localPosition.y, z);
        }

        // 최고 랭킹 표시가 붙을 점: 선의 오른쪽 끝
        if (line != null)
        {
            var point = new GameObject("TagPoint").transform;
            var bounds = line.GetComponent<Renderer>().bounds;
            point.position = new Vector3(bounds.max.x, bounds.center.y, bounds.center.z);
            point.SetParent(line, true);
        }

        EnsureFolder(PrefabRoot);
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(map, MapPrefabPath);
        Debug.Log($"디자이너 맵 프리팹 저장: {MapPrefabPath}");
        return prefab;
    }

    private static void SetLayer(Transform t, string layer)
    {
        if (t != null) RaceLayers.SetLayerRecursively(t.gameObject, RaceLayers.Layer(layer));
    }

    #endregion

    #region 씬

    // 예전 경주 장면(Race, 카메라, URP 볼륨)과 이전 RaceMap 을 지운다
    private static void RemoveOldRace()
    {
        foreach (string name in new[] { "Race", "RaceMap", "Camera_Wheel_1", "Camera_Wheel_2", "Camera_Spectator", "Camera_Spectator_Bg",
                     "Camera_Spectator_1", "Camera_Spectator_2", "RaceVolume" })
        {
            GameObject old = PowerPrefabBuilder.FindRoot(name);
            if (old != null) Undo.DestroyObjectImmediate(old);
        }
    }

    private static void SetupStage(GameObject map)
    {
        // 에디터의 GetComponent 는 없을 때 가짜 null 을 돌려줄 수 있어 ?? 대신 TryGetComponent 를 쓴다
        RaceStage stage = map.TryGetComponent(out RaceStage existing) ? existing : map.AddComponent<RaceStage>();
        Transform track = map.transform.Find("트랙");

        WheelSession[] sessions = PowerPrefabBuilder.SortedSessions();
        var runners = new List<Object>();
        Material ghost = GhostMaterial();
        for (int i = 0; i < 2; i++)
        {
            var go = new GameObject($"Runner_{i + 1}");
            go.transform.SetParent(map.transform, false);
            var runner = go.AddComponent<RaceRunner>();
            var so = new SerializedObject(runner);
            so.FindProperty("wheelId").intValue = i + 1;
            so.FindProperty("runState").stringValue = CharacterRunStates[i];
            so.FindProperty("idleState").stringValue = CharacterIdleStates[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            Transform character = map.transform.Find(Characters[i]);
            Transform wheel = map.transform.Find(Wheels[i]);
            WheelSession session = sessions.FirstOrDefault(s => s.WheelId == i + 1);
            Wire(runner,
                ("session", session),
                ("character", character),
                ("characterAnimator", character != null ? character.GetComponentInChildren<Animator>(true) : null),
                ("ghostMaterial", ghost),
                ("wheel", wheel));
            WireList(runner, "wheelAnimators", wheel != null ? wheel.GetComponentsInChildren<Animator>(true).Cast<Object>().ToList() : new List<Object>());
            runners.Add(runner);
        }

        // 기둥·유리벽 세트: 같은 간격으로 늘어서 있으므로 그 간격 × 개수만큼 돌아가게 한다
        var lamps = map.transform.Cast<Transform>().Where(t => t.name.StartsWith(LampPrefix)).OrderBy(t => t.localPosition.z).ToList();
        float spacing = 6.75f;
        if (lamps.Count > 1)
        {
            var gaps = new List<float>();
            for (int i = 1; i < lamps.Count; i++) gaps.Add(lamps[i].localPosition.z - lamps[i - 1].localPosition.z);
            gaps.Sort();
            spacing = gaps[gaps.Count / 2];
        }

        Transform finish = map.transform.Find("결승선");
        Transform line = map.transform.Find("결과 결승라인");
        Transform character0 = map.transform.Find(Characters[0]);
        float runnerZ = character0 != null ? character0.localPosition.z : -24f;
        var stageSo = new SerializedObject(stage);
        stageSo.FindProperty("recycleBehindZ").floatValue = lamps.Count > 0 ? lamps[0].localPosition.z - spacing * 0.5f : -20f;
        stageSo.FindProperty("loopLength").floatValue = spacing * lamps.Count;
        stageSo.FindProperty("runnerZ").floatValue = runnerZ;
        if (finish != null) stageSo.FindProperty("finishStartDistance").floatValue = finish.localPosition.z - runnerZ;
        // 최고 랭킹 선의 기준(거리가 같을 때) = 85형 바퀴 출발선
        Transform wheel0 = map.transform.Find(Wheels[0]);
        if (wheel0 != null) stageSo.FindProperty("recordZeroZ").floatValue = wheel0.localPosition.z;
        stageSo.ApplyModifiedPropertiesWithoutUndo();

        WireList(stage, "runners", runners);
        WireList(stage, "scrollItems", lamps.Cast<Object>().ToList());
        WireList(stage, "scrollAnimators", track != null ? track.GetComponentsInChildren<Animator>(true).Cast<Object>().ToList() : new List<Object>());
        Wire(stage, ("finishLine", finish), ("recordLine", line), ("recordTagPoint", line != null ? line.Find("TagPoint") : null));
    }

    // 반투명 1위 고스트용 (HDRP Lit, Transparent)
    private static Material GhostMaterial()
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(GhostMaterialPath);
        if (m == null)
        {
            EnsureFolder("Assets/6.Materials/Race");
            m = new Material(Shader.Find("HDRP/Lit"));
            AssetDatabase.CreateAsset(m, GhostMaterialPath);
        }
        if (m.shader.name != "HDRP/Lit") m.shader = Shader.Find("HDRP/Lit");
        m.SetFloat("_SurfaceType", 1f);
        m.SetFloat("_BlendMode", 0f);
        m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.4f));
        HDShaderUtils.ResetMaterialKeywords(m);
        EditorUtility.SetDirty(m);
        return m;
    }

    private static void BuildCameras(Scene designer)
    {
        GameObject race = designer.GetRootGameObjects().FirstOrDefault(r => r.name == RaceCameraName);
        GameObject spec = designer.GetRootGameObjects().FirstOrDefault(r => r.name == SpectatorCameraName);
        for (int i = 1; i <= 2; i++) CopyCamera(race, $"Camera_Wheel_{i}", RaceCamera.Target.Wheel, i, i - 1, i == 1);
        CopyCamera(spec, "Camera_Spectator", RaceCamera.Target.Spectator, 1, 2, false);
    }

    private static void CopyCamera(GameObject source, string name, RaceCamera.Target target, int wheelId, int display, bool listener)
    {
        if (source == null)
        {
            Debug.LogError($"디자이너 씬에 카메라가 없습니다: {name}");
            return;
        }

        var go = Object.Instantiate(source);
        go.name = name;
        go.SetActive(true);
        Undo.RegisterCreatedObjectUndo(go, "Create Race Camera");
        // 디자이너가 시험용으로 붙인 자유 이동 카메라 스크립트와 빠진 스크립트는 뺀다
        foreach (var c in go.GetComponents<MonoBehaviour>())
            if (c != null && c.GetType().Name == "FreeCamera") Object.DestroyImmediate(c);
        GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
        var audio = go.GetComponent<AudioListener>();
        if (audio != null && !listener) Object.DestroyImmediate(audio);

        var cam = go.GetComponent<Camera>();
        cam.targetDisplay = display;
        cam.cullingMask = target == RaceCamera.Target.Spectator ? RaceLayers.SpectatorCameraMask() : RaceLayers.WheelCameraMask(wheelId);
        if (!go.TryGetComponent(out HDAdditionalCameraData _)) go.AddComponent<HDAdditionalCameraData>();

        var rc = go.AddComponent<RaceCamera>();
        var so = new SerializedObject(rc);
        so.FindProperty("screen").enumValueIndex = (int)target;
        so.FindProperty("wheelId").intValue = wheelId;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
