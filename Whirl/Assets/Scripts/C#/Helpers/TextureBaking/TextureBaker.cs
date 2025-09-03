using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class TextureBaker : MonoBehaviour
{
    // --- Capture controls (Play Mode only) ---
    public bool doUpdateRenderMats;
    public bool storeScreenshots = true;
    public Camera targetCamera;
#if UNITY_EDITOR
    public Vector2Int resolution;

    [Tooltip("Pixels with alpha > this value are considered content. 0 = keep any non-zero alpha.")]
    [Range(0, 255)] public byte cropAlphaThreshold = 0;

    public string outputFolder = "Assets/BakedTextures";

    // --- Prefab used to create children for RenderMats ---
    public GameObject childPrefab;           // Must have a MeshRenderer on the root or a child

    // --- Child visibility (Edit & Play) ---
    public Transform texturesRoot;           // Parent whose children are the “textures”
    public int visibleTextureIndex = -1;     // -1 = show none; else 0..last child

    int _lastVisibleIndex = int.MinValue;
    Transform _lastRoot;
    bool _suppressIndexWatcher;

    // --- RenderMat linkage (Edit & Play) ---
    public List<RenderMat> renderMats = new(); // One child per RenderMat, matched by name

    string _lastMatsSig = "";
    string _lastChildrenSig = "";

    void Start()
    {
        SyncChildrenToRenderMats();
        ApplyVisibleChild(visibleTextureIndex);

        // Batch update on start (play mode only)
        if (doUpdateRenderMats && Application.isPlaying && texturesRoot)
        {
            StartCoroutine(BatchUpdateRenderMats());
            Debug.Log("Baked all RenderMat textures.");
        }
    }

    void Update()
    {
        // Keep child set synced with RenderMats by NAME (edit & play)
        if (NeedsSync())
            SyncChildrenToRenderMats();

        // Visibility watcher (edit & play)
        if (!_suppressIndexWatcher && texturesRoot)
        {
            int max = texturesRoot.childCount - 1;
            int clamped = Mathf.Clamp(visibleTextureIndex, -1, max);
            if (clamped != visibleTextureIndex) visibleTextureIndex = clamped;

            if (texturesRoot != _lastRoot || visibleTextureIndex != _lastVisibleIndex)
                ApplyVisibleChild(visibleTextureIndex);
        }

        targetCamera.depth = (visibleTextureIndex > -1 && !Application.isPlaying) ? 5f : -1f;
    }

    // ---------------------- SYNC: RenderMats <-> Children (by NAME) ----------------------
    bool NeedsSync()
    {
        if (!texturesRoot) return false;
        var matNames = renderMats.Where(m => m).Select(m => m.name).OrderBy(n => n);
        var childNames = GetChildren(texturesRoot).Select(t => t.name).OrderBy(n => n);

        string matsSig = string.Join("|", matNames);
        string chSig = string.Join("|", childNames);

        return matsSig != _lastMatsSig || chSig != _lastChildrenSig;
    }

    void SyncChildrenToRenderMats()
    {
        if (!texturesRoot) return;

        // Build lookup of existing children by name
        var children = GetChildren(texturesRoot).ToList();
        var childMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var c in children) childMap[c.name] = c;

        // Ensure each RenderMat has a child named exactly as the RenderMat
        foreach (var mat in renderMats)
        {
            if (!mat) continue;
            if (!childMap.TryGetValue(mat.name, out var child))
            {
                child = CreateChild(mat.name);
                childMap[mat.name] = child;
            }
            // Apply material slot 0
            ApplyMaterialToChild(child, mat.material);
        }

        // Remove any child that has no corresponding RenderMat name
        var matNames = new HashSet<string>(renderMats.Where(m => m).Select(m => m.name), StringComparer.Ordinal);
        foreach (var c in children)
        {
            if (!matNames.Contains(c.name))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(c.gameObject);
                else
                    Destroy(c.gameObject);
#else
                Destroy(c.gameObject);
#endif
            }
        }

        // Update signatures
        var matNamesNow = renderMats.Where(m => m).Select(m => m.name).OrderBy(n => n);
        var childNamesNow = GetChildren(texturesRoot).Select(t => t.name).OrderBy(n => n);
        _lastMatsSig = string.Join("|", matNamesNow);
        _lastChildrenSig = string.Join("|", childNamesNow);

        // Keep visibility index valid
        int max = texturesRoot.childCount - 1;
        if (visibleTextureIndex > max) visibleTextureIndex = max;
        if (max < 0) visibleTextureIndex = -1;
    }

    Transform CreateChild(string childName)
    {
        if (!texturesRoot) return null;

        GameObject go = null;
        if (childPrefab)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                go = (GameObject)PrefabUtility.InstantiatePrefab(childPrefab, texturesRoot);
            else
                go = Instantiate(childPrefab, texturesRoot);
#else
            go = Instantiate(childPrefab, texturesRoot);
#endif
        }
        else
        {
            go = new GameObject("Child");
            go.transform.SetParent(texturesRoot, false);
            if (!go.TryGetComponent<MeshRenderer>(out _)) go.AddComponent<MeshRenderer>();
            if (!go.TryGetComponent<MeshFilter>(out _))   go.AddComponent<MeshFilter>();
        }

        go.name = childName;
        go.transform.localPosition = new(0, 0, 100);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new(75, 75, 1);

        return go.transform;
    }

    void ApplyMaterialToChild(Transform child, Material mat)
    {
        if (!child) return;
        var renderer = child.GetComponentInChildren<MeshRenderer>();
        if (!renderer) return;

#if UNITY_EDITOR
        var arr = !Application.isPlaying ? renderer.sharedMaterials : renderer.materials;
#else
        var arr = renderer.materials;
#endif
        if (arr == null || arr.Length == 0) arr = new Material[1];
        if (arr[0] != mat) arr[0] = mat;

#if UNITY_EDITOR
        if (!Application.isPlaying) renderer.sharedMaterials = arr;
        else renderer.materials = arr;
#else
        renderer.materials = arr;
#endif
    }

    static IEnumerable<Transform> GetChildren(Transform root)
    {
        for (int i = 0; i < root.childCount; i++) yield return root.GetChild(i);
    }

    // ---------------------- Visibility ----------------------
    public void ShowOnlyChild(int index)
    {
        if (!texturesRoot) return;
        int max = texturesRoot.childCount - 1;
        visibleTextureIndex = Mathf.Clamp(index, -1, max);
        ApplyVisibleChild(visibleTextureIndex);
    }

    void ApplyVisibleChild(int index)
    {
        _lastRoot = texturesRoot;
        _lastVisibleIndex = index;
        if (!texturesRoot) return;

        int count = texturesRoot.childCount;
        if (count == 0) return;

        if (index < 0)
        {
            for (int i = 0; i < count; i++)
                SetActive(texturesRoot.GetChild(i), false);
            return;
        }

        int clamped = Mathf.Clamp(index, 0, count - 1);
        for (int i = 0; i < count; i++)
            SetActive(texturesRoot.GetChild(i), i == clamped);
    }

    static void SetActive(Transform t, bool v)
    {
        if (t && t.gameObject.activeSelf != v) t.gameObject.SetActive(v);
    }

    // ---------------------- Batch update (Play Mode) ----------------------
    IEnumerator BatchUpdateRenderMats()
    {
        SyncChildrenToRenderMats();

        int originalIndex = visibleTextureIndex;
        _suppressIndexWatcher = true;

        foreach (var mat in renderMats)
        {
            if (!mat) continue;

            var child = FindChildByName(mat.name);
            if (!child) continue;

            int idx = child.GetSiblingIndex();
            visibleTextureIndex = idx;
            ApplyVisibleChild(idx);

            yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D savedAsset = null;
            yield return StartCoroutine(CaptureAndSaveCoroutine(mat.name, t => savedAsset = t));

        #if UNITY_EDITOR
            if (savedAsset)
            {
                mat.bakedTexture = savedAsset;
                EditorUtility.SetDirty(mat);
            }
        #endif
        }

        #if UNITY_EDITOR
            AssetDatabase.SaveAssets();
        #endif

        visibleTextureIndex = originalIndex;
        ApplyVisibleChild(visibleTextureIndex);
        _suppressIndexWatcher = false;
    }

    Transform FindChildByName(string n)
    {
        if (!texturesRoot) return null;
        for (int i = 0; i < texturesRoot.childCount; i++)
        {
            var c = texturesRoot.GetChild(i);
            if (string.Equals(c.name, n, StringComparison.Ordinal)) return c;
        }
        return null;
    }

    // ---------------------- Capture (Play Mode) ----------------------
    IEnumerator CaptureAndSaveCoroutine(string baseName, Action<Texture2D> onSavedAsset = null)
    {
        var cam = targetCamera ? targetCamera : GetComponent<Camera>();
        if (!cam) yield break;

        int w = resolution.x > 0 ? resolution.x : (Application.isPlaying && cam.pixelWidth  > 0 ? cam.pixelWidth  : 1024);
        int h = resolution.y > 0 ? resolution.y : (Application.isPlaying && cam.pixelHeight > 0 ? cam.pixelHeight : 1024);

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        yield return null;
        yield return new WaitForEndOfFrame();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        tex.Apply(false, false);

        // Crop handling
        Texture2D toSave = tex;
        if (TryGetOpaqueBounds(tex, cropAlphaThreshold, out var bounds))
        {
            if (!(bounds.width == w && bounds.height == h))
            {
                toSave = CropTexture(tex, bounds);
            }
        }

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

    #if UNITY_EDITOR
        Texture2D asset = null;
        if (storeScreenshots)
        {
            asset = SaveTextureAsset(toSave, baseName);
        }
        onSavedAsset?.Invoke(asset);
    #else
        onSavedAsset?.Invoke(null);
    #endif

        // Cleanup
        if (toSave != tex) Destroy(toSave);
        Destroy(rt);
        Destroy(tex);
    }

    // --- Helpers: alpha crop ---
    static bool TryGetOpaqueBounds(Texture2D tex, byte alphaThreshold, out RectInt bounds)
    {
        int w = tex.width;
        int h = tex.height;
        var pixels = tex.GetPixels32();

        int minX = w, minY = h, maxX = -1, maxY = -1;

        // Find tightest bounding box for alpha > threshold
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (pixels[row + x].a > alphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = new RectInt(0, 0, 0, 0);
            return false; // all transparent
        }

        int cw = maxX - minX + 1;
        int ch = maxY - minY + 1;
        bounds = new RectInt(minX, minY, cw, ch);
        return true;
    }

    static Texture2D CropTexture(Texture2D src, RectInt rect)
    {
        int w = rect.width;
        int h = rect.height;
        var srcPixels = src.GetPixels32();
        var dstPixels = new Color32[w * h];

        int srcW = src.width;

        for (int y = 0; y < h; y++)
        {
            int srcY = rect.y + y;
            int srcRow = srcY * srcW;
            int dstRow = y * w;
            for (int x = 0; x < w; x++)
            {
                dstPixels[dstRow + x] = srcPixels[srcRow + rect.x + x];
            }
        }

        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.SetPixels32(dstPixels);
        dst.Apply(false, false);
        return dst;
    }

#if UNITY_EDITOR
    // ---------------------- Editor helpers ----------------------
    Texture2D SaveTextureAsset(Texture2D tex, string baseName)
    {
        string folder = !string.IsNullOrEmpty(outputFolder) && outputFolder.StartsWith("Assets")
            ? outputFolder : "Assets/BakedTextures";
        Directory.CreateDirectory(folder);

        string safe = Sanitize(string.IsNullOrEmpty(baseName) ? name : baseName);
        string path = Path.Combine(folder, $"{safe}_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png").Replace("\\", "/");

        File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true; // read/write
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrEmpty(s) ? "Baked" : s;
    }
#endif
#else
    void Awake()
    {
        var cam = targetCamera ? targetCamera : GetComponent<Camera>();
        cam.depth = -1f;
    }
#endif
}