using UnityEngine;

// 쳇바퀴 1대 레인의 3D 캐릭터. 세션의 달린 거리만큼 코스 위를 앞으로 나아간다.
//
// 표시 방식
//  - Player : 자기 쳇바퀴 체험자 (대기 중에는 출발선에 서 있음)
//  - Leader : 혼자 달리는 회차에서, 참가하지 않은 쪽 레인에 오늘(없으면 전날) 1위 기록을 반투명 고스트로 달리게 한다
//  - Hidden : 보여 줄 것이 없음 (1위 기록이 아직 없는 등)
//
// 같은 캐릭터를 두 벌 만든다.
//  - 본체(Runner{n} 레이어) : 자기 32형과 85형 카메라가 본다. Leader 일 때는 본체도 반투명
//  - 복제(Runner{n}Ghost 레이어, 항상 반투명) : 상대 32형 카메라가 본다 → 2인 경주 때 상대가 반투명으로 보임
// 캐릭터 프리팹에 Animator 가 있으면 "Speed"(m/s) 파라미터를 넘긴다. 없으면 위아래로 통통 튀는 임시 움직임.
public class RaceRunner : MonoBehaviour
{
    public enum Mode { Hidden, Player, Leader }

    [SerializeField] private int wheelId = 1;
    [SerializeField] private WheelSession session;
    [Tooltip("캐릭터 선택 순서와 같은 순서의 캐릭터 프리팹")]
    [SerializeField] private GameObject[] characterPrefabs;
    [Tooltip("반투명 캐릭터용 재질 (URP Lit Transparent). 색은 원래 재질 색에 알파만 바꿔 칠한다.")]
    [SerializeField] private Material ghostMaterial;
    [SerializeField, Range(0f, 1f)] private float ghostAlpha = 0.35f;

    [Header("움직임")]
    [Tooltip("표시 거리가 실제 거리를 따라가는 빠르기")]
    [SerializeField] private float followSharpness = 6f;
    [SerializeField] private float bobHeight = 0.12f;
    [Tooltip("1m 를 달릴 때 발걸음 수")]
    [SerializeField] private float stepsPerMeter = 1.2f;

    public int WheelId => wheelId;
    public WheelSession Session => session;
    public Mode CurrentMode { get; private set; } = Mode.Hidden;
    // 화면에 그려지는 거리(m)
    public float DistanceM { get; private set; }
    public float SpeedMps { get; private set; }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    private GameObject body;
    private GameObject bodyGhost;
    private int builtCharacter = -1;
    private Mode builtMode = Mode.Hidden;
    private float stepPhase;

    private void Update()
    {
        Resolve(out Mode mode, out float target, out int character);

        if (mode != builtMode || character != builtCharacter) Build(mode, character);
        CurrentMode = mode;
        if (mode == Mode.Hidden) return;

        float dt = Time.unscaledDeltaTime;
        float prev = DistanceM;
        // 출발선으로 돌아가는 경우(새 체험)는 바로 옮긴다
        if (target < DistanceM - 0.5f) DistanceM = target;
        else DistanceM = Mathf.Lerp(DistanceM, target, 1f - Mathf.Exp(-followSharpness * dt));
        float speed = dt > 0f ? Mathf.Max(0f, DistanceM - prev) / dt : 0f;
        SpeedMps = Mathf.Lerp(SpeedMps, speed, 1f - Mathf.Exp(-8f * dt));

        var course = RaceCourse.Instance;
        if (course != null)
        {
            transform.position = course.PositionAt(wheelId, DistanceM);
            transform.rotation = Quaternion.LookRotation(course.Forward, Vector3.up);
        }

        Animate(body, dt);
        Animate(bodyGhost, 0f);
    }

    private void Resolve(out Mode mode, out float distance, out int character)
    {
        var round = GameRound.Instance;
        bool raceActive = round != null && round.Current >= GameRound.Phase.Countdown;

        // 혼자 달리는 회차의 빈 레인 → 1위 고스트
        if (raceActive && round.IsSolo && !round.IsParticipant(session) && round.Ghost != null)
        {
            mode = Mode.Leader;
            distance = round.Ghost.DistanceAt(round.RunElapsed);
            character = round.Ghost.characterIndex;
            return;
        }

        if (session != null && !(raceActive && round.IsSolo && !round.IsParticipant(session)))
        {
            mode = Mode.Player;
            distance = session.DistanceM;
            character = session.CharacterIndex;
            return;
        }

        mode = Mode.Hidden;
        distance = 0f;
        character = -1;
    }

    private void Build(Mode mode, int character)
    {
        if (body != null) Destroy(body);
        if (bodyGhost != null) Destroy(bodyGhost);
        body = bodyGhost = null;
        builtMode = mode;
        builtCharacter = character;
        if (mode == Mode.Hidden) return;

        GameObject prefab = PrefabFor(character);
        if (prefab == null)
        {
            Debug.LogWarning($"[Runner {wheelId}] 캐릭터 프리팹 없음: {character}");
            return;
        }

        body = Instantiate(prefab, transform);
        RaceLayers.SetLayerRecursively(body, RaceLayers.Layer(RaceLayers.Runner(wheelId)));
        if (mode == Mode.Leader) MakeGhost(body);

        bodyGhost = Instantiate(prefab, transform);
        bodyGhost.name = prefab.name + "_Ghost";
        RaceLayers.SetLayerRecursively(bodyGhost, RaceLayers.Layer(RaceLayers.RunnerGhost(wheelId)));
        MakeGhost(bodyGhost);
    }

    private GameObject PrefabFor(int character)
    {
        if (characterPrefabs == null || characterPrefabs.Length == 0) return null;
        return characterPrefabs[Mathf.Clamp(character, 0, characterPrefabs.Length - 1)];
    }

    // 모든 재질을 반투명 재질로 바꾸고 원래 색에 알파만 낮춰 칠한다
    private void MakeGhost(GameObject root)
    {
        if (ghostMaterial == null) return;
        var block = new MaterialPropertyBlock();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] original = r.sharedMaterials;
            var replaced = new Material[original.Length];
            for (int i = 0; i < original.Length; i++) replaced[i] = ghostMaterial;
            r.sharedMaterials = replaced;

            for (int i = 0; i < original.Length; i++)
            {
                Color c = original[i] != null && original[i].HasProperty(BaseColorId) ? original[i].GetColor(BaseColorId) : Color.white;
                c.a = ghostAlpha;
                block.Clear();
                block.SetColor(BaseColorId, c);
                r.SetPropertyBlock(block, i);
            }
        }
    }

    private void Animate(GameObject root, float dt)
    {
        if (root == null) return;

        var animator = root.GetComponentInChildren<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetFloat(SpeedParam, SpeedMps);
            return;
        }

        // 임시 캐릭터: 달리는 속도에 맞춰 통통 튄다 (복제는 본체와 같은 위상)
        if (dt > 0f) stepPhase += SpeedMps * stepsPerMeter * Mathf.PI * dt;
        float bob = SpeedMps > 0.2f ? Mathf.Abs(Mathf.Sin(stepPhase)) * bobHeight : 0f;
        root.transform.localPosition = new Vector3(0f, bob, 0f);
        root.transform.localRotation = Quaternion.Euler(Mathf.Clamp(SpeedMps * 2.5f, 0f, 15f), 0f, 0f);
    }
}
