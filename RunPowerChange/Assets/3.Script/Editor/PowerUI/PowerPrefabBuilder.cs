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

// 달려라! 전력 챌린지 화면 프리팹을 만들고 현재 씬에 배치한다.
// 메뉴: Tools > 달려라 전력 챌린지 > 1. 프리팹 생성 및 씬 적용
//
// 구조
//  Canvas_Wheel.prefab (WheelScreen)          32형 2대. 씬에 Canvas_Wheel_1, _2 로 배치, 세션은 인스턴스에서 연결
//   └ Frame (1920x1080) └ Panel_xxx.prefab (10개)
//      ├ StaticLayer  : 정적 PNG 한 장 (4.Sprite/Static/Static_xxx.png, 배경 투명 - 뒤에 3D 경주 장면)
//      └ 동적 요소    : 버튼, 값 글자, 게이지(Gauge 부품), 랭킹 줄(RankRow 런타임 생성)
//  Canvas_Spectator.prefab (SpectatorScreen)  85형 세로. 씬에 1개
//   └ Frame (1080x1920) └ Panel_Spectator.prefab (Player 칸 2개 = 3D 카메라 자리, 랭킹 표, 새 랭킹 팝업)
//
// 이미 있는 프리팹은 덮어쓰지 않는다. 직접 수정한 프리팹을 지키기 위해서다.
// 구조가 바뀌어 처음부터 다시 만들려면 BuildAll(rebuild: true) 를 쓴다 (같은 경로에 덮어써서 GUID 유지).
public static class PowerPrefabBuilder
{
    private const string CanvasInstancePrefix = "Canvas_Wheel_";
    private const string SpectatorInstanceName = "Canvas_Spectator";

    private static GameObject rankRowPrefab;
    private static GameObject gaugePrefab;
    private static bool rebuildAll;

    [MenuItem("Tools/달려라 전력 챌린지/1. 프리팹 생성 및 씬 적용", priority = 1)]
    public static void BuildAllMenu() => BuildAll(false);

    public static void BuildAll(bool rebuild)
    {
        rebuildAll = rebuild;
        try
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
                { "WaitNext", CreateWaitNextPanel },
                { "Guide", CreateGuidePanel },
                { "CharacterSelect", CreateCharacterSelectPanel },
                { "Countdown", CreateCountdownPanel },
                { "Run", CreateRunPanel },
                { "RunResult", CreateRunResultPanel },
                { "Voltage", CreateVoltagePanel },
                { "Transmit", CreateTransmitPanel },
                { "Result", CreateResultPanel },
            };
            var panels = new List<GameObject>();
            foreach (string panel in PanelNames)
                panels.Add(GetOrCreate(PanelPrefabPath(panel), panelBuilders[panel]));

            GameObject canvasPrefab = GetOrCreate(CanvasPrefabPath, () => CreateCanvas(panels));

            GameObject spectatorPanel = GetOrCreate(PanelPrefabPath(SpectatorName), CreateSpectatorPanel);
            GameObject spectatorPrefab = GetOrCreate(SpectatorPrefabPath, () => CreateSpectatorCanvas(spectatorPanel));

