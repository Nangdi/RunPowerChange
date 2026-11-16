using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 플레이 모드 자동 시험 (개발용). 센서 회전 신호를 흉내 내며 2인 경주 → 1인(1위 고스트) 체험을 끝까지 진행하고,
// 화면마다 3D 장면 + UI 를 합친 캡처와 점검 결과를 outDir 에 남긴다.
// 랭킹·달리기 기록은 빈 상태로 시험하고, 끝나면 시험 전 파일로 되돌린다 (실제 랭킹에 시험 기록이 남지 않음).
//  PowerPlayTest.Start(outDir)  → 플레이 모드에 들어가 시험을 시작한다. 끝나면 outDir/done.txt 를 쓰고 플레이 모드를 끈다.
// 에디터 창에 포커스가 없어도 진행되도록 EditorApplication.update 로 돈다 (PlayerSettings.runInBackground 가 켜져 있어야 함).
[InitializeOnLoad]
public static class PowerPlayTest
{
    private const string PendingKey = "PowerPlayTest.OutDir";
    private const string Player2LeadsKey = "PowerPlayTest.Player2Leads";
    private const string SequenceKey = "PowerPlayTest.Sequence";
    private const string BigGapKey = "PowerPlayTest.BigGap";
    private const string QuitKey = "PowerPlayTest.QuitTrailing";
    private const string QuitLeadKey = "PowerPlayTest.QuitLeading";
    private const string FollowKey = "PowerPlayTest.FollowScenario";

    private class Step
    {
        public float at;          // 시험 시작 후 시각(초)
        public string name;
        public Action action;
        // 이 조건이 맞을 때까지 시험 시계를 멈춰 둔다 (플레이 모드 진입·캡처로 게임 진행이 밀려도 뒤 단계가 어긋나지 않게)
        public Func<bool> until;
    }

    private static readonly List<Step> steps = new List<Step>();
    private static readonly StringBuilder log = new StringBuilder();
    private static string outDir;
    private static double startTime;
    private static int nextStep;
    // 쳇바퀴별 흉내 속도(km/h). 0 이면 신호 없음.
    private static readonly float[] speedKmh = new float[3];
    private static readonly double[] nextRotation = new double[3];

    // 시험이 남기는 랭킹·달리기 기록이 실제 기록에 섞이지 않도록, 시작 전에 기록 파일을 떼어 두고 빈 기록으로 시험한 뒤
    // 플레이 모드가 끝나면(시험 완료든 중간에 멈춤이든) 원래 파일로 되돌린다.
    private const string BackupKey = "PowerPlayTest.RecordBackup";
    private static string BackupDir => Path.Combine(Application.temporaryCachePath, "PowerPlayTestRecords");

    private static IEnumerable<string> RecordFiles()
    {
        string dir = Application.persistentDataPath;
        if (!Directory.Exists(dir)) yield break;
        foreach (var f in Directory.GetFiles(dir, "ranking_wheel*.json")) yield return f;
        string race = Path.Combine(dir, "race_records.json");
        if (File.Exists(race)) yield return race;
    }

    private static void BackupRecords()
    {
        if (SessionState.GetBool(BackupKey, false)) RestoreRecords();   // 지난 시험이 되돌리지 못한 백업이 있으면 먼저 되돌린다
        if (Directory.Exists(BackupDir)) Directory.Delete(BackupDir, true);
        Directory.CreateDirectory(BackupDir);
        foreach (var f in new List<string>(RecordFiles())) File.Move(f, Path.Combine(BackupDir, Path.GetFileName(f)));
        SessionState.SetBool(BackupKey, true);
    }

    private static void RestoreRecords()
    {
        if (!SessionState.GetBool(BackupKey, false)) return;
        // 시험을 막 시작해 플레이 모드로 들어가는 중이면 되돌리지 않는다. 바로 앞에 플레이 모드를 멈춘 경우 그 멈춤에 따른
        // 되돌리기(delayCall)가 시험 시작보다 늦게 실행돼, 방금 떼어 둔 실제 기록을 다시 가져와 시험 기록이 섞인 적이 있다.
        if (!string.IsNullOrEmpty(SessionState.GetString(PendingKey, ""))) return;
        foreach (var f in new List<string>(RecordFiles())) File.Delete(f);
        if (Directory.Exists(BackupDir))
            foreach (var f in Directory.GetFiles(BackupDir)) File.Move(f, Path.Combine(Application.persistentDataPath, Path.GetFileName(f)));
        SessionState.EraseBool(BackupKey);
        Debug.Log("[PlayTest] 랭킹·달리기 기록을 시험 전으로 되돌림");
    }

