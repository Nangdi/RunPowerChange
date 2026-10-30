using System;
using UnityEngine;

// 화면(쳇바퀴 32형, 85형 관전, 운영자)별 출력 디스플레이를 실행 중에 바꿀 때 쓰는 공용 통로.
// 설정창에서 디스플레이를 고르면 Setting.json 값을 바꾼 뒤 Apply 를 부르고,
// 캔버스·카메라(WheelScreen, SpectatorScreen, RaceCamera, OperatorDisplay)는 Changed 를 받아 targetDisplay 를 다시 맞춘다.
// Unity 는 한 번 켠 디스플레이를 끌 수 없으므로, 화면을 옮기고 남은 모니터는 검은 화면이 된다.
public static class DisplayRouting
{
    // 에디터는 Game 뷰에서 Display 1~8 을 고를 수 있으므로 8대로 본다
    private const int EditorDisplayCount = 8;

    public static event Action Changed;

    public static int Count => Application.isEditor ? EditorDisplayCount : Display.displays.Length;
    public static bool IsMulti => Count > 1;

    // 설정창 드롭다운에 보일 이름 (Display 1부터)
    public static string Label(int index)
    {
        if (index >= Count) return $"Display {index + 1} (연결 안 됨)";
        if (!Application.isEditor && index < Display.displays.Length)
            return $"Display {index + 1} ({Display.displays[index].systemWidth}x{Display.displays[index].systemHeight})";
        return $"Display {index + 1}";
    }

    // Setting.json 을 바꾼 뒤 부른다. 새 디스플레이를 켜고 화면들에 다시 맞추라고 알린다.
    public static void Apply(int index)
    {
        if (index > 0 && index < Display.displays.Length && !Display.displays[index].active)
            Display.displays[index].Activate();
        Changed?.Invoke();
    }
}
