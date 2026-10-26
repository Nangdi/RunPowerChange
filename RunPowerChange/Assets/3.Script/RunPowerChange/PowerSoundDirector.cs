using UnityEngine;

// 달려라! 전력 챌린지의 게임 진행 → 소리 연결. 소리 이름(키)은 StreamingAssets/Audio/sound.json 과 같아야 한다.
//
// BGM (회차 단계 기준. PC 1대 스피커라 두 쳇바퀴가 같이 듣는다)
//  idle   : 대기·참가 대기·준비·체험 안내
//  race   : 카운트다운·달리기
//  result : 달리기가 끝난 뒤 (전압 선택·판매·랭킹)
// 효과음
//  join (체험 시작·참여하기) / ready (참가 마감, 준비하세요) / count (3·2·1) / go / finish (달리기 끝)
//  select (전압 선택) / card (판매 카드가 하나씩 나타날 때) / ranking (랭킹 화면)
//  판매 금액 카운트(SaleSlot): tick (금액이 오르는 동안) / money (얻은 금액에 도달했을 때)
public class PowerSoundDirector : MonoBehaviour
{
    [SerializeField] private WheelSession[] sessions;

    private GameRound.Phase lastPhase = (GameRound.Phase)(-1);
    private int lastCount = -1;
    private WheelSession.State[] lastStates;
    private int[] saleSteps;
    private int[] slotTick;
    private bool[] slotDone;

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    private void Start()
    {
        lastStates = new WheelSession.State[sessions.Length];
        saleSteps = new int[sessions.Length];
        slotTick = new int[sessions.Length];
        slotDone = new bool[sessions.Length];
        for (int i = 0; i < sessions.Length; i++)
            if (sessions[i] != null) lastStates[i] = sessions[i].Current;
    }

    private void Update()
    {
        if (JsonManager.instance == null) return; // 앱 종료 중
        UpdateRound();
        for (int i = 0; i < sessions.Length; i++)
            if (sessions[i] != null) UpdateSession(i, sessions[i]);
    }

    private void UpdateRound()
    {
        var round = GameRound.Instance;
        if (round == null) return;

        if (round.Current != lastPhase)
        {
            lastPhase = round.Current;
            SoundManager.PlayBgm(BgmFor(round.Current));
            if (round.Current == GameRound.Phase.Ready) SoundManager.PlaySfx("ready");
            if (round.Current == GameRound.Phase.Post) SoundManager.PlaySfx("finish");
            lastCount = -1;
        }

        // 카운트다운 숫자가 바뀔 때마다 (3, 2, 1 → GO)
        if (round.Current == GameRound.Phase.Countdown || (round.Current == GameRound.Phase.Run && lastCount > 0))
        {
            int n = round.Current == GameRound.Phase.Run ? 0 : round.CountdownNumber;
            if (n != lastCount)
            {
                lastCount = n;
                SoundManager.PlaySfx(n > 0 ? "count" : "go");
            }
        }
    }

    private static string BgmFor(GameRound.Phase phase)
    {
        switch (phase)
        {
            case GameRound.Phase.Countdown:
            case GameRound.Phase.Run: return "race";
            case GameRound.Phase.Post: return "result";
            default: return "idle";
        }
    }

    private void UpdateSession(int i, WheelSession s)
    {
        WheelSession.State state = s.Current;
        if (state != lastStates[i])
        {
            WheelSession.State prev = lastStates[i];
            lastStates[i] = state;
            if (prev == WheelSession.State.Idle && state == WheelSession.State.Join) SoundManager.PlaySfx("join");
            if (state == WheelSession.State.Sale)
            {
                SoundManager.PlaySfx("select");
                saleSteps[i] = 0;
                slotTick[i] = -1;
                slotDone[i] = false;
            }
            if (state == WheelSession.State.Ranking) SoundManager.PlaySfx("ranking");
        }

        if (state != WheelSession.State.Sale) return;

        // 판매 화면 카드가 나타나는 순간 (SalePanel 과 같은 간격). 발전량·송전 방식·판매 금액 카드는 card, 화살표는 소리 없음
        if (saleSteps[i] < SaleSlot.SaleStepCount)
        {
            float at = saleSteps[i] * Mathf.Max(0.05f, Config.saleStepSeconds);
            if (s.StateElapsed >= at)
            {
                if (saleSteps[i] % 2 == 0) SoundManager.PlaySfx("card");
                saleSteps[i]++;
            }
        }

        // 판매 금액 카운트 (SalePanel 과 같은 SaleSlot 시간표)
        if (slotDone[i]) return;
        SaleSlot.Frame f = SaleSlot.Evaluate(s.Sale.saleWon, s.StateElapsed, Config);
        if (!f.started) return;
        if (!f.done && f.tick != slotTick[i]) SoundManager.PlaySfx("tick");
        slotTick[i] = f.tick;
        if (f.done)
        {
            slotDone[i] = true;
            SoundManager.PlaySfx("money");
        }
    }
}
