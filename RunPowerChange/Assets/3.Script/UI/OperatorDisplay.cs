using UnityEngine;

// 운영자용 캔버스(설정창, RS232 디버그 패널, F1 도움말)를 Setting.json operatorDisplayIndex 디스플레이에 띄운다.
// 관람객 화면(32형 터치모니터)을 가리지 않도록 관전 화면(85형) 쪽에 둔다. 바꾼 값은 재시작 후 적용된다.
// 설정(JsonManager)이 준비된 뒤인 Start 에서 정한다. DisplaySetup 은 한 프레임 뒤에 캔버스가 그리는 디스플레이를 켠다.
[RequireComponent(typeof(Canvas))]
public class OperatorDisplay : MonoBehaviour
{
    private void Start()
    {
        var cfg = JsonManager.instance != null ? JsonManager.instance.gameDynamicData : null;
        if (cfg == null) return;
        GetComponent<Canvas>().targetDisplay = Mathf.Max(0, cfg.operatorDisplayIndex);
    }

    // 단축키 도움말 등에 쓸 표시 이름 (Display 1부터)
    public static string DisplayName
    {
        get
        {
            var cfg = JsonManager.instance != null ? JsonManager.instance.gameDynamicData : null;
            return $"Display {(cfg != null ? Mathf.Max(0, cfg.operatorDisplayIndex) : 0) + 1}";
        }
    }
}
