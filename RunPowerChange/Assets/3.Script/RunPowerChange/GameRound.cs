using System;
using System.Collections.Generic;
using UnityEngine;

// 두 쳇바퀴가 같이 쓰는 게임 회차. 같은 회차의 체험자는 같이 안내를 보고, 같이 카운트다운하고, 같이 달린다.
//
// Idle ─(한 명 시작)→ Open ─(합류 시간 끝 또는 전원 참가)→ Ready(readySeconds 가 0 이면 건너뜀) → Guide → Countdown → Run → Post ─(참가자 모두 대기화면 복귀)→ Idle
//
// - Open 동안(joinWaitSeconds) 다른 쳇바퀴 화면에는 "참여하기" 버튼이 나오고, 누르면 같은 회차로 합류한다 (2인 경주).
// - 그 뒤(회차 진행 중)에는 다른 쳇바퀴 대기화면의 체험시작이 막히고 "체험 중" 안내가 나온다. 회차가 끝나면 다시 시작할 수 있다.
// - 혼자 달리면 오늘(없으면 전날) 1위 기록을 고스트로 같이 달리게 한다.
// - 달리기가 끝난 뒤의 전압 선택·판매·랭킹은 각자 WheelSession 이 진행한다.
public class GameRound : MonoBehaviour
{
    public enum Phase { Idle, Open, Ready, Guide, Countdown, Run, Post }

    public static GameRound Instance { get; private set; }

    public Phase Current { get; private set; } = Phase.Idle;
    public float PhaseElapsed => Time.unscaledTime - phaseEnterTime;

    public IReadOnlyList<WheelSession> Participants => participants;
    public bool IsSolo => participants.Count == 1;
    public bool IsParticipant(WheelSession s) => participants.Contains(s);
    // 다른 사람과 함께 달리는지, 혼자 1위 기록과 달리는지 정해진 뒤 (달리기 준비부터)
    public bool RaceActive => Current >= Phase.Ready && Current <= Phase.Post;

    // 합류 가능 남은 시간 (Open 일 때만 의미 있음)
    public float JoinRemaining => Current == Phase.Open ? Mathf.Max(0f, Config.joinWaitSeconds - PhaseElapsed) : 0f;
    public bool CanJoin => Current == Phase.Idle || (Current == Phase.Open && JoinRemaining > 0f && participants.Count < AllSessions.Count);
    public float ReadyRemaining => Current == Phase.Ready ? Mathf.Max(0f, Config.readySeconds - PhaseElapsed) : 0f;
    public float GuideRemaining01 => Current == Phase.Guide && Config.guideSeconds > 0f ? Mathf.Clamp01(1f - PhaseElapsed / Config.guideSeconds) : 0f;

    public int CountdownNumber => Mathf.Max(0, Config.countdownSeconds - Mathf.FloorToInt(Current == Phase.Countdown ? PhaseElapsed : 0f));
    public float RunDuration => Mathf.Max(1f, Config.runSeconds);
    public float RunElapsed => Current == Phase.Run ? Mathf.Min(RunDuration, PhaseElapsed) : runElapsedAtEnd;
    public float RunRemaining => Mathf.Max(0f, RunDuration - RunElapsed);

    // 이번 회차 준비 때 고정한 1위 기록. 1인 체험 고스트와 85형 "오늘 최고 랭킹" 선에 쓴다.
    public RaceTrack Leader { get; private set; }
    public RaceTrack Ghost => IsSolo ? Leader : null;
    public bool LeaderFromPreviousDay { get; private set; }

    // 지금 회차가 끝날 때까지 남은 시간 (대략, 대기화면 "체험 중" 안내용). 판매·랭킹 화면 진행은 사람마다 달라 정확하지 않다.
    public float EstimatedWaitSeconds
    {
        get
        {
            var c = Config;
            float post = c.saleSeconds + c.resultAutoCloseSeconds + 10f;
            float run = RunDuration + post;
            switch (Current)
            {
                case Phase.Open: return JoinRemaining + c.readySeconds + c.guideSeconds + c.countdownSeconds + run;
                case Phase.Ready: return ReadyRemaining + c.guideSeconds + c.countdownSeconds + run;
                case Phase.Guide: return Mathf.Max(0f, c.guideSeconds - PhaseElapsed) + c.countdownSeconds + run;
                case Phase.Countdown: return Mathf.Max(0f, c.countdownSeconds - PhaseElapsed) + run;
                case Phase.Run: return RunRemaining + post;
                default: return post;
            }
        }
    }

