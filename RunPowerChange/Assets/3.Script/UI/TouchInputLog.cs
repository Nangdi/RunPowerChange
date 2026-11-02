using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 터치·클릭이 들어올 때마다 위치와 맞은 UI 를 로그(Player.log)에 남긴다. 현장에서 "터치가 안 먹는다"를 확인하는 용도.
// Windows 에서는 터치와 함께 마우스 신호도 따라 들어오므로 둘 다 남겨 어느 쪽이 들어오는지 비교할 수 있게 한다.
// EventSystem 오브젝트에 붙인다.
public class TouchInputLog : MonoBehaviour
{
    [Tooltip("같은 종류 로그 사이 최소 간격(초). 연타로 로그가 넘치지 않게")]
    [SerializeField] private float minInterval = 0.2f;

    private readonly List<RaycastResult> hits = new List<RaycastResult>();
    private float nextTouchLog;
    private float nextMouseLog;

    private void Start()
    {
        Debug.Log($"[Touch] touchSupported={Input.touchSupported} multiTouch={Input.multiTouchEnabled} mousePresent={Input.mousePresent} " +
                  $"디스플레이 {Display.displays.Length}개, 주 화면 렌더 {Display.main.renderingWidth}x{Display.main.renderingHeight} / 시스템 {Display.main.systemWidth}x{Display.main.systemHeight}, Screen {Screen.width}x{Screen.height}");
    }

    private void Update()
    {
        // UI 가 실제로 쓰는 터치 (WindowsTouchInput 이 켜져 있으면 보정된 터치)와 Unity 가 받은 원래 터치를 같이 남긴다
        var module = EventSystem.current != null ? EventSystem.current.currentInputModule : null;
        BaseInput ui = module != null ? module.input : null;
        if (ui != null && ui.touchCount > 0 && Time.unscaledTime >= nextTouchLog)
        {
            for (int i = 0; i < ui.touchCount; i++)
            {
                Touch t = ui.GetTouch(i);
                if (t.phase != TouchPhase.Began) continue;
                nextTouchLog = Time.unscaledTime + minInterval;
                string raw = Input.touchCount > 0 ? Input.GetTouch(0).position.ToString() : "없음";
                Debug.Log($"[Touch] {(ui is WindowsTouchInput w && w.Active ? "보정 터치" : "터치")} #{t.fingerId} {t.position} (Unity 원래 터치 {raw}) → {Describe(t.position)}");
            }
        }

        if (Input.GetMouseButtonDown(0) && Time.unscaledTime >= nextMouseLog)
        {
            nextMouseLog = Time.unscaledTime + minInterval;
            bool toUi = ui == null || ui.GetMouseButtonDown(0);
            Debug.Log($"[Touch] 마우스 {(Vector2)Input.mousePosition} (터치 {Input.touchCount}개 동시, UI 전달 {(toUi ? "함" : "막음")}) → {Describe(Input.mousePosition)}");
        }
    }

    // 디스플레이 판정과 그 위치에서 UI 레이캐스트에 처음 맞는 오브젝트
    private string Describe(Vector2 position)
    {
        Vector3 relative = Display.RelativeMouseAt(position);
        var es = EventSystem.current;
        if (es == null) return $"relative={relative} EventSystem 없음";

        hits.Clear();
        es.RaycastAll(new PointerEventData(es) { position = position }, hits);
        string hit = hits.Count > 0 ? $"{hits[0].gameObject.name} (Display {hits[0].displayIndex + 1})" : "UI 없음";
        return $"relative={relative} 맞은 UI={hit}";
    }
}
