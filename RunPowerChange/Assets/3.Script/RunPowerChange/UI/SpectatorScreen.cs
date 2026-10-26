using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 85형 관전 화면 (세로). Canvas_Spectator 프리팹 루트에 붙는다.
//  - Player 01/02 칸: SpectatorPaneView + RaceCamera(viewport)
//  - 아래: 두 쳇바퀴의 오늘 판매왕 랭킹을 합친 표
//  - 새 기록이 표 안에 들면 랭킹 업데이트 팝업
// 캔버스 출력 디스플레이는 Setting.json spectatorDisplayIndex 를 따른다.
public class SpectatorScreen : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private WheelSession[] sessions;

    [Header("랭킹 표")]
    [SerializeField] private RankRowView rowPrefab;
    [SerializeField] private Transform rowRoot;

    [Header("새 랭킹 팝업")]
    [SerializeField] private GameObject popup;
    [SerializeField] private TMP_Text popupPlayerText;
    [SerializeField] private TMP_Text popupRankText;
    [SerializeField] private TMP_Text popupSpeedText;
    [SerializeField] private TMP_Text popupEnergyText;
    [SerializeField] private TMP_Text popupLossText;
    [SerializeField] private TMP_Text popupArrivedText;
    [SerializeField] private TMP_Text popupSaleText;

    private readonly List<RankRowView> rows = new List<RankRowView>();
    private float popupHideTime;
    private float nextRefreshTime;

    private void Start()
    {
        if (canvas != null) canvas.targetDisplay = Mathf.Max(0, JsonManager.instance.gameDynamicData.spectatorDisplayIndex);
        foreach (var s in sessions)
            if (s != null) s.OnRecordAdded += HandleRecordAdded;
        if (popup != null) popup.SetActive(false);
    }

    private void OnDestroy()
    {
        foreach (var s in sessions)
            if (s != null) s.OnRecordAdded -= HandleRecordAdded;
    }

    private void Update()
    {
        // 세션 랭킹은 WheelSession.Start 에서 만들어지고 날짜가 바뀌면 비워지므로 주기적으로 다시 그린다
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 10f;
            RefreshRanking();
        }
        if (popup != null && popup.activeSelf && Time.unscaledTime >= popupHideTime) popup.SetActive(false);
    }

    private IEnumerable<WheelRankingStore> Stores()
    {
        foreach (var s in sessions)
            if (s != null && s.Ranking != null) yield return s.Ranking;
    }

    private List<PowerRecord> Top() => WheelRankingStore.MergeTop(Stores(), WheelRankingStore.Kind.Sale,
        JsonManager.instance.gameDynamicData.rankingShowCount);

    private void RefreshRanking() => RankTable.Fill(rows, rowPrefab, rowRoot, null, WheelRankingStore.Kind.Sale, Top(), null, 0);

    private void HandleRecordAdded(WheelSession session, PowerRecord record)
    {
        List<PowerRecord> top = Top();
        RankTable.Fill(rows, rowPrefab, rowRoot, null, WheelRankingStore.Kind.Sale, top, top.Contains(record) ? record : null, 0);
        nextRefreshTime = Time.unscaledTime + Mathf.Max(10f, JsonManager.instance.gameDynamicData.rankingPopupSeconds);

        int index = top.IndexOf(record);
        if (index < 0 || popup == null) return;

        SetText(popupPlayerText, $"Player {session.WheelId:00}");
        SetText(popupRankText, $"{index + 1}");
        SetText(popupSpeedText, PowerFormat.Speed(record.maxSpeedKmh));
        SetText(popupEnergyText, PowerFormat.Kwh(record.scaledEnergyWh));
        SetText(popupLossText, $"-{PowerFormat.Kwh(record.lossWh)}");
        SetText(popupArrivedText, PowerFormat.Kwh(record.arrivedWh));
        SetText(popupSaleText, PowerFormat.Won(record.saleWon));
        popup.SetActive(true);
        popupHideTime = Time.unscaledTime + Mathf.Max(1f, JsonManager.instance.gameDynamicData.rankingPopupSeconds);
    }

    private static void SetText(TMP_Text t, string value)
    {
        if (t != null) t.text = value;
    }
}
