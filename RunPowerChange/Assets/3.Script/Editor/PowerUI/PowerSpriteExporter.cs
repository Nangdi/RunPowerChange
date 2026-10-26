using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static PowerUIKit;

// 디자이너용 가이드 이미지와 패널 미리보기 PNG 를 만든다. (화면 이미지 자체는 Tools/psd_export.py 가 PSD 에서 뽑는다)
//  - _Guide/Guide_xxx.png : 정적 레이어 위에 동적 요소 자리를 표시한 작업 가이드
//  - RenderPreview        : 패널 프리팹을 배경 이미지 위에 그려 PNG 로 저장 (플레이 없이 배치 확인용)
public static class PowerSpriteExporter
{
    private static readonly Color GuideColor = new Color(1f, 0.1f, 0.8f, 1f);

    [MenuItem("Tools/달려라 전력 챌린지/2. 가이드 PNG 다시 만들기", priority = 2)]
    public static void ExportGuides()
    {
        EnsureFolder(GuideDir);
        var all = new System.Collections.Generic.List<string>(PanelNames) { SpectatorName };
        foreach (string panel in all)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPrefabPath(panel));
            if (prefab == null)
            {
                Debug.LogWarning($"패널 프리팹이 없어 가이드를 건너뜁니다: {panel}");
                continue;
            }

