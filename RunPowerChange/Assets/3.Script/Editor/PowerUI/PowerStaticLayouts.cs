using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static PowerUIKit;

// 정적 레이어(화면당 PNG 한 장)와 글자가 들어간 부품 스프라이트의 "임시 디자인".
// 디자인 시안(국과천_미디어 연출 계획_1차 디자인 시안, 전기17 달려라! 전력 챌린지)을 따라 그린다.
// 32형은 3D 장면 위에 겹치므로 정적 레이어 배경은 투명하다.
// 여기 있는 내용은 PNG로 구워질 뿐 씬/프리팹에는 오브젝트로 남지 않는다.
// 문구나 배치를 바꾸려면 여기를 고친 뒤 "임시 스프라이트 다시 내보내기"를 실행하거나, 디자인 PNG로 덮어쓴다.
// 동적 요소 위치는 PowerPrefabBuilder 와 같이 쓰도록 아래 상수로 둔다.
public static class PowerStaticLayouts
{
    // 32형 화면 전체(1920x1080)에 그려지는 패널별 정적 내용
    public static readonly Dictionary<string, Action<RectTransform>> Panels = new Dictionary<string, Action<RectTransform>>
    {
        { "Idle", Idle },
        { "WaitNext", WaitNext },
        { "Guide", Guide },
        { "CharacterSelect", CharacterSelect },
        { "Countdown", Countdown },
        { "Run", Run },
        { "RunResult", RunResult },
        { "Voltage", Voltage },
        { "Transmit", Transmit },
        { "Result", Result },
    };

    #region 공용 위치 (PrefabBuilder 와 같이 쓴다)

    public static readonly Vector2 TitlePos = new Vector2(0, 400);
    public static readonly Vector2 AutoGaugePos = new Vector2(0, -435);
    public static readonly Vector2 AutoGaugeSize = new Vector2(1600, 18);
    public static readonly Vector2 HintPos = new Vector2(0, -485);

    // 01. 대기
    public static readonly Vector2 StartButtonPos = new Vector2(0, -300);
    public static readonly Vector2 StartButtonSize = new Vector2(360, 100);
    public static readonly Vector2 JoinBoxPos = new Vector2(0, 40);
    public static readonly Vector2 JoinBoxSize = new Vector2(900, 130);

    // 대기 2. 다음 게임 대기
    public static readonly Vector2 WaitRemainPos = new Vector2(0, -330);
    public static readonly Vector2 WaitRemainSize = new Vector2(560, 84);

    // 02. 체험 안내 (WaitNext 도 같은 카드)
    public static readonly float[] StepX = { -520, 0, 520 };
    public const float StepY = 20f;
    public static readonly Vector2 StepSize = new Vector2(480, 500);
    public const float StepIconY = 70f;      // 카드 중심 기준
    public const float StepBodyY = -140f;
    public static readonly Vector2 StepBodySize = new Vector2(440, 170);
    public const float StepCircleOffsetX = 80f;
    public const float StepCircleSize = 130f;

    // 03. 캐릭터 선택
    public static readonly float[] CharacterX = { -290, 290 };
    public const float CharacterY = 10f;
    public static readonly Vector2 CharacterCardSize = new Vector2(480, 560);
    public static readonly Vector2 WaitingBoxSize = new Vector2(760, 100);

    // 04. 카운트다운
    public static readonly Vector2 CountCirclePos = new Vector2(0, 40);
    public const float CountCircleSize = 300f;

