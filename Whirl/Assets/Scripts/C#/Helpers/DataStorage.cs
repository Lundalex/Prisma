using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class DataStorage : MonoBehaviour
{
    [SerializeField] private string uniqueKey;

    private static readonly Dictionary<string, object> cache = new();
    public static bool hasValue => cache.Count > 0;

    public void SetValue<T>(T value)
    {
        EnsureKey();
        cache[uniqueKey] = value;
    }

    public T GetValue<T>()
    {
        EnsureKey();
        if (cache.TryGetValue(uniqueKey, out var v) && v is T t) return t;
        return default;
    }

    public bool TryGetValue<T>(out T value)
    {
        EnsureKey();
        if (cache.TryGetValue(uniqueKey, out var v) && v is T t)
        { value = t; return true; }
        value = default; return false;
    }

    private void EnsureKey()
    {
        if (string.IsNullOrEmpty(uniqueKey))
            uniqueKey = GenerateRandomKey(20);
    }

    private static string GenerateRandomKey(int length)
    {
        const string letters = "abcdefghijklmnopqrstuvwxyz";
        var r = new System.Random(Guid.NewGuid().GetHashCode());
        var c = new char[length];
        for (int i = 0; i < length; i++) c[i] = letters[r.Next(letters.Length)];
        return new string(c);
    }

#if UNITY_EDITOR
    [ContextMenu("Regenerate Key")]
    private void RegenerateKey_Context()
    {
        Undo.RecordObject(this, "Regenerate DataStorage Key");
        uniqueKey = GenerateRandomKey(20);
        EditorUtility.SetDirty(this);
    }

    [ContextMenu("Rebuild All Keys In Scene")]
    private void RebuildAllKeysInScene_Context() => RebuildAllKeysInScene();

    [MenuItem("Tools/DataStorage/Rebuild Keys In Scene")]
    public static void RebuildAllKeysInScene()
    {
        ClearCache();

        var all = Resources.FindObjectsOfTypeAll<DataStorage>();
        foreach (var ds in all)
        {
            if (ds == null) continue;
            var go = ds.gameObject;
            if (!go.scene.IsValid() || !go.scene.isLoaded) continue;

            Undo.RecordObject(ds, "Rebuild DataStorage Key");
            ds.uniqueKey = GenerateRandomKey(20);
            EditorUtility.SetDirty(ds);
        }
        AssetDatabase.SaveAssets();
    }

    [InitializeOnLoadMethod]
    private static void HookPlaymodeReset()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingPlayMode ||
                state == PlayModeStateChange.EnteredEditMode)
                ClearCache();
        };
    }
#endif

    private static void ClearCache() => cache.Clear();
}