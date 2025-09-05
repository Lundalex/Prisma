using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class TextureBaker : MonoBehaviour
{
    public bool doUpdateRenderMats;
    public Camera targetCamera;

    [Header("Sun Light")]
    [Tooltip("Directional/scene Light whose intensity will be set from RenderMat.light.")]
    public Light sunLight;

#if UNITY_EDITOR
    public Vector2Int resolution;
    [Range(0, 255)] public byte cropAlphaThreshold = 0;
    [Tooltip("Folder where screenshots are written. Files are named <RenderMatName>.png (or .jpg if already present).")]
    public string outputFolder = "Assets/Screenshots";
    public GameObject childPrefab;
    [Tooltip("This root and its children are renamed to 'EditorOnly' so Unity strips them from Player builds.")]
    public Transform texturesRoot;
    public int visibleTextureIndex = -1;

    int _lastVisibleIndex = int.MinValue;
    Transform _lastRoot;
    bool _suppressIndexWatcher;

    // NOTE: We keep your existing RenderMat list as-is.
    public List<RenderMat> renderMats = new();

    string _lastMatsSig = "";
    string _lastChildrenSig = "";

    struct MatSnapshot { public float light; public int materialId; }
    readonly Dictionary<RenderMat, MatSnapshot> _matSnapshots = new();

    void OnValidate()
    {
        if (texturesRoot && texturesRoot.gameObject.name != "EditorOnly")
            texturesRoot.gameObject.name = "EditorOnly";
    }

    void Start()
    {
        if (texturesRoot && texturesRoot.gameObject.name != "EditorOnly")
            texturesRoot.gameObject.name = "EditorOnly";

        SyncChildrenToRenderMats();
        ApplyVisibleChild(visibleTextureIndex);
        PrimeMatSnapshots();

        if (doUpdateRenderMats && Application.isPlaying && texturesRoot)
        {
            StartCoroutine(BatchUpdateRenderMats());
            Debug.Log("Baked all RenderMat textures.");
        }
    }

    void Update()
    {
        if (NeedsSync())
            SyncChildrenToRenderMats();

        WatchRenderMatChanges();

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

    // Use 'matName' if the asset has that string (e.g., CustomMat); else fall back to Object.name.
    string GetMatLabel(RenderMat mat)
    {
        if (!mat) return string.Empty;
#if UNITY_EDITOR
        try
        {
            var so = new SerializedObject(mat);
            var p = so.FindProperty("matName");
            if (p != null && !string.IsNullOrEmpty(p.stringValue))
                return p.stringValue;
        }
        catch { }
#endif
        return mat.name;
    }

    bool NeedsSync()
    {
        if (!texturesRoot) return false;
        var matNames = renderMats.Where(m => m).Select(m => GetMatLabel(m)).OrderBy(n => n);
        var childNames = GetChildren(texturesRoot).Select(t => t.name).OrderBy(n => n);

        string matsSig = string.Join("|", matNames);
        string chSig = string.Join("|", childNames);

        return matsSig != _lastMatsSig || chSig != _lastChildrenSig;
    }

    void SyncChildrenToRenderMats()
    {
        if (!texturesRoot) return;

        var children = GetChildren(texturesRoot).ToList();
        var childMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var c in children) childMap[c.name] = c;

        foreach (var mat in renderMats)
        {
            if (!mat) continue;
            string label = GetMatLabel(mat);
            if (!childMap.TryGetValue(label, out var child))
            {
                child = CreateChild(label);
                childMap[label] = child;
            }
            ApplyMaterialToChild(child, mat.material);
        }

        var matNameSet = new HashSet<string>(renderMats.Where(m => m).Select(m => GetMatLabel(m)), StringComparer.Ordinal);
        foreach (var c in children)
        {
            if (!matNameSet.Contains(c.name))
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

        var matNamesNow = renderMats.Where(m => m).Select(m => GetMatLabel(m)).OrderBy(n => n);
        var childNamesNow = GetChildren(texturesRoot).Select(t => t.name).OrderBy(n => n);
        _lastMatsSig = string.Join("|", matNamesNow);
        _lastChildrenSig = string.Join("|", childNamesNow);

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
            if (!go.TryGetComponent<MeshFilter>(out _)) go.AddComponent<MeshFilter>();
        }

        go.name = childName;
        go.transform.localPosition = new(0, 0, 100);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new(10, 10, 1);

        if (texturesRoot && texturesRoot.gameObject.name != "EditorOnly")
            texturesRoot.gameObject.name = "EditorOnly";

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

        if (sunLight)
        {
            var child = texturesRoot.GetChild(clamped);
            var mat = renderMats.FirstOrDefault(m => m && GetMatLabel(m) == child.name);
            ApplySunLightIntensity(mat);
        }
    }

    static void SetActive(Transform t, bool v)
    {
        if (t && t.gameObject.activeSelf != v) t.gameObject.SetActive(v);
    }

    void ApplySunLightIntensity(RenderMat mat)
    {
        if (!sunLight || mat == null) return;
        sunLight.intensity = mat.light;
    }

    void PrimeMatSnapshots()
    {
        _matSnapshots.Clear();
        foreach (var m in renderMats)
        {
            if (!m) continue;
            _matSnapshots[m] = new MatSnapshot
            {
                light = m.light,
                materialId = m.material ? m.material.GetInstanceID() : 0
            };
        }
    }

    void WatchRenderMatChanges()
    {
        if (_matSnapshots.Count > renderMats.Count)
        {
            var keep = new HashSet<RenderMat>(renderMats.Where(m => m));
            var toRemove = _matSnapshots.Keys.Where(k => !keep.Contains(k)).ToList();
            foreach (var k in toRemove) _matSnapshots.Remove(k);
        }

        foreach (var mat in renderMats)
        {
            if (!mat) continue;

            int curMatId = mat.material ? mat.material.GetInstanceID() : 0;
            float curLight = mat.light;

            MatSnapshot prev;
            bool known = _matSnapshots.TryGetValue(mat, out prev);

            bool lightChanged = !known || !Mathf.Approximately(prev.light, curLight);
            bool materialChanged = !known || prev.materialId != curMatId;

            if (lightChanged || materialChanged)
            {
                _matSnapshots[mat] = new MatSnapshot { light = curLight, materialId = curMatId };

                var child = FindChildByName(GetMatLabel(mat));
                if (!child)
                {
                    SyncChildrenToRenderMats();
                    child = FindChildByName(GetMatLabel(mat));
                }

                if (materialChanged && child)
                    ApplyMaterialToChild(child, mat.material);

                if (lightChanged && sunLight && texturesRoot && visibleTextureIndex >= 0 && visibleTextureIndex < texturesRoot.childCount)
                {
                    var visibleChild = texturesRoot.GetChild(visibleTextureIndex);
                    if (visibleChild && string.Equals(visibleChild.name, GetMatLabel(mat), StringComparison.Ordinal))
                        ApplySunLightIntensity(mat);
                }
            }
        }
    }

    IEnumerator BatchUpdateRenderMats()
    {
        SyncChildrenToRenderMats();

        int originalIndex = visibleTextureIndex;
        _suppressIndexWatcher = true;

        float prevLightIntensity = sunLight ? sunLight.intensity : 0f;

        foreach (var mat in renderMats)
        {
            if (!mat) continue;

            var child = FindChildByName(GetMatLabel(mat));
            if (!child) continue;

            int idx = child.GetSiblingIndex();
            visibleTextureIndex = idx;
            ApplyVisibleChild(idx);

            if (sunLight) ApplySunLightIntensity(mat);

            yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D savedAsset = null;
            // Keep file naming based on the asset object's name.
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

        if (sunLight) sunLight.intensity = prevLightIntensity;
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

    IEnumerator CaptureAndSaveCoroutine(string baseName, Action<Texture2D> onSavedAsset = null)
    {
        var cam = targetCamera ? targetCamera : GetComponent<Camera>();
        if (!cam) yield break;

        int w = resolution.x > 0 ? resolution.x : (Application.isPlaying && cam.pixelWidth > 0 ? cam.pixelWidth : 1024);
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
        Texture2D asset = SaveTextureAsset(toSave, baseName);
        onSavedAsset?.Invoke(asset);
#else
        onSavedAsset?.Invoke(null);
#endif

        if (toSave != tex) Destroy(toSave);
        Destroy(rt);
        Destroy(tex);
    }

    static bool TryGetOpaqueBounds(Texture2D tex, byte alphaThreshold, out RectInt bounds)
    {
        int w = tex.width;
        int h = tex.height;
        var pixels = tex.GetPixels32();

        int minX = w, minY = h, maxX = -1, maxY = -1;

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
            return false;
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

    Texture2D SaveTextureAsset(Texture2D tex, string baseName)
    {
        string folder = !string.IsNullOrEmpty(outputFolder) && outputFolder.StartsWith("Assets")
            ? outputFolder : "Assets/Screenshots";
        Directory.CreateDirectory(folder);

        string safe = Sanitize(string.IsNullOrEmpty(baseName) ? name : baseName);

        string[] candidateExts = { ".png", ".jpg", ".jpeg" };
        string chosenExt = null;
        string path = null;
        foreach (var ext in candidateExts)
        {
            var p = Path.Combine(folder, safe + ext).Replace("\\", "/");
            if (File.Exists(p))
            {
                chosenExt = ext;
                path = p;
                break;
            }
        }
        if (path == null)
        {
            chosenExt = ".png";
            path = Path.Combine(folder, safe + chosenExt).Replace("\\", "/");
        }

        byte[] bytes = (chosenExt.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        chosenExt.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                        ? tex.EncodeToJPG(95)
                        : tex.EncodeToPNG();

        File.WriteAllBytes(path, bytes);
#if UNITY_EDITOR
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }
#endif
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrEmpty(s) ? "Baked" : s;
    }

    public void DestroyPreviewChildrenInBuild()
    {
        if (!texturesRoot) return;

        for (int i = texturesRoot.childCount - 1; i >= 0; i--)
        {
            var c = texturesRoot.GetChild(i);
            DestroyImmediate(c.gameObject);
        }

        if (texturesRoot && texturesRoot.gameObject.name != "EditorOnly")
            texturesRoot.gameObject.name = "EditorOnly";
    }
#else
    void Awake()
    {
        var cam = targetCamera ? targetCamera : GetComponent<Camera>();
        cam.depth = -1f;
    }
#endif
}

#if UNITY_EDITOR
static class TextureBakerBuildHooks
{
    [PostProcessScene]
    static void StripPreviewChildrenInBuild()
    {
        if (!BuildPipeline.isBuildingPlayer) return;

        var bakers = UnityEngine.Object.FindObjectsByType<TextureBaker>(FindObjectsSortMode.None);
        foreach (var baker in bakers)
            baker.DestroyPreviewChildrenInBuild();
    }
}
#endif