    public event Action<Phase> OnPhaseChanged;

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    private readonly List<WheelSession> participants = new List<WheelSession>();
    private readonly List<WheelSession> allSessions = new List<WheelSession>();
    private float phaseEnterTime;
    private float runElapsedAtEnd;

    private List<WheelSession> AllSessions
    {
        get
        {
            if (allSessions.Count == 0) allSessions.AddRange(FindObjectsOfType<WheelSession>());
            return allSessions;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("GameRound 가 씬에 2개 이상 있습니다.", this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── 세션에서 호출 ───────────────────────────────────

    // 시작 요청. 이번 회차에 들어가면 true, 회차가 진행 중이라 들어갈 수 없으면 false.
    public bool RequestJoin(WheelSession s)
    {
        if (participants.Contains(s)) return true;

        if (!CanJoin)
        {
            Debug.Log($"[Round] 쳇바퀴 {s.WheelId} 시작 무시 - 체험 진행 중 ({Current})");
            return false;
        }

        if (Current == Phase.Idle) GoTo(Phase.Open);
        participants.Add(s);
        Debug.Log($"[Round] 쳇바퀴 {s.WheelId} 참가 ({participants.Count}명)");
        return true;
    }

    // 대기화면으로 돌아간 세션은 회차에서 빠진다.
    public void Leave(WheelSession s)
    {
        if (participants.Remove(s))
            Debug.Log($"[Round] 쳇바퀴 {s.WheelId} 종료 (남은 참가자 {participants.Count}명)");
    }

    // 개발자용: 달리기를 바로 끝낸다
    public void DebugFinishRun()
    {
        if (Current == Phase.Run) phaseEnterTime = Time.unscaledTime - RunDuration;
    }

    // ── 진행 ────────────────────────────────────────────

    private void Update()
    {
        if (JsonManager.instance == null) return; // 앱 종료 중
        if (Current != Phase.Idle && Current != Phase.Post && participants.Count == 0)
        {
            EndRound();
            return;
        }

        switch (Current)
        {
            case Phase.Open:
                if (JoinRemaining <= 0f || participants.Count >= AllSessions.Count) BeginReady();
                break;

            case Phase.Ready:
                if (PhaseElapsed >= Config.readySeconds) BeginGuide();
                break;

            case Phase.Guide:
                if (PhaseElapsed >= Config.guideSeconds) BeginCountdown();
                break;

            case Phase.Countdown:
                if (PhaseElapsed >= Config.countdownSeconds) BeginRun();
                break;

            case Phase.Run:
                if (PhaseElapsed >= RunDuration) FinishRun();
                break;

            case Phase.Post:
                if (participants.Count == 0) EndRound();
                break;
        }
    }

    private void BeginReady()
    {
        var leaders = RaceRecordStore.GetLeaders(1);
        Leader = leaders.Count > 0 ? leaders[0] : null;
        LeaderFromPreviousDay = RaceRecordStore.LeadersFromPreviousDay;
        runElapsedAtEnd = 0f;
        GoTo(Phase.Ready);
        if (Config.readySeconds <= 0f) BeginGuide();
    }

    private void BeginGuide()
    {
        GoTo(Phase.Guide);
        foreach (var p in participants.ToArray()) p.BeginGuide();
    }

    private void BeginCountdown()
    {
        GoTo(Phase.Countdown);
        foreach (var p in participants.ToArray()) p.BeginCountdown();
    }

    private void BeginRun()
    {
        GoTo(Phase.Run);
        foreach (var p in participants.ToArray()) p.BeginRun();
    }

    private void FinishRun()
    {
        runElapsedAtEnd = RunDuration;
        GoTo(Phase.Post);
        foreach (var p in participants.ToArray()) p.FinishRun();
    }

    private void EndRound()
    {
        Leader = null;
        GoTo(Phase.Idle);
    }

    private void GoTo(Phase next)
    {
        Current = next;
        phaseEnterTime = Time.unscaledTime;
        Debug.Log($"[Round] {next}");
        OnPhaseChanged?.Invoke(next);
    }
}
