using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using static PowerUIKit;

// 정적 레이어(화면당 PNG 한 장)와 글자가 들어간 부품 스프라이트의 "임시 디자인".
// 여기 있는 내용은 PNG로 구워질 뿐 씬/프리팹에는 오브젝트로 남지 않는다.
// 문구나 배치를 바꾸려면 여기를 고친 뒤 "임시 스프라이트 다시 내보내기"를 실행하거나, 디자인 PNG로 덮어쓴다.
// 동적 요소 위치는 PowerPrefabBuilder 와 같이 쓰도록 아래 상수로 둔다.
public static class PowerStaticLayouts
{
    // 화면 전체(1920x1080)에 그려지는 패널별 정적 내용
    public static readonly Dictionary<string, Action<RectTransform>> Panels = new Dictionary<string, Action<RectTransform>>
    {
        { "Idle", Idle },
        { "Guide", Guide },
        { "Countdown", Countdown },
        { "Run", Run },
        { "RunResult", root => RecordAndRank(root, "발전 완료!", new[] { "환산 발전량", "달린 거리", "오늘의 발전왕 순위" }, new[] { "Wh", "m", "" }, "오늘의 발전왕") },
        { "Voltage", Voltage },
        { "Transmit", Transmit },
        { "SaleResult", root => RecordAndRank(root, "판매 완료!", new[] { "도착한 전력", "판매 금액", "오늘의 판매왕 순위" }, new[] { "Wh", "원", "" }, "오늘의 판매왕") },
        { "Summary", Summary },
        { "End", End },
    };

    #region 공용 위치 (PrefabBuilder 와 같이 쓴다)

    public static readonly Vector2 TitlePos = new Vector2(0, 430);
    public static readonly Vector2 BottomButtonPos = new Vector2(0, -425);
    public static readonly Vector2 NoticePos = new Vector2(0, -500);

    // 04. 발전체험
    public static readonly Vector2 SpeedBoxCenter = new Vector2(-480, 120);
    public static readonly Vector2 PowerBoxCenter = new Vector2(480, 120);
    public static readonly Vector2 BigBoxSize = new Vector2(860, 400);
    public const float BigValueY = 10f;
    public const float PowerGaugeY = -150f;
    public static readonly float[] SmallBoxX = { -620, 0, 620 };
    public const float SmallBoxY = -215f;
    public static readonly Vector2 SmallBoxSize = new Vector2(580, 210);
    public const float SmallValueY = -10f;
    public const float SmallUnitY = -80f;
    // 결과 기록 값의 가로 위치 (값 오른쪽 끝 = 이 값 + 200, 단위는 그 오른쪽)
    public const float RecordValueX = 150f;
    public const float RecordUnitX = 400f;
    public static readonly Vector2 TimeGaugePos = new Vector2(0, -375);
    public static readonly Vector2 TimeGaugeSize = new Vector2(1820, 24);

    // 05·08. 결과 + 랭킹
    public static readonly Vector2 RecordBoxCenter = new Vector2(-470, 20);
    public static readonly Vector2 RankBoxCenter = new Vector2(470, 20);
    public static readonly Vector2 ResultBoxSize = new Vector2(880, 680);
    public static readonly float[] RecordRowY = { 220, 70, -80 };
    public const float PercentY = -175f;
    public const float PriceY = -280f;
    public static readonly Vector2 RankListOffset = new Vector2(0, -40);
    public static readonly Vector2 RankListSize = new Vector2(820, 540);

    // 06. 전압 선택
    public static readonly Vector2 SendBoxCenter = new Vector2(0, 255);
    public static readonly Vector2 SendBoxSize = new Vector2(1200, 150);
    public const float ScaleTextY = 150f;
    public static readonly Vector2 LowButtonPos = new Vector2(-440, -120);
    public static readonly Vector2 HighButtonPos = new Vector2(440, -120);
    public static readonly Vector2 VoltageButtonSize = new Vector2(780, 400);

    // 07. 송전
    public static readonly Vector2 PlantPos = new Vector2(-700, 90);
    public static readonly Vector2 CityPos = new Vector2(700, 90);
    public static readonly Vector2 SiteSize = new Vector2(300, 300);
    public static readonly Vector2 LineGaugePos = new Vector2(0, 90);
    public static readonly Vector2 LineGaugeSize = new Vector2(1000, 30);
    public const float VoltageLabelY = 300f;
    public static readonly float[] FlowBoxX = { -580, 0, 580 };
    public const float FlowBoxY = -215f;
    public static readonly Vector2 FlowBoxSize = new Vector2(540, 220);

