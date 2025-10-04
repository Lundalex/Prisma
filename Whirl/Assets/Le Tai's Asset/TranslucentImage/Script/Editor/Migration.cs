using System.Diagnostics.CodeAnalysis;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace LeTai.Asset.TranslucentImage.Editor
{
public static class Migration
{
    [MenuItem("Tools/Translucent Image/Migrate to 6.0", false, 10000)]
    [SuppressMessage("ReSharper", "Unity.PreferAddressByIdToGraphicsParams")]
    static void MigrateTo60()
    {
        var tiArr = Object.FindObjectsByType<TranslucentImage>(FindObjectsSortMode.None);
        foreach (var ti in tiArr)
        {
            Undo.RecordObject(ti, "Migrate to 6.0");

            var mat   = ti.material;
            var matSo = new SerializedObject(mat);

            // var shader = mat.shader;
            // mat.SetKeyword(new LocalKeyword(shader, "_BACKGROUND_MODE_NORMAL"),   mat.IsKeywordEnabled("_FOREGROUND_MODE_CONSISTENT"));
            // mat.SetKeyword(new LocalKeyword(shader, "_BACKGROUND_MODE_COLORFUL"), mat.IsKeywordEnabled("_FOREGROUND_MODE_COLORFUL"));
            // if (shader.name == "UI/TranslucentImage-Paraform")
            //     mat.SetKeyword(new LocalKeyword(shader, "_BACKGROUND_MODE_OPAQUE"), mat.IsKeywordEnabled("_FOREGROUND_MODE_ONLY"));
            // mat.SetFloat("_BACKGROUND_MODE", (int)mat.GetFloat("_FOREGROUND_MODE"));


            var savedProps = matSo.FindProperty("m_SavedProperties");
            if (TryFindOrphanMaterialProperty(savedProps, "_Vibrancy", out float vibrancy))
                ti.vibrancy = vibrancy;
            if (TryFindOrphanMaterialProperty(savedProps, "_Brightness", out float brightness))
                ti.brightness = brightness;
            if (TryFindOrphanMaterialProperty(savedProps, "_Flatten", out float flatten))
                ti.flatten = flatten;
            if (TryFindOrphanMaterialProperty(savedProps, "_ForegroundOpacity", out float foregroundOpacity))
                ti.foregroundOpacity = foregroundOpacity;

            EditorUtility.SetDirty(mat);
            EditorUtility.SetDirty(ti);
            Debug.Log("Migrated " + ti.name);
        }
        var scene = SceneManager.GetActiveScene();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Migrated scene " + scene.name);
    }

    static bool TryFindOrphanMaterialProperty<T>(SerializedProperty savedProps, string propertyName, out T value)
    {
        value = default;

        var arrayName = typeof(T) == typeof(float) ? "m_Floats"
            : typeof(T) == typeof(Color)           ? "m_Colors"
            : typeof(T) == typeof(Texture)         ? "m_Texs"
                                                     : null;

        if (arrayName == null) return false;

        var array = savedProps.FindPropertyRelative(arrayName);
        for (var i = 0; i < array.arraySize; i++)
        {
            var matProp = array.GetArrayElementAtIndex(i);
            if (matProp.FindPropertyRelative("first").stringValue == propertyName)
            {
                var valProp = matProp.FindPropertyRelative("second");
                value = typeof(T) == typeof(float) ? (T)(object)valProp.floatValue
                    : typeof(T) == typeof(Color)   ? (T)(object)valProp.colorValue
                                                     : (T)(object)valProp.objectReferenceValue;
                return true;
            }
        }

        return false;
    }
}
}