    // 05. 발전 / 08. 송전 HUD
    public static readonly Vector2 HudCardCenter = new Vector2(-760, 210);
    public static readonly Vector2 RunHudSize = new Vector2(320, 400);
    public static readonly float[] RunHudPillY = { 150, -40 };   // 카드 중심 기준
    public static readonly Vector2 TransmitHudCenter = new Vector2(-760, 150);
    public static readonly Vector2 TransmitHudSize = new Vector2(320, 560);
    public static readonly float[] TransmitHudPillY = { 230, 60, -110 };
    public const float HudValueOffsetY = -72f;                    // 라벨 기준 값 위치
    public static readonly Vector2 HudPillSize = new Vector2(240, 42);
    public static readonly Vector2 TimerPos = new Vector2(810, 390);
    public const float TimerSize = 160f;
    public static readonly Vector2 BarPos = new Vector2(0, -390);
    public static readonly Vector2 BarSize = new Vector2(1700, 110);
    public static readonly Vector2 BarFillSize = new Vector2(1676, 86);

    // 06. 발전 완료
    public static readonly Vector2 RunResultCardPos = new Vector2(0, 60);
    public static readonly Vector2 RunResultCardSize = new Vector2(960, 440);
    public static readonly Vector2 RunResultEnergyPos = new Vector2(-200, 30);
    public static readonly Vector2 RunResultRankBoxPos = new Vector2(250, 60);
    public static readonly Vector2 RunResultRankBoxSize = new Vector2(400, 380);
    public static readonly Vector2 TransmitButtonPos = new Vector2(0, -275);
    public static readonly Vector2 TransmitButtonSize = new Vector2(520, 96);

    // 07. 전압 선택
    public static readonly float[] VoltageX = { -330, 330 };
    public const float VoltageY = 10f;
    public static readonly Vector2 VoltageCardSize = new Vector2(600, 520);
    public static readonly Vector2 SendTextPos = new Vector2(0, -330);

    // 09. 결과 확인
    public static readonly Vector2 ResultCardPos = new Vector2(0, 30);
    public static readonly Vector2 ResultCardSize = new Vector2(1760, 660);
    public static readonly float[] ResultRowY = { 200, 110, 20, -70 };
    public const float ResultSaleY = -180f;
    public const float ResultValueX = -175f;     // 값 오른쪽 정렬 영역 중심 (오른쪽 끝 = 가운데 세로선 앞)
    public static readonly Vector2 ResultValueSize = new Vector2(300, 80);
    public static readonly Vector2 ResultListPos = new Vector2(430, -10);
    public static readonly Vector2 ResultListSize = new Vector2(820, 430);
    public static readonly Vector2 PricePos = new Vector2(0, -255);

    // 랭킹 줄 열 (앵커 비율): 순위, 최고속도, 누적발전량, 송전중손실, 도착한전력, 판매금액
    public static readonly float[] RankColumns = { 0f, 0.09f, 0.27f, 0.45f, 0.63f, 0.80f, 0.98f };
    public static readonly string[] RankHeaders = { "순위", "최고 속도", "누적 발전량", "송전 중 손실", "도착한 전력", "판매 금액" };

    #endregion

    #region 85형 관전 화면 위치 (1080x1920, 세로)

    public static readonly Vector2[] PaneCenter = { new Vector2(0, 540), new Vector2(0, -100) };
    public static readonly Vector2 PaneSize = new Vector2(1000, 500);
    public const float StripOffsetY = -290f;                 // Player 칸 중심 기준 기록 줄
    public static readonly Vector2 StripSize = new Vector2(1000, 80);
    public static readonly float[] StripColumnX = { -330, 0, 330 };
    public const float StripLabelOffsetY = 22f;   // 기록 줄 안: 라벨 위, 값 아래
    public const float StripValueOffsetY = -12f;
    public static readonly Vector2 TagOffset = new Vector2(-370, 275);
    public static readonly Vector2 BadgeOffset = new Vector2(370, -205);
    public static readonly Vector2 BadgeSize = new Vector2(220, 56);
    public static readonly Vector2 CompareOffset = new Vector2(-230, -205);
    public static readonly Vector2 CompareSize = new Vector2(500, 56);
    public static readonly Vector2 SpectatorRankPos = new Vector2(0, -690);
    public static readonly Vector2 SpectatorRankSize = new Vector2(1000, 300);
    public const float SpectatorRankHeaderY = -515f;
    public static readonly Vector2 PopupPos = new Vector2(0, 380);
    public static readonly Vector2 PopupSize = new Vector2(760, 760);

