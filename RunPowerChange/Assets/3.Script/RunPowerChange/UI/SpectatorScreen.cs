using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 85형 관전 화면 (세로 2160x3840). Canvas_Spectator 프리팹 루트에 붙는다.
//  - 위: PLAYER 01/02 상태(모집 중 / 참가 완료 / 체험중 / 승리! …)와 누적 환산 발전량. 뒤의 3D 장면은 RaceCamera(Spectator)가 그린다.
//  - 가운데: "오늘 최고 랭킹" 표시가 3D 최고 랭킹 선(RaceStage.RecordLine)을 따라다닌다.
//  - 아래 칸: 오늘의 발전왕·판매왕 순위 → 왜 그럴까요? → 우리와 어떤 관련이 있을까요? 가 spectatorInfoSeconds 마다 바뀐다.
//    설명 그림은 현장에서 바꿀 수 있게 StreamingAssets/Spectator/info_1.png, info_2.png 를 읽는다 (없으면 기본 그림, 로그만 남김).
//  - 가운데 아래 안내 카드(SpectatorNotice): 관람객이 지금 상황을 알 수 있게 회차 단계를 알려 준다.
//      참가자 모집(Open) → 경기 준비(Ready) → 체험 방법 안내(Guide) → 카운트다운 3·2·1·GO(가운데 큰 숫자) → 달리기(카드 없음)
//      → 경기 결과: 승자 발표(1인 체험은 1위 기록 도전 결과, spectatorWinnerSeconds) → 결과 집계 중(선수들이 전압 선택·판매 중)
//      → 참가자가 모두 랭킹 화면에 가면 종합 순위 발표(아래 칸을 랭킹으로 고정, 회차가 먼저 끝나도 spectatorRankingSeconds 동안)
//    2인 경주 승자는 누적 환산 발전량(화면 위 wh 숫자)이 큰 쪽이다. 보이는 숫자가 같으면 무승부.
//    1인 체험은 같이 달린 1위 기록(고스트)과 같은 기준(달린 거리)으로 돌파했는지 본다.
// 랭킹은 두 쳇바퀴의 기록을 합쳐(종합순위) 보여 준다. 캔버스 출력 디스플레이는 Setting.json spectatorDisplayIndex 를 따른다.
public class SpectatorScreen : MonoBehaviour
{
    // 승자 발표 시작 (소리 연결용)
    public static event System.Action WinnerAnnounced;
    // 종합 순위 발표 시작 (소리 연결용)
    public static event System.Action RankingRevealed;

    [SerializeField] private Canvas canvas;
    [SerializeField] private WheelSession[] sessions;

    [Header("PLAYER 01/02 (sessions 와 같은 순서)")]
    [SerializeField] private TMP_Text[] statusTexts;
    [SerializeField] private TMP_Text[] energyTexts;

    [Header("오늘 최고 랭킹 표시")]
    [Tooltip("3D 선 위치를 화면 좌표로 바꿀 85형 카메라")]
    [SerializeField] private Camera raceCamera;
    [SerializeField] private RectTransform recordTag;
    [Tooltip("표시 그림에서 선과 닿는 점(아래 끝)의 피벗 기준 높이 비율. 0 = 그림 아래 끝")]
    [SerializeField] private float recordTagAnchorY = 0f;

    [Header("아래 칸 (나오는 순서)")]
    [SerializeField] private CanvasGroup[] bottomGroups;
    [SerializeField] private float fadeSeconds = 0.5f;

    [Header("설명 그림 (아래 칸 두 번째부터, StreamingAssets/Spectator/info_n.png 로 교체)")]
    [SerializeField] private Image[] infoImages;

    [Header("랭킹 (아래 칸 첫 번째)")]
    [SerializeField] private RankTableView energyTable;
    [SerializeField] private RankTableView saleTable;

