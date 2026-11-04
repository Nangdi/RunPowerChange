using System;
using UnityEngine;
using UnityEngine.UI;

// 화면(쳇바퀴 32형, 85형 관전, 운영자)별 출력 디스플레이를 실행 중에 바꿀 때 쓰는 공용 통로.
// 설정창에서 디스플레이를 고르면 Setting.json 값을 바꾼 뒤 Apply 를 부르고,
// 캔버스·카메라(WheelScreen, SpectatorScreen, RaceCamera, OperatorDisplay)는 Changed 를 받아 targetDisplay 를 다시 맞춘다.
// 번호가 None(-1)이면 "사용 안함": 그 화면의 캔버스·카메라를 꺼서 그리지 않는다. 체험 진행과 센서는 그대로 돌아간다.
// 연결된 모니터는 시작할 때 모두 켜고(UnityAlwaysOnTop) Unity 는 켠 디스플레이를 끌 수 없으므로, 배정된 화면이 없는 모니터는 검은 화면이 된다.
public static class DisplayRouting
{
    // 저장값 "사용 안함"
    public const int None = -1;
    public const string NoneLabel = "사용 안함";

    // 에디터는 Game 뷰에서 Display 1~8 을 고를 수 있으므로 8대로 본다
    private const int EditorDisplayCount = 8;

    public static event Action Changed;

    public static int Count => Application.isEditor ? EditorDisplayCount : Display.displays.Length;
    public static bool IsMulti => Count > 1;

    // 설정창 드롭다운에 보일 이름 (Display 1부터)
    public static string Label(int index)
    {
        if (index < 0) return NoneLabel;
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

    // 저장된 번호대로 캔버스를 옮기거나(0 이상) 끈다(사용 안함). 꺼진 화면은 터치도 받지 않는다.
    public static void ApplyTo(Canvas canvas, int index)
    {
        bool used = index >= 0;
        if (used) canvas.targetDisplay = index;
        canvas.enabled = used;
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (raycaster != null) raycaster.enabled = used;
    }

    // 저장된 번호대로 카메라를 옮기거나(0 이상) 끈다(사용 안함). 오브젝트는 켜 둔다 (같이 달린 AudioListener 와 좌표 계산을 유지).
    public static void ApplyTo(Camera camera, int index)
    {
        bool used = index >= 0;
        if (used) camera.targetDisplay = index;
        camera.enabled = used;
    }
}
