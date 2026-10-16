using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PowerUIKit;
using L = PowerStaticLayouts;

// 달려라! 전력 챌린지 쳇바퀴 화면(32형) 프리팹을 만들고 현재 씬에 쳇바퀴 수만큼 배치한다.
// 메뉴: Tools > 달려라 전력 챌린지 > 1. 프리팹 생성 및 씬 적용
//
// 구조
//  Canvas_Wheel.prefab (WheelScreen)          씬에 Canvas_Wheel_1, Canvas_Wheel_2 로 배치, 세션은 인스턴스에서 연결
//   └ Panel_xxx.prefab (WheelSession.State 순서 10개)
//      ├ StaticLayer  : 정적 PNG 한 장 (4.Sprite/Static/Static_xxx.png)
//      └ 동적 요소    : 버튼, 값 글자, 게이지(Gauge 부품), 랭킹 줄(RankRow 런타임 생성)
//
// 이미 있는 프리팹은 덮어쓰지 않는다. 직접 수정한 프리팹을 지키기 위해서다.
// 프리팹을 처음 상태로 되돌리고 싶으면 해당 .prefab 파일을 지운 뒤 다시 실행한다.
public static class PowerPrefabBuilder
{
    private const string CanvasInstancePrefix = "Canvas_Wheel_";

    private static GameObject rankRowPrefab;
    private static GameObject gaugePrefab;

    [MenuItem("Tools/달려라 전력 챌린지/1. 프리팹 생성 및 씬 적용", priority = 1)]
    public static void BuildAll()
    {
        PowerSpriteExporter.Export(overwrite: false);
        EnsureFolder(PartPrefabDir);
        EnsureFolder(PanelPrefabDir);

        // 부품
        rankRowPrefab = GetOrCreate(PartPrefabPath("RankRow"), CreateRankRow);
        gaugePrefab = GetOrCreate(PartPrefabPath("Gauge"), CreateGauge);

        // 패널
        var panelBuilders = new Dictionary<string, Func<GameObject>>
        {
            { "Idle", CreateIdlePanel },
            { "Guide", CreateGuidePanel },
            { "Countdown", CreateCountdownPanel },
            { "Run", CreateRunPanel },
            { "RunResult", CreateRunResultPanel },
            { "Voltage", CreateVoltagePanel },
            { "Transmit", CreateTransmitPanel },
            { "SaleResult", CreateSaleResultPanel },
            { "Summary", CreateSummaryPanel },
            { "End", CreateEndPanel },
        };
        var panels = new List<GameObject>();
        foreach (string panel in PanelNames)
            panels.Add(GetOrCreate(PanelPrefabPath(panel), panelBuilders[panel]));

        GameObject canvasPrefab = GetOrCreate(CanvasPrefabPath, () => CreateCanvas(panels));

        ApplyToScene(canvasPrefab);
        PowerSpriteExporter.ExportGuides();
        AssetDatabase.SaveAssets();
        Debug.Log("달려라 전력 챌린지 프리팹 적용 완료. 씬을 저장하세요.");
    }

    private static GameObject GetOrCreate(string path, Func<GameObject> build)
    {
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing != null) return existing;