    #endregion

    // 글자가 들어간 부품 스프라이트 (이름, 크기, 그리기). 배경은 투명.
    public static readonly (string name, Vector2Int size, Action<RectTransform> draw)[] TextParts =
    {
        ("Button_Start", new Vector2Int(360, 100), root => PillButton(root, "체험 시작", 40)),
        ("Button_Transmit", new Vector2Int(520, 96), root => PillButton(root, "송전 체험 시작하기", 36)),
        ("Card_Character_1", new Vector2Int(480, 560), root => CharacterCard(root, "싸돌이", "씩씩한 달리기 선수", BlueColor)),
        ("Card_Character_2", new Vector2Int(480, 560), root => CharacterCard(root, "로봇공룡 디노", "지치지 않는 공룡 로봇", GreenColor)),
        ("Button_Low", new Vector2Int(600, 520), root => VoltageCard(root, "낮은 전압", "변전 설비 비용은 없지만\n보내는 동안 사라지는\n전기가 많아요.", 2)),
        ("Button_High", new Vector2Int(600, 520), root => VoltageCard(root, "높은 전압", "변전 설비 비용이 들지만\n보내는 동안 사라지는\n전기가 적어요.", 4)),
        ("Hint_Select", new Vector2Int(1200, 56), root => ShadowFill(root, "원하는 캐릭터를 선택해주세요.", 28)),
        ("Box_Waiting", new Vector2Int(760, 100), root => NavyPill(root, "다른 체험자를 기다리는 중...", 36)),
        ("Spectator_Idle", new Vector2Int(1000, 500), SpectatorIdle),
        ("Popup_Ranking", new Vector2Int(760, 760), RankingPopup),
    };

    private static string Accent(string text) => $"<color={AccentHex}>{text}</color>";
    private static string Hex(Color c) => "#" + ColorUtility.ToHtmlStringRGB(c);

    #region 부품

    private static void PillButton(RectTransform root, string text, float fontSize)
    {
        AddRoundBox(root, Color.white, root.sizeDelta.y * 0.5f);
        FillText(root, "Text", text, fontSize, DarkTextColor, true);
    }

    private static void NavyPill(RectTransform root, string text, float fontSize)
    {
        AddRoundBox(root, new Color(DarkCardColor.r, DarkCardColor.g, DarkCardColor.b, 0.9f), root.sizeDelta.y * 0.5f);
        FillText(root, "Text", text, fontSize, Color.white, true);
    }

    private static void ShadowFill(RectTransform root, string text, float fontSize)
    {
        ShadowText(root, "Text", text, Vector2.zero, root.sizeDelta, fontSize, TitleColor, false);
    }

    // 캐릭터 카드: 반투명 흰 카드 + 캐릭터 자리(임시 원) + 이름. 디자이너 캐릭터 그림으로 덮어쓴다.
    private static void CharacterCard(RectTransform root, string name, string desc, Color color)
    {
        AddRoundBox(root, CardColor, 32);
        CreateRoundBox(root, "Shadow", new Vector2(0, -120), new Vector2(260, 50), new Color(0, 0, 0, 0.12f), 25);
        RectTransform body = CreateRoundBox(root, "Body", new Vector2(0, 0), new Vector2(170, 240), color, 70);
        CreateRoundBox(body, "Head", new Vector2(0, 150), new Vector2(150, 150), new Color32(255, 214, 170, 255), 75);
        CreateText(root, "Name", name, new Vector2(0, -180), new Vector2(440, 70), 48, TextColor, true);
        CreateText(root, "Desc", desc, new Vector2(0, -235), new Vector2(440, 40), 26, SubTextColor);
    }