    [Header("가운데 아래 안내 카드·카운트다운")]
    [Tooltip("비워두면 표시하지 않음")]
    [SerializeField] private SpectatorNotice notice;
    [Tooltip("달리기 시작 후 GO 를 보여 주는 시간(초)")]
    [SerializeField] private float goSeconds = 1f;

    private float nextRefreshTime;
    private int bottomIndex;
    private float bottomTime;
    private int rankingVersion = -1;

    // 이번 회차 결과 (달리기가 끝날 때 고정). 체험자가 대기화면으로 돌아가 값이 지워져도 발표를 이어 간다.
    private class Result
    {
        public int wheelId;
        public float scaledWh;
        public float distanceM;
        public bool reachedRanking;   // 랭킹 화면까지 감 (기록 저장 완료)
        public bool abandoned;        // 전압을 고르지 않고 떠남 (기록 없음)
        public int energyRank;
        public int saleRank;
    }

    private readonly List<Result> results = new List<Result>();
    private float resultTime;          // 달리기가 끝난 시각
    private float revealTime = -1f;    // 종합 순위 발표 시각 (-1 = 아직)
    private int revealTotal;           // 발표 때 오늘 전체 체험자 수
    private RaceTrack resultLeader;    // 1인 체험이 도전한 1위 기록
    private bool resultLeaderPreviousDay;
    private GameRound.Phase lastPhase = (GameRound.Phase)(-1);

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    private void Start()
    {
        ApplyDisplay();
        DisplayRouting.Changed += ApplyDisplay;
        foreach (var s in sessions)
            if (s != null) s.OnRecordAdded += HandleRecordAdded;
        bottomTime = Time.unscaledTime;
        ShowBottom(0, true);
        LoadInfoImages();
    }

    private const string InfoFolder = "Spectator";

