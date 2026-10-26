using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PowerUIKit;

// 달려라! 전력 챌린지 화면 프리팹을 디자이너 PSD 좌표대로 만들고 현재 씬에 배치한다.
// 메뉴: Tools > 달려라 전력 챌린지 > 1. 프리팹 생성 및 씬 적용
// 먼저 Tools/psd_export.py 로 PSD 에서 이미지와 좌표(4.Sprite/_Layout/psd_layout.json)를 뽑아 둔다.
//
// 구조
//  Canvas_Wheel.prefab (WheelScreen)          32형 2대. 씬에 Canvas_Wheel_1, _2 로 배치, 세션은 인스턴스에서 연결
//   └ Frame (1920x1080) └ Panel_xxx.prefab (8개)
//      ├ StaticLayer  : 정적 PNG 한 장 (4.Sprite/Static/Static_xxx.png, 배경 투명 - 뒤에 3D 경주 장면)
//      └ 동적 요소    : 버튼, 숫자 글자, 게이지, 순서대로 나타나는 카드
//  Canvas_Spectator.prefab (SpectatorScreen)  85형 세로. 씬에 1개
//   └ Frame (2160x3840) └ Panel_Spectator.prefab (상태·발전량 글자, 오늘 최고 랭킹 표시, 아래 칸 3개)
//
// 이미 있는 프리팹은 덮어쓰지 않는다. 직접 수정한 프리팹을 지키기 위해서다.
// 구조가 바뀌어 처음부터 다시 만들려면 BuildAll(rebuild: true) 를 쓴다 (같은 경로에 덮어써서 GUID 유지).
public static class PowerPrefabBuilder
{
    private const string CanvasInstancePrefix = "Canvas_Wheel_";
    private const string SpectatorInstanceName = "Canvas_Spectator";

    private static bool rebuildAll;
    // 카드 안 숫자가 카드 가장자리에 닿지 않게 두는 여백(px)
    private const float CardPadding = 36f;
    // 지금 만드는 패널의 화면 크기 (PSD 좌표 변환용)
    private static Vector2 screen = ReferenceResolution;

    [MenuItem("Tools/달려라 전력 챌린지/1. 프리팹 생성 및 씬 적용", priority = 1)]
    public static void BuildAllMenu() => BuildAll(false);

    public static void BuildAll(bool rebuild)
    {
        rebuildAll = rebuild;
        ReloadLayout();
        try
        {
            EnsureFolder(PanelPrefabDir);

            var panelBuilders = new Dictionary<string, Func<GameObject>>
            {
                { "Idle", CreateIdlePanel },
                { "Join", CreateJoinPanel },
                { "Guide", CreateGuidePanel },
                { "Countdown", CreateCountdownPanel },
                { "Run", CreateRunPanel },
                { "Voltage", CreateVoltagePanel },
                { "Sale", CreateSalePanel },
                { "Ranking", CreateRankingPanel },
            };
            screen = ReferenceResolution;
            var panels = new List<GameObject>();
            foreach (string panel in PanelNames)
                panels.Add(GetOrCreate(PanelPrefabPath(panel), panelBuilders[panel]));
            GameObject canvasPrefab = GetOrCreate(CanvasPrefabPath, () => CreateCanvas(panels));

            screen = SpectatorResolution;
            GameObject spectatorPanel = GetOrCreate(PanelPrefabPath(SpectatorName), CreateSpectatorPanel);
            GameObject spectatorPrefab = GetOrCreate(SpectatorPrefabPath, () => CreateSpectatorCanvas(spectatorPanel));
            screen = ReferenceResolution;

            ApplyToScene(canvasPrefab);
            ApplySpectatorToScene(spectatorPrefab);
            EnsureSoundManager();
            PowerSpriteExporter.ExportGuides();
            AssetDatabase.SaveAssets();
            Debug.Log("달려라 전력 챌린지 프리팹 적용 완료. 씬을 저장하세요.");
        }
        finally
        {
            rebuildAll = false;
            screen = ReferenceResolution;
        }
    }

