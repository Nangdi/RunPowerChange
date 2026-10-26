using System.Collections.Generic;
using UnityEngine;

// 쳇바퀴 1대 레인. 디자이너 맵의 캐릭터(32형)와 바퀴(85형)를 움직인다.
// 맵은 캐릭터가 제자리에서 뛰고 바닥·기둥이 다가오는 방식이라(RaceStage), 레인은 앞뒤 간격만 바꾼다.
//
// 32형은 화면마다 자기 캐릭터를 기준으로 보여 준다. 그래서 캐릭터를 화면 수만큼(Screen1/Screen2 레이어) 두고
//  - 그 화면의 체험자 캐릭터는 제자리
//  - 상대 캐릭터는 거리 차만큼 앞(내가 뒤처질 때) 또는 뒤(내가 앞설 때, 화면 밖으로 나가도 됨)로 간다 (RaceStage.ScreenGapZ)
// 85형 바퀴는 두 레인이 다 보이도록 더 많이 달린 쪽만 앞으로 나간다 (RaceStage.GapZ).
// 85형에서는 이번 회차 참가자가 아닌 레인(대기 중 빈 레인, 1위 고스트 레인)의 바퀴를 반투명으로 그려 사람이 없는 레인임을 알린다.
//
// 표시 방식
//  - Player : 자기 쳇바퀴 체험자 (대기 중에는 출발선에 서 있음)
//  - Leader : 혼자 달리는 회차에서, 참가하지 않은 쪽 레인에 오늘(없으면 전날) 1위 기록을 반투명 고스트로 달리게 한다
//  - Hidden : 보여 줄 것이 없음 (이번 회차에 참가하지 않았고 1위 기록도 없음)
public class RaceRunner : MonoBehaviour
{
    public enum Mode { Hidden, Player, Leader }

    private const int ScreenCount = 2;

    [SerializeField] private int wheelId = 1;
    [SerializeField] private WheelSession session;