    // 09. 종합 정산
    public static readonly Vector2 EnergySummaryCenter = new Vector2(-450, 50);
    public static readonly Vector2 SaleSummaryCenter = new Vector2(450, 50);
    public static readonly Vector2 SummaryBoxSize = new Vector2(840, 520);
    public const float SummaryValueY = 20f;
    public const float SummaryRankY = -170f;
    public const float ConditionY = -285f;
    public static readonly Vector2 AgainButtonPos = new Vector2(-220, -425);
    public static readonly Vector2 HomeButtonPos = new Vector2(220, -425);

    #endregion

    // 글자가 들어간 부품 스프라이트 (이름, 크기, 그리기). 배경은 투명.
    public static readonly (string name, Vector2Int size, Action<RectTransform> draw)[] TextParts =
    {
        ("Button_Start", new Vector2Int(520, 130), root => ButtonSprite(root, "체험 시작", AccentColor, DarkTextColor, 52)),
        ("Button_Confirm", new Vector2Int(480, 110), root => ButtonSprite(root, "시작하기", AccentColor, DarkTextColor, 46)),
        ("Button_Transmit", new Vector2Int(480, 110), root => ButtonSprite(root, "송전하러 가기", AccentColor, DarkTextColor, 46)),
        ("Button_Result", new Vector2Int(480, 110), root => ButtonSprite(root, "결과 보기", AccentColor, DarkTextColor, 46)),
        ("Button_Again", new Vector2Int(400, 110), root => ButtonSprite(root, "다시 체험", AccentColor, DarkTextColor, 44)),
        ("Button_Home", new Vector2Int(400, 110), root => ButtonSprite(root, "처음으로", CardColor, TextColor, 44)),
        ("Button_Low", new Vector2Int(780, 400), root => VoltageCard(root, "낮은 전압", "변전 설비가 필요 없지만\n보내는 동안 사라지는 전기가 많아요.")),
        ("Button_High", new Vector2Int(780, 400), root => VoltageCard(root, "높은 전압", "변전 설비 비용이 들지만\n보내는 동안 사라지는 전기가 적어요.")),
    };

    private static string Accent(string text) => $"<color={AccentHex}>{text}</color>";

    private static void ButtonSprite(RectTransform root, string text, Color box, Color textColor, float fontSize)
    {
        AddRoundBox(root, box);
        FillText(root, "Text", text, fontSize, textColor, true);
    }

    // 전압 선택 카드: 강조색 테두리 + 이름 + 설명
    private static void VoltageCard(RectTransform root, string name, string body)
    {
        AddRoundBox(root, AccentColor);
        RectTransform inner = Stretch(CreateRect("Inner", root), Vector2.zero, Vector2.one, 6);
        AddRoundBox(inner, CardColor);
        CreateText(inner, "Name", name, new Vector2(0, 80), new Vector2(700, 110), 72, AccentColor, true);
        CreateText(inner, "Body", body, new Vector2(0, -70), new Vector2(700, 140), 32, TextColor);
    }

    private static void Background(RectTransform root)
    {
        AddImage(StretchFull(CreateRect("Background", root)), BgColor);
        // 위쪽 강조 띠
        AddImage(Stretch(CreateRect("TopBar", root), new Vector2(0, 1), Vector2.one), AccentColor).rectTransform.offsetMin = new Vector2(0, -8);
    }

    private static void Title(RectTransform root, string text)
    {
        CreateText(root, "Title", text, TitlePos, new Vector2(1600, 90), 60, TextColor, true);
    }

    private static void Notice(RectTransform root, string text)
    {
        CreateText(root, "Notice", text, NoticePos, new Vector2(1800, 40), 24, SubTextColor);
    }

    // 라벨이 위에 있는 상자. 값은 동적 요소가 채운다.
    private static RectTransform LabeledBox(RectTransform root, string name, Vector2 center, Vector2 size, string label, string unit,
        float labelY, float unitY)
    {
        RectTransform box = CreateRoundBox(root, name, center, size, CardColor);
        CreateText(box, "Label", label, new Vector2(0, labelY), new Vector2(size.x - 40, 50), 30, SubTextColor, true);
        if (!string.IsNullOrEmpty(unit))
            CreateText(box, "Unit", unit, new Vector2(0, unitY), new Vector2(size.x - 40, 40), 28, SubTextColor);
        return box;
    }

