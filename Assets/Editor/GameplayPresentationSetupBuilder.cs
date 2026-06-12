using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Strategy.UI;

public static class GameplayPresentationSetupBuilder
{
    private const string RenderTexturePath = "Assets/RenderTextures/MinimapRenderTexture.renderTexture";
    private const string RedMaterialPath = "Assets/Materials/MapBoundaryRed.mat";
    private const string BlackMaterialPath = "Assets/Materials/MapOutsideBlack.mat";
    private const string JupiterFontPath = "Assets/Unity UI Samples/Fonts/Jupiter/Jupiter TMP.asset";

    private static readonly string[] GameplayScenes =
    {
        "Assets/Scenes/mainScene.unity",
        "Assets/Scenes/PrototypeMap_1_2Spawns.unity",
        "Assets/Scenes/PrototypeMap_2_3Spawns.unity",
        "Assets/Scenes/PrototypeMap_3_4Spawns.unity"
    };

    private static TMP_FontAsset _font;

    [MenuItem("Strategy/Setup/Gameplay Presentation")]
    public static void Apply()
    {
        AssetDatabase.Refresh();
        _font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(JupiterFontPath);

        RenderTexture minimapTexture = EnsureMinimapTexture();
        Material redMaterial = EnsureMaterial(RedMaterialPath, new Color(1f, 0.03f, 0.02f, 1f));
        Material blackMaterial = EnsureMaterial(BlackMaterialPath, Color.black);

        foreach (string scenePath in GameplayScenes)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            Bounds mapBounds = ResolveMapBounds();

            EnsureSaveToastCanvasGroup();
            EnsureMinimap(minimapTexture, mapBounds);
            EnsureMapBoundary(mapBounds, redMaterial, blackMaterial);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GameplayPresentationSetupBuilder: minimap and map boundary configured.");
    }

    [MenuItem("Strategy/Validate/Gameplay Presentation")]
    public static void Validate()
    {
        foreach (string scenePath in GameplayScenes)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ValidateGameplayScene(scenePath);
        }

