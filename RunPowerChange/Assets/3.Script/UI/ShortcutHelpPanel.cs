using System.Text;
using TMPro;
using UnityEngine;

// F1 단축키 도움말 패널. ShortcutHelp 에 등록된 키 목록을 그대로 보여 준다.
// 기본은 숨김. 캔버스 sortingOrder 는 설정창(100)·디버그 패널(200)보다 위에 둔다.
public class ShortcutHelpPanel : MonoBehaviour
{
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text listText;
    [SerializeField] private KeyCode toggleKey = KeyCode.F1;
    [SerializeField] private bool hideOnStart = true;

    private readonly StringBuilder builder = new StringBuilder();

    private void Start()
    {
        ShortcutHelp.Register(this, toggleKey, "단축키 도움말 열기/닫기", "모든 화면");
        ShortcutHelp.Changed += Rebuild;
        Rebuild();
        if (hideOnStart && panelRoot != null) panelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        ShortcutHelp.Changed -= Rebuild;
        ShortcutHelp.Unregister(this);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey) && panelRoot != null)
        {
            panelRoot.SetActive(!panelRoot.activeSelf);
            if (panelRoot.activeSelf) Rebuild();
        }
    }

    private void Rebuild()
    {
        if (listText == null) return;

        builder.Clear();
        foreach (var e in ShortcutHelp.Entries)
            builder.AppendLine($"<b>{ShortcutHelp.KeyName(e.Key)}</b>  :  {e.Description}  <color=#9AA0A6>({e.Where})</color>");
        if (ShortcutHelp.Entries.Count == 0) builder.AppendLine("등록된 단축키가 없습니다.");
        listText.text = builder.ToString();
    }
}