    private static void Idle(RectTransform root)
    {
        Background(root);
        CreateText(root, "Exhibit", "전기17  |  발전과 송전", new Vector2(0, 330), new Vector2(1200, 50), 30, SubTextColor, true);
        CreateText(root, "Title", "달려라! 전력 챌린지", new Vector2(0, 190), new Vector2(1700, 160), 120, AccentColor, true);
        CreateText(root, "Description", "쳇바퀴를 힘껏 달려 전기를 만들고,\n어떤 전압으로 보낼지 골라 전기를 팔아 보세요!",
            new Vector2(0, 10), new Vector2(1500, 130), 40, TextColor);
        CreateText(root, "Hint_Start", "화면을 터치하면 시작합니다", new Vector2(0, -400), new Vector2(1200, 50), 30, SubTextColor);
    }

    private static void Guide(RectTransform root)
    {
        Background(root);
        CreateText(root, "Title", "체험 방법", new Vector2(0, 420), new Vector2(1200, 90), 60, TextColor, true);

        string[] heads = { "발전하기", "송전하기", "판매하기" };
        string[] bodies =
        {
            "손잡이를 잡고 쳇바퀴를 달려요.\n빨리 달릴수록 전기를\n많이 만들어요.",
            $"만든 전기를 {Accent("낮은 전압")}과\n{Accent("높은 전압")} 중 하나로\n골라 보내요.",
            "도착한 전기를 팔아요.\n발전왕과 판매왕 순위를\n확인해요.",
        };
        for (int i = 0; i < heads.Length; i++)
        {
            float x = -580 + i * 580;
            RectTransform box = CreateRoundBox(root, $"Box_Step{i + 1}", new Vector2(x, 50), new Vector2(520, 500), CardColor);
            RectTransform badge = CreateRoundBox(box, "Badge", new Vector2(0, 170), new Vector2(100, 100), AccentColor);
            FillText(badge, "Text", $"{i + 1}", 56, DarkTextColor, true);
            CreateText(box, "Head", heads[i], new Vector2(0, 65), new Vector2(480, 70), 44, TextColor, true);
            CreateText(box, "Body", bodies[i], new Vector2(0, -95), new Vector2(470, 200), 30, SubTextColor);
            if (i < heads.Length - 1)
                CreateText(root, $"Arrow_{i + 1}", ">", new Vector2(x + 290, 50), new Vector2(60, 80), 56, AccentColor, true);
        }

        RectTransform notice = CreateRoundBox(root, "NoticeBox", new Vector2(0, -285), new Vector2(1500, 70), SoftBoxColor);
        FillText(notice, "Text", "본 체험은 발전과 송전의 원리를 쉽게 이해하기 위한 체험용 가상모델입니다.", 26, TextColor);
    }

    private static void Countdown(RectTransform root)
    {
        Background(root);
        CreateText(root, "Title", "손잡이를 잡고 달릴 준비를 하세요!", new Vector2(0, 330), new Vector2(1600, 90), 56, TextColor, true);
        CreateText(root, "Hint", "START가 나오면 쳇바퀴를 굴려 주세요", new Vector2(0, -380), new Vector2(1200, 50), 30, SubTextColor);
    }

    private static void Run(RectTransform root)
    {
        Background(root);
        Title(root, "전기를 만들고 있어요!");

        LabeledBox(root, "Box_Speed", SpeedBoxCenter, BigBoxSize, "현재 속도", "km/h", 150, -110);
        LabeledBox(root, "Box_Power", PowerBoxCenter, BigBoxSize, "발전 출력", "W", 150, -110);

        string[] labels = { "누적 발전량", "달린 거리", "남은 시간" };
        string[] units = { "Wh", "m", "" };
        for (int i = 0; i < labels.Length; i++)
            LabeledBox(root, $"Box_Small{i + 1}", new Vector2(SmallBoxX[i], SmallBoxY), SmallBoxSize, labels[i], units[i], 70, SmallUnitY);

        Notice(root, "※ 발전량은 쳇바퀴 회전속도를 기준으로 환산한 체험용 수치입니다.");
    }

