using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

// 플레이 모드 자동 시험 (개발용). 센서 회전 신호를 흉내 내며 2인 경주 → 1인(1위 고스트) 체험을 끝까지 진행하고,
// 화면마다 3D 장면 + UI 를 합친 캡처와 점검 결과를 outDir 에 남긴다.
//  PowerPlayTest.Start(outDir)  → 플레이 모드에 들어가 시험을 시작한다. 끝나면 outDir/done.txt 를 쓰고 플레이 모드를 끈다.
// 에디터 창에 포커스가 없어도 진행되도록 EditorApplication.update 로 돈다 (PlayerSettings.runInBackground 가 켜져 있어야 함).
[InitializeOnLoad]
public static class PowerPlayTest
{
    private const string PendingKey = "PowerPlayTest.OutDir";

    private class Step
    {
        public float at;          // 시험 시작 후 시각(초)
        public string name;
        public Action action;
    }

    private static readonly List<Step> steps = new List<Step>();
    private static readonly StringBuilder log = new StringBuilder();
    private static string outDir;
    private static double startTime;
    private static int nextStep;
    // 쳇바퀴별 흉내 속도(km/h). 0 이면 신호 없음.
    private static readonly float[] speedKmh = new float[3];
    private static readonly double[] nextRotation = new double[3];

