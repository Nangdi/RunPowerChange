using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PowerUIKit;

// 정적 PNG 누락, 비어 있는 인스펙터 연결을 찾아 콘솔에 알려준다.
// 메뉴: Tools > 달려라 전력 챌린지 > 4. 연결 검사
public static class PowerUIValidator
{
    [MenuItem("Tools/달려라 전력 챌린지/4. 연결 검사", priority = 4)]
    public static int Validate()
    {
        int problems = 0;

        var staticNames = new System.Collections.Generic.List<string>(PanelNames) { SpectatorName };
        foreach (string panel in staticNames)
        {
            if (AssetDatabase.LoadAssetAtPath<Sprite>(StaticSpritePath(panel)) == null)
                problems += Report($"정적 레이어 이미지가 없습니다: {StaticSpritePath(panel)}", null);
        }

        if (!System.IO.File.Exists(LayoutPath))
            problems += Report($"PSD 좌표 파일이 없습니다: {LayoutPath} (Tools/psd_export.py)", null);

        var sessions = Object.FindObjectsOfType<WheelSession>(true);
        var screens = Object.FindObjectsOfType<WheelScreen>(true);
        if (screens.Length == 0)
            problems += Report("씬에 Canvas_Wheel 이 없습니다. '1. 프리팹 생성 및 씬 적용'을 실행하세요.", null);
        else if (screens.Length != sessions.Length)
            problems += Report($"쳇바퀴 세션 {sessions.Length}개, 화면 {screens.Length}개로 수가 다릅니다.", null);

        var spectators = Object.FindObjectsOfType<SpectatorScreen>(true);
        if (spectators.Length != 1)
            problems += Report($"씬에 85형 관전 캔버스(Canvas_Spectator)가 {spectators.Length}개 있습니다. 1개여야 합니다.", null);
        foreach (var spectator in spectators)
            problems += CheckReferences(spectator.gameObject);
        if (Object.FindObjectsOfType<GameRound>(true).Length != 1)
            problems += Report("씬에 GameRound 가 1개 있어야 합니다. '5. 3D 경주 장면 만들기'를 실행하세요.", null);
        foreach (var runner in Object.FindObjectsOfType<RaceRunner>(true))
            problems += CheckReferences(runner.gameObject);
        foreach (var stage in Object.FindObjectsOfType<RaceStage>(true))
            problems += CheckReferences(stage.gameObject);
        if (Object.FindObjectsOfType<RaceCamera>(true).Length != 3)
            problems += Report("경주 카메라는 32형 2대 + 85형 1대, 3개여야 합니다. '5. 3D 경주 장면 만들기'를 실행하세요.", null);

        foreach (var screen in screens)
        {
            GameObject canvas = screen.gameObject;
            if (!PrefabUtility.IsPartOfPrefabInstance(canvas))
                problems += Report($"{canvas.name} 가 프리팹 인스턴스가 아닙니다.", canvas);

            foreach (var panel in canvas.GetComponentsInChildren<PanelBase>(true))
            {
                Transform layer = panel.transform.Find(StaticLayerName);
                Image image = layer != null ? layer.GetComponent<Image>() : null;
                if (image == null || image.sprite == null)
                    problems += Report($"{panel.name}: StaticLayer 이미지가 비어 있습니다.", panel);
            }

            problems += CheckReferences(canvas);
        }

        if (problems == 0)
            Debug.Log("달려라 전력 챌린지 UI 연결 검사: 문제 없음");
        else
            Debug.LogWarning($"달려라 전력 챌린지 UI 연결 검사: 문제 {problems}건 (위 로그 참고)");
        return problems;
    }

    // 하위 모든 컴포넌트에서 비어 있는 오브젝트 참조를 찾는다. (프로젝트 스크립트만)
    private static int CheckReferences(GameObject root)
    {
        int problems = 0;
        foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null)
            {
                problems += Report($"{root.name}: 스크립트가 없는(Missing) 컴포넌트가 있습니다.", root);
                continue;
            }

            // TMP, UGUI 등 패키지 컴포넌트는 검사하지 않는다
            if (behaviour.GetType().Assembly.GetName().Name != "Assembly-CSharp") continue;

            var so = new SerializedObject(behaviour);
            SerializedProperty property = so.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                if (property.name == "m_Script" || property.objectReferenceValue != null) continue;
                if (IsOptional(behaviour.GetType(), property.name)) continue;

                problems += Report($"{GetPath(behaviour.transform)} ({behaviour.GetType().Name}): '{property.displayName}' 연결이 비어 있습니다.", behaviour);
            }
        }
        return problems;
    }

    // [Tooltip] 에 "비워두면" 이 들어간 필드는 비어 있어도 되는 연결이다
    private static bool IsOptional(System.Type type, string fieldName)
    {
        for (var t = type; t != null; t = t.BaseType)
        {
            var field = t.GetField(fieldName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);
            if (field == null) continue;
            var tip = (TooltipAttribute)System.Attribute.GetCustomAttribute(field, typeof(TooltipAttribute));
            return tip != null && tip.tooltip.Contains("비워두면");
        }
        return false;
    }

    private static string GetPath(Transform t)
    {
        string path = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            path = $"{t.name}/{path}";
        }
        return path;
    }

    private static int Report(string message, Object context)
    {
        Debug.LogWarning($"[UI 검사] {message}", context);
        return 1;
    }
}
