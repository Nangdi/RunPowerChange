using System.Collections.Generic;
using UnityEngine;

// PC 1대에 32형 터치모니터 2대 + 85형 1대를 연결해 쓰므로, Setting.json 에 적힌 디스플레이를 켠다.
// Display 1(0번)은 항상 켜져 있다. 해상도는 각 모니터 기본값을 그대로 쓴다 (코드로 강제하지 않음).
// 캔버스/카메라의 targetDisplay 는 씬에서 맞춰 두고, 여기서는 디스플레이 활성화만 한다.
public class DisplaySetup : MonoBehaviour
{
    private void Start()
    {
        var cfg = JsonManager.instance.gameDynamicData;
        var indices = new HashSet<int> { cfg.spectatorDisplayIndex };
        foreach (var w in cfg.wheels)
            if (w != null) indices.Add(w.displayIndex);

        Debug.Log($"연결된 디스플레이: {Display.displays.Length}개");
        foreach (int i in indices)
        {
            if (i <= 0) continue;
            if (i >= Display.displays.Length)
            {
                Debug.LogWarning($"Display {i + 1} 없음 - 설정된 화면이 표시되지 않음 (에디터에서는 Game 뷰 Display 선택으로 확인)");
                continue;
            }
            if (!Display.displays[i].active) Display.displays[i].Activate();
            Debug.Log($"Display {i + 1} 활성화 ({Display.displays[i].systemWidth}x{Display.displays[i].systemHeight})");
        }
    }
}
