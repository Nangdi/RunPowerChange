using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// PC 1대에 32형 터치모니터 2대 + 85형 1대를 연결해 쓰므로, 화면이 그려지는 디스플레이를 켠다.
// Display 1(0번)은 항상 켜져 있다. 해상도는 각 모니터 기본값을 그대로 쓴다 (코드로 강제하지 않음).
//
// 켜진 카메라나 Overlay 캔버스가 실제로 그리는 디스플레이만 Activate 한다.
// 그릴 것이 없는 디스플레이를 켜면 그 모니터가 검은 화면으로 덮이기 때문이다
// (예: 85형 관전 화면이 아직 없는데 spectatorDisplayIndex 를 켜면 85형이 검게 됨).
// WheelScreen 이 Start 에서 캔버스 targetDisplay 를 Setting.json 값으로 바꾸므로 한 프레임 뒤에 확인한다.
public class DisplaySetup : MonoBehaviour
{
    private IEnumerator Start()
    {
        yield return null;

        var cfg = JsonManager.instance.gameDynamicData;
        var configured = new HashSet<int> { cfg.spectatorDisplayIndex };
        foreach (var w in cfg.wheels)
            if (w != null) configured.Add(w.displayIndex);

        var drawn = DrawnDisplays();
        Debug.Log($"연결된 디스플레이: {Display.displays.Length}개, 그리는 디스플레이: {string.Join(", ", drawn)}");

        foreach (int i in configured)
        {
            if (i <= 0) continue;
            if (!drawn.Contains(i))
            {
                Debug.LogWarning($"Display {i + 1} 은 Setting.json 에 있지만 그리는 카메라·캔버스가 없어 켜지 않음");
                continue;
            }
            if (i >= Display.displays.Length)
            {
                Debug.LogWarning($"Display {i + 1} 없음 - 설정된 화면이 표시되지 않음 (에디터에서는 Game 뷰 Display 선택으로 확인)");
                continue;
            }
            if (!Display.displays[i].active) Display.displays[i].Activate();
            Debug.Log($"Display {i + 1} 활성화 ({Display.displays[i].systemWidth}x{Display.displays[i].systemHeight})");
        }
    }

    // 켜진 카메라와 Overlay 루트 캔버스의 targetDisplay 모음
    private static HashSet<int> DrawnDisplays()
    {
        var result = new HashSet<int>();
        foreach (var cam in Camera.allCameras)
            if (cam.targetTexture == null) result.Add(cam.targetDisplay);
        foreach (var canvas in FindObjectsOfType<Canvas>())
            if (canvas.isRootCanvas && canvas.renderMode == RenderMode.ScreenSpaceOverlay) result.Add(canvas.targetDisplay);
        return result;
    }
}