    // 전압 카드: 송전탑(임시 도형) + 이름 + 설명. bars = 탑 높이(전압이 높을수록 높게)
    private static void VoltageCard(RectTransform root, string name, string body, int bars)
    {
        AddRoundBox(root, CardColor, 32);
        for (int i = 0; i < bars; i++)
            CreateRoundBox(root, $"Bar_{i + 1}", new Vector2(-75 + i * 50, 70 + i * 22), new Vector2(36, 80 + i * 44), AccentColor, 10);
        CreateText(root, "Name", name, new Vector2(0, -60), new Vector2(540, 80), 60, TextColor, true);
        CreateText(root, "Body", body, new Vector2(0, -170), new Vector2(540, 130), 28, SubTextColor);
    }

    // 85형 Player 칸: 체험자가 없을 때 칸 위에 덮는 안내
    private static void SpectatorIdle(RectTransform root)
    {
        AddRoundBox(root, new Color(DarkCardColor.r, DarkCardColor.g, DarkCardColor.b, 0.55f), 16);
        CreateText(root, "Title", "체험 대기 중", new Vector2(0, 40), new Vector2(900, 90), 64, Color.white, true);
        CreateText(root, "Body", "쳇바퀴에 올라 화면을 터치하고\n전력 챌린지에 도전해 보세요!", new Vector2(0, -70), new Vector2(900, 110), 34, Color.white);
    }

    // 85형 새 랭킹 팝업 배경 (숫자는 동적)
    private static void RankingPopup(RectTransform root)
    {
        AddRoundBox(root, new Color(1f, 1f, 1f, 0.96f), 32);
        CreateText(root, "Title", "판매왕 랭킹 업데이트", new Vector2(0, 300), new Vector2(700, 70), 48, TextColor, true);
        CreatePill(root, "PlayerPill", new Vector2(0, 228), new Vector2(220, 52), DarkCardColor);
        CreateText(root, "New", "NEW", new Vector2(-190, 110), new Vector2(160, 70), 52, YellowColor, true);
        CreateText(root, "Unit", "위", new Vector2(150, 90), new Vector2(80, 70), 52, TextColor, true);
        RectTransform box = CreateRoundBox(root, "Box", new Vector2(0, -170), new Vector2(680, 360), DarkCardColor, 24);
        string[] labels = { "최고 속도", "누적 발전량", "송전 중 손실", "도착한 전력" };
        Color[] colors = { BlueColor, GreenColor, YellowColor, AccentColor };
        for (int i = 0; i < labels.Length; i++)
        {
            Vector2 pos = new Vector2(i % 2 == 0 ? -240 : 90, i < 2 ? 125 : 45);
            CreatePill(box, $"Pill_{i + 1}", pos, new Vector2(140, 36), colors[i]);
            CreateText(box, $"Label_{i + 1}", labels[i], pos, new Vector2(140, 36), 20, Color.white, true);
        }
        CreateText(box, "SaleLabel", "전기 판매 금액", new Vector2(0, -25), new Vector2(400, 40), 26, Color.white);
    }

    #endregion

    #region 32형 공통

    private static void Title(RectTransform root, string text) => ShadowText(root, "Title", text, TitlePos, new Vector2(1600, 90), 58, TitleColor);

    private static void Hint(RectTransform root, string text) => ShadowText(root, "Hint", text, HintPos, new Vector2(1700, 44), 26, TitleColor, false);

    // 반투명 남색 HUD 카드 + 색 라벨 알약
    private static void HudCard(RectTransform root, Vector2 center, Vector2 size, float[] pillY, string[] labels, Color[] colors, string[] units)
    {
        RectTransform card = CreateRoundBox(root, "Box_Hud", center, size, new Color(DarkCardColor.r, DarkCardColor.g, DarkCardColor.b, 0.82f), 28);
        for (int i = 0; i < labels.Length; i++)
        {
            CreatePill(card, $"Pill_{i + 1}", new Vector2(0, pillY[i]), HudPillSize, colors[i]);
            CreateText(card, $"Label_{i + 1}", labels[i], new Vector2(0, pillY[i]), HudPillSize, 24, Color.white, true);
            CreateText(card, $"Unit_{i + 1}", units[i], new Vector2(110, pillY[i] + HudValueOffsetY - 14), new Vector2(80, 40), 26, Color.white, false,
                TextAlignmentOptions.MidlineLeft);
        }
    }

