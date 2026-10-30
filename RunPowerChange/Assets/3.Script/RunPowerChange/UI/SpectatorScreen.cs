using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 85형 관전 화면 (세로 2160x3840). Canvas_Spectator 프리팹 루트에 붙는다.
//  - 위: PLAYER 01/02 상태(체험중 / 체험 대기중)와 누적 환산 발전량. 뒤의 3D 장면은 RaceCamera(Spectator)가 그린다.
//  - 가운데: "오늘 최고 랭킹" 표시가 3D 최고 랭킹 선(RaceStage.RecordLine)을 따라다닌다.
//  - 아래 칸: 오늘의 발전왕·판매왕 순위 → 왜 그럴까요? → 우리와 어떤 관련이 있을까요? 가 spectatorInfoSeconds 마다 바뀐다.
//    설명 그림은 현장에서 바꿀 수 있게 StreamingAssets/Spectator/info_1.png, info_2.png 를 읽는다 (없으면 기본 그림, 로그만 남김).
// 랭킹은 두 쳇바퀴의 기록을 합쳐 보여 준다. 캔버스 출력 디스플레이는 Setting.json spectatorDisplayIndex 를 따른다.
public class SpectatorScreen : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private WheelSession[] sessions;

    [Header("PLAYER 01/02 (sessions 와 같은 순서)")]
    [SerializeField] private TMP_Text[] statusTexts;
    [SerializeField] private TMP_Text[] energyTexts;

    [Header("오늘 최고 랭킹 표시")]
    [Tooltip("3D 선 위치를 화면 좌표로 바꿀 85형 카메라")]
    [SerializeField] private Camera raceCamera;
    [SerializeField] private RectTransform recordTag;
    [Tooltip("표시 그림에서 선과 닿는 점(아래 끝)의 피벗 기준 높이 비율. 0 = 그림 아래 끝")]
    [SerializeField] private float recordTagAnchorY = 0f;

    [Header("아래 칸 (나오는 순서)")]
    [SerializeField] private CanvasGroup[] bottomGroups;
    [SerializeField] private float fadeSeconds = 0.5f;

    [Header("설명 그림 (아래 칸 두 번째부터, StreamingAssets/Spectator/info_n.png 로 교체)")]
    [SerializeField] private Image[] infoImages;

    [Header("랭킹 (아래 칸 첫 번째)")]
    [SerializeField] private RankTableView energyTable;
    [SerializeField] private RankTableView saleTable;

    private float nextRefreshTime;
    private int bottomIndex;
    private float bottomTime;

    private static GameDynamicData Config => JsonManager.instance.gameDynamicData;

    private void Start()
    {
        ApplyDisplay();
        DisplayRouting.Changed += ApplyDisplay;
        foreach (var s in sessions)
            if (s != null) s.OnRecordAdded += HandleRecordAdded;
        bottomTime = Time.unscaledTime;
        ShowBottom(0, true);
        LoadInfoImages();
    }

    private const string InfoFolder = "Spectator";

    // 설명 그림을 StreamingAssets 파일로 바꾼다. 파일이 없거나 깨져 있으면 기본 그림을 그대로 쓰고 로그만 남긴다.
    private void LoadInfoImages()
    {
        for (int i = 0; infoImages != null && i < infoImages.Length; i++)
        {
            if (infoImages[i] == null) continue;
            string path = Path.Combine(Application.streamingAssetsPath, InfoFolder, $"info_{i + 1}.png");
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[Spectator] 설명 그림 없음 - 기본 그림 사용: {path}");
                continue;
            }
            try
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(File.ReadAllBytes(path)))
                {
                    Debug.LogWarning($"[Spectator] 설명 그림을 읽지 못함 - 기본 그림 사용: {path}");
                    Destroy(tex);
                    continue;
                }
                tex.wrapMode = TextureWrapMode.Clamp;
                infoImages[i].sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                infoImages[i].preserveAspect = true;
                Debug.Log($"[Spectator] 설명 그림 {i + 1}: {path} ({tex.width}x{tex.height})");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Spectator] 설명 그림 오류 - 기본 그림 사용: {path} ({e.Message})");
            }
        }
    }

    private void ApplyDisplay()
    {
        if (canvas != null) canvas.targetDisplay = Mathf.Max(0, Config.spectatorDisplayIndex);
    }

    private void OnDestroy()
    {
        DisplayRouting.Changed -= ApplyDisplay;
        foreach (var s in sessions)
            if (s != null) s.OnRecordAdded -= HandleRecordAdded;
    }

    private void Update()
    {
        // 앱 종료 중에는 설정 관리자가 먼저 사라질 수 있다
        if (JsonManager.instance == null) return;
        UpdatePlayers();
        UpdateRecordTag();
        UpdateBottom();

        // 세션 랭킹은 WheelSession.Start 에서 만들어지고 날짜가 바뀌면 비워지므로 주기적으로 다시 그린다
        if (Time.unscaledTime >= nextRefreshTime)
        {
            nextRefreshTime = Time.unscaledTime + 10f;
            RefreshRanking();
        }
    }

    private void UpdatePlayers()
    {
        for (int i = 0; i < sessions.Length; i++)
        {
            var s = sessions[i];
            if (s == null) continue;
            bool playing = s.Current != WheelSession.State.Idle;
            SetText(statusTexts, i, playing ? "체험중" : "체험 대기중");
            SetText(energyTexts, i, playing ? PowerFormat.Wh(s.ScaledEnergyWh) : "0.0");
        }
    }

    private void UpdateRecordTag()
    {
        if (recordTag == null) return;
        var stage = RaceStage.Instance;
        bool show = stage != null && stage.RecordLineVisible && raceCamera != null;
        if (recordTag.gameObject.activeSelf != show) recordTag.gameObject.SetActive(show);
        if (!show) return;

        // 선의 오른쪽 끝 위에 표시의 아래 끝이 오도록 화면 좌표 → 캔버스 좌표로 옮긴다
        Vector3 screen = raceCamera.WorldToScreenPoint(stage.RecordLineTagPoint);
        var parent = (RectTransform)recordTag.parent;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, uiCamera, out Vector2 local)) return;
        float offsetY = (recordTag.pivot.y - recordTagAnchorY) * recordTag.rect.height;
        recordTag.anchoredPosition = new Vector2(recordTag.anchoredPosition.x, local.y + offsetY - LocalCenterOffset(parent));
    }

    // 표시의 앵커가 부모 가운데라 local 좌표(부모 피벗 기준)와 anchoredPosition 의 차이를 맞춘다
    private static float LocalCenterOffset(RectTransform parent) => (0.5f - parent.pivot.y) * parent.rect.height;

    private void UpdateBottom()
    {
        if (bottomGroups == null || bottomGroups.Length == 0) return;
        float interval = Mathf.Max(1f, Config.spectatorInfoSeconds);
        if (Time.unscaledTime - bottomTime >= interval)
        {
            bottomTime = Time.unscaledTime;
            ShowBottom((bottomIndex + 1) % bottomGroups.Length, false);
        }

        float step = Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeSeconds);
        for (int i = 0; i < bottomGroups.Length; i++)
        {
            if (bottomGroups[i] == null) continue;
            bottomGroups[i].alpha = Mathf.MoveTowards(bottomGroups[i].alpha, i == bottomIndex ? 1f : 0f, step);
        }
    }

    private void ShowBottom(int index, bool immediate)
    {
        bottomIndex = index;
        if (index == 0) RefreshRanking();
        if (!immediate || bottomGroups == null) return;
        for (int i = 0; i < bottomGroups.Length; i++)
            if (bottomGroups[i] != null) bottomGroups[i].alpha = i == index ? 1f : 0f;
    }

    private IEnumerable<WheelRankingStore> Stores()
    {
        foreach (var s in sessions)
            if (s != null && s.Ranking != null) yield return s.Ranking;
    }

    private void RefreshRanking()
    {
        Fill(energyTable, WheelRankingStore.Kind.Energy);
        Fill(saleTable, WheelRankingStore.Kind.Sale);
    }

    private void Fill(RankTableView table, WheelRankingStore.Kind kind)
    {
        if (table == null) return;
        table.Set(kind, WheelRankingStore.MergeTop(Stores(), kind, table.RowCount), null, 0);
    }

    // 새 기록이 들어오면 바로 랭킹 칸을 보여 준다
    private void HandleRecordAdded(WheelSession session, PowerRecord record)
    {
        bottomTime = Time.unscaledTime;
        ShowBottom(0, false);
    }

    private static void SetText(TMP_Text[] list, int index, string value)
    {
        if (list == null || index >= list.Length || list[index] == null) return;
        if (list[index].text != value) list[index].text = value;
    }
}