        Debug.Log("GameplayPresentationSetupBuilder: validation passed.");
    }

    private static void ValidateGameplayScene(string scenePath)
    {
        Require(GameObject.Find("InGamePauseMenu"), scenePath, "InGamePauseMenu");
        Require(GameObject.Find("InGamePauseMenu")?.GetComponent<CanvasGroup>(), scenePath, "InGamePauseMenu CanvasGroup");
        Require(GameObject.Find("SaveGameToast"), scenePath, "SaveGameToast");
        Require(GameObject.Find("SaveGameToast")?.GetComponent<CanvasGroup>(), scenePath, "SaveGameToast CanvasGroup");

        RawImage minimap = Require(GameObject.Find("MinimapView")?.GetComponent<RawImage>(), scenePath, "MinimapView RawImage");
        Require(minimap.texture, scenePath, "MinimapView texture");

        Camera minimapCamera = Require(GameObject.Find("MinimapCamera")?.GetComponent<Camera>(), scenePath, "MinimapCamera");
        if (!minimapCamera.orthographic)
            throw new System.InvalidOperationException($"{scenePath}: MinimapCamera must be orthographic.");

        if (minimapCamera.targetTexture != minimap.texture)
            throw new System.InvalidOperationException($"{scenePath}: MinimapCamera target texture must match MinimapView texture.");

        Require(GameObject.Find("MapBoundary"), scenePath, "MapBoundary");
        Require(GameObject.Find("RedBorder")?.GetComponent<LineRenderer>(), scenePath, "RedBorder LineRenderer");
        Require(GameObject.Find("OutsideNorth"), scenePath, "OutsideNorth");
        Require(GameObject.Find("OutsideSouth"), scenePath, "OutsideSouth");
        Require(GameObject.Find("OutsideEast"), scenePath, "OutsideEast");
        Require(GameObject.Find("OutsideWest"), scenePath, "OutsideWest");
    }

    private static T Require<T>(T value, string scenePath, string label) where T : Object
    {
        if (value == null)
            throw new System.InvalidOperationException($"{scenePath}: missing {label}.");

        return value;
    }

    private static RenderTexture EnsureMinimapTexture()
    {
        EnsureFolder("Assets/RenderTextures");

        RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(RenderTexturePath);
        if (texture != null)
            return texture;

        texture = new RenderTexture(512, 512, 16, RenderTextureFormat.ARGB32)
        {
            name = "MinimapRenderTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            antiAliasing = 1,
            useMipMap = false
        };

        AssetDatabase.CreateAsset(texture, RenderTexturePath);
        return texture;
    }

    private static Material EnsureMaterial(string path, Color color)
    {
        EnsureFolder(System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/'));

        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Unlit/Color")
                ?? Shader.Find("Sprites/Default");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        SetMaterialColor(material, color);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static void EnsureMinimap(RenderTexture texture, Bounds mapBounds)
    {
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        RectTransform slot = FindRect(canvas != null ? canvas.transform : null, "MinimapSlot");
        if (slot != null)
            RebuildMinimapSlot(slot, texture);

        Camera camera = EnsureMinimapCamera(texture, mapBounds);
        EditorUtility.SetDirty(camera);
    }

    private static void RebuildMinimapSlot(RectTransform slot, RenderTexture texture)
    {
        for (int i = slot.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(slot.GetChild(i).gameObject);

        GameObject viewObject = new("MinimapView", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        viewObject.transform.SetParent(slot, false);
        RectTransform viewRect = (RectTransform)viewObject.transform;
        Stretch(viewRect, new Vector2(10f, 10f), new Vector2(-10f, -10f));

        RawImage rawImage = viewObject.GetComponent<RawImage>();
        rawImage.texture = texture;
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        GameObject titleObject = new("MapTitle", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        titleObject.transform.SetParent(slot, false);
        RectTransform titleRect = (RectTransform)titleObject.transform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(0f, 1f);
        titleRect.pivot = new Vector2(0f, 1f);
        titleRect.anchoredPosition = new Vector2(18f, -14f);
        titleRect.sizeDelta = new Vector2(90f, 24f);

        TMP_Text title = titleObject.GetComponent<TMP_Text>();
        if (_font != null)
            title.font = _font;
        title.text = "MAP";
        title.fontSize = 16f;
        title.fontStyle = FontStyles.Bold;
        title.color = Color.white;
        title.alignment = TextAlignmentOptions.Left;
        title.raycastTarget = false;
    }

    private static Camera EnsureMinimapCamera(RenderTexture texture, Bounds mapBounds)
    {
        GameObject cameraObject = GameObject.Find("MinimapCamera");
        if (cameraObject == null)
            cameraObject = new GameObject("MinimapCamera");

        Camera camera = cameraObject.GetComponent<Camera>();
        if (camera == null)
            camera = cameraObject.AddComponent<Camera>();

        Vector3 center = mapBounds.center;
        camera.transform.position = new Vector3(center.x, 180f, center.z);
        camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        camera.orthographic = true;
        camera.orthographicSize = Mathf.Max(mapBounds.extents.x, mapBounds.extents.z) + 6f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 260f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        camera.targetTexture = texture;
        camera.depth = -20f;
        camera.allowHDR = false;
        camera.allowMSAA = false;
        return camera;
    }

    private static void EnsureSaveToastCanvasGroup()
    {
        GameObject toast = GameObject.Find("SaveGameToast");
        if (toast == null)
            return;

        CanvasGroup group = GetOrAdd<CanvasGroup>(toast);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        Image background = toast.GetComponent<Image>();
        if (background != null)
            background.color = new Color(0.02f, 0.08f, 0.13f, 0.96f);

        TMP_Text message = toast.GetComponentInChildren<TMP_Text>(true);
        if (message != null)
        {
            message.color = Color.white;
            message.fontStyle = FontStyles.Bold;
            message.raycastTarget = false;
        }

        SaveGameToastUI toastUi = toast.GetComponent<SaveGameToastUI>();
        if (toastUi == null)
            return;

        SerializedObject serialized = new(toastUi);
        SetObject(serialized, "_root", toast);
        SetObject(serialized, "_rootGroup", group);
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void EnsureMapBoundary(Bounds bounds, Material redMaterial, Material blackMaterial)
    {
        GameObject old = GameObject.Find("MapBoundary");
        if (old != null)
            Object.DestroyImmediate(old);

        GameObject root = new("MapBoundary");
        root.transform.position = Vector3.zero;

        CreateBorderLine(root.transform, bounds, redMaterial);
        CreateOutsideMask(root.transform, bounds, blackMaterial);
    }

    private static void CreateBorderLine(Transform parent, Bounds bounds, Material material)
    {
        GameObject lineObject = new("RedBorder", typeof(LineRenderer));
        lineObject.transform.SetParent(parent, false);

        LineRenderer line = lineObject.GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 4;
        line.widthMultiplier = 0.35f;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        line.sharedMaterial = material;

        float y = bounds.center.y + 0.08f;
        float minX = bounds.min.x;
        float maxX = bounds.max.x;
        float minZ = bounds.min.z;
        float maxZ = bounds.max.z;
        line.SetPosition(0, new Vector3(minX, y, minZ));
        line.SetPosition(1, new Vector3(maxX, y, minZ));
        line.SetPosition(2, new Vector3(maxX, y, maxZ));
        line.SetPosition(3, new Vector3(minX, y, maxZ));
    }

    private static void CreateOutsideMask(Transform parent, Bounds bounds, Material material)
    {
        float depth = 260f;
        float y = bounds.center.y - 0.015f;
        float mapWidth = bounds.size.x;
        float mapDepth = bounds.size.z;

        CreatePlane(parent, "OutsideNorth", material,
            new Vector3(bounds.center.x, y, bounds.max.z + depth * 0.5f),
            new Vector3((mapWidth + depth * 2f) / 10f, 1f, depth / 10f));
        CreatePlane(parent, "OutsideSouth", material,
            new Vector3(bounds.center.x, y, bounds.min.z - depth * 0.5f),
            new Vector3((mapWidth + depth * 2f) / 10f, 1f, depth / 10f));
        CreatePlane(parent, "OutsideEast", material,
            new Vector3(bounds.max.x + depth * 0.5f, y, bounds.center.z),
            new Vector3(depth / 10f, 1f, mapDepth / 10f));
        CreatePlane(parent, "OutsideWest", material,
            new Vector3(bounds.min.x - depth * 0.5f, y, bounds.center.z),
            new Vector3(depth / 10f, 1f, mapDepth / 10f));
    }

    private static void CreatePlane(Transform parent, string name, Material material, Vector3 position, Vector3 scale)
    {
        GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
        plane.name = name;
        plane.transform.SetParent(parent, false);
        plane.transform.position = position;
        plane.transform.localScale = scale;

        Collider collider = plane.GetComponent<Collider>();
        if (collider != null)
            Object.DestroyImmediate(collider);

        MeshRenderer renderer = plane.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
    }

    private static Bounds ResolveMapBounds()
    {
        GameObject plane = GameObject.Find("Plane");
        Renderer renderer = plane != null ? plane.GetComponent<Renderer>() : null;
        if (renderer != null)
            return renderer.bounds;

        return new Bounds(Vector3.zero, new Vector3(200f, 1f, 200f));
    }

    private static RectTransform FindRect(Transform root, string name)
    {
        if (root == null)
            return null;

        if (root.name == name)
            return root as RectTransform;

        for (int i = 0; i < root.childCount; i++)
        {
            RectTransform result = FindRect(root.GetChild(i), name);
            if (result != null)
                return result;
        }

        return null;
    }

    private static T GetOrAdd<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            return;

        string parent = System.IO.Path.GetDirectoryName(folder)?.Replace('\\', '/');
        string child = System.IO.Path.GetFileName(folder);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(string.IsNullOrWhiteSpace(parent) ? "Assets" : parent, child);
    }
}