    private static void TimerDisc(RectTransform root)
    {
        CreateRoundBox(root, "TimerShadow", TimerPos + new Vector2(3, -5), Vector2.one * (TimerSize + 12), new Color(0, 0, 0, 0.2f), TimerSize);
        CreateRoundBox(root, "TimerDisc", TimerPos, Vector2.one * (TimerSize + 12), Color.white, TimerSize);
    }

    private static void BarTrack(RectTransform root)
    {
        CreatePill(root, "BarShadow", BarPos + new Vector2(0, -5), BarSize, new Color(0, 0, 0, 0.18f));
        CreatePill(root, "BarTrack", BarPos, BarSize, GaugeTrackColor);
    }

    // 체험 안내 3단계 카드 (Guide, WaitNext 공용). step3 본문은 설정값이 들어가 동적.
    private static void StepCards(RectTransform root, float y, float scale)
    {
        string[] bodies =
        {
            "쳇바퀴를 달리면 전기가 만들어져요!\n<color=#E5484D>※ 반드시 정면의 안전바를 잡고\n안전하게 달리세요!</color>",
            "전기 생산을 완료한 후\n송전 전압을 골라\n만든 전기를 판매할 수 있어요!",
            null,
        };
        string[] icons = { "발전", "송전\n판매", null };
        for (int i = 0; i < 3; i++)
        {
            Vector2 center = new Vector2(StepX[i] * scale, y);
            RectTransform card = CreateRoundBox(root, $"Box_Step{i + 1}", center, StepSize * scale, CardColor, 32);
            RectTransform badge = CreatePill(card, "Badge", new Vector2(-110, 230) * scale, new Vector2(170, 52) * scale, DarkCardColor);
            FillText(badge, "Text", $"STEP {i + 1:00}", 26 * scale, Color.white, true);

            if (icons[i] != null)
            {
                RectTransform icon = CreateRoundBox(card, "Icon", new Vector2(0, StepIconY) * scale, Vector2.one * 170 * scale,
                    i == 0 ? AccentColor : BlueColor, 85 * scale);
                FillText(icon, "Text", icons[i], 30 * scale, Color.white, true);
            }
            else
            {
                CreateRoundBox(card, "Circle_Run", new Vector2(-StepCircleOffsetX, StepIconY) * scale, Vector2.one * StepCircleSize * scale, AccentColor,
                    StepCircleSize * scale);
                CreateRoundBox(card, "Circle_Transmit", new Vector2(StepCircleOffsetX, StepIconY) * scale, Vector2.one * StepCircleSize * scale, BlueColor,
                    StepCircleSize * scale);
            }

            if (bodies[i] != null)
                CreateText(card, "Body", bodies[i], new Vector2(0, StepBodyY) * scale, StepBodySize * scale, 28 * scale, TextColor);
        }
    }

    #endregion

    #region 32형 패널

    private static void Idle(RectTransform root)
    {
        ShadowText(root, "Title1", "달려라!", new Vector2(0, 330), new Vector2(1200, 120), 96, TitleColor);
        ShadowText(root, "Title2", "전력 챌린지", new Vector2(0, 200), new Vector2(1400, 160), 140, YellowColor);
        ShadowText(root, "Caption", "달려서 전기를 만들고 송전하여 더 많은 전기를 판매해보세요!", new Vector2(0, -390), new Vector2(1600, 44), 28,
            TitleColor, false);
    }

