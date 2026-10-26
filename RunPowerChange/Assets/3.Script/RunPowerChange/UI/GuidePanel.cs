using TMPro;
using UnityEngine;

// 02. 체험 안내 - 3단계 설명. guideSeconds 후 캐릭터 선택으로 자동으로 넘어간다.
// 발전·송전 시간은 설정값이라 숫자 부분만 동적으로 채운다.
public class GuidePanel : WheelPanel
{
    [SerializeField] private TMP_Text runSecondsText;
    [SerializeField] private TMP_Text transmitSecondsText;
    [SerializeField] private TMP_Text step3Text;

    protected override void Refresh()
    {
        var cfg = JsonManager.instance.gameDynamicData;
        string run = PowerFormat.Seconds(cfg.runSeconds);
        string transmit = PowerFormat.Seconds(cfg.transmitSeconds);
        SetText(runSecondsText, run);
        SetText(transmitSecondsText, transmit);
        SetText(step3Text, $"전기 생산 {run}초, 송전 {transmit}초!\n전기를 판매하여 랭킹을 획득하세요!");
    }
}