    // 결과 기록(왼쪽) + 오늘 랭킹(오른쪽)
    private static void RecordAndRank(RectTransform root, string title, string[] labels, string[] units, string rankHeader)
    {
        Background(root);
        Title(root, title);

        RectTransform record = CreateRoundBox(root, "Box_Record", RecordBoxCenter, ResultBoxSize, CardColor);
        for (int i = 0; i < labels.Length; i++)
        {
            CreateText(record, $"Label_{i + 1}", labels[i], new Vector2(-190, RecordRowY[i]), new Vector2(440, 60), 34, SubTextColor, true,
                TextAlignmentOptions.MidlineLeft);
            if (!string.IsNullOrEmpty(units[i]))
                CreateText(record, $"Unit_{i + 1}", units[i], new Vector2(RecordUnitX, RecordRowY[i] - 10), new Vector2(60, 50), 28, SubTextColor, false,
                    TextAlignmentOptions.MidlineLeft);
            if (i < labels.Length - 1)
                AddImage(Place(CreateRect($"Line_{i + 1}", record), new Vector2(0, RecordRowY[i] - 75), new Vector2(800, 2)), SoftBoxColor);
        }

        RectTransform rank = CreateRoundBox(root, "Box_Rank", RankBoxCenter, ResultBoxSize, CardColor);
        CreateText(rank, "Header", rankHeader, new Vector2(0, 285), new Vector2(800, 70), 40, TextColor, true);
    }

    private static void Voltage(RectTransform root)
    {
        Background(root);
        Title(root, "어떤 전압으로 보낼까요?");

        RectTransform send = CreateRoundBox(root, "Box_Send", SendBoxCenter, SendBoxSize, CardColor);
        CreateText(send, "Label", "보낼 전력", new Vector2(-380, 0), new Vector2(360, 60), 38, SubTextColor, true, TextAlignmentOptions.MidlineLeft);
        CreateText(send, "Unit", "Wh", new Vector2(500, -10), new Vector2(120, 50), 34, SubTextColor, false, TextAlignmentOptions.MidlineLeft);

        Notice(root, "※ 전압은 송전 특성을 비교하기 위한 가상 조건입니다.");
    }

    private static void Transmit(RectTransform root)
    {
        Background(root);
        Title(root, "전기를 보내고 있어요");

        // 발전소: 건물 + 굴뚝
        RectTransform plant = CreateRoundBox(root, "Site_Plant", PlantPos, SiteSize, CardColor);
        AddImage(Place(CreateRect("Building", plant), new Vector2(-20, -20), new Vector2(160, 110)), SubTextColor);
        AddImage(Place(CreateRect("Chimney", plant), new Vector2(70, 30), new Vector2(36, 140)), SubTextColor);
        CreateText(plant, "Label", "발전소", new Vector2(0, -110), new Vector2(260, 50), 32, TextColor, true);

        // 도시: 높이가 다른 건물들
        RectTransform city = CreateRoundBox(root, "Site_City", CityPos, SiteSize, CardColor);
        float[] heights = { 90, 150, 120, 70 };
        for (int i = 0; i < heights.Length; i++)
            AddImage(Place(CreateRect($"Tower_{i + 1}", city), new Vector2(-75 + i * 50, -55 + heights[i] * 0.5f), new Vector2(40, heights[i])), SubTextColor);
        CreateText(city, "Label", "도시", new Vector2(0, -110), new Vector2(260, 50), 32, TextColor, true);

        string[] labels = { "출발한 전력", "송전 중 손실", "도착한 전력" };
        for (int i = 0; i < labels.Length; i++)
            LabeledBox(root, $"Box_Flow{i + 1}", new Vector2(FlowBoxX[i], FlowBoxY), FlowBoxSize, labels[i], "Wh", 70, SmallUnitY);

        Notice(root, "※ 실제 송전손실은 전압과 거리, 전선 등 여러 조건에 따라 달라질 수 있습니다.");
    }

    private static void Summary(RectTransform root)
    {
        Background(root);
        Title(root, "나의 체험 결과");

        LabeledBox(root, "Box_Energy", EnergySummaryCenter, SummaryBoxSize, "발전 기록", "Wh", 200, -75);
        LabeledBox(root, "Box_Sale", SaleSummaryCenter, SummaryBoxSize, "판매 기록", "원", 200, -75);
    }

    private static void End(RectTransform root)
    {
        Background(root);
        CreateText(root, "Title", "참여해 주셔서 감사합니다!", new Vector2(0, 80), new Vector2(1600, 110), 80, AccentColor, true);
        CreateText(root, "Body", "높은 전압으로 보내면 송전 중 손실은 줄지만 변전 설비가 필요해요.\n많은 전기를 멀리 보낼수록 높은 전압이 유리하답니다.",
            new Vector2(0, -70), new Vector2(1600, 120), 34, TextColor);
    }
}