    private static void WaitNext(RectTransform root)
    {
        Title(root, "다음 체험을 기다리고 있어요");
        ShadowText(root, "Sub", "옆 쳇바퀴의 체험이 끝나면 자동으로 시작해요! 그동안 체험 방법을 확인해 보세요.", new Vector2(0, 330),
            new Vector2(1700, 44), 28, TitleColor, false);
        StepCards(root, 10, 0.9f);
        Hint(root, "잠시만 기다려 주세요.");
    }

    private static void Guide(RectTransform root)
    {
        Title(root, "체험 안내");
        StepCards(root, StepY, 1f);
        Hint(root, "잠시 후 캐릭터 선택으로 넘어갑니다.");
    }

    private static void CharacterSelect(RectTransform root)
    {
        Title(root, "캐릭터 선택");
    }

    private static void Countdown(RectTransform root)
    {
        ShadowText(root, "Title", "빠르게 달려 전기를 생산하세요!", new Vector2(0, 330), new Vector2(1600, 90), 60, TitleColor);
        CreateRoundBox(root, "CircleShadow", CountCirclePos + new Vector2(4, -6), Vector2.one * CountCircleSize, new Color(0, 0, 0, 0.2f), CountCircleSize);
        CreateRoundBox(root, "Circle", CountCirclePos, Vector2.one * CountCircleSize, Color.white, CountCircleSize);
        ShadowText(root, "Warning", "반드시 정면의 안전바를 잡고\n안전하게 달리세요!", new Vector2(0, -210), new Vector2(1200, 110), 38, TitleColor);
    }

    private static void Run(RectTransform root)
    {
        HudCard(root, HudCardCenter, RunHudSize, RunHudPillY, new[] { "현재 속도", "환산 발전출력" }, new[] { BlueColor, GreenColor },
            new[] { "km/h", "W" });
        TimerDisc(root);
        BarTrack(root);
        ShadowText(root, "BarLabel", "누적 환산 발전량", BarPos + new Vector2(-700, 85), new Vector2(400, 40), 28, TitleColor, true,
            TextAlignmentOptions.MidlineLeft);
    }

    private static void RunResult(RectTransform root)
    {
        Title(root, "전기 발전 완료");
        RectTransform card = CreateRoundBox(root, "Box_Result", RunResultCardPos, RunResultCardSize, CardColor, 32);
        Vector2 energy = RunResultEnergyPos - RunResultCardPos;
        CreatePill(card, "EnergyPill", energy + new Vector2(0, 130), new Vector2(280, 52), AccentColor);
        CreateText(card, "EnergyLabel", "누적 환산 발전량", energy + new Vector2(0, 130), new Vector2(280, 52), 26, Color.white, true);
        CreateText(card, "EnergyUnit", "kWh", energy + new Vector2(0, -95), new Vector2(200, 50), 36, AccentColor, true);

        RectTransform rank = CreateRoundBox(card, "Box_Rank", RunResultRankBoxPos - RunResultCardPos, RunResultRankBoxSize, DarkCardColor, 28);
        CreateText(rank, "Label", "오늘의 발전왕 순위", new Vector2(0, 110), new Vector2(360, 50), 30, Color.white, true);

        Hint(root, "발전량을 확인 후 \"송전 체험 시작하기\" 버튼을 터치해주세요.");
    }

    private static void Voltage(RectTransform root)
    {
        Title(root, "송전 전압 선택");
        ShadowText(root, "Sub", "송전은 전압에 따른 손실을 비교하기 위한 가상 조건입니다.", new Vector2(0, 335), new Vector2(1600, 44), 28,
            TitleColor, false);
        Hint(root, "송전 전압을 선택해 주세요.");
    }

