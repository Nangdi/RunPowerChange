using System;
using System.Collections.Generic;
using UnityEngine;

// 두 쳇바퀴가 같이 쓰는 게임 회차. 같은 회차의 체험자는 같이 카운트다운하고 같이 달린다.
//
// Idle ─(한 명 시작)→ Open ─(합류 시간 끝 또는 전원 참가, 그리고 전원 캐릭터 선택 완료)→ Countdown → Run → Post ─(참가자 모두 대기화면 복귀)→ Idle
//
// - Open 동안(joinWaitSeconds) 다른 쳇바퀴가 시작을 누르면 같은 회차로 합류한다 (2인 경주).
// - 그 뒤에 누르면 대기열에 들어가고, 이번 회차가 끝나면 자동으로 다음 회차를 시작한다.
// - 혼자 달리면 오늘(없으면 전날) 1위 기록을 고스트로 같이 달리게 한다.
// - 달리기가 끝난 뒤의 결과·송전·판매는 각자 WheelSession 이 진행한다.
public class GameRound : MonoBehaviour
{
    public enum Phase { Idle, Open, Countdown, Run, Post }

    public static GameRound Instance { get; private set; }

    public Phase Current { get; private set; } = Phase.Idle;
    public float PhaseElapsed => Time.unscaledTime - phaseEnterTime;

    public IReadOnlyList<WheelSession> Participants => participants;
    public bool IsSolo => participants.Count == 1;
    public bool IsParticipant(WheelSession s) => participants.Contains(s);
    public bool IsQueued(WheelSession s) => queued.Contains(s);

    // 합류 가능 남은 시간 (Open 일 때만 의미 있음)
    public float JoinRemaining => Current == Phase.Open ? Mathf.Max(0f, Config.joinWaitSeconds - PhaseElapsed) : 0f;
    public bool CanJoin => Current == Phase.Idle || (Current == Phase.Open && JoinRemaining > 0f && participants.Count < AllSessions.Count);

    public int CountdownNumber => Mathf.Max(0, Config.countdownSeconds - Mathf.FloorToInt(Current == Phase.Countdown ? PhaseElapsed : 0f));
    public float RunDuration => Mathf.Max(1f, Config.runSeconds);
    public float RunElapsed => Current == Phase.Run ? Mathf.Min(RunDuration, PhaseElapsed) : runElapsedAtEnd;
    public float RunRemaining => Mathf.Max(0f, RunDuration - RunElapsed);

    // 이번 회차 시작 때 고정한 리더 기록 (거리 내림차순). [0] 이 1인 체험 고스트.
    public IReadOnlyList<RaceTrack> Leaders => leaders;
    public RaceTrack Ghost => IsSolo && leaders.Count > 0 ? leaders[0] : null;
    public bool LeadersFromPreviousDay { get; private set; }

    // 다음 회차 대기 중인 사람이 체감할 남은 시간 (대략). 결과 화면 진행은 사람마다 달라 정확하지 않다.
    public float EstimatedWaitSeconds
    {
        get
        {
            var c = Config;
            float post = c.resultAutoCloseSeconds * 2f + c.transmitSeconds + 10f;
            switch (Current)
            {
                case Phase.Open: return JoinRemaining + c.countdownSeconds + RunDuration + post;
                case Phase.Countdown: return Mathf.Max(0f, c.countdownSeconds - PhaseElapsed) + RunDuration + post;
                case Phase.Run: return RunRemaining + post;
                default: return post;
            }
        }
    }

    public event Action<Phase> OnPhaseChanged;

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    private readonly List<WheelSession> participants = new List<WheelSession>();
    private readonly List<WheelSession> queued = new List<WheelSession>();
    private readonly List<RaceTrack> leaders = new List<RaceTrack>();
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

    // 시작 요청. 이번 회차에 들어가면 true, 다음 회차 대기열에 들어가면 false.
    public bool RequestJoin(WheelSession s)
    {
        if (participants.Contains(s)) return true;

        if (CanJoin)
        {
            queued.Remove(s);
            if (Current == Phase.Idle) GoTo(Phase.Open);
            participants.Add(s);
            Debug.Log($"[Round] 쳇바퀴 {s.WheelId} 참가 ({participants.Count}명)");
            return true;
        }

        if (!queued.Contains(s)) queued.Add(s);
        Debug.Log($"[Round] 쳇바퀴 {s.WheelId} 다음 회차 대기");
        return false;
    }

    // 대기화면으로 돌아간 세션은 회차에서 빠진다.
    public void Leave(WheelSession s)
    {
        queued.Remove(s);
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
        switch (Current)
        {
            case Phase.Open:
                if (participants.Count == 0) { GoTo(Phase.Idle); break; }
                bool joinClosed = JoinRemaining <= 0f || participants.Count >= AllSessions.Count;
                if (joinClosed && participants.TrueForAll(p => p.Current == WheelSession.State.Ready))
                    BeginCountdown();
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

    private void BeginCountdown()
    {
        leaders.Clear();
        leaders.AddRange(RaceRecordStore.GetLeaders(Mathf.Max(1, Config.recordLineCount)));
        LeadersFromPreviousDay = RaceRecordStore.LeadersFromPreviousDay;
        runElapsedAtEnd = 0f;
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
        GoTo(Phase.Idle);

        // 기다리던 사람이 있으면 바로 다음 회차를 연다
        foreach (var s in queued.ToArray()) s.StartFromQueue();
    }

    private void GoTo(Phase next)
    {
        Current = next;
        phaseEnterTime = Time.unscaledTime;
        Debug.Log($"[Round] {next}");
        OnPhaseChanged?.Invoke(next);
    }
}
