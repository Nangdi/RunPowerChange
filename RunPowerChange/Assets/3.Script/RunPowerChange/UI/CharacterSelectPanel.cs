using UnityEngine;
using UnityEngine.UI;

// 03. 캐릭터 선택 - 카드를 누르면 선택. 고른 뒤(Ready)에는 같은 화면에서 다른 체험자를 기다린다.
// characterSelectSeconds 안에 고르지 않으면 기본 캐릭터로 넘어간다.
public class CharacterSelectPanel : WheelPanel
{
    [Tooltip("캐릭터 순서 = RaceRunner 캐릭터 프리팹 순서")]
    [SerializeField] private Button[] cardButtons;
    [Tooltip("cardButtons 와 같은 순서의 선택 테두리")]
    [SerializeField] private GameObject[] highlights;
    [Tooltip("선택 후 다른 체험자를 기다리는 동안 보이는 묶음")]
    [SerializeField] private GameObject waitingGroup;
    [Tooltip("선택 전 안내 문구 묶음")]
    [SerializeField] private GameObject hintGroup;

    private void Awake()
    {
        for (int i = 0; cardButtons != null && i < cardButtons.Length; i++)
        {
            int index = i;
            OnClick(cardButtons[i], () => Session?.SelectCharacter(index));
        }
    }

    protected override void Refresh() => Update();

    private void Update()
    {
        if (Session == null) return;
        bool ready = Session.Current == WheelSession.State.Ready;
        for (int i = 0; highlights != null && i < highlights.Length; i++)
            if (highlights[i] != null) highlights[i].SetActive(ready && i == Session.CharacterIndex);
        if (waitingGroup != null && waitingGroup.activeSelf != ready) waitingGroup.SetActive(ready);
        if (hintGroup != null && hintGroup.activeSelf == ready) hintGroup.SetActive(!ready);
    }
}