    private static void Transmit(RectTransform root)
    {
        HudCard(root, TransmitHudCenter, TransmitHudSize, TransmitHudPillY, new[] { "출발 발전량", "송전 중 손실", "도착한 전력" },
            new[] { GreenColor, YellowColor, AccentColor }, new[] { "kWh", "kWh", "kWh" });
        TimerDisc(root);
        BarTrack(root);
        // 출발 발전량 전체 (연한 초록). 도착 전력(주황)이 그 위에서 줄어든다.
        CreatePill(root, "BarTotal", BarPos, BarFillSize, new Color(GreenColor.r, GreenColor.g, GreenColor.b, 0.45f));
        ShadowText(root, "Notice", "※ 송전은 전압에 따른 손실을 비교하기 위한 가상 조건입니다.", HintPos, new Vector2(1700, 44), 24, TitleColor, false);
    }

    private static void Result(RectTransform root)
    {
        Title(root, "결과 확인");
        RectTransform card = CreateRoundBox(root, "Box_Result", ResultCardPos, ResultCardSize, CardColor, 32);
        Vector2 o = -ResultCardPos;

        // 왼쪽: 최종 결과
        CreatePill(card, "ResultPill", o + new Vector2(-690, 275), new Vector2(200, 52), DarkCardColor);
        CreateText(card, "ResultLabel", "최종 결과", o + new Vector2(-690, 275), new Vector2(200, 52), 26, Color.white, true);
        string[] labels = { "최고 속도", "누적 발전량", "송전 중 손실", "도착한 전력" };
        string[] units = { "km/h", "kWh", "kWh", "kWh" };
        Color[] colors = { BlueColor, GreenColor, YellowColor, AccentColor };
        for (int i = 0; i < labels.Length; i++)
        {
            float y = ResultRowY[i];
            CreateRoundBox(card, $"Dot_{i + 1}", o + new Vector2(-790, y), new Vector2(14, 14), colors[i], 7);
            CreateText(card, $"Label_{i + 1}", labels[i], o + new Vector2(-640, y), new Vector2(260, 60), 34, TextColor, true,
                TextAlignmentOptions.MidlineLeft);
            CreatePill(card, $"UnitPill_{i + 1}", o + new Vector2(-430, y), new Vector2(110, 34), colors[i]);
            CreateText(card, $"Unit_{i + 1}", units[i], o + new Vector2(-430, y), new Vector2(110, 34), 20, Color.white, true);
        }
        CreateRoundBox(card, "WonCircle", o + new Vector2(-770, ResultSaleY), new Vector2(54, 54), YellowColor, 27);
        CreateText(card, "WonMark", "₩", o + new Vector2(-770, ResultSaleY), new Vector2(54, 54), 30, Color.white, true);
        CreateText(card, "SaleLabel", "전기 판매 금액", o + new Vector2(-580, ResultSaleY), new Vector2(300, 60), 36, TextColor, true,
            TextAlignmentOptions.MidlineLeft);
        CreateText(card, "SaleUnit", "원", o + new Vector2(-30, ResultSaleY - 6), new Vector2(60, 50), 32, TextColor, true);

        // 가운데 세로선
        AddImage(Place(CreateRect("Divider", card), o + new Vector2(0, 0), new Vector2(2, 520)), new Color(0, 0, 0, 0.12f));

        // 오른쪽: 랭킹 머리줄
        CreatePill(card, "RankPill", o + new Vector2(130, 275), new Vector2(160, 52), AccentColor);
        CreateText(card, "RankLabel", "랭킹", o + new Vector2(130, 275), new Vector2(160, 52), 26, Color.white, true);
        RankHeader(card, o + ResultListPos + new Vector2(0, ResultListSize.y * 0.5f + 20), ResultListSize.x, 18);

        Hint(root, "체험이 종료 되었습니다. 잠시 후 처음화면으로 돌아갑니다.");
    }