    // 설명 그림을 StreamingAssets 파일로 바꾼다. 파일이 없거나 깨져 있으면 기본 그림을 그대로 쓰고 로그만 남긴다.
    private void LoadInfoImages()
    {
        for (int i = 0; infoImages != null && i < infoImages.Length; i++)
        {
            if (infoImages[i] == null) continue;
            string path = Path.Combine(Application.streamingAssetsPath, InfoFolder, $"info_{i + 1}.png");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Spectator] 설명 그림 없음 - 기본 그림 사용: {path}");
                continue;
            }
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                {
                    Debug.LogWarning($"[Spectator] 설명 그림을 읽지 못함 - 기본 그림 사용: {path}");
                    Destroy(tex);
                    continue;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                infoImages[i].sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                infoImages[i].preserveAspect = true;
                Debug.Log($"[Spectator] 설명 그림 {i + 1}: {path} ({tex.width}x{tex.height})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Spectator] 설명 그림 오류 - 기본 그림 사용: {path} ({e.Message})");
            }
        }
    }

    private void ApplyDisplay()
    {
        if (canvas != null) DisplayRouting.ApplyTo(canvas, Config.spectatorDisplayIndex);
    }

    private void OnDestroy()
    {
        DisplayRouting.Changed -= ApplyDisplay;
        foreach (var s in sessions)
            if (s != null) s.OnRecordAdded -= HandleRecordAdded;
    }

    private void Update()
    {
        // 앱 종료 중에는 설정 관리자가 먼저 사라질 수 있다
        if (JsonManager.instance == null) return;
        UpdateRound();
        UpdatePlayers();
        UpdateRecordTag();
        UpdateBottom();
        UpdateNotice();

        // 세션 랭킹은 WheelSession.Start 에서 만들어지고 날짜가 바뀌면 비워지므로 주기적으로 다시 그린다. 기록이 바뀌면 바로 그린다.
        if (Time.unscaledTime >= nextRefreshTime || rankingVersion != WheelRankingStore.Version)
        {
            nextRefreshTime = Time.unscaledTime + 10f;
            RefreshRanking();
        }
    }

    // ── 회차 결과 추적 ─────────────────────────────────

    private void UpdateRound()
    {
        var round = GameRound.Instance;
        if (round == null) return;

        if (round.Current != lastPhase)
        {
            lastPhase = round.Current;
            if (round.Current == GameRound.Phase.Post) BeginResult(round);
            else if (round.Current != GameRound.Phase.Idle) ClearResult();   // 새 회차가 시작되면 지난 발표는 끝
        }
        if (results.Count == 0) return;

        foreach (var r in results)
        {
            WheelSession s = Session(r.wheelId);
            if (s == null || r.abandoned) continue;
            if (s.Current == WheelSession.State.Ranking)
            {
                // 같은 회차의 다른 체험자가 나중에 저장하면 순위가 바뀌므로 랭킹 화면에 있는 동안 계속 읽는다
                r.reachedRanking = true;
                r.energyRank = s.EnergyRank;
                r.saleRank = s.SaleRank;
            }
            else if (s.Current == WheelSession.State.Idle && !r.reachedRanking)
            {
                r.abandoned = true;
                Debug.Log($"[Spectator] PLAYER {r.wheelId:00} 기록 없이 종료 - 종합 순위 발표에서 뺌");
            }
        }

        if (revealTime < 0f && Time.unscaledTime - resultTime >= Config.spectatorWinnerSeconds && AllRanked())
        {
            revealTime = Time.unscaledTime;
            revealTotal = WheelRankingStore.TotalCount;
            ShowBottom(0, false);
            var line = new StringBuilder($"[Spectator] 종합 순위 발표 (오늘 {revealTotal}명)");
            foreach (var r in results)
                if (r.reachedRanking) line.Append($" / PLAYER {r.wheelId:00} 발전 {r.energyRank}위 판매 {r.saleRank}위");
            Debug.Log(line.ToString());
            RankingRevealed?.Invoke();
        }

        // 회차가 끝났고 발표 시간도 지났으면 결과를 지운다
        if (round.Current == GameRound.Phase.Idle)
        {
            bool rankingShowing = revealTime >= 0f && Time.unscaledTime - revealTime < Mathf.Max(0f, Config.spectatorRankingSeconds);
            bool winnerShowing = Time.unscaledTime - resultTime < Config.spectatorWinnerSeconds;
            if (!rankingShowing && !winnerShowing) ClearResult();
        }
    }

    private void BeginResult(GameRound round)
    {
        ClearResult();
        foreach (var p in round.Participants)
        {
            if (p == null) continue;
            results.Add(new Result { wheelId = p.WheelId, scaledWh = p.ScaledEnergyWh, distanceM = p.DistanceM });
        }
        if (results.Count == 0) return;
        results.Sort((a, b) => a.wheelId.CompareTo(b.wheelId));
        resultTime = Time.unscaledTime;
        resultLeader = round.IsSolo ? round.Leader : null;
        resultLeaderPreviousDay = round.LeaderFromPreviousDay;
        Debug.Log($"[Spectator] 경기 결과: {ResultLog()}");
        WinnerAnnounced?.Invoke();
    }

    private void ClearResult()
    {
        results.Clear();
        revealTime = -1f;
        resultLeader = null;
    }

    // 기록을 남긴 참가자가 1명 이상이고, 떠나지 않은 참가자가 모두 랭킹 화면까지 감
    private bool AllRanked()
    {
        bool any = false;
        foreach (var r in results)
        {
            if (r.abandoned) continue;
            if (!r.reachedRanking) return false;
            any = true;
        }
        return any;
    }

    // 2인 경주 승자 wheelId. 무승부면 0, 1인 체험이면 -1.
    private int WinnerId()
    {
        if (results.Count < 2) return -1;
        Result best = null;
        bool tie = false;
        foreach (var r in results)
        {
            if (best == null) { best = r; continue; }
            int c = CompareShown(r.scaledWh, best.scaledWh);
            if (c > 0) { best = r; tie = false; }
            else if (c == 0) tie = true;
        }
        return tie ? 0 : best.wheelId;
    }

    // 화면 위 wh 숫자로 비교한다 (보이는 숫자가 같은데 한쪽이 이기는 일이 없게)
    private static int CompareShown(float a, float b) => PowerFormat.Wh(a) == PowerFormat.Wh(b) ? 0 : a.CompareTo(b);

    // 1인 체험: 같이 달린 1위 기록(고스트)보다 멀리 달렸는지
    private bool SoloBeatLeader() => resultLeader != null && results.Count == 1 && results[0].distanceM > resultLeader.distanceM;

    private string ResultLog()
    {
        var sb = new StringBuilder();
        foreach (var r in results) sb.Append($"PLAYER {r.wheelId:00} {PowerFormat.Wh(r.scaledWh)}wh {r.distanceM:0}m, ");
        int w = WinnerId();
        if (w > 0) sb.Append($"PLAYER {w:00} 승리");
        else if (w == 0) sb.Append("무승부");
        else if (resultLeader != null) sb.Append($"1위 기록 {resultLeader.distanceM:0}m {(SoloBeatLeader() ? "돌파" : "미달")}");
        else sb.Append("첫 기록");
        return sb.ToString();
    }

    private Result ResultOf(int wheelId) => results.Find(r => r.wheelId == wheelId);

    private WheelSession Session(int wheelId)
    {
        foreach (var s in sessions)
            if (s != null && s.WheelId == wheelId) return s;
        return null;
    }

    // ── PLAYER 01/02 칸 ──────────────────────────────────

    private void UpdatePlayers()
    {
        var round = GameRound.Instance;
        int winner = WinnerId();
        for (int i = 0; i < sessions.Length; i++)
        {
            var s = sessions[i];
            if (s == null) continue;
            Result r = ResultOf(s.WheelId);
            if (r != null)
            {
                // 경기 결과 발표 중: 체험자가 대기화면으로 돌아가도 결과 값을 유지
                SetText(statusTexts, i, winner > 0 ? (winner == s.WheelId ? "승리!" : "완주") : winner == 0 ? "무승부" : "완주");
                SetText(energyTexts, i, PowerFormat.Wh(r.scaledWh));
                continue;
            }
            bool playing = s.Current != WheelSession.State.Idle;
            SetText(statusTexts, i, StatusOf(round, s, playing));
            SetText(energyTexts, i, playing ? PowerFormat.Wh(s.ScaledEnergyWh) : "0.0");
        }
    }

    private static string StatusOf(GameRound round, WheelSession s, bool playing)
    {
        if (round == null) return playing ? "체험중" : "체험 대기중";
        bool joined = round.IsParticipant(s);
        switch (round.Current)
        {
            case GameRound.Phase.Open: return joined ? "참가 완료" : "모집 중";
            case GameRound.Phase.Ready:
            case GameRound.Phase.Guide: return joined ? "준비 중" : "체험 대기중";
            default: return playing ? "체험중" : "체험 대기중";
        }
    }

    // ── 가운데 아래 안내 카드 ─────────────────────────────

    private void UpdateNotice()
    {
        if (notice == null) return;
        var round = GameRound.Instance;
        if (round == null)
        {
            notice.HideCard();
            notice.ShowCountdown(-1);
            return;
        }

        // 카운트다운 (3, 2, 1 → 달리기 시작 후 goSeconds 동안 GO)
        if (round.Current == GameRound.Phase.Countdown) notice.ShowCountdown(Mathf.Max(1, round.CountdownNumber));
        else if (round.Current == GameRound.Phase.Run && round.RunElapsed < goSeconds) notice.ShowCountdown(0);
        else notice.ShowCountdown(-1);

        switch (round.Current)
        {
            case GameRound.Phase.Open:
                notice.ShowCard("참가자 모집", "함께 달릴 참가자를 모집 중입니다!",
                    "비어 있는 쳇바퀴 화면에서 참여하기를 누르면\n같이 달릴 수 있어요", Seconds(round.JoinRemaining));
                return;

            case GameRound.Phase.Ready:
                ShowReady(round);
                return;

            case GameRound.Phase.Guide:
                notice.ShowCard("체험 방법 안내", "선수들이 체험 방법을 확인하고 있어요",
                    "달려서 전기를 만들고, 송전 방식을 골라 판매해요\n잠시 후 경기가 시작됩니다",
                    Seconds(Config.guideSeconds - round.PhaseElapsed));
                return;

            case GameRound.Phase.Countdown:
            case GameRound.Phase.Run:
                notice.HideCard();
                return;
        }

        // Post, 또는 회차가 끝난 뒤 남은 발표
        if (results.Count == 0) notice.HideCard();
        else if (Time.unscaledTime - resultTime < Config.spectatorWinnerSeconds) ShowWinner();
        else if (revealTime >= 0f) ShowRanking();
        else if (round.Current == GameRound.Phase.Post && HasPending())
            notice.ShowCard("결과 집계", "선수들이 만든 전기를 판매하고 있어요", "잠시 후 오늘의 종합 순위가 발표됩니다");
        else notice.HideCard();
    }

    private bool HasPending()
    {
        foreach (var r in results)
            if (!r.abandoned) return true;
        return false;
    }

    private void ShowReady(GameRound round)
    {
        string count = Seconds(round.ReadyRemaining);
        var ps = round.Participants;
        if (ps.Count >= 2)
        {
            notice.ShowCard("경기 준비", $"{notice.Player(ps[0].WheelId)}  VS  {notice.Player(ps[1].WheelId)}",
                "두 선수의 대결이 곧 시작됩니다!\n누가 더 많은 전기를 만들까요?", count);
        }
        else if (ps.Count == 1 && round.Leader != null)
        {
            string day = round.LeaderFromPreviousDay ? "전날" : "오늘의";
            notice.ShowCard("경기 준비", $"{notice.Player(ps[0].WheelId)}, {day} 1위 기록에 도전!",
                $"{day} 1위 기록과 함께 달립니다\n곧 경기가 시작됩니다", count);
        }
        else if (ps.Count == 1)
        {
            notice.ShowCard("경기 준비", $"{notice.Player(ps[0].WheelId)} 도전 시작!",
                "오늘의 첫 기록에 도전합니다\n곧 경기가 시작됩니다", count);
        }
    }

    private void ShowWinner()
    {
        if (results.Count >= 2)
        {
            int winner = WinnerId();
            var sb = new StringBuilder();
            for (int i = 0; i < results.Count; i++)
            {
                if (i > 0) sb.Append("   VS   ");
                sb.Append($"{notice.Player(results[i].wheelId)} {PowerFormat.Wh(results[i].scaledWh)} wh");
            }
            sb.Append("\n두 선수 모두 수고했어요!");
            notice.ShowCard("경기 결과", winner > 0 ? $"{notice.Player(winner)} 승리!" : "무승부!", sb.ToString());
            return;
        }

        Result r = results[0];
        string me = $"{notice.Player(r.wheelId)} {PowerFormat.Wh(r.scaledWh)} wh";
        string day = resultLeaderPreviousDay ? "전날" : "오늘의";
        if (resultLeader == null)
            notice.ShowCard("경기 결과", "오늘의 첫 기록 달성!", me + "\n완주를 축하합니다!");
        else if (SoloBeatLeader())
            notice.ShowCard("경기 결과", $"{day} 1위 기록 돌파!", me + "\n최고 기록보다 더 멀리 달렸어요!");
        else
            notice.ShowCard("경기 결과", "완주! 멋진 도전이었어요", me + $"\n{day} 1위 기록에 아깝게 못 미쳤어요");
    }

    private void ShowRanking()
    {
        var sb = new StringBuilder();
        foreach (var r in results)
        {
            if (!r.reachedRanking) continue;
            if (sb.Length > 0) sb.Append('\n');
            sb.Append($"{notice.Player(r.wheelId)}   발전왕 {r.energyRank}위  ·  판매왕 {r.saleRank}위");
        }
        notice.ShowCard("종합 순위 발표", $"오늘 체험자 {revealTotal}명 중 종합 순위", sb.ToString());
    }

    private static string Seconds(float remain) => remain > 0f ? $"{Mathf.CeilToInt(remain)}초" : "";

    // ── 오늘 최고 랭킹 표시 ──────────────────────────────

    private void UpdateRecordTag()
    {
        if (recordTag == null) return;
        var stage = RaceStage.Instance;
        bool show = stage != null && stage.RecordLineVisible && raceCamera != null;
        if (recordTag.gameObject.activeSelf != show) recordTag.gameObject.SetActive(show);
        if (!show) return;

        // 선의 오른쪽 끝 위에 표시의 아래 끝이 오도록 화면 좌표 → 캔버스 좌표로 옮긴다
        Vector3 screen = raceCamera.WorldToScreenPoint(stage.RecordLineTagPoint);
        var parent = (RectTransform)recordTag.parent;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local)) return;
        float offsetY = (recordTag.pivot.y - recordTagAnchorY) * recordTag.rect.height;
        recordTag.anchoredPosition = new Vector2(recordTag.anchoredPosition.x, local.y + offsetY - LocalCenterOffset(parent));
    }

    // 표시의 앵커가 부모 가운데라 local 좌표(부모 피벗 기준)와 anchoredPosition 의 차이를 맞춘다
    private static float LocalCenterOffset(RectTransform parent) => (0.5f - parent.pivot.y) * parent.rect.height;

    // ── 아래 칸 ─────────────────────────────────────────

    private void UpdateBottom()
    {
        if (bottomGroups == null || bottomGroups.Length == 0) return;
        float interval = Mathf.Max(1f, Config.spectatorInfoSeconds);
        if (revealTime >= 0f && results.Count > 0)
        {
            // 종합 순위 발표 중에는 랭킹 칸에 고정한다
            bottomTime = Time.unscaledTime;
            if (bottomIndex != 0) ShowBottom(0, false);
        }
        else if (Time.unscaledTime - bottomTime >= interval)
        {
            bottomTime = Time.unscaledTime;
            ShowBottom((bottomIndex + 1) % bottomGroups.Length, false);
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
        for (int i = 0; i < bottomGroups.Length; i++)
        {
            if (bottomGroups[i] == null) continue;
            bottomGroups[i].alpha = Mathf.MoveTowards(bottomGroups[i].alpha, i == bottomIndex ? 1f : 0f, step);
        }
    }

    private void ShowBottom(int index, bool immediate)
    {
        bottomIndex = index;
        if (index == 0) RefreshRanking();
        if (!immediate || bottomGroups == null) return;
        for (int i = 0; i < bottomGroups.Length; i++)
            if (bottomGroups[i] != null) bottomGroups[i].alpha = i == index ? 1f : 0f;
    }

    private void RefreshRanking()
    {
        rankingVersion = WheelRankingStore.Version;
        Fill(energyTable, WheelRankingStore.Kind.Energy);
        Fill(saleTable, WheelRankingStore.Kind.Sale);
    }

    private void Fill(RankTableView table, WheelRankingStore.Kind kind)
    {
        if (table == null) return;
        table.Set(kind, WheelRankingStore.TotalTop(kind, table.RowCount), null, 0);
    }

    // 새 기록이 들어오면 바로 랭킹 칸을 보여 준다
    private void HandleRecordAdded(WheelSession session, PowerRecord record)
    {
        bottomTime = Time.unscaledTime;
        ShowBottom(0, false);
    }

    private static void SetText(TMP_Text[] list, int index, string value)
    {
        if (list == null || index >= list.Length || list[index] == null) return;
        if (list[index].text != value) list[index].text = value;
    }
}
