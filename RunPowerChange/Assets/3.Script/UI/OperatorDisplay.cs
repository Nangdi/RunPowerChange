using UnityEngine;

// 운영자용 캔버스(설정창, RS232 디버그 패널, F1 도움말)를 Setting.json operatorDisplayIndex 디스플레이에 띄운다.
// 관람객 화면(32형 터치모니터)을 가리지 않도록 관전 화면(85형) 쪽에 둔다. 설정창에서 바꾸면 바로 옮긴다 (DisplayRouting).
// 설정(JsonManager)이 준비된 뒤인 Start 에서 정한다. DisplaySetup 은 한 프레임 뒤에 캔버스가 그리는 디스플레이를 켠다.
// 85형은 세로(2160x3840)라 운영자 캔버스는 세로 기준 해상도(1080x1920, Expand)를 쓴다.
// 화면 절반을 기준으로 RS232 디버그 패널은 위쪽, 설정창은 아래쪽(폭에 맞춰 축소)에 두어 함께 열어도 겹치지 않게 한다.
[RequireComponent(typeof(Canvas))]
public class OperatorDisplay : MonoBehaviour
{
    private void Start()
    {
        ApplyDisplay();
        DisplayRouting.Changed += ApplyDisplay;
    }

    private void OnDestroy() => DisplayRouting.Changed -= ApplyDisplay;

    private void ApplyDisplay()
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
