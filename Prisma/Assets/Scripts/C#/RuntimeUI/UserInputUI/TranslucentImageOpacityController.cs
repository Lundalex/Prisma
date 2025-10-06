using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using LeTai.Asset.TranslucentImage;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
public class TranslucentImageOpacityController : MonoBehaviour
{
    [Header("Translucent Image Controls")]
    [Range(0f, 1f)]
    [SerializeField] private float opacity = 0.5f;

    [Tooltip("Scales the RGB of Image.color for each TranslucentImage")]
    [SerializeField] private float colorMultiplier = 1f;

    [Tooltip("Minimum seconds between runtime updates")]
    [SerializeField] private float applyIntervalSeconds = 1f;

    [SerializeField, HideInInspector] private float _lastAppliedOpacity = -1f;
    [SerializeField, HideInInspector] private float _lastAppliedColorMul = 1f;

    private float _lastApplyTime = float.NegativeInfinity;

    private void OnEnable()
    {
        if (Application.isPlaying)
            ApplyAll(force: true);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (Time.unscaledTime - _lastApplyTime < Mathf.Max(0.01f, applyIntervalSeconds))
            return;

        if (!Mathf.Approximately(_lastAppliedOpacity, opacity) ||
            !Mathf.Approximately(_lastAppliedColorMul, colorMultiplier))
        {
            ApplyAll();
        }
    }

    private void ApplyAll(bool force = false)
    {
        var images = GetAllImages(); // includes inactive

        for (int i = 0; i < images.Length; i++)
        {
            var img = images[i];

            // Opacity
            if (force || !Mathf.Approximately(_lastAppliedOpacity, opacity))
                img.foregroundOpacity = opacity;

            // Color multiplier (RGB only): divide out old, multiply in new
            if (force || !Mathf.Approximately(_lastAppliedColorMul, colorMultiplier))
            {
                var c = img.color;
                float oldMul = Mathf.Approximately(_lastAppliedColorMul, 0f) ? 1f : _lastAppliedColorMul;
                c.r = (c.r / oldMul) * colorMultiplier;
                c.g = (c.g / oldMul) * colorMultiplier;
                c.b = (c.b / oldMul) * colorMultiplier;
                img.color = c;
            }

            // Refraction OFF
            DisableRefraction(img);
        }

        _lastAppliedOpacity  = opacity;
        _lastAppliedColorMul = colorMultiplier;
        _lastApplyTime       = Time.unscaledTime;
    }

    private static void DisableRefraction(TranslucentImage img)
    {
        if (!img) return;

        var t = img.GetType();

        // Try common API first
        if (TrySetEnumByName(t, img, "refractionMode", "Off")) return;
        if (TrySetEnumByName(t, img, "RefractionMode", "Off")) return;
        if (TrySetBoolByName(t, img, "refraction", false)) return;
        if (TrySetBoolByName(t, img, "enableRefraction", false)) return;

        // Fallback to material hints
        var mat = img.materialForRendering;
        if (mat)
        {
            if (mat.HasProperty("_Eta")) mat.SetFloat("_Eta", 1f);
            if (mat.HasProperty("_RefractionStrength")) mat.SetFloat("_RefractionStrength", 0f);
            mat.DisableKeyword("KW_REFRACTION");
            mat.DisableKeyword("REFRACTION");
        }
    }

    private static bool TrySetEnumByName(Type t, object obj, string memberName, string enumValueName)
    {
        var prop = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite && prop.PropertyType.IsEnum)
        {
            try { prop.SetValue(obj, Enum.Parse(prop.PropertyType, enumValueName, true)); return true; } catch { }
        }

        var field = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType.IsEnum)
        {
            try { field.SetValue(obj, Enum.Parse(field.FieldType, enumValueName, true)); return true; } catch { }
        }

        return false;
    }

    private static bool TrySetBoolByName(Type t, object obj, string memberName, bool value)
    {
        var prop = t.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
        {
            try { prop.SetValue(obj, value); return true; } catch { }
        }

        var field = t.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field != null && field.FieldType == typeof(bool))
        {
            try { field.SetValue(obj, value); return true; } catch { }
        }

        return false;
    }

    private static TranslucentImage[] GetAllImages()
    {
#if UNITY_2023_1_OR_NEWER
        // IMPORTANT: this overload includes inactive when explicitly requested
        return UnityEngine.Object.FindObjectsByType<TranslucentImage>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
#else
        // Fallback that also sees inactive, but only those actually in open scenes
        var all = Resources.FindObjectsOfTypeAll<TranslucentImage>();
        var list = new List<TranslucentImage>(all.Length);
        for (int i = 0; i < all.Length; i++)
        {
            var img = all[i];
            if (img && img.gameObject.scene.IsValid()) // skip prefab assets, only scene objects
                list.Add(img);
        }
        return list.ToArray();
#endif
    }

#if UNITY_EDITOR
    private static TranslucentImageOpacityController FindController()
    {
        if (Selection.activeGameObject)
        {
            var sel = Selection.activeGameObject.GetComponentInParent<TranslucentImageOpacityController>();
            if (sel) return sel;
        }
        return UnityEngine.Object.FindFirstObjectByType<TranslucentImageOpacityController>();
    }

    [MenuItem("Tools/Translucent Image/Apply Opacity (Edit Mode)")]
    private static void MenuApplyOpacity()
    {
        var ctrl = FindController();
        float targetOpacity = ctrl ? ctrl.opacity : 0.5f;

        var images = GetAllImages();
        Undo.RecordObjects(images, "Apply TranslucentImage Opacity");
        foreach (var img in images) img.foregroundOpacity = targetOpacity;

        if (ctrl)
        {
            Undo.RecordObject(ctrl, "Update Last Applied Opacity");
            ctrl._lastAppliedOpacity = targetOpacity;
            EditorUtility.SetDirty(ctrl);
        }

        foreach (var img in images) EditorUtility.SetDirty(img);
        Debug.Log($"Applied opacity {targetOpacity} to {images.Length} TranslucentImage(s).");
    }

    [MenuItem("Tools/Translucent Image/Apply Color Multiplier (Edit Mode)")]
    private static void MenuApplyColorMultiplier()
    {
        var ctrl = FindController();
        float newMul = ctrl ? ctrl.colorMultiplier : 1f;
        float oldMul = ctrl ? ctrl._lastAppliedColorMul : 1f;
        if (Mathf.Approximately(oldMul, 0f)) oldMul = 1f;

        var images = GetAllImages();
        Undo.RecordObjects(images, "Apply TranslucentImage Color Multiplier");

        foreach (var img in images)
        {
            var c = img.color;
            c.r = (c.r / oldMul) * newMul;
            c.g = (c.g / oldMul) * newMul;
            c.b = (c.b / oldMul) * newMul;
            img.color = c;
            EditorUtility.SetDirty(img);
        }

        if (ctrl)
        {
            Undo.RecordObject(ctrl, "Update Last Applied Color Multiplier");
            ctrl._lastAppliedColorMul = newMul;
            EditorUtility.SetDirty(ctrl);
        }

        Debug.Log($"Applied color multiplier {newMul} (was {oldMul}) to {images.Length} TranslucentImage(s).");
    }

    [MenuItem("Tools/Translucent Image/Disable Refraction (Edit Mode)")]
    private static void MenuDisableRefraction()
    {
        var images = GetAllImages();
        Undo.RecordObjects(images, "Disable TranslucentImage Refraction");
        foreach (var img in images)
        {
            DisableRefraction(img);
            EditorUtility.SetDirty(img);
        }
        Debug.Log($"Disabled refraction on {images.Length} TranslucentImage(s).");
    }
#endif
}