    static PowerPlayTest()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.delayCall += RestoreRecords;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) RestoreRecords();
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            string dir = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(dir)) return;
            SessionState.EraseString(PendingKey);
            Begin(dir);
        };
    }

    // previousDayLeader: 오늘 기록 없이 전날 1위 기록(12km/h 로 달린 기록)만 있는 상태로 시작한다 (날이 바뀐 뒤 첫 대결 점검:
    // 1위 기록 선 표시가 "전날 최고 랭킹"으로 나오고, 순위는 오늘 기록만 세는지)
    // player2Leads: 2인 경주에서 쳇바퀴 2 가 빠르게 달린다 (오른쪽 레인 표시 점검)
    // sequence: 2인 경주 동안 85형을 1초마다 캡처한다 (캡처하는 동안 화면이 멈춰 위치 기록(trace.csv)에 그만큼 튐이 생긴다)
    // bigGap: 2인 경주를 20km/h 대 6km/h 로 달려 거리 차가 벌어지게 한다 (85형이 앞선 사람을 따라가고 추격 중 표시가 나오는지 점검)
    // quitTrailing: 2인 경주 9초에 뒤처진 쪽이 멈춘다 (중도포기: spectatorQuitSeconds 뒤 85형이 앞선 사람 시점으로 바뀌는지 점검)
    // quitLeading: 2인 경주 9초에 앞선 쪽이 멈춘다 (앞선 사람 중도포기: 뒤처진 사람 시점으로 바뀌고, 추월하면 다시 그 사람 시점인지 점검)
    public static void Start(string dir, bool previousDayLeader = false, bool player2Leads = false, bool sequence = true, bool bigGap = false, bool quitTrailing = false, bool quitLeading = false)
    {
        SessionState.SetBool(QuitLeadKey, quitLeading);
        SessionState.SetBool(FollowKey, false);
        SessionState.SetBool(BigGapKey, bigGap);
        SessionState.SetBool(QuitKey, quitTrailing);
        SessionState.SetBool(Player2LeadsKey, player2Leads);
        SessionState.SetBool(SequenceKey, sequence);
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        PlayerSettings.runInBackground = true;
        SessionState.EraseString(PendingKey);   // 지난 시험이 플레이 모드에 못 들어가 남은 표시가 있으면 지운다 (아래 되돌리기가 건너뛰지 않게)
        BackupRecords();
        if (previousDayLeader) WritePreviousDayLeader();
        SessionState.SetString(PendingKey, dir);
        EditorApplication.isPlaying = true;
    }

    // 85형 시점 전환 점검: 2인 경주 하나만 길게(100초) 달리며 아래 속도표대로 접전·역전·거리 차 벌어짐·따라잡기·한쪽 포기·
    // 둘 다 멈춤을 차례로 만들고, 매 프레임 두 바퀴의 85형 화면 위치와 시점을 follow.csv 에 남긴다 (캡처 없음, 프레임 멈춤 없게).
    public static void StartFollow(string dir)
    {
        Start(dir, false, false, false, false, false, false);
        SessionState.SetBool(FollowKey, true);
    }

    // (달리기 시작 후 초, 쳇바퀴 1 km/h, 쳇바퀴 2 km/h)
    private static readonly float[,] FollowScript =
    {
        { 0f, 12f, 11.5f },   // 접전 (거리 차 몇 m)
        { 10f, 11f, 13f },    // 2 가 천천히 따라잡아 역전
        { 22f, 0f, 12.5f },   // 1(뒤처짐) 잠깐 멈춤 → 포기 판정 → 앞선 사람(2) 시점
        { 28f, 12.5f, 12f },  // 1 다시 달림 (거리 차는 기준보다 작음) → 바로 뒤처진 사람 시점으로 돌아와야 함
        { 30f, 20f, 8f },     // 1 이 크게 앞섬 → 앞선 사람 시점
        { 42f, 8f, 20f },     // 2 가 따라잡아 역전 → 뒤처진 사람 시점 → 다시 벌어지면 2 시점
        { 58f, 0f, 12f },     // 1(뒤처짐) 포기
        { 66f, 14f, 0f },     // 1 다시 달림, 2(앞섬) 포기 → 1 시점, 1 이 추월
        { 80f, 0f, 0f },      // 둘 다 멈춤
        { 86f, 13f, 13f },    // 둘 다 같은 속도
    };

    private static void BuildFollowSteps(GameDynamicData c)
    {
        c.runSeconds = 100f;
        Add(1f, "follow_start_w1", () => Session(1).StartExperience());
        Add(2.2f, "follow_start_w2", () => Session(2).StartExperience());
        Add(4f, "follow_wait_run", () => Log("  [follow] 달리기 시작"), () => GameRound.Instance.Current == GameRound.Phase.Run);
        // 시험 시계는 달리기 시작을 기다리는 동안 멈춰 있으므로, 시작 뒤 달리기 시간이 지난 시각에 끝을 확인한다
        Add(4f + c.runSeconds + 2f, "follow_end", () => { Array.Clear(speedKmh, 0, speedKmh.Length); Log("  [follow] 달리기 끝"); },
            () => GameRound.Instance.Current != GameRound.Phase.Run);
        Add(4f + c.runSeconds + 3f, "finish", Finish);
    }

    private static void FollowSpeeds()
    {
        var round = GameRound.Instance;
        if (round == null || round.Current != GameRound.Phase.Run) return;
        float t = round.RunElapsed;
        int row = 0;
        for (int i = 0; i < FollowScript.GetLength(0); i++)
            if (t >= FollowScript[i, 0]) row = i;
        speedKmh[1] = FollowScript[row, 1];
        speedKmh[2] = FollowScript[row, 2];
    }

    private static readonly StringBuilder followTrace = new StringBuilder();
    private static Camera spectatorCamera;
    private static int lastTraceFrame = -1;

    private static void TraceFollow()
    {
        var round = GameRound.Instance;
        var stage = RaceStage.Instance;
        if (round == null || stage == null || round.Current != GameRound.Phase.Run) return;
        if (Time.frameCount == lastTraceFrame) return;   // 에디터 업데이트가 한 프레임에 여러 번 불려도 한 줄만
        lastTraceFrame = Time.frameCount;
        if (spectatorCamera == null)
            foreach (var rc in UnityEngine.Object.FindObjectsOfType<RaceCamera>())
                if (rc.Screen == RaceCamera.Target.Spectator) spectatorCamera = rc.GetComponent<Camera>();
        var spec = UnityEngine.Object.FindObjectOfType<SpectatorScreen>();
        var so = spec != null ? new SerializedObject(spec) : null;
        if (followTrace.Length == 0)
            followTrace.AppendLine("frame,run,kmh1,kmh2,d1,d2,base,lead,cut,tq,lq,vy1,vy2,ahead1,ahead2,behind1,behind2,record");
        string Active(string prop, int i)
        {
            var arr = so?.FindProperty(prop);
            var m = arr != null && i < arr.arraySize ? arr.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform : null;
            return m != null && m.gameObject.activeSelf ? "1" : "0";
        }
        float Vy(int id)
        {
            if (spectatorCamera == null) return float.NaN;
            Vector3 v = spectatorCamera.WorldToViewportPoint(stage.Runner(id).WheelCenterPoint);
            return v.z <= 0f ? -9f : v.y;
        }
        followTrace.AppendLine($"{Time.frameCount},{round.RunElapsed:0.000},{speedKmh[1]:0.#},{speedKmh[2]:0.#}," +
            $"{stage.Runner(1).DistanceM:0.00},{stage.Runner(2).DistanceM:0.00},{stage.SpectatorBase:0.00}," +
            $"{(stage.SpectatorFollowingLeader ? 1 : 0)},{(stage.SpectatorCut ? 1 : 0)},{(stage.TrailingQuit ? 1 : 0)},{(stage.LeadingQuit ? 1 : 0)}," +
            $"{Vy(1):0.0000},{Vy(2):0.0000},{Active("aheadMarkers", 0)},{Active("aheadMarkers", 1)},{Active("behindMarkers", 0)},{Active("behindMarkers", 1)},{Active("aheadMarkers", 2)}");
    }

    private static void WritePreviousDayLeader()
    {
        var track = new RaceTrack { ticks = DateTime.Now.AddDays(-1).Ticks, wheelId = 1, characterIndex = 0 };
        float mps = 12f / 3.6f;
        for (int i = 0; i <= 120; i++) track.samples.Add(i * RaceTrack.SampleSeconds * mps);
        track.distanceM = track.samples[track.samples.Count - 1];
        var data = new RaceRecordData
        {
            day = DateTime.Now.ToString("yyyy-MM-dd"),
            previousDay = DateTime.Now.AddDays(-1).ToString("yyyy-MM-dd"),
            previous = new List<RaceTrack> { track },
        };
        File.WriteAllText(Path.Combine(Application.persistentDataPath, "race_records.json"), JsonUtility.ToJson(data), new UTF8Encoding(false));
    }

    private static GameDynamicData Cfg => JsonManager.instance.gameDynamicData;
    private static WheelSession Session(int id) => Array.Find(UnityEngine.Object.FindObjectsOfType<WheelSession>(), s => s.WheelId == id);

    private static void Begin(string dir)
    {
        outDir = dir;
        log.Clear();
        trace.Clear();
        steps.Clear();
        nextStep = 0;
        waitStart = -1;
        Array.Clear(speedKmh, 0, speedKmh.Length);

        // 화면 하나씩 볼 시간만 남기고 줄인다 (메모리 값만 바꾸고 파일에는 저장하지 않는다)
        var c = Cfg;
        c.joinWaitSeconds = 3f; c.readySeconds = 1.5f; c.countdownSeconds = 3; c.runSeconds = 24f;
        c.saleSeconds = 4f; c.saleStepSeconds = 0.4f; c.resultAutoCloseSeconds = 3f; c.finishApproachSeconds = 8f; c.idleTimeoutSeconds = 30f;
        c.spectatorWinnerSeconds = 3f; c.spectatorRankingSeconds = 4f;

        // ── 2인 경주: 쳇바퀴 1 이 빠르고(14km/h) 2 가 느리다(9km/h) ──
        // 시각: 1.0 쳇바퀴1 시작(Open) → 2.2 쳇바퀴2 참여(전원 → Ready) → 준비·안내·카운트다운 → 달리기
        Add(0.5f, "operator_display", () =>
        {
            foreach (var od in UnityEngine.Object.FindObjectsOfType<OperatorDisplay>(true))
                Log($"  운영자 캔버스 {od.name}: Display {od.GetComponent<Canvas>().targetDisplay + 1}");
        });
        Add(1f, "duo_start_w1", () => Session(1).StartExperience());
        Add(1.8f, "duo_join_screens", () => { Capture("01_join_w1_waiting", 1); Capture("02_join_w2_invite", 2); });
        Add(2.2f, "duo_start_w2", () => Session(2).StartExperience());
        float count = 2.2f + c.readySeconds;
        float run = count + c.countdownSeconds;
        Add(2.2f + c.readySeconds * 0.5f, "duo_ready", () => { Capture("03_ready_w1", 1); });
        Add(count + 0.3f, "duo_count", () => { Capture("05_count_w1", 1); bool swap = SessionState.GetBool(Player2LeadsKey, false); bool big = SessionState.GetBool(BigGapKey, false);
            float fast = big ? 20f : 14f, slow = big ? 6f : 9f;
            speedKmh[1] = swap ? slow : fast; speedKmh[2] = swap ? fast : slow; });
        float t;
        if (SessionState.GetBool(QuitKey, false) || SessionState.GetBool(QuitLeadKey, false))
            Add(run + 9f, "duo_quit", () =>
            {
                bool p2Leads = SessionState.GetBool(Player2LeadsKey, false);
                bool leading = SessionState.GetBool(QuitLeadKey, false);
                int stop = leading ? (p2Leads ? 2 : 1) : (p2Leads ? 1 : 2);
                speedKmh[stop] = 0f;
                Log($"  [quit] 쳇바퀴 {stop} 멈춤 ({(leading ? "앞선" : "뒤처진")} 사람 중도포기 흉내)");
            });
        Add(run + 1f, "duo_run_1s", () => { Capture("06_run_1s_w1", 1); Capture("06_run_1s_w2", 2); });
        Add(run + 8f, "duo_run_8s", () => { Capture("07_run_8s_w1", 1); Capture("07_run_8s_w2", 2); CaptureSpectator("07_spectator_run"); Check("run8"); });
        // 85형 앞서는 중 표시 확인용: 2인 경주 동안 1초마다 위쪽 캡처 (seq_xx.jpg)
        for (int k = 0; k < (SessionState.GetBool(SequenceKey, true) ? 20 : 0); k++)
        {
            int n = k;
            Add(run + 2f + k + 0.5f, $"spec_seq_{n:00}", () => { LogFollow($"seq_{n:00}"); CaptureSpectator($"seq_{n:00}"); });
        }
        Add(run + 18f, "duo_run_18s", () => { Capture("08_run_finish_w1", 1); CaptureSpectator("08_spectator_run_18s"); Check("run18"); });
        // 긴 프레임 멈춤 점검: 달리기 끝 3초 전에 에디터를 12초 멈춘다 (현장 PC 순간 멈춤 흉내).
        // 멈춘 뒤 첫 프레임에 달리기가 끝나도, 두 쳇바퀴 모두 남은 체험 시간만큼만 발전량이 더해져야 한다 (스크립트 실행 순서와 무관).
        Add(run + 20f, "duo_hitch", () => System.Threading.Thread.Sleep(12000),
            () => GameRound.Instance.Current == GameRound.Phase.Run && GameRound.Instance.RunRemaining <= 3f);
        Add(run + c.runSeconds + 0.5f, "duo_voltage", () => { speedKmh[1] = speedKmh[2] = 0f; Capture("09_voltage_w1", 1); LogRanks("voltage"); CheckEnergyRatio(); },
            () => Session(1).Current == WheelSession.State.SelectVoltage && Session(2).Current == WheelSession.State.SelectVoltage);
        Add(run + c.runSeconds + 1.5f, "duo_select", () => { Session(1).SelectVoltage(1); Session(2).SelectVoltage(0); },
            () => Session(1).Current == WheelSession.State.SelectVoltage && Session(2).Current == WheelSession.State.SelectVoltage);
        // 판매 금액 슬롯: 도는 중 / 일부 자리 멈춤 / 다 맞춰짐
        float slot = run + c.runSeconds + 1.5f + SaleSlot.StartTime(c);
        Add(slot + 0.15f, "slot_spin", () => Capture("10a_slot_spin_w1", 1));
        Add(slot + SaleSlot.Duration(c) * 0.6f, "slot_partial", () => Capture("10b_slot_partial_w1", 1));
        Add(slot + SaleSlot.Duration(c) + 0.2f, "duo_sale", () => { Capture("10_sale_w1", 1); Capture("10_sale_w2", 2); });
        Add(run + c.runSeconds + 1.5f + c.saleSeconds + 1f, "duo_ranking", () => { Capture("11_ranking_w1", 1); Capture("11_ranking_w2", 2); CaptureSpectator("11_spectator_ranking"); LogRanks("ranking"); });
        t = run + c.runSeconds + 1.5f + c.saleSeconds + c.resultAutoCloseSeconds + 1.5f;
        Add(t, "duo_idle", () => { Capture("12_idle_after_w1", 1); CaptureSpectator("12_spectator_idle"); Check("idle_after_duo"); });

        // ── 1인 체험 (빈 레인에 방금 기록 중 1위가 고스트로) ──
        t += 1f;
        Add(t, "solo_start_w2", () => Session(2).StartExperience(), () => GameRound.Instance.Current == GameRound.Phase.Idle);
        count = t + c.joinWaitSeconds + c.readySeconds;
        Add(count + 0.3f, "solo_count", () => speedKmh[2] = 11f);
        run = count + c.countdownSeconds;
        Add(run + 6f, "solo_run", () => { Capture("13_solo_run_w2", 2); Capture("14_solo_busy_w1", 1); CaptureSpectator("13_spectator_solo"); Check("solo_run"); });
        Add(run + c.runSeconds + 0.5f, "solo_end", () => { speedKmh[2] = 0f; Session(2).SelectVoltage(1); LogRanks("solo"); },
            () => Session(2).Current == WheelSession.State.SelectVoltage);
        t = run + c.runSeconds + 0.5f + c.saleSeconds + c.resultAutoCloseSeconds + 1.5f;
        Add(t, "solo_idle", () => { CaptureSpectator("15_spectator_idle"); Check("idle_after_solo"); });

        // ── 전압을 고르지 않고 떠남: 무입력 시간이 지나면 기록 없이 대기화면으로 돌아가고 회차도 끝나야 한다 ──
        t += 1f;
        int recordsBefore = 0;
        Add(t, "leave_start_w1", () => { recordsBefore = Session(1).Ranking.Count; Session(1).StartExperience(); }, () => GameRound.Instance.Current == GameRound.Phase.Idle);
        run = t + c.joinWaitSeconds + c.readySeconds + c.countdownSeconds;
        Add(run + c.runSeconds + 0.5f, "leave_voltage", () => c.idleTimeoutSeconds = 3f, () => Session(1).Current == WheelSession.State.SelectVoltage);
        t = run + c.runSeconds + 0.5f + 4f;
        Add(t, "leave_idle", () =>
        {
            bool ok = Session(1).Current == WheelSession.State.Idle && GameRound.Instance.Current == GameRound.Phase.Idle && Session(1).Ranking.Count == recordsBefore;
            Log($"  [leave] 무입력 복귀 {(ok ? "정상" : "실패")} - 기록 수 {recordsBefore} → {Session(1).Ranking.Count}");
        });
        // ── 쳇바퀴 2 화면 사용 안 함: 캔버스·카메라가 꺼지고 시작이 무시되며, 85형에 점검 중으로 나와야 한다 (메모리 값만 바꿈) ──
        int savedDisplay = 0;
        Add(t + 0.5f, "disabled_w2", () =>
        {
            savedDisplay = Cfg.wheels[1].displayIndex;
            Cfg.wheels[1].displayIndex = -1;
            DisplayRouting.Apply(-1);
            Session(2).StartExperience();
        });
        Add(t + 1.5f, "disabled_w2_check", () =>
        {
            Canvas canvas = null;
            foreach (var ws in UnityEngine.Object.FindObjectsOfType<WheelScreen>(true))
                if (ws.name.EndsWith("2")) canvas = ws.GetComponent<Canvas>();
            Camera cam = null;
            foreach (var rc in UnityEngine.Object.FindObjectsOfType<RaceCamera>(true))
                if (rc.Screen == RaceCamera.Target.Wheel && rc.WheelId == 2) cam = rc.GetComponent<Camera>();
            bool ok = Session(2).Current == WheelSession.State.Idle && !Session(2).Enabled
                && canvas != null && !canvas.enabled && cam != null && !cam.enabled;
            Log($"  [disabled] 쳇바퀴 2 사용 안 함 {(ok ? "정상" : "실패")} - 상태 {Session(2).Current}, 캔버스 {(canvas != null ? canvas.enabled.ToString() : "없음")}, 카메라 {(cam != null ? cam.enabled.ToString() : "없음")}");
            CaptureSpectator("16_spectator_w2_disabled");
            Cfg.wheels[1].displayIndex = savedDisplay;
            DisplayRouting.Apply(savedDisplay);
        });
        Add(t + 2.5f, "disabled_w2_restore", () =>
        {
            Log($"  [disabled] 되돌림: 쳇바퀴 2 Enabled={Session(2).Enabled}");
        });
        Add(t + 3f, "finish", Finish);

        // 85형 시점 전환 점검이면 위 시험 대신 긴 2인 경주 하나만
        followTrace.Clear();
        spectatorCamera = null;
        if (SessionState.GetBool(FollowKey, false))
        {
            steps.Clear();
            BuildFollowSteps(c);
        }

        watchPhase = (GameRound.Phase)(-1);
        watchRound = 0;
        watchShot.Clear();
        revealAt = -1f;
        SpectatorScreen.RankingRevealed -= HandleRankingRevealed;
        SpectatorScreen.RankingRevealed += HandleRankingRevealed;

        startTime = EditorApplication.timeSinceStartup;
        nextRotation[1] = nextRotation[2] = startTime;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Log("시험 시작");
    }

    private static void Add(float at, string name, Action action, Func<bool> until = null)
    {
        // 시각 순서대로 끼워 넣는다 (같은 시각이면 먼저 넣은 것이 먼저)
        int i = steps.Count;
        while (i > 0 && steps[i - 1].at > at) i--;
        steps.Insert(i, new Step { at = at, name = name, action = action, until = until });
    }

    // 85형 앞서는 중 표시·선의 위치를 달리는 동안 매 업데이트 기록한다 (trace.csv). 흔들림을 수치로 보기 위해서다.
    private static readonly StringBuilder trace = new StringBuilder();
    private static void TraceAhead(float elapsed)
    {
        var round = GameRound.Instance;
        if (round == null || round.Current != GameRound.Phase.Run) return;
        var spec = UnityEngine.Object.FindObjectOfType<SpectatorScreen>();
        if (spec == null) return;
        var so = new SerializedObject(spec);
        var markers = so.FindProperty("aheadMarkers");
        if (trace.Length == 0) trace.AppendLine("t,run,i,marker,mx,my,dist1,dist2");
        for (int i = 0; i < markers.arraySize; i++)
        {
            var m = markers.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
            if (m == null) continue;
            trace.AppendLine($"{elapsed:0.000},{round.RunElapsed:0.000},{i},{(m.gameObject.activeSelf ? 1 : 0)},{m.anchoredPosition.x:0.0},{m.anchoredPosition.y:0.0}," +
                $"{RaceStage.Instance.Runner(1).DistanceM:0.00},{RaceStage.Instance.Runner(2).DistanceM:0.00}");
        }
    }

    // 85형 앞선 사람 따라가기 상태 (화면 기준 거리, 따라가는 중, 중도포기, 바퀴 화면 위치, 추격 중 표시)
    private static void LogFollow(string tag)
    {
        var stage = RaceStage.Instance;
        var spec = UnityEngine.Object.FindObjectOfType<SpectatorScreen>();
        if (stage == null || spec == null) return;
        var behind = new SerializedObject(spec).FindProperty("behindMarkers");
        var sb = new StringBuilder($"  [follow {tag}] 거리 1={stage.Runner(1).DistanceM:0.0} 2={stage.Runner(2).DistanceM:0.0} 기준={stage.SpectatorBase:0.0} 앞선사람시점={stage.SpectatorFollowingLeader} 뒤포기={stage.TrailingQuit} 앞포기={stage.LeadingQuit}");
        Camera cam = null;
        foreach (var rc in UnityEngine.Object.FindObjectsOfType<RaceCamera>())
            if (rc.Screen == RaceCamera.Target.Spectator) cam = rc.GetComponent<Camera>();
        for (int id = 1; id <= 2; id++)
        {
            if (cam != null)
            {
                Vector3 v = cam.WorldToViewportPoint(stage.Runner(id).WheelCenterPoint);
                sb.Append($" 바퀴{id} 화면y={v.y:0.00}{(v.z <= 0f ? "(카메라 뒤)" : "")}");
            }
            var m = behind.arraySize >= id ? behind.GetArrayElementAtIndex(id - 1).objectReferenceValue as RectTransform : null;
            if (m != null && m.gameObject.activeSelf) sb.Append($" 추격표시{id}=\"{m.GetComponentInChildren<TMPro.TMP_Text>().text}\"");
        }
        Log(sb.ToString());
    }

    // 조건을 기다리는 최대 시간(초). 넘으면 실패로 남기고 그대로 진행한다.
    private const float MaxWaitSeconds = 30f;
    private static double waitStart = -1;

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        float elapsed = (float)(now - startTime);

        // 센서 회전 신호 흉내: 신호 1개 게임 거리(둘레 / pulsesPerRotation × 속도 배율) / 화면 속도 마다 신호 1개
        for (int id = 1; id <= 2; id++)
        {
            if (speedKmh[id] <= 0f || now < nextRotation[id]) { if (speedKmh[id] <= 0f) nextRotation[id] = now; continue; }
            var sensor = WheelSensorLink.Link != null ? WheelSensorLink.Link.GetSensor(id) : null;
            // 다음 시각을 '지금 + 간격'으로 잡으면 업데이트 주기만큼 간격이 늘어 속도가 낮게 나온다.
            // 예정 시각에 간격을 더해 가고, 업데이트가 늦어 밀린 신호는 실제 수신처럼 한 번에 보낸다.
            float interval = Cfg.GameDistancePerPulseM / (speedKmh[id] / 3.6f);
            if (now - nextRotation[id] > 0.5) nextRotation[id] = now;   // 캡처 등으로 크게 멈췄으면 몰아서 보내지 않는다
            while (now >= nextRotation[id])
            {
                sensor?.ApplyPulse(Time.unscaledTime, Cfg);
                nextRotation[id] += interval;
            }
        }

        WatchSpectator();
        TraceAhead(elapsed);
        if (SessionState.GetBool(FollowKey, false)) { FollowSpeeds(); TraceFollow(); }

        while (nextStep < steps.Count && elapsed >= steps[nextStep].at)
        {
            Step s = steps[nextStep];
            if (s.until != null && !s.until())
            {
                if (waitStart < 0) waitStart = now;
                if (now - waitStart < MaxWaitSeconds)
                {
                    startTime = now - s.at;   // 시험 시계를 이 단계에 멈춰 둔다
                    break;
                }
                Log($"{s.name}: 대기 조건이 {MaxWaitSeconds}s 안에 맞지 않음 (실패) - {StateLine()}");
            }
            waitStart = -1;
            nextStep++;
            try
            {
                s.action();
                Log($"{s.name}: {StateLine()}");
            }
            catch (Exception e)
            {
                Log($"{s.name}: 오류 {e.Message}");
            }
        }
    }

    // 85형 관전 화면은 시험 시간표가 아니라 회차 단계가 바뀐 시점을 기준으로 찍는다 (플레이 모드 진입·캡처로 진행이 밀려도 안내 카드를 놓치지 않게).
    // 단계마다 들어간 지 0.6초 뒤 1장, 경기 결과(Post)는 승자 발표·결과 집계·종합 순위 발표를 각각 1장.
    private static GameRound.Phase watchPhase = (GameRound.Phase)(-1);
    private static float watchEnter;
    private static int watchEnterFrame;
    private static int revealFrame;
    // 캡처 전에 지나야 하는 게임 프레임 수. 긴 멈춤 뒤에는 시간은 지나도 프레임이 1~2번뿐이라 카드 페이드인이 안 끝난 모습이 찍힌다.
    private const int SettleFrames = 10;
    private static int watchRound;
    private static readonly HashSet<string> watchShot = new HashSet<string>();
    private static float revealAt = -1f;

    private static void WatchSpectator()
    {
        var r = GameRound.Instance;
        if (r == null) return;
        float now = Time.unscaledTime;
        if (r.Current != watchPhase)
        {
            watchPhase = r.Current;
            watchEnter = now;
            watchEnterFrame = Time.frameCount;
            if (r.Current == GameRound.Phase.Open)
            {
                watchRound++;
                revealAt = -1f;
            }
        }
        float e = Time.frameCount - watchEnterFrame >= SettleFrames ? now - watchEnter : 0f;
        string prefix = $"s{watchRound}_";
        switch (r.Current)
        {
            case GameRound.Phase.Open:
            case GameRound.Phase.Ready:
                ShotOnce(e >= 0.6f, prefix + r.Current.ToString().ToLower());
                break;
            case GameRound.Phase.Countdown:
                ShotOnce(e >= 0.25f, prefix + "count");
                break;
            case GameRound.Phase.Run:
                ShotOnce(e >= 0.3f, prefix + "go");
                break;
            case GameRound.Phase.Post:
                ShotOnce(e >= 0.6f, prefix + "post_winner");
                ShotOnce(e >= Cfg.spectatorWinnerSeconds + 0.6f && revealAt < 0f, prefix + "post_tally");
                break;
        }
        ShotOnce(revealAt >= 0f && now - revealAt >= 0.6f && Time.frameCount - revealFrame >= SettleFrames, prefix + "post_ranking");
    }

    private static void ShotOnce(bool when, string name)
    {
        if (!when || !watchShot.Add(name)) return;
        CaptureSpectator(name);
        var notice = UnityEngine.Object.FindObjectOfType<SpectatorNotice>(true);
        string card = "-";
        if (notice != null)
        {
            var so = new SerializedObject(notice);
            var group = (CanvasGroup)so.FindProperty("card").objectReferenceValue;
            var title = (TMPro.TMP_Text)so.FindProperty("titleText").objectReferenceValue;
            card = $"카드 {(group.gameObject.activeSelf ? "켜짐" : "꺼짐")} 알파 {group.alpha:0.00} 제목 \"{title.text}\" dt={Time.unscaledDeltaTime:0.000}";
        }
        Log($"  [spectator] {name} 캡처 ({StateLine()}) {card}");
    }

    private static void HandleRankingRevealed()
    {
        revealAt = Time.unscaledTime;
        revealFrame = Time.frameCount;
    }

    // 종합순위 점검: 두 쳇바퀴 기록을 합친 순위가 나와야 한다
    // 발전량은 출력(속도에 비례) × 시간 적산, 거리는 회전 신호 수라 따로 계산된다. 일정한 속도로 달리면 거리 1m 당 발전량은 두 쳇바퀴가 같아야 한다.
    private static void CheckEnergyRatio()
    {
        float r1 = Session(1).EnergyWh / Mathf.Max(0.1f, Session(1).DistanceM);
        float r2 = Session(2).EnergyWh / Mathf.Max(0.1f, Session(2).DistanceM);
        float diff = Mathf.Abs(r1 / Mathf.Max(1e-6f, r2) - 1f);
        Log($"  [energy] 1m 당 발전량 w1 {r1 * 1000f:0.00}mWh/m, w2 {r2 * 1000f:0.00}mWh/m, 차이 {diff * 100f:0}% → {(diff < 0.15f ? "정상" : "실패")}");
    }

    private static void LogRanks(string tag)
    {
        var sb = new StringBuilder($"  [{tag}] 오늘 전체 {WheelRankingStore.TotalCount}명");
        for (int id = 1; id <= 2; id++)
        {
            var s = Session(id);
            sb.Append($" / w{id} {s.Current} 발전 {s.EnergyRank}위 판매 {s.SaleRank}위 ({s.EnergyWh:0.000}Wh {s.DistanceM:0.0}m, 체험자 {s.TodayCount}명)");
        }
        Log(sb.ToString());
    }

    private static string StateLine()
    {
        var r = GameRound.Instance;
        return $"round={r?.Current} w1={Session(1)?.Current} w2={Session(2)?.Current}";
    }

    // 레인 위치·표시 점검
    private static void Check(string tag)
    {
        var stage = RaceStage.Instance;
        var spec = UnityEngine.Object.FindObjectOfType<SpectatorScreen>();
        if (spec != null)
        {
            var markers = new SerializedObject(spec).FindProperty("aheadMarkers");
            var line = new StringBuilder($"  [{tag}] 85형 앞서는 중 표시:");
            for (int i = 0; i < markers.arraySize; i++)
            {
                var m = markers.GetArrayElementAtIndex(i).objectReferenceValue as RectTransform;
                if (m == null) continue;
                line.Append($" {m.name}={(m.gameObject.activeSelf ? m.GetComponentInChildren<TMPro.TMP_Text>().text : "숨김")}");
            }
            Log(line.ToString());
        }
        for (int id = 1; id <= 2; id++)
        {
            RaceRunner runner = stage.Runner(id);
            var so = new SerializedObject(runner);
            var character = (Transform)so.FindProperty("character").objectReferenceValue;
            var wheel = (Transform)so.FindProperty("wheel").objectReferenceValue;
            var anim = (Animator)so.FindProperty("characterAnimator").objectReferenceValue;
            string state = anim != null && anim.isActiveAndEnabled ? (anim.GetCurrentAnimatorStateInfo(0).loop ? "loop" : "once") + " t=" + anim.GetCurrentAnimatorStateInfo(0).normalizedTime.ToString("F1") + " spd=" + anim.speed.ToString("F2") : "-";
            var cam = GameObject.Find($"Camera_Wheel_{id}")?.GetComponent<Camera>();
            Vector3 vp = cam != null && character != null ? cam.WorldToViewportPoint(character.position) : Vector3.zero;
            Transform screen2 = character != null ? character.parent.Find(character.name + "_Screen2") : null;
            Log($"  [{tag}] lane{id} mode={runner.CurrentMode} dist={runner.DistanceM:0.0} speed={runner.SpeedMps:0.0} " +
                $"charZ(화면1)={(character ? character.localPosition.z : 0):0.00} charZ(화면2)={(screen2 ? screen2.localPosition.z : 0):0.00} " +
                $"wheelZ={(wheel ? wheel.localPosition.z : 0):0.00} anim={state} " +
                $"viewport=({vp.x:0.00},{vp.y:0.00},{vp.z:0.0}) session={Session(id)?.DistanceM:0.0}m " +
                $"1위선(화면{id})={stage.ScreenRecordVisible(id)} z={(stage.ScreenRecordVisible(id) ? stage.ScreenRecordPoint(id).z : 0f):0.0} " +
                $"지금순위={Session(id)?.LiveEnergyRank}/{Session(id)?.LiveTotal}");
        }
    }

    private static Texture2D RenderCamera(string name, int w, int h)
    {
        var cam = GameObject.Find(name)?.GetComponent<Camera>();
        if (cam == null) return null;
        var rt = RenderTexture.GetTemporary(w, h, 24, RenderTextureFormat.ARGB32);
        RenderTexture prev = cam.targetTexture;
        cam.targetTexture = rt;
        cam.Render();
        cam.targetTexture = prev;
        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return tex;
    }

    private static void Capture(string file, int wheelId)
    {
        Texture2D bg = RenderCamera($"Camera_Wheel_{wheelId}", 1920, 1080);
        GameObject canvas = GameObject.Find($"Canvas_Wheel_{wheelId}");
        GameObject panel = null;
        foreach (var p in canvas.GetComponentsInChildren<WheelPanel>(false)) panel = p.gameObject;
        if (panel != null) PowerSpriteExporter.RenderLive(panel, bg, PowerUIKit.ReferenceResolution, Path.Combine(outDir, file + ".jpg"));
        if (bg != null) UnityEngine.Object.DestroyImmediate(bg);
    }

    private static void CaptureSpectator(string file)
    {
        Texture2D bg = RenderCamera("Camera_Spectator", 1080, 1920);
        GameObject panel = GameObject.Find("Canvas_Spectator").transform.Find("Frame").GetChild(0).gameObject;
        PowerSpriteExporter.RenderLive(panel, bg, PowerUIKit.SpectatorResolution, Path.Combine(outDir, file + ".jpg"));
        if (bg != null) UnityEngine.Object.DestroyImmediate(bg);
    }

    private static void Log(string line)
    {
        string text = $"{EditorApplication.timeSinceStartup - startTime:0.0}s {line}";
        log.AppendLine(text);
        Debug.Log("[PlayTest] " + text);
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        File.WriteAllText(Path.Combine(outDir, "log.txt"), log.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outDir, "trace.csv"), trace.ToString(), new UTF8Encoding(false));
        if (followTrace.Length > 0) File.WriteAllText(Path.Combine(outDir, "follow.csv"), followTrace.ToString(), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(outDir, "done.txt"), "done");
        EditorApplication.isPlaying = false;
    }
}