            ApplyToScene(canvasPrefab);
            ApplySpectatorToScene(spectatorPrefab);
            PowerSpriteExporter.ExportGuides();
            AssetDatabase.SaveAssets();
            Debug.Log("달려라 전력 챌린지 프리팹 적용 완료. 씬을 저장하세요.");
        }
        finally
        {
            rebuildAll = false;
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

    // 표시만 하는 글자 구운 스프라이트
    private static Image CreateSpriteImage(RectTransform parent, string name, string sprite, Vector2 pos, Vector2 size)
    {
        Image image = AddSprite(Place(CreateRect(name, parent), pos, size), Part(sprite));
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        return image;
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

    // 색 칠한 둥근 상자 (Card_Bg 9-slice)
    private static Image CreateBox(RectTransform parent, string name, Vector2 pos, Vector2 size, Color color)
    {
        return AddSprite(Place(CreateRect(name, parent), pos, size), Part("Card_Bg"), color);
    }

    // Gauge 부품 인스턴스 (중첩 프리팹 링크 유지). trackColor 가 null 이면 바탕을 숨긴다 (정적 레이어가 그림).
    private static GaugeView CreateGauge(RectTransform parent, string name, Vector2 pos, Vector2 size, Color fillColor, Color? trackColor)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(gaugePrefab, parent);
        go.name = name;
        Place((RectTransform)go.transform, pos, size);
        go.transform.Find("Fill").GetComponent<Image>().color = fillColor;
        Image track = go.transform.Find("Track").GetComponent<Image>();
        track.color = trackColor ?? Color.clear;
        return go.GetComponent<GaugeView>();
    }

    // 자동 넘김 화면 하단 남은 시간 바
    private static GaugeView CreateAutoGauge(RectTransform panel)
    {
        return CreateGauge(panel, "AutoGauge", L.AutoGaugePos, L.AutoGaugeSize, AccentColor, new Color(1f, 1f, 1f, 0.85f));
    }

    // 원형 남은 시간: 주황 원(Filled) 위에 흰 원, 그 위에 숫자
    private static (Image ring, TMP_Text text) CreateTimer(RectTransform panel)
    {
        RectTransform ringRt = Place(CreateRect("TimerRing", panel), L.TimerPos, Vector2.one * L.TimerSize);
        Image ring = AddSprite(ringRt, Part("Circle"), AccentColor);
        ring.type = Image.Type.Filled;
        ring.fillMethod = Image.FillMethod.Radial360;
        ring.fillOrigin = (int)Image.Origin360.Top;
        ring.fillClockwise = false;
        ring.fillAmount = 0.7f;
        AddSprite(Place(CreateRect("TimerInner", panel), L.TimerPos, Vector2.one * (L.TimerSize - 30)), Part("Circle"), Color.white);
        TMP_Text text = CreateValue(panel, "TimerText", "42", L.TimerPos, Vector2.one * (L.TimerSize - 40), 70, TextColor);
        return (ring, text);
    }

    private static RectTransform CreateRankList(RectTransform panel, Vector2 pos, Vector2 size, out GameObject gapMark)
    {
        RectTransform list = Place(CreateRect("RankList", panel), pos, size);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text gap = AddText(CreateRect("GapMark", list), "...", 28, SubTextColor, true);
        gap.gameObject.AddComponent<LayoutElement>().preferredHeight = 26;
        gapMark = gap.gameObject;
        gapMark.SetActive(false);
        return list;
    }

    private static void WireAutoGauge(WheelPanel view, GaugeView gauge) => Wire(view, ("autoGauge", gauge));

    #endregion

    #region 부품 프리팹

    // 랭킹 한 줄: 순위 / 최고속도 / 누적발전량 / 송전중손실 / 도착한전력 / 판매금액. Highlight = 방금 체험한 기록
    private static GameObject CreateRankRow()
    {
        RectTransform root = CreateRect("RankRow", null);
        root.sizeDelta = new Vector2(820, 54);
        root.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
        AddSprite(root, Part("RankRow_Bg"), new Color32(242, 243, 247, 255));

        RectTransform highlight = StretchFull(CreateRect("Highlight", root));
        AddSprite(highlight, Part("RankRow_Bg"), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.28f));
        highlight.gameObject.SetActive(false);

        float[] c = L.RankColumns;
        RectTransform rankCell = Stretch(CreateRect("RankCell", root), new Vector2(c[0], 0f), new Vector2(c[1], 1f));
        RectTransform badge = Place(CreateRect("FirstBadge", rankCell), Vector2.zero, new Vector2(40, 40));
        AddSprite(badge, Part("Dot"), AccentColor);
        badge.gameObject.SetActive(false);

        TMP_Text Cell(string name, RectTransform parent, float x0, float x1, string sample, float size, Color color)
        {
            TMP_Text t = AddText(Stretch(CreateRect(name, parent), new Vector2(x0, 0f), new Vector2(x1, 1f)), sample, size, color, true);
            t.enableWordWrapping = false;
            t.enableAutoSizing = true;
            t.fontSizeMin = size * 0.6f;
            t.fontSizeMax = size;
            return t;
        }

        TMP_Text rank = Cell("Rank", rankCell, 0f, 1f, "1", 26, TextColor);
        TMP_Text speed = Cell("Speed", root, c[1], c[2], "11.2", 28, BlueColor);
        TMP_Text energy = Cell("Energy", root, c[2], c[3], "18.0", 28, GreenColor);
        TMP_Text loss = Cell("Loss", root, c[3], c[4], "-0.9", 28, YellowColor);
        TMP_Text arrived = Cell("Arrived", root, c[4], c[5], "17.1", 28, AccentColor);
        TMP_Text sale = Cell("Sale", root, c[5], c[6], "2,565", 30, TextColor);

        var view = root.gameObject.AddComponent<RankRowView>();
        Wire(view, ("rankText", rank), ("speedText", speed), ("energyText", energy), ("lossText", loss), ("arrivedText", arrived),
            ("saleText", sale), ("firstBadge", badge.gameObject), ("highlight", highlight.gameObject));
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

    #region 32형 패널 프리팹

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

        // 합류 안내 (옆 쳇바퀴가 먼저 시작했을 때)
        RectTransform join = Place(CreateRect("Group_Join", panel), L.JoinBoxPos, L.JoinBoxSize);
        AddSprite(join, Part("Card_Bg"), new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.95f));
        TMP_Text joinText = AddText(Stretch(CreateRect("Text", join), Vector2.zero, Vector2.one, 12),
            "옆 쳇바퀴에서 체험을 시작했어요!\n<b>12초</b> 안에 시작하면 함께 달려요", 34, Color.white);
        join.gameObject.SetActive(false);

        // 시작 버튼 그림은 표시만 하고, 터치는 화면 전체를 덮는 투명 버튼이 받는다
        CreateSpriteImage(panel, "StartBadge", "Button_Start", L.StartButtonPos, L.StartButtonSize);

        RectTransform touch = StretchFull(CreateRect("StartButton", panel));
        Image touchImage = AddImage(touch, new Color(0, 0, 0, 0));
        touchImage.raycastTarget = true;
        var start = touch.gameObject.AddComponent<Button>();
        start.targetGraphic = touchImage;
        start.transition = Selectable.Transition.None;

        var view = panel.gameObject.AddComponent<IdlePanel>();
        Wire(view, ("startButton", start), ("joinGroup", join.gameObject), ("joinText", joinText));
        return panel.gameObject;
    }

    // 3단계 안내 카드의 동적 숫자·문구 (Guide, WaitNext 공용)
    private static void CreateStepTexts(RectTransform panel, GuidePanel view, float y, float scale)
    {
        Vector2 card = new Vector2(L.StepX[2] * scale, y);
        Vector2 circle = Vector2.one * (L.StepCircleSize - 20) * scale;
        TMP_Text run = CreateValue(panel, "RunSecondsText", "50", card + new Vector2(-L.StepCircleOffsetX, L.StepIconY) * scale, circle, 64 * scale,
            Color.white);
        TMP_Text transmit = CreateValue(panel, "TransmitSecondsText", "20", card + new Vector2(L.StepCircleOffsetX, L.StepIconY) * scale, circle,
            64 * scale, Color.white);
        TMP_Text step3 = CreateText(panel, "Step3Text", "전기 생산 50초, 송전 20초!\n전기를 판매하여 랭킹을 획득하세요!",
            card + new Vector2(0, L.StepBodyY) * scale, L.StepBodySize * scale, 28 * scale, TextColor);
        Wire(view, ("runSecondsText", run), ("transmitSecondsText", transmit), ("step3Text", step3));
    }

    private static GameObject CreateWaitNextPanel()
    {
        RectTransform panel = CreatePanelRoot("WaitNext");
        var view = panel.gameObject.AddComponent<WaitNextPanel>();
        CreateStepTexts(panel, view, 10, 0.9f);

        RectTransform box = Place(CreateRect("Box_Remain", panel), L.WaitRemainPos, L.WaitRemainSize);
        AddSprite(box, Part("Card_Bg"), new Color(DarkCardColor.r, DarkCardColor.g, DarkCardColor.b, 0.9f));
        TMP_Text remain = AddText(StretchFull(CreateRect("Text", box)), "약 120초 후 시작", 40, Color.white, true);
        Wire(view, ("remainText", remain));
        return panel.gameObject;
    }

    private static GameObject CreateGuidePanel()
    {
        RectTransform panel = CreatePanelRoot("Guide");
        var view = panel.gameObject.AddComponent<GuidePanel>();
        CreateStepTexts(panel, view, L.StepY, 1f);
        WireAutoGauge(view, CreateAutoGauge(panel));
        return panel.gameObject;
    }

    private static GameObject CreateCharacterSelectPanel()
    {
        RectTransform panel = CreatePanelRoot("CharacterSelect");

        var buttons = new List<UnityEngine.Object>();
        var highlights = new List<UnityEngine.Object>();
        for (int i = 0; i < L.CharacterX.Length; i++)
        {
            Button card = CreateSpriteButton(panel, $"Card_Character_{i + 1}", $"Card_Character_{i + 1}", new Vector2(L.CharacterX[i], L.CharacterY),
                L.CharacterCardSize);
            RectTransform highlight = Stretch(CreateRect("Highlight", (RectTransform)card.transform), Vector2.zero, Vector2.one, -12);
            AddSprite(highlight, Part("Frame_Bg"), AccentColor);
            highlight.gameObject.SetActive(false);
            buttons.Add(card);
            highlights.Add(highlight.gameObject);
        }

        Image hint = CreateSpriteImage(panel, "Hint_Select", "Hint_Select", L.HintPos, new Vector2(1200, 56));
        Image waiting = CreateSpriteImage(panel, "Box_Waiting", "Box_Waiting", new Vector2(0, -330), L.WaitingBoxSize);
        waiting.gameObject.SetActive(false);

        var view = panel.gameObject.AddComponent<CharacterSelectPanel>();
        WireList(view, "cardButtons", buttons);
        WireList(view, "highlights", highlights);
        Wire(view, ("waitingGroup", waiting.gameObject), ("hintGroup", hint.gameObject));
        WireAutoGauge(view, CreateAutoGauge(panel));
        return panel.gameObject;
    }

    private static GameObject CreateCountdownPanel()
    {
        RectTransform panel = CreatePanelRoot("Countdown");
        TMP_Text count = CreateValue(panel, "CountText", "3", L.CountCirclePos, Vector2.one * (L.CountCircleSize - 40), 200, AccentColor);

        var view = panel.gameObject.AddComponent<CountdownPanel>();
        Wire(view, ("countText", count));
        return panel.gameObject;
    }

    // HUD 카드 안 값 (라벨 알약 아래, 단위 왼쪽에서 끝나도록 오른쪽 정렬)
    private static TMP_Text CreateHudValue(RectTransform panel, string name, string sample, Vector2 card, float pillY, Color color)
    {
        return CreateValue(panel, name, sample, card + new Vector2(-35, pillY + L.HudValueOffsetY), new Vector2(200, 84), 66, color,
            TextAlignmentOptions.MidlineRight);
    }

    private static GameObject CreateRunPanel()
    {
        RectTransform panel = CreatePanelRoot("Run");
        TMP_Text speed = CreateHudValue(panel, "SpeedText", "10.2", L.HudCardCenter, L.RunHudPillY[0], Color.white);
        TMP_Text watt = CreateHudValue(panel, "WattText", "120", L.HudCardCenter, L.RunHudPillY[1], Color.white);
        var (ring, remain) = CreateTimer(panel);

        GaugeView energyGauge = CreateGauge(panel, "EnergyGauge", L.BarPos, L.BarFillSize, GreenColor, null);
        TMP_Text energy = CreateValue(panel, "EnergyText", "12.0 <size=60%>kWh</size>", L.BarPos + new Vector2(560, 0), new Vector2(520, 80), 56,
            TextColor, TextAlignmentOptions.MidlineRight);
        TMP_Text scale = CreateText(panel, "ScaleText", "현재 속도로 100시간 동안 달려 생산한 전력량", L.HintPos, new Vector2(1600, 44), 26,
            Color.white, true);

        var view = panel.gameObject.AddComponent<RunPanel>();
        Wire(view,
            ("speedText", speed),
            ("wattText", watt),
            ("energyText", energy),
            ("energyGauge", energyGauge),
            ("scaleText", scale),
            ("remainText", remain),
            ("remainRing", ring));
        return panel.gameObject;
    }

    private static GameObject CreateRunResultPanel()
    {
        RectTransform panel = CreatePanelRoot("RunResult");
        TMP_Text energy = CreateValue(panel, "EnergyText", "12.0", L.RunResultEnergyPos, new Vector2(440, 160), 130, AccentColor);
        TMP_Text rank = CreateValue(panel, "RankText", "12<size=50%>위</size>", L.RunResultRankBoxPos + new Vector2(0, -40),
            new Vector2(360, 160), 120, GreenColor);
        Button next = CreateSpriteButton(panel, "NextButton", "Button_Transmit", L.TransmitButtonPos, L.TransmitButtonSize);

        var view = panel.gameObject.AddComponent<RunResultPanel>();
        Wire(view, ("energyText", energy), ("rankText", rank), ("nextButton", next));
        WireAutoGauge(view, CreateAutoGauge(panel));
        return panel.gameObject;
    }

    private static GameObject CreateVoltagePanel()
    {
        RectTransform panel = CreatePanelRoot("Voltage");
        Button low = CreateSpriteButton(panel, "LowButton", "Button_Low", new Vector2(L.VoltageX[0], L.VoltageY), L.VoltageCardSize);
        Button high = CreateSpriteButton(panel, "HighButton", "Button_High", new Vector2(L.VoltageX[1], L.VoltageY), L.VoltageCardSize);
        RectTransform sendBox = Place(CreateRect("Box_Send", panel), L.SendTextPos, new Vector2(560, 76));
        AddSprite(sendBox, Part("Card_Bg"), new Color(DarkCardColor.r, DarkCardColor.g, DarkCardColor.b, 0.9f));
        TMP_Text send = AddText(StretchFull(CreateRect("SendText", sendBox)), "보낼 전력  <b>12.0</b> kWh", 34, Color.white, true);

        var view = panel.gameObject.AddComponent<VoltagePanel>();
        Wire(view, ("lowButton", low), ("highButton", high), ("sendText", send));
        WireAutoGauge(view, CreateAutoGauge(panel));
        return panel.gameObject;
    }

    private static GameObject CreateTransmitPanel()
    {
        RectTransform panel = CreatePanelRoot("Transmit");

        RectTransform pill = Place(CreateRect("Box_Voltage", panel), new Vector2(0, 400), new Vector2(560, 76));
        AddSprite(pill, Part("Card_Bg"), new Color(DarkCardColor.r, DarkCardColor.g, DarkCardColor.b, 0.9f));
        TMP_Text voltage = AddText(StretchFull(CreateRect("Text", pill)), "높은 전압으로 송전 중", 36, Color.white, true);

        TMP_Text sent = CreateHudValue(panel, "SentText", "12.0", L.TransmitHudCenter, L.TransmitHudPillY[0], Color.white);
        TMP_Text loss = CreateHudValue(panel, "LossText", "-0.6", L.TransmitHudCenter, L.TransmitHudPillY[1], YellowColor);
        TMP_Text arrived = CreateHudValue(panel, "ArrivedText", "11.4", L.TransmitHudCenter, L.TransmitHudPillY[2], AccentColor);
        var (ring, remain) = CreateTimer(panel);

        GaugeView arrivedGauge = CreateGauge(panel, "ArrivedGauge", L.BarPos, L.BarFillSize, AccentColor, null);
        TMP_Text arrivedBar = CreateValue(panel, "ArrivedBarText", "11.4 <size=60%>kWh</size>", L.BarPos + new Vector2(-560, 0), new Vector2(480, 80),
            52, Color.white, TextAlignmentOptions.MidlineLeft);
        TMP_Text sentBar = CreateValue(panel, "SentBarText", "누적 환산 발전량 <b>12.0</b> kWh", L.BarPos + new Vector2(540, 0), new Vector2(560, 60), 30,
            TextColor, TextAlignmentOptions.MidlineRight);

        var view = panel.gameObject.AddComponent<TransmitPanel>();
        Wire(view,
            ("voltageText", voltage),
            ("sentText", sent),
            ("lossText", loss),
            ("arrivedText", arrived),
            ("arrivedGauge", arrivedGauge),
            ("arrivedBarText", arrivedBar),
            ("sentBarText", sentBar),
            ("remainText", remain),
            ("remainRing", ring));
        return panel.gameObject;
    }

    private static GameObject CreateResultPanel()
    {
        RectTransform panel = CreatePanelRoot("Result");

        string[] names = { "MaxSpeedText", "EnergyText", "LossText", "ArrivedText" };
        string[] samples = { "11.2", "12.0", "-0.6", "11.4" };
        Color[] colors = { BlueColor, GreenColor, YellowColor, AccentColor };
        var values = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
            values[i] = CreateValue(panel, names[i], samples[i], new Vector2(L.ResultValueX, L.ResultRowY[i]), L.ResultValueSize, 60, colors[i],
                TextAlignmentOptions.MidlineRight);
        TMP_Text sale = CreateValue(panel, "SaleText", "1,710", new Vector2(L.ResultValueX - 35, L.ResultSaleY), new Vector2(300, 90), 76, TextColor,
            TextAlignmentOptions.MidlineRight);
        TMP_Text price = CreateText(panel, "PriceText", "기준단가 150원/kWh | 변전 설비 비용 300원 차감 (가상 조건)", L.PricePos, new Vector2(1600, 36), 22,
            SubTextColor);

        RectTransform list = CreateRankList(panel, L.ResultListPos, L.ResultListSize, out GameObject gap);

        var view = panel.gameObject.AddComponent<ResultPanel>();
        Wire(view,
            ("maxSpeedText", values[0]),
            ("energyText", values[1]),
            ("lossText", values[2]),
            ("arrivedText", values[3]),
            ("saleText", sale),
            ("priceText", price),
            ("rowPrefab", rankRowPrefab.GetComponent<RankRowView>()),
            ("rowRoot", list),
            ("gapMark", gap));
        WireAutoGauge(view, CreateAutoGauge(panel));
        return panel.gameObject;
    }

    #endregion

    #region 85형 관전 화면 프리팹

    private static GameObject CreateSpectatorPanel()
    {
        RectTransform panel = StretchFull(CreateRect($"Panel_{SpectatorName}", null));
        Image staticLayer = AddSprite(StretchFull(CreateRect(StaticLayerName, panel)), LoadSprite(StaticSpritePath(SpectatorName)));
        staticLayer.type = Image.Type.Simple;

        for (int p = 0; p < 2; p++)
        {
            Vector2 c = L.PaneCenter[p];
            // 3D 카메라가 그리는 자리 (RaceCamera viewport). 비어 있는 RectTransform.
            RectTransform pane = Place(CreateRect($"Pane_{p + 1}", panel), c, L.PaneSize);

            Image idle = AddSprite(StretchFull(CreateRect("Group_Idle", pane)), Part("Spectator_Idle"));
            idle.type = Image.Type.Simple;

            RectTransform badge = Place(CreateRect("Badge", pane), L.BadgeOffset, L.BadgeSize);
            AddSprite(badge, Part("Card_Bg"), Color.white);
            TMP_Text badgeText = AddText(StretchFull(CreateRect("Text", badge)), "체험 중...", 26, AccentColor, true);

            RectTransform compare = Place(CreateRect("Compare", pane), L.CompareOffset, L.CompareSize);
            AddSprite(compare, Part("Card_Bg"), new Color(DarkCardColor.r, DarkCardColor.g, DarkCardColor.b, 0.85f));
            TMP_Text compareText = AddText(StretchFull(CreateRect("Text", compare)), "오늘 1위보다 12m 앞서는 중", 28, Color.white, true);
            compareText.enableWordWrapping = false;

            var values = new TMP_Text[3];
            string[] samples = { "10.2 <size=55%>km/h</size>", "120 <size=55%>W</size>", "12.0 <size=55%>kWh</size>" };
            Color[] colors = { new Color32(120, 180, 255, 255), new Color32(110, 220, 160, 255), new Color32(255, 140, 110, 255) };
            for (int i = 0; i < 3; i++)
                values[i] = CreateValue(panel, $"Pane{p + 1}_Value_{i + 1}", samples[i],
                    c + new Vector2(L.StripColumnX[i], L.StripOffsetY + L.StripValueOffsetY), new Vector2(300, 46), 34, colors[i]);

            var view = pane.gameObject.AddComponent<SpectatorPaneView>();
            Wire(view, ("badgeText", badgeText), ("maxSpeedText", values[0]), ("wattText", values[1]), ("energyText", values[2]),
                ("compareText", compareText), ("compareGroup", compare.gameObject), ("idleGroup", idle.gameObject));
        }

        // 랭킹 표
        RectTransform list = Place(CreateRect("RankList", panel), L.SpectatorRankPos, L.SpectatorRankSize);
        var layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 새 랭킹 팝업
        RectTransform popup = Place(CreateRect("Group_Popup", panel), L.PopupPos, L.PopupSize);
        AddSprite(popup, Part("Popup_Ranking")).type = Image.Type.Simple;
        TMP_Text player = CreateValue(popup, "PlayerText", "Player 02", new Vector2(0, 228), new Vector2(200, 50), 28, Color.white);
        TMP_Text rank = CreateValue(popup, "RankText", "3", new Vector2(0, 110), new Vector2(240, 190), 170, TextColor);
        Vector2 box = new Vector2(0, -170);
        Vector2[] pills = { new Vector2(-240, 125), new Vector2(90, 125), new Vector2(-240, 45), new Vector2(90, 45) };
        Color[] colors2 = { new Color32(120, 180, 255, 255), new Color32(110, 220, 160, 255), YellowColor, new Color32(255, 140, 110, 255) };
        string[] names = { "SpeedText", "EnergyText", "LossText", "ArrivedText" };
        var popupValues = new TMP_Text[4];
        for (int i = 0; i < 4; i++)
            popupValues[i] = CreateValue(popup, names[i], "11.2", box + pills[i] + new Vector2(150, 0), new Vector2(150, 60), 44, colors2[i],
                TextAlignmentOptions.MidlineLeft);
        TMP_Text sale = CreateValue(popup, "SaleText", "3,100", box + new Vector2(0, -100), new Vector2(600, 110), 96, Color.white);
        popup.gameObject.SetActive(false);

        // 링크 정보만 담는 빈 컴포넌트는 두지 않는다. SpectatorScreen(캔버스 루트)이 아래 이름으로 연결한다.
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
        Transform popup = panel.transform.Find("Group_Popup");

        var screen = go.AddComponent<SpectatorScreen>();
        Wire(screen,
            ("canvas", canvas),
            ("rowPrefab", rankRowPrefab.GetComponent<RankRowView>()),
            ("rowRoot", panel.transform.Find("RankList")),
            ("popup", popup.gameObject),
            ("popupPlayerText", popup.Find("PlayerText").GetComponent<TMP_Text>()),
            ("popupRankText", popup.Find("RankText").GetComponent<TMP_Text>()),
            ("popupSpeedText", popup.Find("SpeedText").GetComponent<TMP_Text>()),
            ("popupEnergyText", popup.Find("EnergyText").GetComponent<TMP_Text>()),
            ("popupLossText", popup.Find("LossText").GetComponent<TMP_Text>()),
            ("popupArrivedText", popup.Find("ArrivedText").GetComponent<TMP_Text>()),
            ("popupSaleText", popup.Find("SaleText").GetComponent<TMP_Text>()));
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

        var screen = go.AddComponent<WheelScreen>();
        Wire(screen,
            ("canvas", canvas),
            ("idlePanel", instances[0].GetComponent<IdlePanel>()),
            ("waitNextPanel", instances[1].GetComponent<WaitNextPanel>()),
            ("guidePanel", instances[2].GetComponent<GuidePanel>()),
            ("characterSelectPanel", instances[3].GetComponent<CharacterSelectPanel>()),
            ("countdownPanel", instances[4].GetComponent<CountdownPanel>()),
            ("runPanel", instances[5].GetComponent<RunPanel>()),
            ("runResultPanel", instances[6].GetComponent<RunResultPanel>()),
            ("voltagePanel", instances[7].GetComponent<VoltagePanel>()),
            ("transmitPanel", instances[8].GetComponent<TransmitPanel>()),
            ("resultPanel", instances[9].GetComponent<ResultPanel>()));
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

            Wire(canvas.GetComponent<WheelScreen>(), ("session", session));
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
        WireList(canvas.GetComponent<SpectatorScreen>(), "sessions", Array.ConvertAll(sessions, s => (UnityEngine.Object)s));
        var panes = canvas.GetComponentsInChildren<SpectatorPaneView>(true);
        for (int i = 0; i < panes.Length && i < sessions.Length; i++)
            Wire(panes[i], ("session", sessions[i]));
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
