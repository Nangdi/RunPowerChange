using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 달려라! 전력 챌린지 UI 에디터 도구들이 같이 쓰는 경로, 색, 글꼴, PSD 좌표, 생성 헬퍼.
public static class PowerUIKit
{
    #region 경로

    public const string SpriteRoot = "Assets/4.Sprite";
    public const string StaticDir = SpriteRoot + "/Static";
    public const string PartsDir = SpriteRoot + "/Parts";
    public const string GuideDir = SpriteRoot + "/_Guide";
    // Tools/psd_export.py 가 디자이너 PSD 에서 뽑은 부품·글자 자리 좌표
    public const string LayoutPath = SpriteRoot + "/_Layout/psd_layout.json";

    public const string PrefabRoot = "Assets/2.Model/Prefabs/RunPowerChange";
    public const string PartPrefabDir = PrefabRoot + "/Parts";
    public const string PanelPrefabDir = PrefabRoot + "/Panels";
    // 쳇바퀴 화면(32형) 캔버스. 씬에 쳇바퀴 수만큼 배치한다.
    public const string CanvasPrefabPath = PrefabRoot + "/Canvas_Wheel.prefab";
    // 85형 관전 화면 캔버스 (세로)
    public const string SpectatorPrefabPath = PrefabRoot + "/Canvas_Spectator.prefab";
    public const string SpectatorName = "Spectator";

    public const string StaticLayerName = "StaticLayer";

    private const string FontDir = "Assets/10.Font/Paperlogy";

    // 32형 패널 이름 (Panel_xxx 프리팹, Static_xxx 이미지, Guide_xxx 이미지에 같이 쓰인다). WheelSession.State 순서.
    public static readonly string[] PanelNames =
        { "Idle", "Join", "Guide", "Countdown", "Run", "Voltage", "Sale", "Ranking" };

    public static string StaticSpritePath(string panel) => $"{StaticDir}/Static_{panel}.png";
    public static string GuidePath(string panel) => $"{GuideDir}/Guide_{panel}.png";
    public static string PanelPrefabPath(string panel) => $"{PanelPrefabDir}/Panel_{panel}.prefab";
    public static string PartSpritePath(string name) => $"{PartsDir}/{name}.png";

    #endregion

    #region 화면 규격과 색

    // 32형 터치모니터 (쳇바퀴 내부, 가로) = 디자인 PSD 크기
    public const int ScreenWidth = 1920;
    public const int ScreenHeight = 1080;
    public static readonly Vector2 ReferenceResolution = new Vector2(ScreenWidth, ScreenHeight);

    // 85형 관전 화면 (세로) = 디자인 PSD 크기. 1080x1920 모니터도 같은 비율이라 Expand 로 그대로 줄어든다.
    public const int SpectatorWidth = 2160;
    public const int SpectatorHeight = 3840;
    public static readonly Vector2 SpectatorResolution = new Vector2(SpectatorWidth, SpectatorHeight);