            Vector2 size = panel == SpectatorName ? SpectatorResolution : ReferenceResolution;
            Texture2D tex = Render((int)size.x, (int)size.y, root => DrawGuide(root, prefab, size));
            WritePng(GuidePath(panel), tex);
        }
        Debug.Log($"가이드 PNG 생성 완료: {GuideDir}");
    }

    // 패널 프리팹을 배경(없으면 회색) 위에 그려 outPath 에 저장한다. prepare 로 표시 상태(켜고 끌 오브젝트)를 바꿀 수 있다.
    public static void RenderPreview(string panelPrefabPath, string outPath, string backgroundPath = null, Action<GameObject> prepare = null)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(panelPrefabPath);
        if (prefab == null) throw new ArgumentException($"프리팹이 없습니다: {panelPrefabPath}");
        bool spectator = panelPrefabPath.Contains(SpectatorName);
        Vector2 size = spectator ? SpectatorResolution : ReferenceResolution;
        Texture2D bg = null;
        if (!string.IsNullOrEmpty(backgroundPath) && File.Exists(backgroundPath))
        {
            bg = new Texture2D(2, 2);
            bg.LoadImage(File.ReadAllBytes(backgroundPath));
        }

        Texture2D tex = Render((int)size.x, (int)size.y, root =>
        {
            RectTransform back = StretchFull(CreateRect("Background", root));
            if (bg != null) back.gameObject.AddComponent<RawImage>().texture = bg;
            else AddImage(back, new Color(0.25f, 0.27f, 0.32f));
            var panel = (GameObject)UnityEngine.Object.Instantiate(prefab, root);
            panel.SetActive(true);
            prepare?.Invoke(panel);
        });
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        if (bg != null) UnityEngine.Object.DestroyImmediate(bg);
    }

    // 실행 중인 패널(지금 상태 그대로)을 3D 화면 캡처 위에 그려 저장한다 (플레이 모드 시험용).
    // 복제본은 스크립트를 떼고 그리므로 실행 중인 패널에는 영향이 없다.
    public static void RenderLive(GameObject livePanel, Texture background, Vector2 size, string outPath, bool stretch = true)
    {
        Texture2D tex = Render((int)size.x, (int)size.y, root =>
        {
            RectTransform back = StretchFull(CreateRect("Background", root));
            if (background != null) back.gameObject.AddComponent<RawImage>().texture = background;
            else AddImage(back, Color.black);

            // 꺼진 부모 아래에 복제해 Awake 가 돌지 않게 하고, 프로젝트 스크립트를 뗀 뒤 켠다
            RectTransform holder = StretchFull(CreateRect("Holder", root));
            holder.gameObject.SetActive(false);
            var clone = UnityEngine.Object.Instantiate(livePanel, holder);
            foreach (var mb in clone.GetComponentsInChildren<MonoBehaviour>(true))
                if (mb != null && mb.GetType().Assembly.GetName().Name == "Assembly-CSharp") UnityEngine.Object.DestroyImmediate(mb);
            var rt = (RectTransform)clone.transform;
            if (stretch)
            {
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
            }
            clone.SetActive(true);
            holder.gameObject.SetActive(true);
        });
        File.WriteAllBytes(outPath, tex.EncodeToJPG(85));
        UnityEngine.Object.DestroyImmediate(tex);
    }

    #region 가이드

    private static void DrawGuide(RectTransform root, GameObject panelPrefab, Vector2 size)
    {
        AddImage(StretchFull(CreateRect("Background", root)), new Color(0.25f, 0.27f, 0.32f));
        var panel = (RectTransform)UnityEngine.Object.Instantiate(panelPrefab, root).transform;
        panel.name = panelPrefab.name;
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);

        foreach (Transform child in panel)
        {
            if (child.name == StaticLayerName) continue;
            child.gameObject.SetActive(true);
            MarkDynamicArea((RectTransform)child);
        }

        var legend = CreateText(root, "Legend", "■ 분홍 박스 = 동적 요소 자리 (정적 PNG에 그리지 말 것)",
            new Vector2(0, size.y * 0.5f - 24), new Vector2(size.x, 44), size.x > 2000 ? 40 : 24, GuideColor, Weight.Bold);
        legend.alignment = TextAlignmentOptions.Center;
    }

    private static void MarkDynamicArea(RectTransform target)
    {
        // 화면 전체를 덮는 묶음(카드 묶음 등)은 안쪽 자식마다 표시한다
        if (target.anchorMin == Vector2.zero && target.anchorMax == Vector2.one && target.GetComponent<Graphic>() == null)
        {
            foreach (Transform child in target) MarkDynamicArea((RectTransform)child);
            return;
        }

        RectTransform box = StretchFull(CreateRect("GuideBox", target));
        box.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
        AddImage(box, new Color(GuideColor.r, GuideColor.g, GuideColor.b, 0.18f));

        const float t = 3f;
        AddImage(Edge(box, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -t), Vector2.zero), GuideColor);
        AddImage(Edge(box, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, t)), GuideColor);
        AddImage(Edge(box, Vector2.zero, new Vector2(0, 1), Vector2.zero, new Vector2(t, 0)), GuideColor);
        AddImage(Edge(box, new Vector2(1, 0), Vector2.one, new Vector2(-t, 0), Vector2.zero), GuideColor);

        var label = AddText(Stretch(CreateRect("GuideLabel", box), Vector2.zero, Vector2.one, 6),
            target.name, 20, GuideColor, Weight.Bold, TextAlignmentOptions.TopLeft);
        label.enableWordWrapping = false;
        label.enableAutoSizing = false;
        label.overflowMode = TextOverflowModes.Overflow;
    }

    private static RectTransform Edge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rt = CreateRect("Edge", parent);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = offsetMin;
        rt.offsetMax = offsetMax;
        return rt;
    }

    #endregion

    #region 렌더링

    // 임시 프리뷰 씬에 UI를 만들어 카메라로 찍는다. 현재 씬은 건드리지 않는다.
    private static Texture2D Render(int width, int height, Action<RectTransform> build)
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        try
        {
            var camGo = new GameObject("ExportCamera");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = height * 0.5f;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 1000f;
            cam.transform.position = new Vector3(0, 0, -100);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.targetTexture = rt;

            var canvasGo = new GameObject("ExportCanvas", typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(canvasGo, scene);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            var root = (RectTransform)canvasGo.transform;
            root.sizeDelta = new Vector2(width, height);
            root.position = Vector3.zero;

            build(root);

            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Canvas.ForceUpdateCanvases();
            foreach (var text in canvasGo.GetComponentsInChildren<TMP_Text>(true))
                text.ForceMeshUpdate(true, true);
            Canvas.ForceUpdateCanvases();

            cam.Render();
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            Color32[] pixels = tex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i].a = 255;
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
        finally
        {
            RenderTexture.ReleaseTemporary(rt);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void WritePng(string path, Texture2D tex)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }

    #endregion
}
