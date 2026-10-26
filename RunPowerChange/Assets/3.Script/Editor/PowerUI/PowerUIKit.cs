using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 달려라! 전력 챌린지 UI 에디터 도구들이 같이 쓰는 경로, 색, 생성 헬퍼.
public static class PowerUIKit
{
    #region 경로

    public const string SpriteRoot = "Assets/4.Sprite";
    public const string StaticDir = SpriteRoot + "/Static";
    public const string PartsDir = SpriteRoot + "/Parts";
    public const string GuideDir = SpriteRoot + "/_Guide";

    public const string PrefabRoot = "Assets/2.Model/Prefabs/RunPowerChange";
    public const string PartPrefabDir = PrefabRoot + "/Parts";
    public const string PanelPrefabDir = PrefabRoot + "/Panels";
    // 쳇바퀴 화면(32형) 캔버스. 씬에 쳇바퀴 수만큼 배치한다.
    public const string CanvasPrefabPath = PrefabRoot + "/Canvas_Wheel.prefab";
    // 85형 관전 화면 캔버스 (세로)
    public const string SpectatorPrefabPath = PrefabRoot + "/Canvas_Spectator.prefab";
    public const string SpectatorName = "Spectator";

    public const string StaticLayerName = "StaticLayer";

    private const string BoldFontPath = "Assets/10.Font/defaultFont/NotoSansKR-Bold SDF.asset";
    private const string MediumFontPath = "Assets/10.Font/defaultFont/NotoSansKR-Medium SDF.asset";

    // 32형 패널 이름 (Panel_xxx 프리팹, Static_xxx 이미지, Guide_xxx 이미지에 같이 쓰인다)
    public static readonly string[] PanelNames =
        { "Idle", "WaitNext", "Guide", "CharacterSelect", "Countdown", "Run", "RunResult", "Voltage", "Transmit", "Result" };

    public static string StaticSpritePath(string panel) => $"{StaticDir}/Static_{panel}.png";
    public static string GuidePath(string panel) => $"{GuideDir}/Guide_{panel}.png";
    public static string PanelPrefabPath(string panel) => $"{PanelPrefabDir}/Panel_{panel}.prefab";
    public static string PartSpritePath(string name) => $"{PartsDir}/{name}.png";
    public static string PartPrefabPath(string name) => $"{PartPrefabDir}/{name}.prefab";

    #endregion

    #region 화면 규격과 색

    // 32형 터치모니터 (쳇바퀴 내부, 가로)
    public const int ScreenWidth = 1920;
    public const int ScreenHeight = 1080;
    public static readonly Vector2 ReferenceResolution = new Vector2(ScreenWidth, ScreenHeight);

    // 85형 관전 화면 (세로). 2160x3840 모니터도 같은 비율이라 Expand 로 그대로 커진다.
    public const int SpectatorWidth = 1080;
    public const int SpectatorHeight = 1920;
    public static readonly Vector2 SpectatorResolution = new Vector2(SpectatorWidth, SpectatorHeight);

