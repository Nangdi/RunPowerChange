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
//   └ Frame (2160x3840) └ Panel_Spectator.prefab (상태·발전량 글자, 오늘 최고 랭킹 표시, 아래 칸 3개, 안내 카드·카운트다운 Notice)
//
// 이미 있는 프리팹은 덮어쓰지 않는다. 직접 수정한 프리팹을 지키기 위해서다.
// (뒤에 추가된 요소는 예외로, 예전 프리팹에 없으면 그 부분만 덧붙인다:
//  85형 안내 카드 EnsureSpectatorNotice, 85형 랭킹 표 PLAYER 줄 순위 숫자 EnsureSpectatorPlayerRows,
//  32형 발전 화면의 지금 순위·1위 기록 선 표시 EnsureRunExtras)
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
            EnsureRunExtras();
            GameObject canvasPrefab = GetOrCreate(CanvasPrefabPath, () => CreateCanvas(panels));

            screen = SpectatorResolution;
            GameObject spectatorPanel = GetOrCreate(PanelPrefabPath(SpectatorName), CreateSpectatorPanel);
            GameObject spectatorPrefab = GetOrCreate(SpectatorPrefabPath, () => CreateSpectatorCanvas(spectatorPanel));
            EnsureSpectatorNotice();
            EnsureSpectatorPlayerRows();
            EnsureRecordTagViews();
            EnsureAheadMarkers();
            EnsureBehindMarkers();
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

        // 체험시작 버튼 그림을 눌러야만 시작한다 (화면의 다른 곳은 반응하지 않음)
        Button start = PartButton(panel, "StartButton", "Button_Start");
        start.transition = Selectable.Transition.None;
        Image badge = (Image)start.targetGraphic;

        // 옆 쳇바퀴 체험 진행 중 안내 (버튼 바로 위)
        Rect button = Psd("Button_Start");
        RectTransform busy = StretchFull(CreateRect("Group_Busy", panel));
        Image busyBox = RoundBox(busy, "Box", new Vector2(960f, button.yMin - 50f), new Vector2(1180f, 72f));
        TMP_Text busyText = AddText(StretchFull(CreateRect("Text", busyBox.rectTransform)),
            $"지금 <color={YellowHex}>다른 체험</color>이 진행 중이에요. 약 90초 후 체험할 수 있어요.", 34, WhiteColor, Weight.SemiBold);
        busyText.enableWordWrapping = false;
        busy.gameObject.SetActive(false);

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
        var (rankGroup, rankText) = CreateRunRank(panel);
        RectTransform recordTag = CreateRunRecordTag(panel);

        Image shine = FilledImage(panel, "BarShine", "Run_BarShine");
        GaugeView bar = CreateGauge(panel, "EnergyBar", "Run_BarFill", null, shine);
        shine.transform.SetAsLastSibling();
        // 숫자는 오른쪽 끝(wh 표시 바로 앞)에 붙는다
        TMP_Text energy = PsdText(panel, "EnergyText", "Run_BarText", "0.0", 45, WhiteColor, Weight.ExtraBold,
            HorizontalAlignmentOptions.Right, 360f, true);

        HandWarningView warning = CreateHandWarning(panel);

        var view = panel.gameObject.AddComponent<RunPanel>();
        Wire(view, ("handWarning", warning));
        Wire(view, ("rankGroup", rankGroup), ("rankText", rankText), ("recordTag", recordTag), ("recordTagView", recordTag.GetComponent<RecordTagView>()));
        WireColor(view, ("rankColor", YellowColor));
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

    // 지금 순위 ("지금 기록이면 N명 중 M등"): 상대와 거리 차 상자 바로 아래
    private static (GameObject group, TMP_Text text) CreateRunRank(RectTransform panel)
    {
        Image box = RoundBox(panel, "Group_Rank", new Vector2(960f, 146f), new Vector2(620f, 60f));
        TMP_Text text = AddText(StretchFull(CreateRect("Text", box.rectTransform)), $"지금 기록이면 12명 중 <color={YellowHex}>3등</color>이에요!", 32,
            WhiteColor, Weight.SemiBold);
        text.enableWordWrapping = false;
        box.gameObject.SetActive(false);
        return (box.gameObject, text);
    }

    // 1위 기록 선 위 "오늘 최고 랭킹" 표시 (85형과 같은 그림을 32형 크기로 줄임). 아래 끝(기둥 끝)이 피벗.
    // 3D 장면 바로 위·다른 표시 아래에 오도록 StaticLayer 바로 다음에 둔다.
    private static RectTransform CreateRunRecordTag(RectTransform panel)
    {
        Image tag = AddSprite(Place(CreateRect("RecordTag", panel), Vector2.zero, new Vector2(216f, 132f)), Part("Spectator_RecordTag"));
        tag.preserveAspect = true;
        tag.raycastTarget = false;
        tag.rectTransform.pivot = new Vector2(0.5f, 0f);
        tag.transform.SetSiblingIndex(1);
        tag.gameObject.SetActive(false);
        AddRecordTagView(tag);
        return tag.rectTransform;
    }

    // 1위 기록 선 표시에 오늘/전날 그림 바꾸기를 붙인다 (전날 그림: Tools/make_record_tag_prev.py)
    private static RecordTagView AddRecordTagView(Image tag)
    {
        var view = tag.GetComponent<RecordTagView>();
        if (view == null) view = tag.gameObject.AddComponent<RecordTagView>();   // 에디터의 가짜 null 때문에 ?? 를 쓰지 않는다
        Wire(view, ("image", tag), ("todaySprite", Part("Spectator_RecordTag")), ("previousDaySprite", Part("Spectator_RecordTag_Prev")));
        return view;
    }

    // 예전에 만든 프리팹의 1위 기록 선 표시에 RecordTagView 가 없으면 붙이고 연결한다 (32형 Panel_Run, 85형 Panel_Spectator·Canvas_Spectator)
    private static void EnsureRecordTagViews()
    {
        string runPath = PanelPrefabPath("Run");
        GameObject run = PrefabUtility.LoadPrefabContents(runPath);
        try
        {
            var view = run.GetComponent<RunPanel>();
            var tag = view != null ? new SerializedObject(view).FindProperty("recordTag").objectReferenceValue as RectTransform : null;
            if (tag != null && new SerializedObject(view).FindProperty("recordTagView").objectReferenceValue == null)
            {
                Wire(view, ("recordTagView", AddRecordTagView(tag.GetComponent<Image>())));
                PrefabUtility.SaveAsPrefabAsset(run, runPath);
                Debug.Log($"발전 화면 1위 기록 선 표시 오늘/전날 그림 연결: {runPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(run);
        }

        string specPath = PanelPrefabPath(SpectatorName);
        GameObject spec = PrefabUtility.LoadPrefabContents(specPath);
        try
        {
            Transform tag = spec.transform.Find("RecordTag");
            if (tag != null && tag.GetComponent<RecordTagView>() == null)
            {
                AddRecordTagView(tag.GetComponent<Image>());
                PrefabUtility.SaveAsPrefabAsset(spec, specPath);
                Debug.Log($"85형 1위 기록 선 표시 오늘/전날 그림 추가: {specPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(spec);
        }

        GameObject canvasRoot = PrefabUtility.LoadPrefabContents(SpectatorPrefabPath);
        try
        {
            var screenView = canvasRoot.GetComponent<SpectatorScreen>();
            var tagView = canvasRoot.GetComponentInChildren<RecordTagView>(true);
            if (screenView != null && tagView != null && new SerializedObject(screenView).FindProperty("recordTagView").objectReferenceValue != tagView)
            {
                Wire(screenView, ("recordTagView", tagView));
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, SpectatorPrefabPath);
                Debug.Log($"85형 1위 기록 선 표시 연결: {SpectatorPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvasRoot);
        }
    }

    // 이미 만든 Panel_Run 프리팹에 지금 순위·1위 기록 선 표시가 없으면 추가하고 RunPanel 에 연결한다 (다른 부분은 건드리지 않음)
    private static void EnsureRunExtras()
    {
        string path = PanelPrefabPath("Run");
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var view = root.GetComponent<RunPanel>();
            if (view == null) return;
            var panel = (RectTransform)root.transform;
            var so = new SerializedObject(view);
            bool changed = false;
            if (so.FindProperty("rankGroup").objectReferenceValue == null)
            {
                var (group, text) = CreateRunRank(panel);
                Transform compare = panel.Find("Group_Compare");
                if (compare != null) group.transform.SetSiblingIndex(compare.GetSiblingIndex() + 1);
                Wire(view, ("rankGroup", group), ("rankText", text));
                WireColor(view, ("rankColor", YellowColor));
                changed = true;
            }
            if (so.FindProperty("recordTag").objectReferenceValue == null)
            {
                Wire(view, ("recordTag", CreateRunRecordTag(panel)));
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"발전 화면 지금 순위·1위 기록 선 표시 추가: {path}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 손잡이를 놓았을 때(FREE) 경고: 빨간 테두리(맥박) + PSD 경고문구(어두운 바탕, 손잡이 그림, 문구, 깜빡이는 아이콘).
    // 발전 화면의 다른 요소 위에 덮이도록 맨 마지막 자식으로 둔다.
    private static HandWarningView CreateHandWarning(RectTransform panel)
    {
        CanvasGroup group = Group(panel, "Group_HandWarning");
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;
        var root = (RectTransform)group.transform;

        Image edge = PartImage(root, "RedEdge", "Warn_RedEdge");
        edge.preserveAspect = false;
        edge.color = WarnRedColor;
        PartImage(root, "Dim", "Warn_Dim");

        // 손잡이 그림·문구는 같이 살짝 커졌다 작아진다 (화면 가운데 기준)
        RectTransform pulse = StretchFull(CreateRect("Pulse", root));
        PartImage(pulse, "Hands", "Warn_Hands");
        PartImage(pulse, "Text", "Warn_Text");
        Image icon = PartImage(pulse, "Icon", "Warn_Icon");

        foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;

        var view = root.gameObject.AddComponent<HandWarningView>();
        Wire(view, ("redEdge", edge), ("icon", icon), ("pulseTarget", pulse));
        root.SetAsLastSibling();
        return view;
    }

    // 이미 만든 Panel_Run 프리팹에 손잡이 경고가 없으면 추가하고 RunPanel 에 연결한다 (다른 부분은 건드리지 않음).
    [MenuItem("Tools/달려라 전력 챌린지/손잡이 경고 추가 (Panel_Run)")]
    public static void EnsureHandWarning()
    {
        string path = PanelPrefabPath("Run");
        ReloadLayout();
        screen = ReferenceResolution;
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var run = root.GetComponent<RunPanel>();
            var existing = root.GetComponentInChildren<HandWarningView>(true);
            if (existing == null) existing = CreateHandWarning((RectTransform)root.transform);
            Wire(run, ("handWarning", existing));
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Debug.Log($"손잡이 경고 연결: {path}");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
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
        AddRecordTagView(tag);
        CreateSpectatorTitle(panel);
        CreateAheadMarkers(panel);
        CreateBehindMarkers(panel);

        // 아래 칸 3개 (같은 자리에 겹쳐 두고 번갈아 보여 준다)
        CanvasGroup ranking = Group(panel, "Bottom_1_Ranking");
        PartImage((RectTransform)ranking.transform, "Board", "Spectator_Ranking");
        AddPlayerRowRanks(CreateRankTable((RectTransform)ranking.transform, "Table_Energy", "Spec_E", "Spec_E", 116.5f, 32f, 54.5f, false), SpecEnergyRankX);
        AddPlayerRowRanks(CreateRankTable((RectTransform)ranking.transform, "Table_Sale", "Spec_S", "Spec_E", 116.5f, 32f, 54.5f, false), SpecSaleRankX);

        CanvasGroup info1 = Group(panel, "Bottom_2_Info");
        PartImage((RectTransform)info1.transform, "Board", "Spectator_Info_1");
        info1.alpha = 0f;
        CanvasGroup info2 = Group(panel, "Bottom_3_Info");
        PartImage((RectTransform)info2.transform, "Board", "Spectator_Info_2");
        info2.alpha = 0f;

        CreateSpectatorNotice(panel);

        // SpectatorScreen(캔버스 루트)이 위 이름으로 연결한다
        return panel.gameObject;
    }

    // 85형: 바퀴·1위 기록 선이 실제 거리만큼 앞서 화면 위로 완전히 나가면 띄우는 표시. 다른 게임처럼 화면 맨 위에
    // 레인 색 화살표(▲)와 "PLAYER 01 63m 앞서는 중" 상자를 두고, x 만 대상 자리를 따라간다 (SpectatorScreen).
    // 레인 표시는 첫째 줄, 1위 기록 선 표시는 둘째 줄 (둘이 함께 떠도 겹치지 않게).
    private static readonly string[] AheadMarkerNames = { "Ahead_1", "Ahead_2", "Ahead_Record" };
    private static readonly float[] AheadMarkerPsdY = { 125f, 290f, 290f };   // 묶음(화살표 + 상자) 가운데 높이
    private static readonly float[] AheadMarkerWidth = { 640f, 640f, 720f };
    private static Color AheadLineColor(int i) => i == 0 ? Player1Color : i == 1 ? Player2Color : RecordLineColor;

    private static void CreateAheadMarkers(RectTransform panel)
    {
        // 2인 경주에서 레인 표시는 앞선 한 쪽만 뜨므로 쳇바퀴 1·2 는 같은 줄, 1위 기록 선은 둘째 줄
        float[] rowY = { AheadMarkerPsdY[0], AheadMarkerPsdY[0], AheadMarkerPsdY[2] };
        Sprite arrow = Part("Spectator_AheadArrow");   // 흰 삼각형 (Tools/make_ahead_arrow.py), 레인 색으로 칠함
        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        for (int i = 0; i < AheadMarkerNames.Length; i++)
        {
            const float boxH = 90f, arrowH = 56f, gap = 10f;
            RectTransform root = Place(CreateRect(AheadMarkerNames[i], panel), PsdPoint(new Vector2(1080f, rowY[i]), screen),
                new Vector2(AheadMarkerWidth[i], boxH + gap + arrowH));

            // 위쪽 화살표
            AddSprite(Place(CreateRect("Arrow", root), new Vector2(0f, (boxH + gap) * 0.5f), new Vector2(84f, arrowH)), arrow, AheadLineColor(i));

            Image box = AddSprite(Place(CreateRect("Box", root), new Vector2(0f, -(arrowH + gap) * 0.5f), new Vector2(AheadMarkerWidth[i], boxH)),
                rounded, new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f));
            box.type = Image.Type.Sliced;
            box.pixelsPerUnitMultiplier = 0.1f;
            TMP_Text text = AddText(StretchFull(CreateRect("Text", box.rectTransform)),
                i == 2 ? $"오늘 최고 랭킹 <color={YellowHex}>63m</color> 앞" : $"PLAYER 0{i + 1} <color={YellowHex}>63m</color> 앞서는 중", 48, WhiteColor, Weight.SemiBold);
            text.enableWordWrapping = false;
            foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            root.gameObject.SetActive(false);
        }
        Transform notice = panel.Find("Notice");
        if (notice != null) notice.SetAsLastSibling();   // 안내 카드는 맨 위
    }

    // 85형이 앞선 사람을 따라갈 때 화면 아래로 빠진 뒤처진 바퀴 표시. 아래 칸(PSD y 약 3070) 바로 위에
    // "PLAYER 02 60m 추격 중" 상자와 아래쪽 화살표(▼)를 두고, x 는 그 레인 자리 (SpectatorScreen.UpdateBehindMarkers).
    private static readonly string[] BehindMarkerNames = { "Behind_1", "Behind_2" };
    private const float BehindMarkerPsdY = 2970f;   // 묶음(상자 + 화살표) 가운데 높이

    private static void CreateBehindMarkers(RectTransform panel)
    {
        Sprite arrow = Part("Spectator_AheadArrow");
        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        for (int i = 0; i < BehindMarkerNames.Length; i++)
        {
            const float boxH = 90f, arrowH = 56f, gap = 10f, width = 640f;
            RectTransform root = Place(CreateRect(BehindMarkerNames[i], panel), PsdPoint(new Vector2(1080f, BehindMarkerPsdY), screen),
                new Vector2(width, boxH + gap + arrowH));

            Image box = AddSprite(Place(CreateRect("Box", root), new Vector2(0f, (arrowH + gap) * 0.5f), new Vector2(width, boxH)),
                rounded, new Color(TextColor.r, TextColor.g, TextColor.b, 0.85f));
            box.type = Image.Type.Sliced;
            box.pixelsPerUnitMultiplier = 0.1f;
            TMP_Text text = AddText(StretchFull(CreateRect("Text", box.rectTransform)),
                $"PLAYER 0{i + 1} <color={YellowHex}>60m</color> 추격 중", 48, WhiteColor, Weight.SemiBold);
            text.enableWordWrapping = false;

            // 아래쪽 화살표 (위쪽 화살표 그림을 뒤집음)
            RectTransform arrowRect = Place(CreateRect("Arrow", root), new Vector2(0f, -(boxH + gap) * 0.5f), new Vector2(84f, arrowH));
            AddSprite(arrowRect, arrow, AheadLineColor(i));
            arrowRect.localEulerAngles = new Vector3(0f, 0f, 180f);

            foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            root.gameObject.SetActive(false);
        }
        Transform notice = panel.Find("Notice");
        if (notice != null) notice.SetAsLastSibling();   // 안내 카드는 맨 위
    }

    private static void WireBehindMarkers(Transform panel, SpectatorScreen view)
    {
        int n = BehindMarkerNames.Length;
        var markers = new UnityEngine.Object[n];
        var texts = new UnityEngine.Object[n];
        for (int i = 0; i < n; i++)
        {
            Transform m = panel.Find(BehindMarkerNames[i]);
            markers[i] = m;
            texts[i] = m != null ? m.Find("Box/Text").GetComponent<TMP_Text>() : null;
        }
        WireList(view, "behindMarkers", markers);
        WireList(view, "behindTexts", texts);
    }

    // 예전에 만든 85형 프리팹에 추격 중 표시가 없으면 만들고 SpectatorScreen 에 연결한다
    private static void EnsureBehindMarkers()
    {
        string panelPath = PanelPrefabPath(SpectatorName);
        GameObject panelRoot = PrefabUtility.LoadPrefabContents(panelPath);
        Vector2 previousScreen = screen;
        screen = SpectatorResolution;   // 85형 좌표 (따로 불려도 32형 기준으로 놓이지 않게)
        try
        {
            if (panelRoot.transform.Find(BehindMarkerNames[0]) == null)
            {
                CreateBehindMarkers((RectTransform)panelRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(panelRoot, panelPath);
                Debug.Log($"85형 추격 중 표시 추가: {panelPath}");
            }
        }
        finally
        {
            screen = previousScreen;
            PrefabUtility.UnloadPrefabContents(panelRoot);
        }

        GameObject canvasRoot = PrefabUtility.LoadPrefabContents(SpectatorPrefabPath);
        try
        {
            var view = canvasRoot.GetComponent<SpectatorScreen>();
            Transform panel = canvasRoot.transform.Find("Frame").GetChild(0);
            var markers = view != null ? new SerializedObject(view).FindProperty("behindMarkers") : null;
            if (markers != null && (markers.arraySize == 0 || markers.GetArrayElementAtIndex(0).objectReferenceValue == null))
            {
                WireBehindMarkers(panel, view);
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, SpectatorPrefabPath);
                Debug.Log($"85형 추격 중 표시 연결: {SpectatorPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvasRoot);
        }
    }

    private static void WireAheadMarkers(Transform panel, SpectatorScreen view)
    {
        int n = AheadMarkerNames.Length;
        var markers = new UnityEngine.Object[n];
        var texts = new UnityEngine.Object[n];
        for (int i = 0; i < n; i++)
        {
            Transform m = panel.Find(AheadMarkerNames[i]);
            markers[i] = m;
            texts[i] = m != null ? m.Find("Box/Text").GetComponent<TMP_Text>() : null;
        }
        WireList(view, "aheadMarkers", markers);
        WireList(view, "aheadTexts", texts);
        WireColor(view, ("aheadColor", YellowColor));
    }

    // 85형 제목(달려라! 전력 챌린지). 정적 그림에서 빼서 따로 두고 달리는 동안 숨긴다 (SpectatorScreen.titleGroup)
    private static CanvasGroup CreateSpectatorTitle(RectTransform panel)
    {
        CanvasGroup group = Group(panel, "Group_Title");
        PartImage((RectTransform)group.transform, "Title", "Spectator_Title");
        group.interactable = false;
        group.blocksRaycasts = false;
        group.transform.SetSiblingIndex(1);   // 정적 그림 바로 위
        return group;
    }

    // 예전에 만든 85형 프리팹에 화면 밖 표시(화살표)·제목 묶음이 없으면 다시 만들고 SpectatorScreen 에 연결한다.
    // (앞 버전 표시: 움직이는 상자, 세로선·점 달린 상자는 지우고 새로 만든다)
    private static void EnsureAheadMarkers()
    {
        string panelPath = PanelPrefabPath(SpectatorName);
        GameObject panelRoot = PrefabUtility.LoadPrefabContents(panelPath);
        try
        {
            bool changed = false;
            Image oldArrow = panelRoot.transform.Find($"{AheadMarkerNames[0]}/Arrow")?.GetComponent<Image>();
            if (oldArrow == null || oldArrow.sprite != Part("Spectator_AheadArrow"))
            {
                foreach (string name in AheadMarkerNames)
                    foreach (string suffix in new[] { "", "_Line", "_Dot" })
                    {
                        Transform old = panelRoot.transform.Find(name + suffix);
                        if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
                    }
                CreateAheadMarkers((RectTransform)panelRoot.transform);
                changed = true;
            }
            if (panelRoot.transform.Find("Group_Title") == null)
            {
                CreateSpectatorTitle((RectTransform)panelRoot.transform);
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(panelRoot, panelPath);
                Debug.Log($"85형 화면 밖 표시·제목 묶음 추가: {panelPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(panelRoot);
        }

        GameObject canvasRoot = PrefabUtility.LoadPrefabContents(SpectatorPrefabPath);
        try
        {
            var view = canvasRoot.GetComponent<SpectatorScreen>();
            Transform panel = canvasRoot.transform.Find("Frame").GetChild(0);
            var so = view != null ? new SerializedObject(view) : null;
            var markers = so?.FindProperty("aheadMarkers");
            bool rewire = so != null && (markers.arraySize == 0 || markers.GetArrayElementAtIndex(0).objectReferenceValue == null
                || so.FindProperty("titleGroup").objectReferenceValue == null);
            if (rewire)
            {
                WireAheadMarkers(panel, view);
                Wire(view, ("titleGroup", panel.Find("Group_Title").GetComponent<CanvasGroup>()));
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, SpectatorPrefabPath);
                Debug.Log($"85형 화면 밖 표시·제목 묶음 연결: {SpectatorPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvasRoot);
        }
    }

    // 85형 랭킹 판 그림(Spectator_Ranking) 안 줄 순위 숫자 자리 (판 그림 기준 px). 4·5줄 숫자는 psd_export.py 가 지웠다.
    private const float SpecEnergyRankX = 82f;
    private const float SpecSaleRankX = 1113f;
    private static readonly float[] SpecPlayerRowY = { 508f, 625f };

    // 85형 랭킹 표 아래 두 줄(4·5위)의 순위 숫자 글자와 PLAYER 줄 강조. 순위가 그때마다 달라서 그림 대신 글자로 쓴다.
    // 평소에는 4·5위 기록 줄이고, 종합 순위 발표 중에만 PLAYER 줄이 된다: 레인 색 바탕 띠(옅게) + 순위 숫자 뒤 동그라미
    // (1위 줄 동그라미처럼) + 굵고 큰 이름 글자. 띠·동그라미는 꺼 두고 실행 중 RankTableView.SetRow 가 켜고 칠한다.
    private const float SpecRowBandWidth = 900f;
    private const float SpecRowBandHeight = 104f;
    private const float SpecRowBandOffsetX = 412f;   // 순위 숫자 자리 → 줄(가로선 37~951) 가운데
    private const float SpecRowBadgeSize = 66f;
    private const float SpecPlayerLabelSize = 42f;   // 기록 시각 글자는 32

    private static void AddPlayerRowRanks(RankTableView view, float boardX)
    {
        Rect board = Psd("Spectator_Ranking");
        var root = (RectTransform)view.transform;
        Sprite rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite circle = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var texts = new UnityEngine.Object[5];
        var bands = new UnityEngine.Object[5];
        var badges = new UnityEngine.Object[5];
        var so = new SerializedObject(view);
        SerializedProperty labels = so.FindProperty("timeTexts");
        for (int k = 0; k < SpecPlayerRowY.Length; k++)
        {
            int row = texts.Length - SpecPlayerRowY.Length + k;
            Color accent = k == 0 ? Player1Color : Player2Color;
            float y = board.yMin + SpecPlayerRowY[k];

            Image band = AddSprite(Place(CreateRect($"Row{row + 1}_Band", root), PsdPoint(new Vector2(board.xMin + boardX + SpecRowBandOffsetX, y), screen),
                new Vector2(SpecRowBandWidth, SpecRowBandHeight)), rounded, new Color(accent.r, accent.g, accent.b, 0.22f));
            band.type = Image.Type.Sliced;
            band.pixelsPerUnitMultiplier = 0.35f;
            band.transform.SetSiblingIndex(k);   // 표 글자들보다 뒤
            band.enabled = false;
            bands[row] = band;

            Image badge = AddSprite(Place(CreateRect($"Row{row + 1}_Badge", root), PsdPoint(new Vector2(board.xMin + boardX, y), screen),
                new Vector2(SpecRowBadgeSize, SpecRowBadgeSize)), circle, accent);
            badge.enabled = false;
            badges[row] = badge;

            texts[row] = ValueText(root, $"Row{row + 1}_Rank", "12", new Vector2(board.xMin + boardX, y),
                new Vector2(SpecRowBadgeSize - 8f, 50f), 36f, TextColor, Weight.Bold, HorizontalAlignmentOptions.Center);

            // 이름 글자는 평소 기록 시각 모양 그대로 (예전 프리팹에서 굵게 바꿔 둔 것 되돌림). PLAYER 줄 모양은 RankTableView 가 바꾼다.
            var label = labels.arraySize > row ? labels.GetArrayElementAtIndex(row).objectReferenceValue as TMP_Text : null;
            var first = labels.arraySize > 0 ? labels.GetArrayElementAtIndex(0).objectReferenceValue as TMP_Text : null;
            if (label != null && first != null)
            {
                label.font = first.font;
                label.fontStyle = first.fontStyle;
                label.fontSizeMax = first.fontSizeMax;
                label.fontSize = first.fontSize;
                label.color = first.color;
                label.rectTransform.sizeDelta = new Vector2(label.rectTransform.sizeDelta.x, SpecPlayerLabelSize * 1.4f);
                EditorUtility.SetDirty(label);
            }
        }
        foreach (var o in bands) if (o is Graphic g) g.raycastTarget = false;
        foreach (var o in badges) if (o is Graphic g) g.raycastTarget = false;
        WireList(view, "rankTexts", texts);
        WireList(view, "rowBands", bands);
        WireList(view, "rankBadges", badges);
        Wire(view, ("playerLabelFont", Font(Weight.ExtraBold)));
        var viewSo = new SerializedObject(view);
        viewSo.FindProperty("playerLabelSize").floatValue = SpecPlayerLabelSize;
        viewSo.FindProperty("badgeTextColor").colorValue = WhiteColor;
        viewSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // 예전에 만든 85형 프리팹의 랭킹 표에 PLAYER 줄 순위 숫자·강조가 없으면 (예전 순위 숫자는 지우고) 다시 만든다
    private static void EnsureSpectatorPlayerRows(bool rebuild = false)
    {
        string path = PanelPrefabPath(SpectatorName);
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Vector2 previousScreen = screen;
        screen = SpectatorResolution;   // 85형 좌표 (따로 불려도 32형 기준으로 놓이지 않게)
        try
        {
            bool changed = false;
            foreach (var (table, x) in new[] { ("Bottom_1_Ranking/Table_Energy", SpecEnergyRankX), ("Bottom_1_Ranking/Table_Sale", SpecSaleRankX) })
            {
                var view = root.transform.Find(table)?.GetComponent<RankTableView>();
                var viewSo = view != null ? new SerializedObject(view) : null;
                if (view == null || (!rebuild && viewSo.FindProperty("rowBands").arraySize > 0
                    && viewSo.FindProperty("playerLabelFont").objectReferenceValue != null)) continue;
                for (int c = view.transform.childCount - 1; c >= 0; c--)
                {
                    GameObject child = view.transform.GetChild(c).gameObject;
                    if (child.name.EndsWith("_Rank") || child.name.EndsWith("_Band") || child.name.EndsWith("_Badge"))
                        UnityEngine.Object.DestroyImmediate(child);
                }
                AddPlayerRowRanks(view, x);
                changed = true;
            }
            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"85형 랭킹 표 PLAYER 줄 순위 숫자·강조 추가: {path}");
            }
        }
        finally
        {
            screen = previousScreen;
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    // 85형 가운데 아래 안내 카드 + 가운데 카운트다운 (SpectatorNotice). 패널 맨 위(마지막 자식)에 둔다.
    // 디자이너 시안에 없는 화면이라 (지금은 없앤) 32형 체험 안내 카드·STEP 배지 모양의 임시 그림(Tools/make_spectator_notice_parts.py)으로 만든다.
    // 카드는 아래 칸(PSD y 3068~) 위, 3D 바퀴 아래의 트랙 자리(PSD y 약 2340~3020)에 둬서 바퀴를 가리지 않는다.
    private static SpectatorNotice CreateSpectatorNotice(RectTransform panel)
    {
        RectTransform root = StretchFull(CreateRect("Notice", panel));
        var view = root.gameObject.AddComponent<SpectatorNotice>();

        // 카운트다운: 32형과 같은 디자이너 숫자 그림을 3D 트랙 가운데에 크게
        Vector2 countPos = new Vector2(0f, 260f);
        const float countScale = 1.8f;
        var numbers = new List<UnityEngine.Object>();
        foreach (string n in new[] { "Count_1", "Count_2", "Count_3" })
        {
            Sprite sprite = Part(n);
            Image image = AddSprite(Place(CreateRect(n, root), countPos, SpriteSize(sprite) * countScale), sprite);
            image.preserveAspect = true;
            image.gameObject.SetActive(false);
            numbers.Add(image.rectTransform);
        }
        Sprite goSprite = Part("Count_Go");
        Image go = AddSprite(Place(CreateRect("Count_Go", root), countPos, SpriteSize(goSprite) * countScale), goSprite);
        go.preserveAspect = true;
        go.gameObject.SetActive(false);

        // 안내 카드
        RectTransform cardRt = Place(CreateRect("Group_Card", root), new Vector2(0f, -760f), new Vector2(1880f, 680f));
        CanvasGroup card = cardRt.gameObject.AddComponent<CanvasGroup>();
        card.interactable = false;
        card.blocksRaycasts = false;
        Image background = AddSprite(StretchFull(CreateRect("Background", cardRt)), Part("Spectator_NoticeCard"));
        background.type = Image.Type.Sliced;
        Image pill = AddSprite(Place(CreateRect("Pill", cardRt), new Vector2(0f, 222f), new Vector2(560f, 100f)), Part("Spectator_NoticePill"));
        pill.type = Image.Type.Sliced;
        TMP_Text tag = OneLine(CreateText(pill.rectTransform, "Label", "참가자 모집", Vector2.zero, new Vector2(500f, 90f), 54f, WhiteColor, Weight.SemiBold), 36f);
        TMP_Text title = OneLine(CreateText(cardRt, "Title", "함께 달릴 참가자를 모집 중입니다!", new Vector2(0f, 75f), new Vector2(1720f, 140f),
            104f, TextColor, Weight.ExtraBold), 60f);
        TextMeshProUGUI sub = CreateText(cardRt, "Sub", "비어 있는 쳇바퀴 화면에서 참여하기를 누르면\n같이 달릴 수 있어요", new Vector2(0f, -92f), new Vector2(1720f, 180f),
            60f, TextColor, Weight.Medium);
        sub.enableAutoSizing = true;
        sub.fontSizeMin = 40f;
        sub.fontSizeMax = 60f;
        TMP_Text count = OneLine(CreateText(cardRt, "Count", "12초", new Vector2(0f, -248f), new Vector2(600f, 100f), 84f, OrangeColor, Weight.ExtraBold), 50f);

        Wire(view, ("card", card), ("tagText", tag), ("titleText", title), ("subText", sub), ("countText", count), ("go", go.rectTransform));
        WireList(view, "numbers", numbers);
        var so = new SerializedObject(view);
        SerializedProperty colors = so.FindProperty("playerColors");
        colors.arraySize = 2;
        colors.GetArrayElementAtIndex(0).colorValue = Player1Color;
        colors.GetArrayElementAtIndex(1).colorValue = Player2Color;
        so.ApplyModifiedPropertiesWithoutUndo();
        return view;
    }

    private static Vector2 SpriteSize(Sprite sprite) => sprite != null ? sprite.rect.size : new Vector2(400f, 400f);

    // 한 줄 글자: 넘치면 자동 축소
    private static TMP_Text OneLine(TextMeshProUGUI text, float minSize)
    {
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Overflow;
        text.enableAutoSizing = true;
        text.fontSizeMin = minSize;
        text.fontSizeMax = text.fontSize;
        return text;
    }

    // 예전에 만든 85형 프리팹에 안내 카드가 없으면 덧붙이고 SpectatorScreen 에 연결한다 (프리팹을 새로 만들지 않아 다른 수정은 그대로 둔다)
    private static void EnsureSpectatorNotice()
    {
        string panelPath = PanelPrefabPath(SpectatorName);
        GameObject panelRoot = PrefabUtility.LoadPrefabContents(panelPath);
        try
        {
            if (panelRoot.GetComponentInChildren<SpectatorNotice>(true) == null)
            {
                CreateSpectatorNotice((RectTransform)panelRoot.transform);
                PrefabUtility.SaveAsPrefabAsset(panelRoot, panelPath);
                Debug.Log($"85형 안내 카드 추가: {panelPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(panelRoot);
        }

        GameObject canvasRoot = PrefabUtility.LoadPrefabContents(SpectatorPrefabPath);
        try
        {
            var view = canvasRoot.GetComponent<SpectatorScreen>();
            var notice = canvasRoot.GetComponentInChildren<SpectatorNotice>(true);
            if (view != null && notice != null && new SerializedObject(view).FindProperty("notice").objectReferenceValue != notice)
            {
                Wire(view, ("notice", notice));
                PrefabUtility.SaveAsPrefabAsset(canvasRoot, SpectatorPrefabPath);
                Debug.Log($"85형 안내 카드 연결: {SpectatorPrefabPath}");
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(canvasRoot);
        }
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
            ("recordTagView", Find<RecordTagView>("RecordTag")),
            ("energyTable", Find<RankTableView>("Bottom_1_Ranking/Table_Energy")),
            ("saleTable", Find<RankTableView>("Bottom_1_Ranking/Table_Sale")),
            ("notice", Find<SpectatorNotice>("Notice")));
        WireAheadMarkers(t, screenView);
        WireBehindMarkers(t, screenView);
        Wire(screenView, ("titleGroup", Find<CanvasGroup>("Group_Title")));
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
            ("countdownPanel", instances[2].GetComponent<CountdownPanel>()),
            ("runPanel", instances[3].GetComponent<RunPanel>()),
            ("voltagePanel", instances[4].GetComponent<VoltagePanel>()),
            ("salePanel", instances[5].GetComponent<SalePanel>()),
            ("rankingPanel", instances[6].GetComponent<RankingPanel>()));
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
