using System;
using System.Collections.Generic;
using UnityEngine;

// 운영·개발용 단축키 목록. 각 기능 스크립트가 실제로 쓰는 키와 설명을 등록하고,
// F1 도움말 패널(ShortcutHelpPanel)은 등록된 목록을 그대로 보여 준다.
// 키를 바꾸거나 추가해도 안내가 어긋나지 않도록 패널에는 키를 직접 적지 않는다.
public static class ShortcutHelp
{
    public class Entry
    {
        public object Owner;
        public KeyCode Key;
        public string Description;
        // 어느 화면에서 동작하는지 (예: "모든 화면", "쳇바퀴 1 발전 중")
        public string Where;
    }

    private static readonly List<Entry> entries = new List<Entry>();

    public static IReadOnlyList<Entry> Entries => entries;
    public static event Action Changed;

    // owner 가 같은 키를 다시 등록하면 설명만 바꾼다. KeyCode.None 은 등록하지 않는다.
    public static void Register(object owner, KeyCode key, string description, string where)
    {
        if (owner == null || key == KeyCode.None) return;

        foreach (var e in entries)
        {
            if (e.Owner == owner && e.Key == key)
            {
                e.Description = description;
                e.Where = where;
                Changed?.Invoke();
                return;
            }
        }

        entries.Add(new Entry { Owner = owner, Key = key, Description = description, Where = where });
        Changed?.Invoke();
    }

    // owner 가 등록한 키를 모두 지운다 (OnDestroy/OnDisable 에서 호출).
    public static void Unregister(object owner)
    {
        if (entries.RemoveAll(e => e.Owner == owner) > 0) Changed?.Invoke();
    }

    // 표시용 키 이름
    public static string KeyName(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Escape: return "ESC";
            case KeyCode.UpArrow: return "↑";
            case KeyCode.DownArrow: return "↓";
            case KeyCode.LeftArrow: return "←";
            case KeyCode.RightArrow: return "→";
            default:
                string name = key.ToString();
                return name.StartsWith("Alpha") ? name.Substring(5) : name;
        }
    }
}