    [Header("32형 캐릭터")]
    [Tooltip("쳇바퀴 1 화면용 캐릭터. 쳇바퀴 2 화면용은 실행할 때 복제한다")]
    [SerializeField] private Transform character;
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private string runState = "달리기";
    [SerializeField] private string idleState = "아이들";
    [Tooltip("이 속도(m/s)일 때 달리기 애니메이션을 1배속으로 재생")]
    [SerializeField] private float runAnimationMps = 3f;
    [Tooltip("달리기 애니메이션 배속 범위. 좁을수록 속도가 출렁여도 동작이 자연스럽다")]
    [SerializeField] private Vector2 runAnimationSpeedRange = new Vector2(0.7f, 1.6f);
    [Tooltip("이 속도(m/s)보다 빠르면 달리기, runToIdleMps 보다 느리면 서기 (사이 값에서는 지금 동작 유지)")]
    [SerializeField] private float idleToRunMps = 0.6f;
    [SerializeField] private float runToIdleMps = 0.25f;
    [Tooltip("애니메이션에 쓰는 속도의 평활 시간(초). 회전 신호가 띄엄띄엄 와도 배속이 튀지 않게")]
    [SerializeField] private float animationSmoothingSeconds = 0.6f;
    [Tooltip("ME 표시와 캐릭터 머리 꼭대기 사이 여백 (유닛)")]
    [SerializeField] private float headMargin = 0.1f;
    [Tooltip("반투명 고스트용 재질 (HDRP Lit Transparent). 원래 재질의 텍스처·색에 알파만 낮춰 칠한다.")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.4f;

    [Header("85형 바퀴")]
    [SerializeField] private Transform wheel;
    [Tooltip("바퀴 회전·화살표 흐름 애니메이터 (1배속 = 1초에 1바퀴)")]
    [SerializeField] private Animator[] wheelAnimators;
    [Tooltip("이 속도(m/s)일 때 바퀴 애니메이션을 1배속으로 재생")]
    [SerializeField] private float wheelAnimationMps = 3f;

    [Header("움직임")]
    [Tooltip("표시 거리가 실제 거리를 따라가는 빠르기")]
    [SerializeField] private float followSharpness = 6f;
    [Tooltip("앞뒤 간격이 바뀌는 빠르기")]
    [SerializeField] private float gapSharpness = 3f;
    [Tooltip("32형에서 상대가 내 캐릭터보다 이만큼(유닛) 뒤로 처지면 옅어지기 시작한다")]
    [SerializeField] private float fadeStartBehind = 0.3f;
    [Tooltip("이만큼(유닛) 뒤로 처지면 완전히 사라진다. 카메라 바로 앞을 크게 지나가기 전에 서서히 없어지게")]
    [SerializeField] private float fadeEndBehind = 2.2f;

    public int WheelId => wheelId;
    public WheelSession Session => session;
    public Mode CurrentMode { get; private set; } = Mode.Hidden;
    // 화면에 그려지는 거리(m)
    public float DistanceM { get; private set; }
    public float SpeedMps { get; private set; }
    public bool IsActive => CurrentMode != Mode.Hidden;

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    // HDRP Lit 은 _BaseColorMap, URP Lit 은 _BaseMap
    private static readonly int[] BaseMapIds = { Shader.PropertyToID("_BaseColorMap"), Shader.PropertyToID("_BaseMap") };

    // 화면별 캐릭터 한 벌 (인덱스 = 화면 wheelId - 1)
    private class View
    {
        public Transform root;
        public Animator animator;
        public Renderer[] renderers;
        public float gapZ;
        public float alpha = -1f;   // 지금 칠한 불투명도 (-1 = 아직 안 칠함)
    }

    private readonly View[] views = new View[ScreenCount];
    private Vector3 characterBase;
    private Vector3 wheelBase;
    private float wheelGapZ;
    private bool snapGap = true;
    private float animSpeedMps;
    private bool running;
    private Mode builtMode = (Mode)(-1);
    private readonly Dictionary<Renderer, Material[]> originalMaterials = new Dictionary<Renderer, Material[]>();
    private readonly Dictionary<Renderer, Material[]> wheelMaterials = new Dictionary<Renderer, Material[]>();
    private bool wheelGhost;
    private bool wheelGhostBuilt;

    private void Awake()
    {
        if (wheel != null)
        {
            wheelBase = wheel.localPosition;
            foreach (var r in wheel.GetComponentsInChildren<Renderer>(true))
                wheelMaterials[r] = r.sharedMaterials;
        }
        if (character == null) return;

        characterBase = character.localPosition;
        for (int i = 0; i < ScreenCount; i++)
        {
            Transform root = i == 0 ? character : Instantiate(character, character.parent);
            if (i > 0) root.name = $"{character.name}_Screen{i + 1}";
            RaceLayers.SetLayerRecursively(root.gameObject, RaceLayers.Layer(RaceLayers.Screen(i + 1)));
            Animator anim = i == 0 ? characterAnimator : root.GetComponentInChildren<Animator>(true);
            views[i] = new View { root = root, animator = anim, renderers = root.GetComponentsInChildren<Renderer>(true) };
            foreach (var r in views[i].renderers)
                originalMaterials[r] = r.sharedMaterials;
        }
    }

    // 쳇바퀴 screenWheelId 화면에 그려지는 이 레인 캐릭터의 머리 꼭대기 위 (ME 표시 자리, 월드 좌표).
    // 달리는 동작에 따라 머리 높이가 바뀌므로 메시 범위로 구한다.
    public Vector3 HeadPoint(int screenWheelId)
    {
        View view = ViewFor(screenWheelId);
        if (view == null) return transform.position;
        bool any = false;
        Bounds b = default;
        foreach (var r in view.renderers)
        {
            if (r == null || !r.enabled) continue;
            if (!any) { b = r.bounds; any = true; }
            else b.Encapsulate(r.bounds);
        }
        if (!any) return view.root.position;
        return new Vector3(b.center.x, b.max.y + headMargin, b.center.z);
    }

    private View ViewFor(int screenWheelId)
    {
        int i = screenWheelId - 1;
        return i >= 0 && i < ScreenCount ? views[i] : null;
    }

    private void Update()
    {
        Resolve(out Mode mode, out float target);
        if (mode != builtMode) Apply(mode);
        CurrentMode = mode;

        // 85형: 이번 회차에 사람이 달리는 레인만 진하게
        var round = GameRound.Instance;
        bool playing = mode == Mode.Player && round != null && round.Current != GameRound.Phase.Idle && round.IsParticipant(session);
        if (!wheelGhostBuilt || wheelGhost == playing)
        {
            wheelGhostBuilt = true;
            wheelGhost = !playing;
            ApplyAlpha(wheelMaterials.Keys, wheelMaterials, wheelGhost ? ghostAlpha : 1f);
        }

        float dt = Time.unscaledDeltaTime;
        float prev = DistanceM;
        // 출발선으로 돌아가는 경우(새 체험)는 바로 옮기고 앞뒤 간격도 바로 맞춘다
        if (target < DistanceM - 0.5f)
        {
            DistanceM = target;
            prev = target;
            snapGap = true;
        }
        else DistanceM = Mathf.Lerp(DistanceM, target, 1f - Mathf.Exp(-followSharpness * dt));
        float speed = dt > 0f ? Mathf.Max(0f, DistanceM - prev) / dt : 0f;
        SpeedMps = Mathf.Lerp(SpeedMps, speed, 1f - Mathf.Exp(-8f * dt));
        if (mode == Mode.Hidden) SpeedMps = 0f;
        animSpeedMps = Mathf.Lerp(animSpeedMps, SpeedMps, 1f - Mathf.Exp(-dt / Mathf.Max(0.05f, animationSmoothingSeconds)));
    }

    // RaceStage 가 레인들의 거리를 본 뒤 화면별 앞뒤 간격과 애니메이션을 정한다
    public void ApplyStage(RaceStage stage, float dt)
    {
        float t = snapGap ? 1f : 1f - Mathf.Exp(-gapSharpness * dt);
        Vector3 forward = stage.LocalForward;
        for (int i = 0; i < ScreenCount; i++)
        {
            View view = views[i];
            if (view == null) continue;
            float target = IsActive ? stage.ScreenGapZ(i + 1, this) : 0f;
            view.gapZ = Mathf.Lerp(view.gapZ, target, t);
            view.root.localPosition = characterBase + forward * view.gapZ;

            // 뒤로 처질수록 서서히 옅어지다 사라진다 (1위 고스트는 원래 반투명에서 더 옅어진다)
            float fade = Mathf.InverseLerp(-fadeEndBehind, -fadeStartBehind, view.gapZ);
            float alpha = (CurrentMode == Mode.Leader ? ghostAlpha : 1f) * fade;
            bool visible = IsActive && fade > 0.01f;
            if (view.root.gameObject.activeSelf != visible)
            {
                view.root.gameObject.SetActive(visible);
                // 다시 켜지면 애니메이터가 기본 동작부터 시작하므로 지금 동작(달리기/서기)으로 맞춘다
                if (visible && view.animator != null) view.animator.Play(running ? runState : idleState, 0, 0f);
            }
            if (visible && (view.alpha < 0f || Mathf.Abs(view.alpha - alpha) > 0.02f || (alpha >= 0.999f) != (view.alpha >= 0.999f)))
            {
                view.alpha = alpha;
                ApplyAlpha(view.renderers, originalMaterials, alpha);
            }
        }

        wheelGapZ = Mathf.Lerp(wheelGapZ, IsActive ? stage.GapZ(DistanceM) : 0f, t);
        if (wheel != null) wheel.localPosition = wheelBase + forward * wheelGapZ;
        snapGap = false;

        AnimateCharacter();
        float wheelSpeed = SpeedMps / Mathf.Max(0.1f, wheelAnimationMps);
        if (wheelAnimators != null)
            foreach (var a in wheelAnimators)
                if (a != null) a.speed = wheelSpeed;
    }

    private void AnimateCharacter()
    {
        bool run = running ? animSpeedMps > runToIdleMps : animSpeedMps > idleToRunMps;
        bool changed = run != running;
        running = run;
        float target = run ? Mathf.Clamp(animSpeedMps / Mathf.Max(0.1f, runAnimationMps), runAnimationSpeedRange.x, runAnimationSpeedRange.y) : 1f;
        foreach (var view in views)
        {
            Animator a = view?.animator;
            if (a == null || !a.isActiveAndEnabled) continue;
            if (changed) a.CrossFadeInFixedTime(run ? runState : idleState, 0.25f);
            a.speed = Mathf.Lerp(a.speed, target, 1f - Mathf.Exp(-4f * Time.unscaledDeltaTime));
        }
    }

    private void Resolve(out Mode mode, out float distance)
    {
        var round = GameRound.Instance;
        bool inRound = round != null && round.Current != GameRound.Phase.Idle;
        bool participant = round != null && round.IsParticipant(session);

        // 혼자 달리는 회차의 빈 레인 → 1위 고스트
        if (inRound && !participant && round.RaceActive && round.Ghost != null)
        {
            mode = Mode.Leader;
            distance = round.Current >= GameRound.Phase.Run ? round.Ghost.DistanceAt(round.RunElapsed) : 0f;
            return;
        }

        // 회차가 열린 뒤에는 참가자 캐릭터만 보인다. 회차가 없을 때(대기)는 두 캐릭터가 출발선에 서 있다.
        if (session != null && (!inRound || participant))
        {
            mode = Mode.Player;
            distance = session.DistanceM;
            return;
        }

        mode = Mode.Hidden;
        distance = 0f;
    }

    private void Apply(Mode mode)
    {
        builtMode = mode;
        // 화면별 캐릭터는 ApplyStage 가 앞뒤 간격과 함께 켜고 끈다
        if (wheel != null) wheel.gameObject.SetActive(mode != Mode.Hidden);
        foreach (var view in views)
            if (view != null) view.alpha = -1f;   // ApplyStage 가 이 모드의 불투명도로 다시 칠한다
        running = false;
        snapGap = true;
        foreach (var view in views)
        {
            if (view == null) continue;
            view.root.gameObject.SetActive(mode != Mode.Hidden);
            if (view.animator != null && view.animator.isActiveAndEnabled) view.animator.Play(idleState, 0, 0f);
        }
    }

    // alpha 가 1 이면 원래 재질, 1 보다 작으면 모든 재질을 반투명 재질로 바꾸고 원래 텍스처·색에 알파만 낮춰 칠한다
    private void ApplyAlpha(IEnumerable<Renderer> renderers, Dictionary<Renderer, Material[]> materials, float alpha)
    {
        var block = new MaterialPropertyBlock();
        foreach (Renderer r in renderers)
        {
            if (r == null || !materials.TryGetValue(r, out Material[] original)) continue;
            if (alpha >= 0.999f || ghostMaterial == null)
            {
                r.sharedMaterials = original;
                for (int i = 0; i < original.Length; i++) r.SetPropertyBlock(null, i);
                continue;
            }

            var replaced = new Material[original.Length];
            for (int i = 0; i < original.Length; i++) replaced[i] = ghostMaterial;
            r.sharedMaterials = replaced;
            for (int i = 0; i < original.Length; i++)
            {
                Material m = original[i];
                Color c = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId) : Color.white;
                c.a = alpha;
                block.Clear();
                block.SetColor(BaseColorId, c);
                foreach (int id in BaseMapIds)
                {
                    Texture tex = m != null && m.HasProperty(id) ? m.GetTexture(id) : null;
                    if (tex != null) block.SetTexture(id, tex);
                }
                r.SetPropertyBlock(block, i);
            }
        }
    }
}