    // 디자인 PSD 글자 색
    public static readonly Color TextColor = new Color32(52, 55, 90, 255);       // 남색 (카드·표 위 글자)
    public static readonly Color OrangeColor = new Color32(250, 114, 85, 255);   // 발전량 숫자
    public static readonly Color GreenColor = new Color32(87, 181, 136, 255);    // 순위 숫자
    public static readonly Color YellowColor = new Color32(251, 176, 59, 255);   // 문구 강조
    public const string YellowHex = "#FBB03B";
    public static readonly Color WhiteColor = Color.white;
    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);

    #endregion

    #region 글꼴

    // 디자인 글꼴: 문구 Paperlogy, 숫자 HG꼬딕씨(설치본 없음 → Paperlogy ExtraBold 로 대신함)
    public enum Weight { Medium, SemiBold, Bold, ExtraBold }

    private static readonly Dictionary<Weight, TMP_FontAsset> fonts = new Dictionary<Weight, TMP_FontAsset>();

    public static TMP_FontAsset Font(Weight weight)
    {
        if (fonts.TryGetValue(weight, out TMP_FontAsset font) && font != null) return font;
        string file = weight switch
        {
            Weight.Medium => "Paperlogy-5Medium SDF.asset",
            Weight.SemiBold => "Paperlogy-6SemiBold SDF.asset",
            Weight.Bold => "Paperlogy-7Bold SDF.asset",
            _ => "Paperlogy-8ExtraBold SDF.asset",
        };
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{FontDir}/{file}");
        if (font == null)
        {
            Debug.LogWarning($"폰트를 찾지 못해 TMP 기본 폰트를 사용합니다: {FontDir}/{file}");
            font = TMP_Settings.defaultFontAsset;
        }
        fonts[weight] = font;
        return font;
    }

    #endregion

    #region PSD 좌표

    [Serializable]
    private class LayoutRect
    {
        public string name;
        public float x, y, w, h;
    }

    [Serializable]
    private class LayoutFile
    {
        public int wheelWidth, wheelHeight, spectatorWidth, spectatorHeight;
        public List<LayoutRect> rects = new List<LayoutRect>();
    }

    private static Dictionary<string, Rect> layout;

    public static void ReloadLayout() => layout = null;

    // PSD 픽셀 사각형 (왼쪽 위 원점)
    public static Rect Psd(string name)
    {
        if (layout == null)
        {
            layout = new Dictionary<string, Rect>();
            if (!File.Exists(LayoutPath))
            {
                Debug.LogError($"PSD 좌표 파일이 없습니다: {LayoutPath} (Tools/psd_export.py 를 먼저 실행하세요)");
            }
            else
            {
                var file = JsonUtility.FromJson<LayoutFile>(File.ReadAllText(LayoutPath));
                foreach (var r in file.rects) layout[r.name] = new Rect(r.x, r.y, r.w, r.h);
            }
        }
        if (layout.TryGetValue(name, out Rect rect)) return rect;
        Debug.LogError($"PSD 좌표에 '{name}' 이 없습니다 ({LayoutPath})");
        return new Rect(0, 0, 100, 100);
    }

    // PSD 픽셀 좌표(왼쪽 위 원점) → 캔버스 가운데 원점 좌표
    public static Vector2 PsdPoint(Vector2 psd, Vector2 screen) => new Vector2(psd.x - screen.x * 0.5f, screen.y * 0.5f - psd.y);
    public static Vector2 PsdCenter(Rect r, Vector2 screen) => PsdPoint(r.center, screen);

    #endregion

    #region 리소스

    public static Sprite LoadSprite(string path)
    {
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            Debug.LogError($"스프라이트를 찾지 못했습니다: {path}");
        return sprite;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion

    #region 생성 헬퍼

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        if (parent != null)
            go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    // 부모 중앙 기준 위치/크기 배치
    public static RectTransform Place(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    // 앵커 비율로 늘려 배치. inset이 음수면 바깥으로 커진다.
    public static RectTransform Stretch(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, float inset = 0f)
    {
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    public static RectTransform StretchFull(RectTransform rt) => Stretch(rt, Vector2.zero, Vector2.one);

    public static Image AddImage(RectTransform rt, Color color)
    {
        var image = rt.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    public static Image AddSprite(RectTransform rt, Sprite sprite, Color? tint = null)
    {
        Image image = AddImage(rt, tint ?? Color.white);
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        return image;
    }

    public static TextMeshProUGUI AddText(RectTransform rt, string text, float fontSize, Color color, Weight weight = Weight.SemiBold,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = Font(weight);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static TextMeshProUGUI CreateText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, Weight weight = Weight.SemiBold, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        return AddText(Place(CreateRect(name, parent), pos, size), text, fontSize, color, weight, alignment);
    }

    #endregion

    #region 인스펙터 연결

    public static void Wire(UnityEngine.Object target, params (string field, UnityEngine.Object value)[] pairs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in pairs)
        {
            SerializedProperty property = so.FindProperty(field);
            if (property == null)
            {
                Debug.LogError($"{target.GetType().Name}에 '{field}' 필드가 없습니다.");
                continue;
            }
            property.objectReferenceValue = value;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void WireList(UnityEngine.Object target, string field, IList<UnityEngine.Object> values)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null || !property.isArray)
        {
            Debug.LogError($"{target.GetType().Name}에 '{field}' 리스트 필드가 없습니다.");
            return;
        }

        property.arraySize = values.Count;
        for (int i = 0; i < values.Count; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
}