    private static GameObject GetOrCreate(string path, Func<GameObject> build)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null && !rebuildAll) return existing;

        GameObject temp = build();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        UnityEngine.Object.DestroyImmediate(temp);
        Debug.Log($"프리팹 {(existing != null ? "다시 생성" : "생성")}: {path}");
        return prefab;
    }

    #region 생성 헬퍼

    private static Sprite Part(string name) => LoadSprite(PartSpritePath(name));

    // PSD 사각형 자리에 놓는 빈 RectTransform
    private static RectTransform PsdRect(RectTransform parent, string name, string rectName)
    {
        Rect r = Psd(rectName);
        return Place(CreateRect(name, parent), PsdCenter(r, screen), r.size);
    }

    // 디자이너 부품 그림 (그림 크기 = PSD 자리 크기)
    private static Image PartImage(RectTransform parent, string name, string sprite, string rectName = null)
    {
        Image image = AddSprite(PsdRect(parent, name, rectName ?? sprite), Part(sprite));
        image.preserveAspect = true;
        return image;
    }

    private static Button PartButton(RectTransform parent, string name, string sprite)
    {
        Image image = PartImage(parent, name, sprite);
        image.raycastTarget = true;
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    private static void WireColor(UnityEngine.Object target, params (string field, Color value)[] pairs)
    {
        var so = new SerializedObject(target);
        foreach (var (field, value) in pairs) so.FindProperty(field).colorValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 아래 끝 가운데를 피벗으로 둔 부품 그림 (3D 위치를 따라다니는 표시)
    private static Image TagImage(RectTransform parent, string sprite)
    {
        Image image = PartImage(parent, sprite, sprite);
        RectTransform rt = image.rectTransform;
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition -= new Vector2(0f, rt.sizeDelta.y * 0.5f);
        return image;
    }

    // 남색 반투명 둥근 상자 (Unity 기본 UI 스프라이트 9-slice). PSD 에 없는 안내 문구 바탕용.
    private static Image RoundBox(RectTransform parent, string name, Vector2 psdCenter, Vector2 size)
    {
        Image box = AddSprite(Place(CreateRect(name, parent), PsdPoint(psdCenter, screen), size),
            AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"), new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f));
        box.type = Image.Type.Sliced;
        box.pixelsPerUnitMultiplier = 0.1f;
        return box;
    }

    // 왼쪽에서 오른쪽으로 채워지는 게이지 그림
    private static Image FilledImage(RectTransform parent, string name, string sprite, string rectName = null)
    {
        Image image = PartImage(parent, name, sprite, rectName);
        image.preserveAspect = false;
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = (int)Image.OriginHorizontal.Left;
        image.fillAmount = 0.7f;
        return image;
    }

    private static GaugeView CreateGauge(RectTransform parent, string name, string sprite, string rectName = null, params Image[] followers)
    {
        Image fill = FilledImage(parent, name, sprite, rectName);
        var view = fill.gameObject.AddComponent<GaugeView>();
        Wire(view, ("fill", fill));
        WireList(view, "followers", Array.ConvertAll(followers, f => (UnityEngine.Object)f));
        return view;
    }

    // 자동 넘김 화면 하단 남은 시간 바 (같은 그림을 화면마다 같은 자리에 둔다)
    private static void CreateAutoGauge(RectTransform panel, WheelPanel view, string rectName)
    {
        GaugeView gauge = CreateGauge(panel, "AutoGauge", "Auto_Gauge", rectName);
        Wire(view, ("autoGauge", gauge));
    }

    // PSD 글자 자리(잉크 영역)에 맞춘 값 글자. 한 줄, 넘치면 자동 축소. 세로는 글자 모양(잉크) 가운데를 맞춘다.
    // align 이 Left/Right 면 PSD 글자의 왼쪽/오른쪽 끝을 기준으로 width 만큼 늘린다.
    // baseline 이 true 면 PSD 숫자 잉크의 아래 끝(= 기준선)에 글자 기준선을 맞춘다. 옆에 단위(wh, 위, ₩)가 그림으로 붙어 있는 숫자용:
    // 값이 길어져 자동 축소돼도 단위와 같은 기준선에 앉는다.
    private static TMP_Text PsdText(RectTransform parent, string name, string rectName, string sample, float fontSize, Color color,
        Weight weight, HorizontalAlignmentOptions align = HorizontalAlignmentOptions.Center, float width = 0f, bool baseline = false)
    {
        Rect r = Psd(rectName);
        float w = Mathf.Max(width, r.width + 20f);
        float cx = align == HorizontalAlignmentOptions.Left ? r.xMin + w * 0.5f
            : align == HorizontalAlignmentOptions.Right ? r.xMax - w * 0.5f
            : r.center.x;
        TMP_Text text = ValueText(parent, name, sample, new Vector2(cx, baseline ? r.yMax : r.center.y), new Vector2(w, fontSize * 1.4f),
            fontSize, color, weight, align);
        if (baseline) text.verticalAlignment = VerticalAlignmentOptions.Baseline;
        return text;
    }

    private static TMP_Text ValueText(RectTransform parent, string name, string sample, Vector2 psdCenter, Vector2 size, float fontSize,
        Color color, Weight weight, HorizontalAlignmentOptions align)
    {
        TextMeshProUGUI text = AddText(Place(CreateRect(name, parent), PsdPoint(psdCenter, screen), size), sample, fontSize, color, weight);
        text.horizontalAlignment = align;
        text.verticalAlignment = VerticalAlignmentOptions.Geometry;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * 0.4f;
        text.fontSizeMax = fontSize;
        return text;
    }

    private static CanvasGroup Group(RectTransform parent, string name)
    {
        RectTransform rt = StretchFull(CreateRect(name, parent));
        return rt.gameObject.AddComponent<CanvasGroup>();
    }

    private static UnityEngine.Object[] Objects(params Component[] items) =>
        Array.ConvertAll(items, c => c != null ? (UnityEngine.Object)c.gameObject : null);

    #endregion

    #region 32형 패널 프리팹

    private static RectTransform CreatePanelRoot(string panel)
    {
        RectTransform root = StretchFull(CreateRect($"Panel_{panel}", null));
        AddSprite(StretchFull(CreateRect(StaticLayerName, root)), LoadSprite(StaticSpritePath(panel)));
        return root;
    }

    private static GameObject CreateIdlePanel()
    {
        RectTransform panel = CreatePanelRoot("Idle");

        // 시작 버튼 그림은 표시만 하고, 터치는 화면 전체를 덮는 투명 버튼이 받는다
        Image badge = PartImage(panel, "StartBadge", "Button_Start");

        // 옆 쳇바퀴 체험 진행 중 안내 (버튼 바로 위)
        Rect button = Psd("Button_Start");
        RectTransform busy = StretchFull(CreateRect("Group_Busy", panel));
        Image busyBox = RoundBox(busy, "Box", new Vector2(960f, button.yMin - 50f), new Vector2(1180f, 72f));
        TMP_Text busyText = AddText(StretchFull(CreateRect("Text", busyBox.rectTransform)),
            $"지금 <color={YellowHex}>다른 체험</color>이 진행 중이에요. 약 90초 후 체험할 수 있어요.", 34, WhiteColor, Weight.SemiBold);
        busyText.enableWordWrapping = false;
        busy.gameObject.SetActive(false);

        RectTransform touch = StretchFull(CreateRect("StartButton", panel));
        Image touchImage = AddImage(touch, new Color(0, 0, 0, 0));
        touchImage.raycastTarget = true;
        var start = touch.gameObject.AddComponent<Button>();
        start.targetGraphic = touchImage;
        start.transition = Selectable.Transition.None;

        var view = panel.gameObject.AddComponent<IdlePanel>();
        Wire(view, ("startButton", start), ("startBadge", badge), ("busyGroup", busy.gameObject), ("busyText", busyText));
        WireColor(view, ("highlightColor", YellowColor));
        return panel.gameObject;
    }

    private static GameObject CreateJoinPanel()
    {
        RectTransform panel = CreatePanelRoot("Join");

        Image msgWait = PartImage(panel, "Msg_Wait", "Join_Msg_Wait");
        Image msgInvite = PartImage(panel, "Msg_Invite", "Join_Msg_Invite");
        Image msgReady = PartImage(panel, "Msg_Ready", "Join_Msg_Ready");
        Image subWait = PartImage(panel, "Sub_Wait", "Join_Sub_Wait");
        Image subInvite = PartImage(panel, "Sub_Invite", "Join_Sub_Invite");

        // 남은 초: 회전하는 원 + 숫자 그림 (두 자리까지)
        RectTransform counter = StretchFull(CreateRect("Group_Counter", panel));
        Image ring = PartImage(counter, "Ring", "Join_Ring");
        Rect digitRect = Psd("Join_Digit");
        Sprite[] digitSprites = new Sprite[10];
        for (int d = 0; d < 10; d++) digitSprites[d] = Part($"Join_Digit_{d}");
        Vector2 cell = digitSprites[0] != null ? digitSprites[0].rect.size : digitRect.size;
        RectTransform digitRoot = Place(CreateRect("Digits", counter), PsdCenter(digitRect, screen), new Vector2(cell.x * 2f, cell.y));
        var layout = digitRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = layout.childControlHeight = false;
        layout.childForceExpandWidth = layout.childForceExpandHeight = false;
        layout.spacing = -cell.x * 0.15f;
        var cells = new UnityEngine.Object[2];
        for (int i = 0; i < 2; i++)
        {
            RectTransform c = CreateRect($"Digit_{i + 1}", digitRoot);
            c.sizeDelta = cell;
            cells[i] = AddSprite(c, digitSprites[9]);
        }
        var digits = digitRoot.gameObject.AddComponent<DigitView>();
        WireList(digits, "digitSprites", Array.ConvertAll(digitSprites, s => (UnityEngine.Object)s));
        WireList(digits, "cells", cells);

        Button join = PartButton(panel, "JoinButton", "Join_Button");
        join.gameObject.SetActive(false);

        Image ready1 = PartImage(panel, "Badge_Ready_1", "Badge_Ready_1");
        Image wait1 = PartImage(panel, "Badge_Wait_1", "Badge_Wait_1");
        Image ready2 = PartImage(panel, "Badge_Ready_2", "Badge_Ready_2");
        Image wait2 = PartImage(panel, "Badge_Wait_2", "Badge_Wait_2");
        wait1.gameObject.SetActive(false);
        ready2.gameObject.SetActive(false);
        msgInvite.gameObject.SetActive(false);
        msgReady.gameObject.SetActive(false);
        subInvite.gameObject.SetActive(false);

        var view = panel.gameObject.AddComponent<JoinPanel>();
        WireList(view, "messages", Objects(msgWait, msgInvite, msgReady));
        WireList(view, "subMessages", Objects(subWait, subInvite));
        WireList(view, "readyBadges", Objects(ready1, ready2));
        WireList(view, "waitBadges", Objects(wait1, wait2));
        Wire(view, ("counterGroup", counter.gameObject), ("ring", ring.rectTransform), ("digits", digits), ("joinButton", join));
        return panel.gameObject;
    }

    private static GameObject CreateGuidePanel()
    {
        RectTransform panel = CreatePanelRoot("Guide");
        var view = panel.gameObject.AddComponent<GuidePanel>();
        CreateAutoGauge(panel, view, "Guide_Gauge");
        return panel.gameObject;
    }

    private static GameObject CreateCountdownPanel()
    {
        RectTransform panel = CreatePanelRoot("Countdown");
        var numbers = new UnityEngine.Object[3];
        for (int i = 0; i < 3; i++)
        {
            Image n = PartImage(panel, $"Count_{i + 1}", $"Count_{i + 1}");
            n.gameObject.SetActive(i == 2);
            numbers[i] = n.rectTransform;
        }
        Image go = PartImage(panel, "Count_Go", "Count_Go");
        go.gameObject.SetActive(false);

        var view = panel.gameObject.AddComponent<CountdownPanel>();
        WireList(view, "numbers", numbers);
        Wire(view, ("go", go.rectTransform));
        return panel.gameObject;
    }

    private static GameObject CreateRunPanel()
    {
        RectTransform panel = CreatePanelRoot("Run");

        // 속도계: 바늘(가운데 원 포함)을 돌리고 그 위에 숫자
        Image needle = PartImage(panel, "Needle", "Run_Needle");
        TMP_Text speed = PsdText(panel, "SpeedText", "Run_SpeedText", "00", 108, WhiteColor, Weight.ExtraBold);

        // 남은 시간: 원형 바가 반시계 방향으로 없어진다
        Image ring = PartImage(panel, "TimerRing", "Run_TimerRing");
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = true;
        ring.fillAmount = 0.75f;
        TMP_Text remain = PsdText(panel, "RemainText", "Run_TimeText", "00", 110, WhiteColor, Weight.ExtraBold);

        // 캐릭터 머리 위를 따라다니므로 아래 끝(말풍선 꼬리)을 피벗으로 둔다
        Image meL = TagImage(panel, "Tag_L_Me");
        Image otherL = TagImage(panel, "Tag_L_Other");
        Image meR = TagImage(panel, "Tag_R_Me");
        Image otherR = TagImage(panel, "Tag_R_Other");
        otherL.gameObject.SetActive(false);
        meR.gameObject.SetActive(false);

        // 상대(혼자면 오늘 1위)와 거리 차: 위쪽 가운데
        Image compareBox = RoundBox(panel, "Group_Compare", new Vector2(960f, 70f), new Vector2(760f, 76f));
        TMP_Text compare = AddText(StretchFull(CreateRect("Text", compareBox.rectTransform)), "PLAYER 02보다 <color=#57B588>12m</color> 앞서는 중!", 38,
            WhiteColor, Weight.SemiBold);
        compare.enableWordWrapping = false;
        compareBox.gameObject.SetActive(false);

        Image shine = FilledImage(panel, "BarShine", "Run_BarShine");
        GaugeView bar = CreateGauge(panel, "EnergyBar", "Run_BarFill", null, shine);
        shine.transform.SetAsLastSibling();
        // 숫자는 오른쪽 끝(wh 표시 바로 앞)에 붙는다
        TMP_Text energy = PsdText(panel, "EnergyText", "Run_BarText", "0.0", 45, WhiteColor, Weight.ExtraBold,
            HorizontalAlignmentOptions.Right, 360f, true);

        var view = panel.gameObject.AddComponent<RunPanel>();
        Wire(view,
            ("speedText", speed),
            ("needle", needle.rectTransform),
            ("remainText", remain),
            ("remainRing", ring),
            ("energyText", energy),
            ("energyGauge", bar),
            ("compareGroup", compareBox.gameObject),
            ("compareText", compare));
        WireColor(view, ("aheadColor", GreenColor), ("behindColor", OrangeColor), ("evenColor", YellowColor));
        WireList(view, "meTags", Objects(meL, meR));
        WireList(view, "otherTags", Objects(otherL, otherR));
        return panel.gameObject;
    }

    private static GameObject CreateVoltagePanel()
    {
        RectTransform panel = CreatePanelRoot("Voltage");
        // 숫자 칸 왼쪽 끝 = 카드 왼쪽 끝 + 여백 (값이 길어지면 카드 밖으로 나가지 않고 줄어든다)
        TMP_Text energy = PsdText(panel, "EnergyText", "Voltage_EnergyText", "3.5", 185, OrangeColor, Weight.ExtraBold,
            HorizontalAlignmentOptions.Right, Psd("Voltage_EnergyText").xMax - Psd("Voltage_Card").xMin - CardPadding, true);
        TMP_Text rank = PsdText(panel, "RankText", "Voltage_RankText", "35", 185, GreenColor, Weight.ExtraBold,
            HorizontalAlignmentOptions.Right, 280f, true);
        Button high = PartButton(panel, "HighButton", "Button_High");
        Button low = PartButton(panel, "LowButton", "Button_Low");

        var view = panel.gameObject.AddComponent<VoltagePanel>();
        Wire(view, ("lowButton", low), ("highButton", high), ("energyText", energy), ("rankText", rank));
        CreateAutoGauge(panel, view, "Voltage_Gauge");
        return panel.gameObject;
    }

    private static GameObject CreateSalePanel()
    {
        RectTransform panel = CreatePanelRoot("Sale");

        CanvasGroup energyStep = Group(panel, "Step_1_Energy");
        PartImage((RectTransform)energyStep.transform, "Card", "Sale_Energy");
        TMP_Text energy = PsdText((RectTransform)energyStep.transform, "EnergyText", "Sale_EnergyText", "0.0", 185, OrangeColor,
            Weight.ExtraBold, HorizontalAlignmentOptions.Right, Psd("Sale_EnergyText").xMax - Psd("Sale_Energy").xMin - CardPadding, true);

        CanvasGroup arrow1 = Group(panel, "Step_2_Arrow");
        PartImage((RectTransform)arrow1.transform, "Arrow", "Sale_Arrow_1");

        CanvasGroup voltageStep = Group(panel, "Step_3_Voltage");
        Image low = PartImage((RectTransform)voltageStep.transform, "Card_Low", "Sale_Low");
        Image high = PartImage((RectTransform)voltageStep.transform, "Card_High", "Sale_High");
        low.gameObject.SetActive(false);

        CanvasGroup arrow2 = Group(panel, "Step_4_Arrow");
        PartImage((RectTransform)arrow2.transform, "Arrow", "Sale_Arrow_2");

        CanvasGroup moneyStep = Group(panel, "Step_5_Money");
        PartImage((RectTransform)moneyStep.transform, "Card", "Sale_Money");
        TMP_Text money = PsdText((RectTransform)moneyStep.transform, "MoneyText", "Sale_MoneyText", "0,000", 160, WhiteColor,
            Weight.ExtraBold, HorizontalAlignmentOptions.Center, 420f);

        var view = panel.gameObject.AddComponent<SalePanel>();
        WireList(view, "steps", new UnityEngine.Object[] { energyStep, arrow1, voltageStep, arrow2, moneyStep });
        WireList(view, "voltageCards", Objects(low, high));
        Wire(view, ("energyText", energy), ("moneyText", money));
        CreateAutoGauge(panel, view, "Sale_Gauge");
        return panel.gameObject;
    }

    // 랭킹 표 한쪽: 1~5위 줄의 기록 시각(왼쪽 정렬)과 값(오른쪽 정렬), 나의 순위 줄
    // 값은 오른쪽의 단위 그림(wh, ₩)과 기준선을 맞춘다. 기준선은 발전왕 값 샘플(쉼표 없는 숫자)의 잉크 아래 끝에서 줄 간격씩 올라간다.
    private static RankTableView CreateRankTable(RectTransform panel, string name, string prefix, string baselinePrefix, float rowPitch,
        float timeSize, float valueSize, bool myRow)
    {
        RectTransform root = StretchFull(CreateRect(name, panel));
        Rect times = Psd($"{prefix}_Times");
        Rect values = Psd($"{prefix}_Values");
        float lastBaseline = Psd($"{baselinePrefix}_Values").yMax;
        float timeInk = timeSize * 0.85f;
        float firstY = times.yMin + timeInk * 0.5f;

        var timeTexts = new UnityEngine.Object[5];
        var valueTexts = new UnityEngine.Object[5];
        for (int i = 0; i < 5; i++)
        {
            float y = firstY + rowPitch * i;
            timeTexts[i] = ValueText(root, $"Row{i + 1}_Time", "26.10.22 1:22:43", new Vector2(times.xMin + 200f, y),
                new Vector2(400f, timeSize * 1.4f), timeSize, TextColor, Weight.SemiBold, HorizontalAlignmentOptions.Left);
            TMP_Text value = ValueText(root, $"Row{i + 1}_Value", "4.5", new Vector2(values.xMax - 150f, lastBaseline - rowPitch * (4 - i)),
                new Vector2(300f, valueSize * 1.4f), valueSize, TextColor, Weight.ExtraBold, HorizontalAlignmentOptions.Right);
            value.verticalAlignment = VerticalAlignmentOptions.Baseline;
            valueTexts[i] = value;
        }

        var view = root.gameObject.AddComponent<RankTableView>();
        WireList(view, "timeTexts", timeTexts);
        WireList(view, "valueTexts", valueTexts);
        if (myRow)
        {
            TMP_Text rank = PsdText(root, "My_Rank", $"{prefix}_MyRank", "35", 23, WhiteColor, Weight.Bold, HorizontalAlignmentOptions.Center, 60f);
            TMP_Text time = PsdText(root, "My_Time", $"{prefix}_MyTime", "26.10.22 1:22:43", timeSize, WhiteColor, Weight.SemiBold,
                HorizontalAlignmentOptions.Left, 400f);
            Rect myValue = Psd($"{prefix}_MyValue");
            TMP_Text value = ValueText(root, "My_Value", "3.5", new Vector2(myValue.xMax - 150f, Psd($"{baselinePrefix}_MyValue").yMax),
                new Vector2(300f, valueSize * 1.4f), valueSize, WhiteColor, Weight.ExtraBold, HorizontalAlignmentOptions.Right);
            value.verticalAlignment = VerticalAlignmentOptions.Baseline;
            Wire(view, ("myRankText", rank), ("myTimeText", time), ("myValueText", value));
        }
        return view;
    }

    private static GameObject CreateRankingPanel()
    {
        RectTransform panel = CreatePanelRoot("Ranking");
        RankTableView energy = CreateRankTable(panel, "Table_Energy", "Rank_E", "Rank_E", 80.5f, 22.4f, 38f, true);
        RankTableView sale = CreateRankTable(panel, "Table_Sale", "Rank_S", "Rank_E", 80.5f, 22.4f, 38f, true);

        var view = panel.gameObject.AddComponent<RankingPanel>();
        Wire(view, ("energyTable", energy), ("saleTable", sale));
        CreateAutoGauge(panel, view, "Ranking_Gauge");
        return panel.gameObject;
    }

    #endregion

    #region 85형 관전 화면 프리팹

    private static GameObject CreateSpectatorPanel()
    {
        RectTransform panel = StretchFull(CreateRect($"Panel_{SpectatorName}", null));
        AddSprite(StretchFull(CreateRect(StaticLayerName, panel)), LoadSprite(StaticSpritePath(SpectatorName)));

        for (int p = 1; p <= 2; p++)
        {
            PsdText(panel, $"Status_{p}", $"Spec_Badge_{p}", "체험 대기중", 43, TextColor, Weight.SemiBold,
                HorizontalAlignmentOptions.Center, 230f);
            // PLAYER 01 은 wh 표시 오른쪽에서 시작(왼쪽 정렬), PLAYER 02 는 wh 표시 왼쪽에서 끝(오른쪽 정렬)
            PsdText(panel, $"Energy_{p}", $"Spec_Value_{p}", "0.0", 185, OrangeColor, Weight.ExtraBold,
                p == 1 ? HorizontalAlignmentOptions.Left : HorizontalAlignmentOptions.Right, 640f, true);
        }

        Image tag = PartImage(panel, "RecordTag", "Spectator_RecordTag");
        tag.gameObject.SetActive(false);

        // 아래 칸 3개 (같은 자리에 겹쳐 두고 번갈아 보여 준다)
        CanvasGroup ranking = Group(panel, "Bottom_1_Ranking");
        PartImage((RectTransform)ranking.transform, "Board", "Spectator_Ranking");
        CreateRankTable((RectTransform)ranking.transform, "Table_Energy", "Spec_E", "Spec_E", 116.5f, 32f, 54.5f, false);
        CreateRankTable((RectTransform)ranking.transform, "Table_Sale", "Spec_S", "Spec_E", 116.5f, 32f, 54.5f, false);

        CanvasGroup info1 = Group(panel, "Bottom_2_Info");
        PartImage((RectTransform)info1.transform, "Board", "Spectator_Info_1");
        info1.alpha = 0f;
        CanvasGroup info2 = Group(panel, "Bottom_3_Info");
        PartImage((RectTransform)info2.transform, "Board", "Spectator_Info_2");
        info2.alpha = 0f;

        // SpectatorScreen(캔버스 루트)이 위 이름으로 연결한다
        return panel.gameObject;
    }

    private static GameObject CreateSpectatorCanvas(GameObject panelPrefab)
    {
        var go = new GameObject("Canvas_Spectator", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;
        canvas.targetDisplay = 2;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = SpectatorResolution;
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        go.AddComponent<GraphicRaycaster>();

        RectTransform frame = Place(CreateRect("Frame", go.transform), Vector2.zero, SpectatorResolution);
        var panel = (GameObject)PrefabUtility.InstantiatePrefab(panelPrefab, frame);
        Transform t = panel.transform;
        T Find<T>(string path) where T : Component => t.Find(path).GetComponent<T>();

        var screenView = go.AddComponent<SpectatorScreen>();
        Wire(screenView,
            ("canvas", canvas),
            ("recordTag", Find<RectTransform>("RecordTag")),
            ("energyTable", Find<RankTableView>("Bottom_1_Ranking/Table_Energy")),
            ("saleTable", Find<RankTableView>("Bottom_1_Ranking/Table_Sale")));
        WireList(screenView, "statusTexts", new UnityEngine.Object[] { Find<TMP_Text>("Status_1"), Find<TMP_Text>("Status_2") });
        WireList(screenView, "energyTexts", new UnityEngine.Object[] { Find<TMP_Text>("Energy_1"), Find<TMP_Text>("Energy_2") });
        WireList(screenView, "bottomGroups", new UnityEngine.Object[]
        {
            Find<CanvasGroup>("Bottom_1_Ranking"), Find<CanvasGroup>("Bottom_2_Info"), Find<CanvasGroup>("Bottom_3_Info"),
        });
        WireList(screenView, "infoImages", new UnityEngine.Object[] { Find<Image>("Bottom_2_Info/Board"), Find<Image>("Bottom_3_Info/Board") });
        return go;
    }

    #endregion

    #region 캔버스와 씬

    private static GameObject CreateCanvas(List<GameObject> panels)
    {
        var go = new GameObject("Canvas_Wheel", typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0; // 설정창(100), 디버그 패널보다 아래

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = ReferenceResolution;
        // 비율이 다른 모니터에서도 잘리지 않게 Expand. 남는 부분은 3D 장면이 채운다.
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        go.AddComponent<GraphicRaycaster>();

        // 패널은 기준 해상도 크기의 고정 틀 안에 둔다
        RectTransform frame = Place(CreateRect("Frame", go.transform), Vector2.zero, ReferenceResolution);

        // 편집하기 쉽도록 대기 화면만 켜 둔다. 실행 시에는 WheelScreen 이 상태에 맞게 켜고 끈다.
        var instances = new List<GameObject>();
        for (int i = 0; i < panels.Count; i++)
        {
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(panels[i], frame);
            panel.SetActive(i == 0);
            instances.Add(panel);
        }

        var screenView = go.AddComponent<WheelScreen>();
        Wire(screenView,
            ("canvas", canvas),
            ("idlePanel", instances[0].GetComponent<IdlePanel>()),
            ("joinPanel", instances[1].GetComponent<JoinPanel>()),
            ("guidePanel", instances[2].GetComponent<GuidePanel>()),
            ("countdownPanel", instances[3].GetComponent<CountdownPanel>()),
            ("runPanel", instances[4].GetComponent<RunPanel>()),
            ("voltagePanel", instances[5].GetComponent<VoltagePanel>()),
            ("salePanel", instances[6].GetComponent<SalePanel>()),
            ("rankingPanel", instances[7].GetComponent<RankingPanel>()));
        return go;
    }

    // 씬의 WheelSession 마다 Canvas_Wheel_<번호> 를 하나씩 두고 세션을 연결한다.
    // 에디터에서 두 화면을 Game 뷰 Display 1/2 로 나눠 볼 수 있도록 targetDisplay 도 미리 맞춘다 (실행 시 Setting.json 값으로 다시 맞춤).
    private static void ApplyToScene(GameObject canvasPrefab)
    {
        Undo.SetCurrentGroupName("Apply RunPowerChange UI");
        int undoGroup = Undo.GetCurrentGroup();

        WheelSession[] sessions = SortedSessions();
        if (sessions.Length == 0)
            Debug.LogWarning("씬에 WheelSession 이 없습니다. Wheels/WheelSession_1, _2 를 먼저 만드세요.");

        GameObject last = null;
        for (int i = 0; i < sessions.Length; i++)
        {
            WheelSession session = sessions[i];
            string name = CanvasInstancePrefix + session.WheelId;
            GameObject canvas = FindRoot(name);

            // 프리팹이 아닌 캔버스는 프리팹으로 교체한다
            if (canvas != null && PrefabUtility.GetCorrespondingObjectFromSource(canvas) != canvasPrefab)
            {
                Undo.DestroyObjectImmediate(canvas);
                canvas = null;
            }

            if (canvas == null)
            {
                canvas = (GameObject)PrefabUtility.InstantiatePrefab(canvasPrefab, SceneManager.GetActiveScene());
                canvas.name = name;
                canvas.GetComponent<Canvas>().targetDisplay = i;
                Undo.RegisterCreatedObjectUndo(canvas, "Create Wheel Canvas");
            }

            Camera cam = FindRoot($"Camera_Wheel_{session.WheelId}")?.GetComponent<Camera>();
            if (cam == null) Debug.LogWarning($"Camera_Wheel_{session.WheelId} 이 없습니다. '5. 3D 경주 장면 만들기'를 먼저 실행하세요.");
            Wire(canvas.GetComponent<WheelScreen>(), ("session", session), ("raceCamera", cam));
            last = canvas;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (last != null) Selection.activeGameObject = last;
    }

    private static void ApplySpectatorToScene(GameObject spectatorPrefab)
    {
        GameObject canvas = FindRoot(SpectatorInstanceName);
        if (canvas != null && PrefabUtility.GetCorrespondingObjectFromSource(canvas) != spectatorPrefab)
        {
            Undo.DestroyObjectImmediate(canvas);
            canvas = null;
        }
        if (canvas == null)
        {
            canvas = (GameObject)PrefabUtility.InstantiatePrefab(spectatorPrefab, SceneManager.GetActiveScene());
            canvas.name = SpectatorInstanceName;
            Undo.RegisterCreatedObjectUndo(canvas, "Create Spectator Canvas");
        }

        WheelSession[] sessions = SortedSessions();
        var view = canvas.GetComponent<SpectatorScreen>();
        WireList(view, "sessions", Array.ConvertAll(sessions, s => (UnityEngine.Object)s));
        foreach (var cam in UnityEngine.Object.FindObjectsOfType<RaceCamera>(true))
            if (cam.Screen == RaceCamera.Target.Spectator) Wire(view, ("raceCamera", cam.GetComponent<Camera>()));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    // 소리 매니저(BGM·효과음)와 게임 진행 → 소리 연결
    private static void EnsureSoundManager()
    {
        GameObject go = FindRoot("SoundManager");
        if (go == null)
        {
            go = new GameObject("SoundManager");
            Undo.RegisterCreatedObjectUndo(go, "Create SoundManager");
        }
        if (!go.TryGetComponent(out SoundManager _)) go.AddComponent<SoundManager>();
        PowerSoundDirector director = go.TryGetComponent(out PowerSoundDirector existing) ? existing : go.AddComponent<PowerSoundDirector>();
        WireList(director, "sessions", Array.ConvertAll(SortedSessions(), s => (UnityEngine.Object)s));
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    public static WheelSession[] SortedSessions()
    {
        var sessions = UnityEngine.Object.FindObjectsOfType<WheelSession>(true);
        Array.Sort(sessions, (a, b) => a.WheelId.CompareTo(b.WheelId));
        return sessions;
    }

    public static GameObject FindRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

    #endregion
}