    static PowerPlayTest()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode) return;
            string dir = SessionState.GetString(PendingKey, "");
            if (string.IsNullOrEmpty(dir)) return;
            SessionState.EraseString(PendingKey);
            Begin(dir);
        };
    }

    public static void Start(string dir)
    {
        Directory.CreateDirectory(dir);
        foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
        PlayerSettings.runInBackground = true;
        SessionState.SetString(PendingKey, dir);
        EditorApplication.isPlaying = true;
    }

    private static GameDynamicData Cfg => JsonManager.instance.gameDynamicData;
    private static WheelSession Session(int id) => Array.Find(UnityEngine.Object.FindObjectsOfType<WheelSession>(), s => s.WheelId == id);

    private static void Begin(string dir)
    {
        outDir = dir;
        log.Clear();
        steps.Clear();
        nextStep = 0;
        Array.Clear(speedKmh, 0, speedKmh.Length);

        // 화면 하나씩 볼 시간만 남기고 줄인다 (메모리 값만 바꾸고 파일에는 저장하지 않는다)
        var c = Cfg;
        c.joinWaitSeconds = 3f; c.readySeconds = 1.5f; c.guideSeconds = 2f; c.countdownSeconds = 3; c.runSeconds = 24f;
        c.saleSeconds = 4f; c.saleStepSeconds = 0.4f; c.resultAutoCloseSeconds = 3f; c.finishApproachSeconds = 8f; c.idleTimeoutSeconds = 30f;

        // ── 2인 경주: 쳇바퀴 1 이 빠르고(14km/h) 2 가 느리다(9km/h) ──
        // 시각: 1.0 쳇바퀴1 시작(Open) → 2.2 쳇바퀴2 참여(전원 → Ready) → 준비·안내·카운트다운 → 달리기
        Add(1f, "duo_start_w1", () => Session(1).StartExperience());
        Add(1.8f, "duo_join_screens", () => { Capture("01_join_w1_waiting", 1); Capture("02_join_w2_invite", 2); });
        Add(2.2f, "duo_start_w2", () => Session(2).StartExperience());
        float guide = 2.2f + c.readySeconds;
        float count = guide + c.guideSeconds;
        float run = count + c.countdownSeconds;
        Add(2.2f + c.readySeconds * 0.5f, "duo_ready", () => Capture("03_ready_w1", 1));
        Add(guide + c.guideSeconds * 0.5f, "duo_guide", () => Capture("04_guide_w1", 1));
        Add(count + 0.3f, "duo_count", () => { Capture("05_count_w1", 1); speedKmh[1] = 14f; speedKmh[2] = 9f; });
        float t;
        Add(run + 1f, "duo_run_1s", () => { Capture("06_run_1s_w1", 1); Capture("06_run_1s_w2", 2); });
        Add(run + 8f, "duo_run_8s", () => { Capture("07_run_8s_w1", 1); Capture("07_run_8s_w2", 2); CaptureSpectator("07_spectator_run"); Check("run8"); });
        Add(run + 18f, "duo_run_18s", () => { Capture("08_run_finish_w1", 1); Check("run18"); });
        Add(run + c.runSeconds + 0.5f, "duo_voltage", () => { speedKmh[1] = speedKmh[2] = 0f; Capture("09_voltage_w1", 1); });
        Add(run + c.runSeconds + 1.5f, "duo_select", () => { Session(1).SelectVoltage(1); Session(2).SelectVoltage(0); });
        Add(run + c.runSeconds + 4.5f, "duo_sale", () => { Capture("10_sale_w1", 1); Capture("10_sale_w2", 2); });
        Add(run + c.runSeconds + 1.5f + c.saleSeconds + 1f, "duo_ranking", () => { Capture("11_ranking_w1", 1); CaptureSpectator("11_spectator_post"); });
        t = run + c.runSeconds + 1.5f + c.saleSeconds + c.resultAutoCloseSeconds + 1.5f;
        Add(t, "duo_idle", () => { Capture("12_idle_after_w1", 1); CaptureSpectator("12_spectator_idle"); Check("idle_after_duo"); });

        // ── 1인 체험 (빈 레인에 방금 기록 중 1위가 고스트로) ──
        t += 1f;
        Add(t, "solo_start_w2", () => Session(2).StartExperience());
        count = t + c.joinWaitSeconds + c.readySeconds + c.guideSeconds;
        Add(count + 0.3f, "solo_count", () => speedKmh[2] = 11f);
        run = count + c.countdownSeconds;
        Add(run + 6f, "solo_run", () => { Capture("13_solo_run_w2", 2); Capture("14_solo_busy_w1", 1); CaptureSpectator("13_spectator_solo"); Check("solo_run"); });
        Add(run + c.runSeconds + 0.5f, "solo_end", () => { speedKmh[2] = 0f; Session(2).SelectVoltage(1); });
        t = run + c.runSeconds + 0.5f + c.saleSeconds + c.resultAutoCloseSeconds + 1.5f;
        Add(t, "solo_idle", () => { CaptureSpectator("15_spectator_idle"); Check("idle_after_solo"); });
        Add(t + 0.5f, "finish", Finish);

        startTime = EditorApplication.timeSinceStartup;
        nextRotation[1] = nextRotation[2] = startTime;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        Log("시험 시작");
    }

    private static void Add(float at, string name, Action action) => steps.Add(new Step { at = at, name = name, action = action });

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            EditorApplication.update -= Tick;
            return;
        }
        double now = EditorApplication.timeSinceStartup;
        float elapsed = (float)(now - startTime);

        // 센서 회전 신호 흉내: 1회전 거리 / 속도 마다 신호 1개
        for (int id = 1; id <= 2; id++)
        {
            if (speedKmh[id] <= 0f || now < nextRotation[id]) { if (speedKmh[id] <= 0f) nextRotation[id] = now; continue; }
            var sensor = WheelSensorLink.Link != null ? WheelSensorLink.Link.GetSensor(id) : null;
            sensor?.ApplyRotation(Time.unscaledTime, Cfg);
            nextRotation[id] = now + Cfg.wheelCircumferenceM / (speedKmh[id] / 3.6f);
        }

        while (nextStep < steps.Count && elapsed >= steps[nextStep].at)
        {
            Step s = steps[nextStep++];
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

    private static string StateLine()
    {
        var r = GameRound.Instance;
        return $"round={r?.Current} w1={Session(1)?.Current} w2={Session(2)?.Current}";
    }

    // 레인 위치·표시 점검
    private static void Check(string tag)
    {
        var stage = RaceStage.Instance;
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
                $"viewport=({vp.x:0.00},{vp.y:0.00},{vp.z:0.0}) session={Session(id)?.DistanceM:0.0}m");
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
        File.WriteAllText(Path.Combine(outDir, "done.txt"), "done");
        EditorApplication.isPlaying = false;
    }
}
