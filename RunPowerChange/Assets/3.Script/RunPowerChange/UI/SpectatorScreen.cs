using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 85형 관전 화면 (세로 2160x3840). Canvas_Spectator 프리팹 루트에 붙는다.
//  - 위: PLAYER 01/02 상태(모집 중 / 참가 완료 / 체험중 / 승리! …)와 누적 환산 발전량. 뒤의 3D 장면은 RaceCamera(Spectator)가 그린다.
//  - 가운데: "오늘 최고 랭킹" 표시가 3D 최고 랭킹 선(RaceStage.RecordLine)의 가운데(화면 중앙, 두 레인 사이) 위에 붙어 따라다닌다.
//    선이 화면 위쪽까지 가도 숨기지 않고 화면 밖으로 나갈 때까지 따라간다. 화면 위 PLAYER 01/02 글자는 양옆에 있고
//    가운데 제목은 달리는 동안 숨기므로 가리는 것이 없다.
//  - 제목(달려라! 전력 챌린지)은 카운트다운부터 달리기가 끝날 때까지 숨겨 앞으로 나간 바퀴가 가리지 않게 한다.
//  - 바퀴·선은 실제 거리 차만큼 앞으로 가서 많이 앞서면 화면 위로 나간다. 화면 밖으로 완전히 나가면
//    다른 게임처럼 화면 맨 위 그 x 자리에 화살표와 "PLAYER 01 63m 앞서는 중"(1위 기록 선은 한 줄 아래 "오늘 최고 랭킹 63m 앞")을 띄운다.
//  - 아래 칸: 오늘의 발전왕·판매왕 순위 → 왜 그럴까요? → 우리와 어떤 관련이 있을까요? 가 spectatorInfoSeconds 마다 바뀐다.
//    설명 그림은 현장에서 바꿀 수 있게 StreamingAssets/Spectator/info_1.png, info_2.png 를 읽는다 (없으면 기본 그림, 로그만 남김).
//  - 가운데 아래 안내 카드(SpectatorNotice): 관람객이 지금 상황을 알 수 있게 회차 단계를 알려 준다.
//      참가자 모집(Open) → 경기 준비(Ready) → 카운트다운 3·2·1·GO(가운데 큰 숫자) → 달리기(카드 없음)
//      → 경기 결과: 승자 발표(1인 체험은 1위 기록 도전 결과, spectatorWinnerSeconds) → 결과 집계 중(선수들이 전압 선택·판매 중)
//      → 참가자가 모두 랭킹 화면에 가면 종합 순위 발표(아래 칸을 랭킹으로 고정, 회차가 먼저 끝나도 spectatorRankingSeconds 동안)
//    2인 경주 승자는 누적 환산 발전량(화면 위 wh 숫자)이 큰 쪽이다. 보이는 숫자가 같으면 무승부.
//    1인 체험은 같이 달린 1위 기록(고스트)과 같은 기준(달린 거리)으로 돌파했는지 본다.
// 랭킹은 두 쳇바퀴의 기록을 합쳐(종합순위) 보여 준다. 평소에는 1~5위 기록. 회차가 끝나 종합 순위를 발표하는 동안만
// 아래 줄(2인이면 4·5위 자리, 1인이면 5위 자리)을 이번 참가자의 PLAYER 줄(방금 기록과 종합 순위, 레인 색 강조)로 바꾼다.
// 캔버스 출력 디스플레이는 Setting.json spectatorDisplayIndex 를 따른다.
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
    [Tooltip("오늘/전날 그림 바꾸기 (선이 전날 1위면 \"전날 최고 랭킹\"). 비워두면 바꾸지 않음")]
    [SerializeField] private RecordTagView recordTagView;
    [Tooltip("표시 그림에서 선과 닿는 점(아래 끝)의 피벗 기준 높이 비율. 0 = 그림 아래 끝")]
    [SerializeField] private float recordTagAnchorY = 0f;

    [Header("제목 (달리는 동안 숨김)")]
    [Tooltip("비워두면 숨기지 않음")]
    [SerializeField] private CanvasGroup titleGroup;

    [Header("화면 밖으로 나간 레인 표시 (인덱스 0 = 쳇바퀴 1, 1 = 쳇바퀴 2, 2 = 1위 기록 선)")]
    [Tooltip("화살표 + 상자 묶음. x 만 대상 자리를 따라간다. 비워두면 표시하지 않음")]
    [SerializeField] private RectTransform[] aheadMarkers;
    [SerializeField] private TMP_Text[] aheadTexts;
    [Tooltip("거리 숫자 색 (프리팹 생성 도구가 PowerUIKit 색으로 채운다)")]
    [SerializeField] private Color aheadColor = new Color32(251, 176, 59, 255);
    [Tooltip("화면 아래로 빠진 레인 표시 (인덱스 0 = 쳇바퀴 1, 1 = 쳇바퀴 2). 앞선 사람을 따라갈 때 뒤처진 바퀴용. 비워두면 표시하지 않음")]
    [SerializeField] private RectTransform[] behindMarkers;
    [SerializeField] private TMP_Text[] behindTexts;

    [Header("아래 칸 (나오는 순서)")]
    [SerializeField] private CanvasGroup[] bottomGroups;
    [SerializeField] private float fadeSeconds = 0.5f;

    [Header("설명 그림 (아래 칸 두 번째부터, StreamingAssets/Spectator/info_n.png 로 교체)")]
    [SerializeField] private Image[] infoImages;

    [Header("랭킹 (아래 칸 첫 번째)")]
    [SerializeField] private RankTableView energyTable;
    [SerializeField] private RankTableView saleTable;
    [Tooltip("PLAYER 01/02 레인 색 (랭킹 표 PLAYER 줄 강조). 3D 레인·화면 밖 화살표와 같은 색")]
    [SerializeField] private Color[] playerColors = { new Color32(47, 128, 237, 255), new Color32(236, 56, 140, 255) };

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
        UpdateAheadMarkers();
        UpdateBehindMarkers();
        UpdateTitle();
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
        bool wasRevealing = Revealing;
        results.Clear();
        revealTime = -1f;
        if (wasRevealing) RefreshRanking();   // PLAYER 줄을 걷고 1~5위로
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
        if (!s.Enabled) return "점검 중";
        if (round == null) return playing ? "체험중" : "체험 대기중";
        bool joined = round.IsParticipant(s);
        switch (round.Current)
        {
            case GameRound.Phase.Open: return joined ? "참가 완료" : "모집 중";
            case GameRound.Phase.Ready: return joined ? "준비 중" : "체험 대기중";
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
        if (recordTagView != null) recordTagView.Set(GameRound.Instance != null && GameRound.Instance.LeaderFromPreviousDay);

        // 선 위, 두 레인 정중앙에 표시의 아래 끝이 오도록 화면 좌표 → 캔버스 좌표로 옮긴다
        Vector3 screen = raceCamera.WorldToScreenPoint(stage.RecordLineLaneCenter);
        var parent = (RectTransform)recordTag.parent;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local)) return;
        float offsetY = (recordTag.pivot.y - recordTagAnchorY) * recordTag.rect.height;
        Vector2 center = (new Vector2(0.5f, 0.5f) - parent.pivot) * parent.rect.size;   // 표시 앵커(부모 가운데) 기준으로 맞춤
        recordTag.anchoredPosition = new Vector2(local.x - center.x, local.y + offsetY - center.y);
    }

    // ── 화면 위로 나간 레인 표시 ─────────────────────────

    private string Paint(string text) => $"<color=#{ColorUtility.ToHtmlStringRGB(aheadColor)}>{text}</color>";

    // 카운트다운부터 달리기가 끝날 때까지 제목을 숨긴다
    private void UpdateTitle()
    {
        if (titleGroup == null) return;
        var round = GameRound.Instance;
        bool racing = round != null && (round.Current == GameRound.Phase.Countdown || round.Current == GameRound.Phase.Run);
        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
        titleGroup.alpha = Mathf.MoveTowards(titleGroup.alpha, racing ? 0f : 1f, step);
    }

    private void UpdateAheadMarkers()
    {
        if (aheadMarkers == null) return;
        var stage = RaceStage.Instance;
        var round = GameRound.Instance;
        bool racing = stage != null && raceCamera != null && round != null && round.Current == GameRound.Phase.Run;

        for (int i = 0; i < aheadMarkers.Length; i++)
        {
            RectTransform marker = aheadMarkers[i];
            if (marker == null) continue;
            var panel = (RectTransform)marker.parent;
            string text = null;
            Vector3 world = default;

            if (racing && i < 2)
            {
                // 앞선 바퀴가 화면 위로 완전히 나갔을 때 (바퀴 아래 끝이 화면 위 끝보다 위)
                RaceRunner runner = stage.Runner(i + 1);
                float ahead = runner != null && runner.IsActive ? runner.DistanceM - stage.TrailingDistance : 0f;
                if (ahead >= 1f && OffTop(runner.WheelBottomPoint, panel))
                {
                    world = runner.WheelCenterPoint;
                    string who = runner.CurrentMode == RaceRunner.Mode.Leader
                        ? (round.LeaderFromPreviousDay ? "전날 1위" : "오늘 1위")
                        : $"PLAYER {runner.WheelId:00}";
                    text = $"{who} {Paint($"{ahead:0}m")} 앞서는 중";
                }
            }
            else if (racing && round.Ghost == null && stage.RecordLineVisible && stage.RecordAheadM >= 1f && OffTop(stage.RecordLineCenter, panel))
            {
                // 1위 기록 선 (2인 경주만. 혼자 달리면 1위 기록은 고스트 레인이라 위 레인 표시로 나온다)
                world = stage.RecordLineCenter;
                text = $"{(round.LeaderFromPreviousDay ? "전날" : "오늘")} 최고 랭킹 {Paint($"{stage.RecordAheadM:0}m")} 앞";
            }

            bool show = false;
            if (text != null && ToPanel(raceCamera.WorldToScreenPoint(world), panel, out Vector2 p))
            {
                // x 만 대상 자리를 따라가고(화면 안으로 제한) 높이는 화면 맨 위에 고정
                show = true;
                float half = panel.rect.width * 0.5f - marker.rect.width * 0.5f - 20f;
                marker.anchoredPosition = new Vector2(Mathf.Clamp(p.x, -half, half), marker.anchoredPosition.y);
                if (aheadTexts != null && i < aheadTexts.Length && aheadTexts[i] != null && aheadTexts[i].text != text) aheadTexts[i].text = text;
            }
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
        }
    }

    // 85형이 앞선 사람을 따라가 뒤처진 바퀴가 화면 아래(아래 칸 뒤)로 빠지면 아래 칸 바로 위에 "PLAYER 02 60m 추격 중" 표시.
    // 바퀴 윗면이 표시 아래 끝보다 아래로 내려가거나 카메라 뒤로 지나가면 띄운다. x 는 그 레인 자리.
    private void UpdateBehindMarkers()
    {
        if (behindMarkers == null) return;
        var stage = RaceStage.Instance;
        var round = GameRound.Instance;
        bool racing = stage != null && raceCamera != null && round != null && round.Current == GameRound.Phase.Run && stage.SpectatorFollowingLeader;

        for (int i = 0; i < behindMarkers.Length; i++)
        {
            RectTransform marker = behindMarkers[i];
            if (marker == null) continue;
            var panel = (RectTransform)marker.parent;
            string text = null;
            float x = marker.anchoredPosition.x;

            RaceRunner runner = racing ? stage.Runner(i + 1) : null;
            if (runner != null && runner.CurrentMode == RaceRunner.Mode.Player && round.IsParticipant(runner.Session))
            {
                float behind = stage.LeadingDistance - runner.DistanceM;
                float bottomEdge = marker.anchoredPosition.y - marker.rect.height * 0.5f;
                bool below = !ToPanel(raceCamera.WorldToScreenPoint(runner.WheelTopPoint), panel, out Vector2 top) || top.y < bottomEdge;
                if (behind >= 1f && below)
                {
                    text = $"PLAYER {runner.WheelId:00} {Paint($"{behind:0}m")} 추격 중";
                    // 레인 가로 자리: 카메라 앞 일정 거리의 같은 레인 점
                    Vector3 lane = runner.WheelCenterPoint;
                    lane.z = raceCamera.transform.position.z + 15f;
                    if (ToPanel(raceCamera.WorldToScreenPoint(lane), panel, out Vector2 p)) x = p.x;
                }
            }

            bool show = text != null;
            if (show)
            {
                float half = panel.rect.width * 0.5f - marker.rect.width * 0.5f - 20f;
                marker.anchoredPosition = new Vector2(Mathf.Clamp(x, -half, half), marker.anchoredPosition.y);
                if (behindTexts != null && i < behindTexts.Length && behindTexts[i] != null && behindTexts[i].text != text) behindTexts[i].text = text;
            }
            if (marker.gameObject.activeSelf != show) marker.gameObject.SetActive(show);
        }
    }

    // 월드 점이 화면(패널) 위 끝보다 위에 있는지 (카메라 뒤면 false)
    private bool OffTop(Vector3 world, RectTransform panel) =>
        ToPanel(raceCamera.WorldToScreenPoint(world), panel, out Vector2 p) && p.y > panel.rect.height * 0.5f;

    // 화면 좌표 → 패널 가운데 기준 좌표
    private bool ToPanel(Vector3 screen, RectTransform panel, out Vector2 point)
    {
        point = default;
        if (screen.z <= 0f) return false;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, screen, uiCamera, out Vector2 local)) return false;
        point = local - (Vector2.one * 0.5f - panel.pivot) * panel.rect.size;
        return true;
    }

    // ── 아래 칸 ─────────────────────────────────────────

    private void UpdateBottom()
    {
        if (bottomGroups == null || bottomGroups.Length == 0) return;
        float interval = Mathf.Max(1f, Config.spectatorInfoSeconds);
        if (Revealing)
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

        // 종합 순위 발표 중이면 이번 회차에서 기록을 남긴 참가자의 방금 기록과 종합 순위. 순위가 높은 사람을 위 줄에 둔다
        // (같으면 PLAYER 번호 순). 발전왕·판매왕 표마다 따로 정렬하므로 두 표의 줄 순서가 다를 수 있다.
        var rows = new List<(WheelSession s, PowerRecord latest, int rank)>();
        if (Revealing)
        {
            foreach (var r in results)
            {
                WheelSession s = Session(r.wheelId);
                if (s == null || !r.reachedRanking) continue;
                PowerRecord latest = s.Ranking != null ? s.Ranking.Latest : null;
                int rank = latest != null ? WheelRankingStore.TotalRank(kind, kind == WheelRankingStore.Kind.Energy ? latest.energyWh : latest.saleWon) : 0;
                rows.Add((s, latest, rank));
            }
            rows.Sort((a, b) =>
            {
                int ra = a.rank > 0 ? a.rank : int.MaxValue, rb = b.rank > 0 ? b.rank : int.MaxValue;
                return ra != rb ? ra.CompareTo(rb) : a.s.WheelId.CompareTo(b.s.WheelId);
            });
        }

        // PLAYER 줄은 맨 아래 줄부터 채운다. 혼자 체험했으면 1~4위 기록 + 5번째 줄 PLAYER, 둘이면 1~3위 + PLAYER 두 줄.
        int top = Mathf.Max(0, table.RowCount - rows.Count);
        table.Set(kind, WheelRankingStore.TotalTop(kind, top), null, 0);
        for (int i = 0; i < rows.Count && top + i < table.RowCount; i++)
            table.SetRow(top + i, kind, $"PLAYER {rows[i].s.WheelId:00}", rows[i].latest, rows[i].rank, PlayerColor(rows[i].s.WheelId));
    }

    // 종합 순위 발표 중 (랭킹 칸 고정, PLAYER 줄 표시)
    private bool Revealing => revealTime >= 0f && results.Count > 0;

    private Color PlayerColor(int wheelId)
    {
        int i = wheelId - 1;
        return playerColors != null && i >= 0 && i < playerColors.Length ? playerColors[i] : Color.white;
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