        GameObject temp = build();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(temp, path);
        UnityEngine.Object.DestroyImmediate(temp);
        Debug.Log($"프리팹 생성: {path}");
        return prefab;
    }

    private static Sprite Part(string name) => LoadSprite(PartSpritePath(name));

    // 글자가 구워진 스프라이트 버튼 (스프라이트 크기 = 배치 크기)
    private static Button CreateSpriteButton(RectTransform parent, string name, string sprite, Vector2 pos, Vector2 size)
    {
        RectTransform rt = Place(CreateRect(name, parent), pos, size);
        Image image = AddSprite(rt, Part(sprite));
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.raycastTarget = true;
        var button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        return button;
    }

    // 값 글자 (굵게, 한 줄, 넘치면 자동 축소)
    private static TMP_Text CreateValue(RectTransform parent, string name, string sample, Vector2 pos, Vector2 size, float fontSize,
        Color? color = null, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
    {
        TextMeshProUGUI text = CreateText(parent, name, sample, pos, size, fontSize, color ?? TextColor, true, alignment);
        text.enableWordWrapping = false;
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * 0.5f;
        text.fontSizeMax = fontSize;
        return text;
    }

    // Gauge 부품 인스턴스 (중첩 프리팹 링크 유지)
    private static GaugeView CreateGauge(RectTransform parent, string name, Vector2 pos, Vector2 size, Color fillColor)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(gaugePrefab, parent);
        go.name = name;
        Place((RectTransform)go.transform, pos, size);
        go.transform.Find("Fill").GetComponent<Image>().color = fillColor;
        return go.GetComponent<GaugeView>();
    }

    private static RectTransform CreateRankList(RectTransform panel)
    {
        RectTransform list = Place(CreateRect("RankList", panel), L.RankBoxCenter + L.RankListOffset, L.RankListSize);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return list;
    }

    #region 부품 프리팹

    // 랭킹 한 줄: 순위 / 기록 시각 / 값. Highlight = 방금 체험한 기록
    private static GameObject CreateRankRow()
    {
        RectTransform root = CreateRect("RankRow", null);
        root.sizeDelta = new Vector2(820, 64);
        AddSprite(root, Part("RankRow_Bg"), RankRowColor);

        RectTransform highlight = StretchFull(CreateRect("Highlight", root));
        AddSprite(highlight, Part("RankRow_Bg"), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.35f));
        highlight.gameObject.SetActive(false);

        TMP_Text rankText = AddText(Stretch(CreateRect("Rank", root), new Vector2(0.03f, 0f), new Vector2(0.2f, 1f)),
            "1위", 32, TextColor, true, TextAlignmentOptions.MidlineLeft);
        TMP_Text nameText = AddText(Stretch(CreateRect("Name", root), new Vector2(0.2f, 0f), new Vector2(0.55f, 1f)),
            "14:05:32", 28, SubTextColor, false, TextAlignmentOptions.MidlineLeft);
        TMP_Text valueText = AddText(Stretch(CreateRect("Value", root), new Vector2(0.55f, 0f), new Vector2(0.97f, 1f)),
            "7.52 <size=60%>Wh</size>", 32, TextColor, true, TextAlignmentOptions.MidlineRight);
        foreach (var t in new[] { rankText, nameText, valueText }) t.enableWordWrapping = false;

        var view = root.gameObject.AddComponent<RankRowView>();
        Wire(view, ("rankText", rankText), ("nameText", nameText), ("valueText", valueText), ("highlight", highlight.gameObject));
        return root.gameObject;
    }

    // 가로 게이지: Track(바탕) + Fill(채움, 오른쪽 앵커로 길이 조절)
    private static GameObject CreateGauge()
    {
        RectTransform root = CreateRect("Gauge", null);
        root.sizeDelta = new Vector2(800, 24);
        AddSprite(StretchFull(CreateRect("Track", root)), Part("Gauge_Bg"), GaugeTrackColor);

        RectTransform fill = Stretch(CreateRect("Fill", root), Vector2.zero, new Vector2(0.6f, 1f));
        AddSprite(fill, Part("Gauge_Bg"), AccentColor);

        var view = root.gameObject.AddComponent<GaugeView>();
        Wire(view, ("fill", fill));
        return root.gameObject;
    }

    #endregion

    #region 패널 프리팹

    private static RectTransform CreatePanelRoot(string panel)
    {
        RectTransform root = StretchFull(CreateRect($"Panel_{panel}", null));
        Image staticLayer = AddSprite(StretchFull(CreateRect(StaticLayerName, root)), LoadSprite(StaticSpritePath(panel)));
        staticLayer.type = Image.Type.Simple;
        return root;
    }

    private static GameObject CreateIdlePanel()
    {
        RectTransform panel = CreatePanelRoot("Idle");

        // 시작 버튼 그림은 표시만 하고, 터치는 화면 전체를 덮는 투명 버튼이 받는다
        RectTransform badge = Place(CreateRect("StartBadge", panel), new Vector2(0, -230), new Vector2(520, 130));
        AddSprite(badge, Part("Button_Start")).preserveAspect = true;

        RectTransform touch = StretchFull(CreateRect("StartButton", panel));
        Image touchImage = AddImage(touch, new Color(0, 0, 0, 0));
        touchImage.raycastTarget = true;
        var start = touch.gameObject.AddComponent<Button>();
        start.targetGraphic = touchImage;
        start.transition = Selectable.Transition.None;

        var view = panel.gameObject.AddComponent<IdlePanel>();
        Wire(view, ("startButton", start));
        return panel.gameObject;
    }

    private static GameObject CreateGuidePanel()
    {
        RectTransform panel = CreatePanelRoot("Guide");
        Button confirm = CreateSpriteButton(panel, "ConfirmButton", "Button_Confirm", L.BottomButtonPos, new Vector2(480, 110));

        var view = panel.gameObject.AddComponent<GuidePanel>();
        Wire(view, ("confirmButton", confirm));
        return panel.gameObject;
    }

    private static GameObject CreateCountdownPanel()
    {
        RectTransform panel = CreatePanelRoot("Countdown");
        TMP_Text count = CreateValue(panel, "CountText", "3", new Vector2(0, -20), new Vector2(1000, 400), 300, AccentColor);

        var view = panel.gameObject.AddComponent<CountdownPanel>();
        Wire(view, ("countText", count));
        return panel.gameObject;
    }

    private static GameObject CreateRunPanel()
    {
        RectTransform panel = CreatePanelRoot("Run");

        Vector2 bigValue = new Vector2(780, 200);
        TMP_Text speed = CreateValue(panel, "SpeedText", "18.5", L.SpeedBoxCenter + new Vector2(0, L.BigValueY), bigValue, 170);
        TMP_Text watt = CreateValue(panel, "WattText", "150", L.PowerBoxCenter + new Vector2(0, L.BigValueY), bigValue, 170, AccentColor);
        GaugeView power = CreateGauge(panel, "PowerGauge", L.PowerBoxCenter + new Vector2(0, L.PowerGaugeY), new Vector2(760, 26), AccentColor);

        Vector2 smallValue = new Vector2(540, 100);
        string[] names = { "EnergyText", "DistanceText", "RemainText" };
        string[] samples = { "2.45", "312", "02:15" };
        var values = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
            values[i] = CreateValue(panel, names[i], samples[i], new Vector2(L.SmallBoxX[i], L.SmallBoxY + L.SmallValueY), smallValue, 80);

        GaugeView time = CreateGauge(panel, "TimeGauge", L.TimeGaugePos, L.TimeGaugeSize, AccentColor);

        var view = panel.gameObject.AddComponent<RunPanel>();
        Wire(view,
            ("speedText", speed),
            ("wattText", watt),
            ("energyText", values[0]),
            ("distanceText", values[1]),
            ("remainText", values[2]),
            ("timeGauge", time),
            ("powerGauge", power));
        return panel.gameObject;
    }

    // 결과 + 랭킹 공통 값 글자: 기록 3줄의 값, 백분위
    private static TMP_Text[] CreateRecordValues(RectTransform panel, string[] names, string[] samples)
    {
        var values = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            Vector2 pos = L.RecordBoxCenter + new Vector2(L.RecordValueX, L.RecordRowY[i]);
            values[i] = CreateValue(panel, names[i], samples[i], pos, new Vector2(400, 100), 64, i == 2 ? AccentColor : TextColor,
                TextAlignmentOptions.MidlineRight);
        }
        return values;
    }

    private static TMP_Text CreatePercent(RectTransform panel)
    {
        return CreateValue(panel, "PercentText", "상위 12%", L.RecordBoxCenter + new Vector2(L.RecordValueX, L.PercentY), new Vector2(400, 60), 40,
            AccentColor, TextAlignmentOptions.MidlineRight);
    }

    private static GameObject CreateRunResultPanel()
    {
        RectTransform panel = CreatePanelRoot("RunResult");
        TMP_Text[] values = CreateRecordValues(panel, new[] { "EnergyText", "DistanceText", "RankText" },
            new[] { "7.52", "1,024", "14위 <size=50%>/ 126명</size>" });
        TMP_Text percent = CreatePercent(panel);
        RectTransform list = CreateRankList(panel);
        Button next = CreateSpriteButton(panel, "NextButton", "Button_Transmit", L.BottomButtonPos, new Vector2(480, 110));

        var view = panel.gameObject.AddComponent<RunResultPanel>();
        Wire(view,
            ("energyText", values[0]),
            ("distanceText", values[1]),
            ("rankText", values[2]),
            ("percentText", percent),
            ("rowPrefab", rankRowPrefab.GetComponent<RankRowView>()),
            ("rowRoot", list),
            ("nextButton", next));
        return panel.gameObject;
    }

    private static GameObject CreateVoltagePanel()
    {
        RectTransform panel = CreatePanelRoot("Voltage");
        TMP_Text send = CreateValue(panel, "SendText", "150.00", L.SendBoxCenter + new Vector2(150, 0), new Vector2(560, 120), 90,
            AccentColor, TextAlignmentOptions.MidlineRight);
        TMP_Text scale = CreateText(panel, "ScaleText", "지금 속도로 60분 동안 달렸다면 만들 수 있는 전기", new Vector2(0, L.ScaleTextY),
            new Vector2(1400, 40), 26, SubTextColor);
        Button low = CreateSpriteButton(panel, "LowButton", "Button_Low", L.LowButtonPos, L.VoltageButtonSize);
        Button high = CreateSpriteButton(panel, "HighButton", "Button_High", L.HighButtonPos, L.VoltageButtonSize);

        var view = panel.gameObject.AddComponent<VoltagePanel>();
        Wire(view, ("sendText", send), ("scaleText", scale), ("lowButton", low), ("highButton", high));
        return panel.gameObject;
    }

    private static GameObject CreateTransmitPanel()
    {
        RectTransform panel = CreatePanelRoot("Transmit");
        TMP_Text voltage = CreateValue(panel, "VoltageText", "높은 전압", new Vector2(0, L.VoltageLabelY), new Vector2(800, 80), 52, AccentColor);
        GaugeView line = CreateGauge(panel, "LineGauge", L.LineGaugePos, L.LineGaugeSize, AccentColor);

        string[] names = { "SentText", "LossText", "ArrivedText" };
        string[] samples = { "150.00", "7.50", "142.50" };
        Color[] colors = { TextColor, LossColor, AccentColor };
        var values = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
            values[i] = CreateValue(panel, names[i], samples[i], new Vector2(L.FlowBoxX[i], L.FlowBoxY + L.SmallValueY), new Vector2(500, 100), 76, colors[i]);

        var view = panel.gameObject.AddComponent<TransmitPanel>();
        Wire(view,
            ("voltageText", voltage),
            ("sentText", values[0]),
            ("lossText", values[1]),
            ("arrivedText", values[2]),
            ("lineGauge", line));
        return panel.gameObject;
    }

    private static GameObject CreateSaleResultPanel()
    {
        RectTransform panel = CreatePanelRoot("SaleResult");
        TMP_Text[] values = CreateRecordValues(panel, new[] { "ArrivedText", "SaleText", "RankText" },
            new[] { "142.50", "18.4", "7위 <size=50%>/ 126명</size>" });
        TMP_Text percent = CreatePercent(panel);
        TMP_Text price = CreateText(panel, "PriceText", "기준단가 150원/kWh | 변전 설비 비용 3원 차감",
            L.RecordBoxCenter + new Vector2(0, L.PriceY), new Vector2(820, 40), 24, SubTextColor);
        RectTransform list = CreateRankList(panel);
        Button next = CreateSpriteButton(panel, "NextButton", "Button_Result", L.BottomButtonPos, new Vector2(480, 110));

        var view = panel.gameObject.AddComponent<SaleResultPanel>();
        Wire(view,
            ("arrivedText", values[0]),
            ("saleText", values[1]),
            ("priceText", price),
            ("rankText", values[2]),
            ("percentText", percent),
            ("rowPrefab", rankRowPrefab.GetComponent<RankRowView>()),
            ("rowRoot", list),
            ("nextButton", next));
        return panel.gameObject;
    }

    private static GameObject CreateSummaryPanel()
    {
        RectTransform panel = CreatePanelRoot("Summary");
        Vector2 valueSize = new Vector2(760, 150);
        Vector2 rankSize = new Vector2(760, 70);
        TMP_Text energy = CreateValue(panel, "EnergyText", "7.52", L.EnergySummaryCenter + new Vector2(0, L.SummaryValueY), valueSize, 120);
        TMP_Text energyRank = CreateValue(panel, "EnergyRankText", "14위 <size=50%>/ 126명</size>  상위 12%",
            L.EnergySummaryCenter + new Vector2(0, L.SummaryRankY), rankSize, 44, AccentColor);
        TMP_Text sale = CreateValue(panel, "SaleText", "18.4", L.SaleSummaryCenter + new Vector2(0, L.SummaryValueY), valueSize, 120);
        TMP_Text saleRank = CreateValue(panel, "SaleRankText", "7위 <size=50%>/ 126명</size>  상위 6%",
            L.SaleSummaryCenter + new Vector2(0, L.SummaryRankY), rankSize, 44, AccentColor);
        TMP_Text condition = CreateText(panel, "ConditionText", "높은 전압으로 송전 | 1,024m 달림", new Vector2(0, L.ConditionY),
            new Vector2(1600, 50), 32, SubTextColor);
        Button again = CreateSpriteButton(panel, "AgainButton", "Button_Again", L.AgainButtonPos, new Vector2(400, 110));
        Button home = CreateSpriteButton(panel, "HomeButton", "Button_Home", L.HomeButtonPos, new Vector2(400, 110));

        var view = panel.gameObject.AddComponent<SummaryPanel>();
        Wire(view,
            ("energyText", energy),
            ("energyRankText", energyRank),
            ("saleText", sale),
            ("saleRankText", saleRank),
            ("conditionText", condition),
            ("againButton", again),
            ("homeButton", home));
        return panel.gameObject;
    }

    // 종료 화면은 상호작용이 없어 정적 레이어 한 장뿐이다.
    private static GameObject CreateEndPanel()
    {
        RectTransform panel = CreatePanelRoot("End");
        panel.gameObject.AddComponent<EndPanel>();
        return panel.gameObject;
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
        scaler.matchWidthOrHeight = 0.5f;

        go.AddComponent<GraphicRaycaster>();

        // 편집하기 쉽도록 대기 화면만 켜 둔다. 실행 시에는 WheelScreen 이 상태에 맞게 켜고 끈다.
        var instances = new List<GameObject>();
        for (int i = 0; i < panels.Count; i++)
        {
            var panel = (GameObject)PrefabUtility.InstantiatePrefab(panels[i], go.transform);
            panel.SetActive(i == 0);
            instances.Add(panel);
        }

        var screen = go.AddComponent<WheelScreen>();
        Wire(screen,
            ("canvas", canvas),
            ("idlePanel", instances[0].GetComponent<IdlePanel>()),
            ("guidePanel", instances[1].GetComponent<GuidePanel>()),
            ("countdownPanel", instances[2].GetComponent<CountdownPanel>()),
            ("runPanel", instances[3].GetComponent<RunPanel>()),
            ("runResultPanel", instances[4].GetComponent<RunResultPanel>()),
            ("voltagePanel", instances[5].GetComponent<VoltagePanel>()),
            ("transmitPanel", instances[6].GetComponent<TransmitPanel>()),
            ("saleResultPanel", instances[7].GetComponent<SaleResultPanel>()),
            ("summaryPanel", instances[8].GetComponent<SummaryPanel>()),
            ("endPanel", instances[9].GetComponent<EndPanel>()));
        return go;
    }

    // 씬의 WheelSession 마다 Canvas_Wheel_<번호> 를 하나씩 두고 세션을 연결한다.
    // 에디터에서 두 화면을 Game 뷰 Display 1/2 로 나눠 볼 수 있도록 targetDisplay 도 미리 맞춘다 (실행 시 Setting.json 값으로 다시 맞춤).
    private static void ApplyToScene(GameObject canvasPrefab)
    {
        Undo.SetCurrentGroupName("Apply RunPowerChange UI");
        int undoGroup = Undo.GetCurrentGroup();

        var sessions = UnityEngine.Object.FindObjectsOfType<WheelSession>(true);
        if (sessions.Length == 0)
            Debug.LogWarning("씬에 WheelSession 이 없습니다. Wheels/WheelSession_1, _2 를 먼저 만드세요.");
        Array.Sort(sessions, (a, b) => a.WheelId.CompareTo(b.WheelId));

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

            Wire(canvas.GetComponent<WheelScreen>(), ("session", session));
            last = canvas;
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        if (last != null) Selection.activeGameObject = last;
    }

    private static GameObject FindRoot(string name)
    {
        foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            if (root.name == name)
                return root;
        return null;
    }

    #endregion
}