    // 랭킹 표 열 이름 (RankRow 와 같은 열 비율)
    private static void RankHeader(RectTransform parent, Vector2 center, float width, float fontSize)
    {
        for (int i = 0; i < RankHeaders.Length; i++)
        {
            float x0 = Mathf.Lerp(-width * 0.5f, width * 0.5f, RankColumns[i]);
            float x1 = Mathf.Lerp(-width * 0.5f, width * 0.5f, RankColumns[i + 1]);
            CreateText(parent, $"Header_{i + 1}", RankHeaders[i], new Vector2((x0 + x1) * 0.5f, center.y), new Vector2(x1 - x0, 30), fontSize,
                SubTextColor, true).transform.localPosition += new Vector3(center.x, 0, 0);
        }
    }

    #endregion

    #region 85형 관전 화면

    // 85형 세로 화면(1080x1920) 정적 내용. Player 칸(3D 카메라가 그리는 자리)은 비워 둔다.
    public static void Spectator(RectTransform root)
    {
        CreateText(root, "Title", "전력 챌린지", new Vector2(0, 905), new Vector2(1000, 90), 64, TextColor, true);

        string[] players = { "Player 01", "Player 02" };
        string[] sides = { "← 왼쪽 체험화면", "오른쪽 체험화면 →" };
        string[] labels = { "최고 속도", "환산 발전출력", "누적 환산 발전량" };
        for (int p = 0; p < 2; p++)
        {
            Vector2 c = PaneCenter[p];
            // 칸 테두리 (3D 화면 둘레)
            Frame(root, $"PaneFrame_{p + 1}", c, PaneSize, 6, DarkCardColor);

            RectTransform strip = CreateRoundBox(root, $"Strip_{p + 1}", c + new Vector2(0, StripOffsetY), StripSize, DarkCardColor, 20);
            for (int i = 0; i < labels.Length; i++)
                CreateText(strip, $"Label_{i + 1}", labels[i], new Vector2(StripColumnX[i], StripLabelOffsetY), new Vector2(300, 30), 18,
                    new Color(1, 1, 1, 0.7f));

            RectTransform tag = CreatePill(root, $"Tag_{p + 1}", c + TagOffset, new Vector2(220, 56), DarkCardColor);
            FillText(tag, "Text", players[p], 30, Color.white, true);
            CreateText(root, $"Side_{p + 1}", sides[p], c + TagOffset + new Vector2(250, 0), new Vector2(260, 40), 22, AccentColor, true,
                TextAlignmentOptions.MidlineLeft);
        }

        CreateText(root, "RankTitle", "전력 챌린지 랭킹 (판매왕)", new Vector2(0, SpectatorRankHeaderY + 50), new Vector2(900, 60), 40, TextColor, true);
        RankHeader(root, new Vector2(SpectatorRankPos.x, SpectatorRankHeaderY), SpectatorRankSize.x, 18);
        CreateText(root, "Footer", "※ 모든 순위는 당일(00:00 ~ 23:59) 기준이며, 매일 자동 초기화됩니다.", new Vector2(0, -890), new Vector2(1000, 40), 22,
            SubTextColor);
    }

    // 속이 빈 사각 테두리 (3D 칸 둘레)
    private static void Frame(RectTransform root, string name, Vector2 center, Vector2 size, float t, Color color)
    {
        RectTransform frame = Place(CreateRect(name, root), center, size + Vector2.one * t * 2);
        AddImage(Stretch(CreateRect("Top", frame), new Vector2(0, 1), Vector2.one), color).rectTransform.offsetMin = new Vector2(0, -t);
        AddImage(Stretch(CreateRect("Bottom", frame), Vector2.zero, new Vector2(1, 0)), color).rectTransform.offsetMax = new Vector2(0, t);
        AddImage(Stretch(CreateRect("Left", frame), Vector2.zero, new Vector2(0, 1)), color).rectTransform.offsetMax = new Vector2(t, 0);
        AddImage(Stretch(CreateRect("Right", frame), new Vector2(1, 0), Vector2.one), color).rectTransform.offsetMin = new Vector2(-t, 0);
    }

    #endregion
}