    // 디자인 시안 색. 32형은 3D 장면 위에 반투명 흰 카드, 85형은 밝은 회색 바탕.
    public static readonly Color BgColor = new Color32(236, 236, 241, 255);
    public static readonly Color CardColor = new Color32(255, 255, 255, 228);
    public static readonly Color DarkCardColor = new Color32(46, 49, 84, 255);
    public static readonly Color SoftBoxColor = new Color32(255, 255, 255, 60);
    // 주황 강조 (버튼, 선택, 남은 시간). 리치 텍스트 강조는 AccentHex 와 같은 값을 쓴다.
    public static readonly Color AccentColor = new Color32(242, 106, 61, 255);
    public const string AccentHex = "#F26A3D";
    public static readonly Color GreenColor = new Color32(45, 179, 111, 255);
    public static readonly Color BlueColor = new Color32(58, 134, 214, 255);
    public static readonly Color YellowColor = new Color32(242, 168, 29, 255);
    // 카드 위 글자 (남색), 3D 장면 위 제목 글자 (흰색)
    public static readonly Color TextColor = new Color32(46, 49, 84, 255);
    public static readonly Color TitleColor = Color.white;
    public static readonly Color SubTextColor = new Color32(107, 112, 136, 255);
    public static readonly Color DarkTextColor = new Color32(46, 49, 84, 255);
    public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.45f);

    // 게이지와 랭킹 줄 (흰 스프라이트에 프리팹이 칠하는 색)
    public static readonly Color GaugeTrackColor = new Color(1f, 1f, 1f, 0.92f);
    public static readonly Color LossColor = YellowColor;
    public static readonly Color RankRowColor = new Color(1f, 1f, 1f, 0.75f);

    #endregion

    #region 리소스

    private static TMP_FontAsset boldFont;
    private static TMP_FontAsset mediumFont;
    private static Sprite builtinRound;

    private static Sprite roundSprite;
    public static Sprite RoundSprite => roundSprite != null ? roundSprite : roundSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{PartsDir}/Card_Bg.png");

    public static TMP_FontAsset BoldFont => boldFont != null ? boldFont : boldFont = LoadFont(BoldFontPath);
    public static TMP_FontAsset MediumFont => mediumFont != null ? mediumFont : mediumFont = LoadFont(MediumFontPath);

    // 정적 레이어 렌더링용 둥근 박스 (유니티 기본 UISprite)
    public static Sprite BuiltinRound => builtinRound != null
        ? builtinRound
        : builtinRound = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

    private static TMP_FontAsset LoadFont(string path)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (font != null) return font;

        Debug.LogWarning($"폰트를 찾지 못해 TMP 기본 폰트를 사용합니다: {path}");
        return TMP_Settings.defaultFontAsset;
    }

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

    // 스프라이트 이미지. 9-slice 테두리가 있으면 Sliced로 늘린다.
    public static Image AddSprite(RectTransform rt, Sprite sprite, Color? tint = null)
    {
        Image image = AddImage(rt, tint ?? Color.white);
        image.sprite = sprite;
        image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        return image;
    }

    // 정적 레이어 렌더링용 둥근 박스 (기본 UISprite의 둥글기를 화면 규격에 맞게 키움)
    // 정적 레이어 렌더링용 둥근 박스. 부품 Card_Bg(흰 둥근 사각, 9-slice)를 radius 에 맞게 늘려 쓴다.
    // 반지름이 상자 절반보다 크면 Unity 가 모서리를 줄여 원·알약 모양이 된다.
    public static Image AddRoundBox(RectTransform rt, Color color, float radius = 24f)
    {
        Image image = AddImage(rt, color);
        Sprite round = RoundSprite;
        image.sprite = round != null ? round : BuiltinRound;
        image.type = Image.Type.Sliced;
        float spriteRadius = round != null ? round.border.x : 3f;
        image.pixelsPerUnitMultiplier = spriteRadius / Mathf.Max(1f, radius);
        return image;
    }

    public static RectTransform CreateRoundBox(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color, float radius = 24f)
    {
        RectTransform box = Place(CreateRect(name, parent), pos, size);
        AddRoundBox(box, color, radius);
        return box;
    }

    // 양 끝이 반원인 알약 모양 상자
    public static RectTransform CreatePill(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
        => CreateRoundBox(parent, name, pos, size, color, size.y * 0.5f);

    public static TextMeshProUGUI AddText(RectTransform rt, string text, float fontSize, Color color, bool bold = false,
        TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.font = bold ? BoldFont : MediumFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    // 3D 장면 위 제목: 그림자 글자를 아래에 한 번 더 그린다 (정적 레이어 렌더링용)
    public static TextMeshProUGUI ShadowText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, bool bold = true, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        CreateText(parent, name + "_Shadow", text, pos + new Vector2(2f, -3f), size, fontSize, ShadowColor, bold, alignment);
        return CreateText(parent, name, text, pos, size, fontSize, color, bold, alignment);
    }

    public static TextMeshProUGUI CreateText(RectTransform parent, string name, string text, Vector2 pos, Vector2 size,
        float fontSize, Color color, bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        return AddText(Place(CreateRect(name, parent), pos, size), text, fontSize, color, bold, alignment);
    }

    // 부모 영역을 가득 채우는 텍스트
    public static TextMeshProUGUI FillText(RectTransform parent, string name, string text, float fontSize, Color color, bool bold = false)
    {
        return AddText(Stretch(CreateRect(name, parent), Vector2.zero, Vector2.one, 10), text, fontSize, color, bold);
    }

    #endregion

    #region 인스펙터 연결

    public static void Wire(Object target, params (string field, Object value)[] pairs)
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

    public static void WireList(Object target, string field, IList<Object> values)
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
