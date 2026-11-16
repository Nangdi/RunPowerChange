using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 랭킹 표 하나 (발전왕 또는 판매왕). 디자인 그림의 줄 칸에 맞춰 둔 글자들에 기록 시각과 값을 채운다.
// 32형 랭킹 화면과 85형 관전 화면이 같이 쓴다. 나의 순위 줄은 비워두면 표시하지 않는다.
// 85형은 회차가 끝나 종합 순위를 발표하는 동안만 아래 줄(4·5위 자리)을 PLAYER 01/02 의 방금 기록 줄로 쓴다(SetRow).
// 혼자 체험했으면 1~4위 기록 + 5번째 줄 PLAYER.
// 평소(Set)에는 1~5위 기록을 그대로 보여 주고 PLAYER 줄 강조를 걷어 낸다. 4·5줄 순위 숫자는 그림이 아니라 rankTexts 글자다.
public class RankTableView : MonoBehaviour
{
    [Tooltip("1위부터 순서대로 줄의 기록 시각")]
    [SerializeField] private TMP_Text[] timeTexts;
    [Tooltip("1위부터 순서대로 줄의 값 (발전량 Wh 또는 판매 금액)")]
    [SerializeField] private TMP_Text[] valueTexts;
    [Tooltip("줄 순위 숫자 글자 (그림에 숫자가 없는 줄만, 나머지 칸은 비워 둠). 비워두면 표시하지 않음")]
    [SerializeField] private TMP_Text[] rankTexts;

    [Header("PLAYER 줄 강조 (85형)")]
    [Tooltip("줄 바탕 띠 (PLAYER 줄만, 나머지 칸은 비워 둠). 레인 색을 bandAlpha 만큼 옅게 칠한다")]
    [SerializeField] private Image[] rowBands;
    [Tooltip("순위 숫자 뒤 동그라미 (PLAYER 줄만). 레인 색으로 칠하고 숫자는 흰색")]
    [SerializeField] private Image[] rankBadges;
    [Range(0f, 1f)]
    [SerializeField] private float bandAlpha = 0.22f;
    [Tooltip("PLAYER 이름 글꼴 (가장 굵은 글꼴). 비워두면 기록 시각 글꼴 그대로")]
    [SerializeField] private TMP_FontAsset playerLabelFont;
    [SerializeField] private float playerLabelSize = 42f;
    [Tooltip("동그라미 위 순위 숫자 색")]
    [SerializeField] private Color badgeTextColor = Color.white;

    // 평소 줄 모양 (1위 줄 기록 시각 글자에서 읽음). PLAYER 줄로 썼던 줄을 되돌릴 때 쓴다.
    private bool styleRead;
    private TMP_FontAsset normalFont;
    private FontStyles normalStyle;
    private float normalSize;
    private Color normalColor;

    private void ReadNormalStyle()
    {
        if (styleRead || RowCount == 0 || timeTexts[0] == null) return;
        styleRead = true;
        TMP_Text t = timeTexts[0];
        normalFont = t.font;
        normalStyle = t.fontStyle;
        normalSize = t.enableAutoSizing ? t.fontSizeMax : t.fontSize;
        normalColor = t.color;
    }

    private void SetLabelStyle(TMP_Text t, TMP_FontAsset font, FontStyles style, float size, Color color)
    {
        if (t == null) return;
        if (font != null && t.font != font) t.font = font;
        t.fontStyle = style;
        t.fontSizeMax = size;
        t.fontSize = size;
        t.color = color;
    }

    // 줄 하나의 PLAYER 강조를 켜고 끈다
    private void SetEmphasis(int row, bool on, Color accent)
    {
        ReadNormalStyle();
        if (!styleRead) return;
        if (on) SetLabelStyle(timeTexts[row], playerLabelFont != null ? playerLabelFont : normalFont, normalStyle | FontStyles.Bold, playerLabelSize, accent);
        else SetLabelStyle(timeTexts[row], normalFont, normalStyle, normalSize, normalColor);
        Image band = rowBands != null && row < rowBands.Length ? rowBands[row] : null;
        if (band != null)
        {
            band.enabled = on;
            band.color = new Color(accent.r, accent.g, accent.b, bandAlpha);
        }
        Image badge = rankBadges != null && row < rankBadges.Length ? rankBadges[row] : null;
        if (badge != null)
        {
            badge.enabled = on;
            badge.color = accent;
        }
        if (rankTexts != null && row < rankTexts.Length && rankTexts[row] != null)
            rankTexts[row].color = on && badge != null ? badgeTextColor : normalColor;
    }

    [Header("나의 순위")]
    [Tooltip("비워두면 표시하지 않음 (85형)")]
    [SerializeField] private TMP_Text myRankText;
    [Tooltip("비워두면 표시하지 않음 (85형)")]
    [SerializeField] private TMP_Text myTimeText;
    [Tooltip("비워두면 표시하지 않음 (85형)")]
    [SerializeField] private TMP_Text myValueText;

    public int RowCount => timeTexts != null ? timeTexts.Length : 0;

    public void Set(WheelRankingStore.Kind kind, List<PowerRecord> top, PowerRecord mine, int myRank)
    {
        for (int i = 0; i < RowCount; i++)
        {
            PowerRecord r = top != null && i < top.Count ? top[i] : null;
            SetText(timeTexts[i], r != null ? PowerFormat.RecordTime(r.Time) : "-");
            if (valueTexts != null && i < valueTexts.Length) SetText(valueTexts[i], r != null ? Value(kind, r) : "");
            if (rankTexts != null && i < rankTexts.Length) SetText(rankTexts[i], (i + 1).ToString());
            SetEmphasis(i, false, normalColor);
        }

        SetText(myRankText, mine != null ? myRank.ToString() : "");
        SetText(myTimeText, mine != null ? PowerFormat.RecordTime(mine.Time) : "");
        SetText(myValueText, mine != null ? Value(kind, mine) : "");
    }

    // 줄 하나를 PLAYER 줄로 채운다 (85형 종합 순위 발표). record 가 null 이면 기록 없음(-)으로 둔다. Set 다음에 부른다.
    // accent 는 그 플레이어의 레인 색: 이름 글자, 순위 동그라미, 줄 바탕 띠를 칠한다 (정렬로 줄이 바뀌어도 색이 사람을 따라간다).
    public void SetRow(int row, WheelRankingStore.Kind kind, string label, PowerRecord record, int rank, Color accent)
    {
        if (row < 0 || row >= RowCount) return;
        SetText(timeTexts[row], label);
        if (valueTexts != null && row < valueTexts.Length) SetText(valueTexts[row], record != null ? Value(kind, record) : "-");
        if (rankTexts != null && row < rankTexts.Length) SetText(rankTexts[row], record != null && rank > 0 ? rank.ToString() : "-");
        SetEmphasis(row, true, accent);
    }

    // 발전왕은 화면에 보이는 누적 환산 발전량(Wh), 판매왕은 판매 금액(원)
    private static string Value(WheelRankingStore.Kind kind, PowerRecord r) =>
        kind == WheelRankingStore.Kind.Energy ? PowerFormat.Wh(r.scaledEnergyWh) : PowerFormat.Won(r.saleWon);

    private static void SetText(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
