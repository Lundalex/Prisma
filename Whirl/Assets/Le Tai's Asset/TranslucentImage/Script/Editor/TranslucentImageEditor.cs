using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using LeTai.Common.Editor;
using LeTai.Paraform.Scaffold.Editor;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.UI;
using UnityEngine;
using Debug = System.Diagnostics.Debug;
using Image = UnityEngine.UI.Image;
using Object = UnityEngine.Object;

#pragma warning disable CS0162 // Unreachable code detected

namespace LeTai.Asset.TranslucentImage.Editor
{
[CustomEditor(typeof(TranslucentImage))]
[CanEditMultipleObjects]
public class TranslucentImageEditor : ImageEditor
{
    SerializedProperty type;
    SerializedProperty sprite;
    SerializedProperty preserveAspect;
    SerializedProperty useSpriteMesh;

    EditorProperty source;
    EditorProperty foregroundOpacity;
    EditorProperty _vibrancy;
    EditorProperty _brightness;
    EditorProperty _flatten;

    GUIStyle styleBtnSource;

    static readonly EditorPrefValue<bool> SHOW_PARAFORM_SHAPE_CONTROLS = new("TranslucentImage_ShowParaformShapeControls", HAVE_PARAFORM);
    static readonly EditorPrefValue<bool> SHOW_MATERIAL_PROPERTIES     = new("TranslucentImage_ShowMaterialProperties", true);

    AnimBool showTypeAnim;

    List<TranslucentImage> tiList;
    UnityEditor.Editor     materialEditor;

    ParaformEditor paraformEditor;

    bool needValidateSource;
    bool needValidateMaterial;
    bool materialUsedInDifferentSource;
    bool usingIncorrectShader;

    const bool HAVE_PARAFORM =
#if LETAI_PARAFORM
        true;
#else
        false;
#endif

    protected override void OnEnable()
    {
        base.OnEnable();

        typeof(ImageEditor).GetField("m_SpriteContent", BindingFlags.Instance | BindingFlags.NonPublic)
                           .SetValue(this, new GUIContent("Sprite"));

        sprite         = serializedObject.FindProperty("m_Sprite");
        type           = serializedObject.FindProperty("m_Type");
        preserveAspect = serializedObject.FindProperty("m_PreserveAspect");
        useSpriteMesh  = serializedObject.FindProperty("m_UseSpriteMesh");

        source            = new EditorProperty(serializedObject, nameof(TranslucentImage.source), "_source");
        foregroundOpacity = new EditorProperty(serializedObject, nameof(TranslucentImage.foregroundOpacity));
        _vibrancy         = new EditorProperty(serializedObject, nameof(TranslucentImage.vibrancy));
        _brightness       = new EditorProperty(serializedObject, nameof(TranslucentImage.brightness));
        _flatten          = new EditorProperty(serializedObject, nameof(TranslucentImage.flatten));
        paraformEditor    = new ParaformEditor(serializedObject.FindProperty("paraformConfig"));

        showTypeAnim = new AnimBool(sprite.objectReferenceValue);
        showTypeAnim.valueChanged.AddListener(Repaint);

        tiList = targets.Cast<TranslucentImage>().ToList();
        if (tiList.Count > 0)
        {
            CheckMaterialUsedInDifferentSource();
            CheckCorrectShader();
        }
    }

    void InitStyle()
    {
        if (styleBtnSource != null)
            return;

        styleBtnSource = new GUIStyle(GUI.skin.button) {
            alignment = TextAnchor.MiddleLeft,
            richText  = true,
        };
    }

    public override void OnInspectorGUI()
    {
        InitStyle();

        serializedObject.Update();

        tiList = targets.Cast<TranslucentImage>().ToList();
        Debug.Assert(tiList.Count > 0, "Translucent Image Editor serializedObject target is null");

        DrawSourceControls();

        DrawShapeControls();

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Appearance", EditorStyles.boldLabel);

        DrawSpriteControls();
        EditorGUILayout.PropertyField(m_Color);
        foregroundOpacity.Draw();
        _vibrancy.Draw();
        _brightness.Draw();
        _flatten.Draw();
        EditorGUILayout.Space();
        DrawMaterialControls();
        DrawMaterialProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Interaction", EditorStyles.boldLabel);
        RaycastControlsGUI();
        MaskableControlsGUI();

        serializedObject.ApplyModifiedProperties();

        if (needValidateSource) ValidateSource();
        if (needValidateMaterial) ValidateMaterial();
    }

    void DrawSourceControls()
    {
        using (var changes = new EditorGUI.ChangeCheckScope())
        {
            source.Draw();
            if (changes.changed)
                needValidateSource = true;
        }

        if (!source.serializedProperty.objectReferenceValue)
        {
            var existingSources = Shims.FindObjectsOfType<TranslucentImageSource>();
            if (existingSources.Length > 0)
            {
                using (new EditorGUI.IndentLevelScope())
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PrefixLabel("From current Scene");
                    using (new EditorGUILayout.VerticalScope())
                    {
                        int smallFontSize = Mathf.RoundToInt(styleBtnSource.fontSize * .8f);
                        foreach (var s in existingSources)
                        {
                            var label = $"{s.gameObject.name}\t<size={smallFontSize}>STR: </size>{s.BlurConfig.Strength:0.#}";
                            if (GUILayout.Button(label, styleBtnSource))
                            {
                                Undo.RecordObject(target, $"Set source to {s.gameObject.name}");
                                source.serializedProperty.objectReferenceValue = s;
                                source.CallSetters(s);
                                needValidateSource = true;
                            }
                        }
                    }
                }

                EditorGUILayout.Space();
            }
            else
            {
                EditorGUILayout.HelpBox("No Translucent Image Source(s) found in current scene", MessageType.Warning);
            }
        }
    }


    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
    [SuppressMessage("ReSharper", "HeuristicUnreachableCode")]
    void DrawShapeControls()
    {
        var useParaform = tiList.Any(ti => ti.material && ti.material.shader.name.EndsWith("-Paraform"));

        using (new EditorGUILayout.HorizontalScope())
        {
            var label = "Paraform";
            if (!HAVE_PARAFORM)
                label += " (not installed)";
            else if (!useParaform)
                label += " (Not supported by material)";
            SHOW_PARAFORM_SHAPE_CONTROLS.Value = EditorGUILayout.BeginFoldoutHeaderGroup(SHOW_PARAFORM_SHAPE_CONTROLS, label);

            if (HAVE_PARAFORM && !useParaform)
            {
                if (GUILayout.Button("Use Paraform Shader"))
                {
                    foreach (var ti in tiList)
                    {
                        var mat = ti.material;
                        Undo.RecordObject(mat, "Use Paraform Shader");
                        mat.shader = Shader.Find("UI/TranslucentImage-Paraform");
                    }
                }
            }
        }

        using var disabledGroupScope = new EditorGUI.DisabledGroupScope(!HAVE_PARAFORM || !useParaform);

        if (SHOW_PARAFORM_SHAPE_CONTROLS)
        {
            using var changes = new EditorGUI.ChangeCheckScope();

            paraformEditor?.DrawShapeControls();

            if (changes.changed)
            {
                serializedObject.ApplyModifiedProperties();
                foreach (var ti in tiList) ti.paraformConfig.NotifyChanged();
            }

            ParaformEditor.MaybeGetParaformLinkBtn();
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    void DrawSpriteControls()
    {
        SpriteGUI();
        showTypeAnim.target = sprite.objectReferenceValue != null;
        if (EditorGUILayout.BeginFadeGroup(showTypeAnim.faded))
            TypeGUI();
        EditorGUILayout.EndFadeGroup();

        Image.Type type = (Image.Type)this.type.enumValueIndex;
        bool showNativeSize = (type == Image.Type.Simple || type == Image.Type.Filled)
                           && sprite.objectReferenceValue != null;
        SetShowNativeSize(showNativeSize, false);

        if (EditorGUILayout.BeginFadeGroup(m_ShowNativeSize.faded))
        {
            EditorGUI.indentLevel++;

            if ((Image.Type)this.type.enumValueIndex == Image.Type.Simple)
                EditorGUILayout.PropertyField(useSpriteMesh);

            EditorGUILayout.PropertyField(preserveAspect);
            EditorGUI.indentLevel--;
        }
        EditorGUILayout.EndFadeGroup();
        NativeSizeButtonGUI();
    }

    void DrawMaterialControls()
    {
        using var changes = new EditorGUI.ChangeCheckScope();
        EditorGUILayout.PropertyField(m_Material);
        if (changes.changed)
            needValidateMaterial = true;

        if (usingIncorrectShader)
        {
            EditorGUILayout.HelpBox("Material is using unsupported shader", MessageType.Warning);
        }
        if (materialUsedInDifferentSource)
        {
            EditorGUILayout.HelpBox("Translucent Images with different Sources" +
                                    " should also use different Materials",
                                    MessageType.Error);
        }
    }

    void DrawMaterialProperties()
    {
        using var change          = new EditorGUI.ChangeCheckScope();
        var       targetMaterials = tiList.Select(t => t.material).Cast<Object>().ToArray();

        using (_ = new EditorGUI.IndentLevelScope())
        {
            SHOW_MATERIAL_PROPERTIES.Value = EditorGUILayout.BeginFoldoutHeaderGroup(SHOW_MATERIAL_PROPERTIES, "Material settings");
            if (SHOW_MATERIAL_PROPERTIES)
            {
                bool prevGuiEnabled = GUI.enabled;
                if (targetMaterials.Any(m => m.hideFlags == HideFlags.HideAndDontSave))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        EditorGUILayout.HelpBox("Create a new Material to edit", MessageType.Info);
                        if (GUILayout.Button("Create Material", GUILayout.ExpandHeight(true)))
                        {
                            var path = EditorUtility.SaveFilePanelInProject("Save New Material", "Translucent Image Material", "mat", "");
                            if (!string.IsNullOrEmpty(path))
                            {
                                var material = Instantiate(HAVE_PARAFORM
                                                               ? DefaultResources.Instance.paraformMaterial
                                                               : DefaultResources.Instance.material);
                                m_Material.objectReferenceValue = material;
                                AssetDatabase.CreateAsset(material, path);
                            }
                        }
                    }
                    GUI.enabled = false;
                }

                CreateCachedEditor(targetMaterials, typeof(MaterialEditor), ref materialEditor);
                var materialProperties = MaterialEditor.GetMaterialProperties(targetMaterials);
                TranslucentImageShaderGUI.DrawProperties((MaterialEditor)materialEditor, materialProperties, true);

                GUI.enabled = prevGuiEnabled;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }


        if (change.changed)
        {
            foreach (var ti in tiList)
            {
                if (ti.materialForRendering != ti.material)
                {
                    Undo.RecordObject(ti.materialForRendering, $"Modify material {ti.material.name}");
                    TranslucentImage.CopyMaterialPropertiesTo(ti.material, ti.materialForRendering);
                }
            }
        }
    }

    void ValidateSource()
    {
        CheckMaterialUsedInDifferentSource();
        needValidateSource = false;
    }

    void ValidateMaterial()
    {
        CheckMaterialUsedInDifferentSource();
        CheckCorrectShader();
        needValidateMaterial = false;
    }

    private void CheckCorrectShader()
    {
        usingIncorrectShader = tiList.Any(ti => !ti.material.shader.name.Contains("TranslucentImage"));
    }

    private void CheckMaterialUsedInDifferentSource()
    {
        if (!tiList[0].source
         || tiList[0].material.IsKeywordEnabled(ShaderID.KW_BACKGROUND_MODE_OPAQUE))
        {
            materialUsedInDifferentSource = false;
            return;
        }

        var diffSource = Shims.FindObjectsOfType<TranslucentImage>()
                              .Where(ti => ti.source != tiList[0].source)
                              .ToList();

        if (!diffSource.Any())
        {
            materialUsedInDifferentSource = false;
            return;
        }

        var sameMat = diffSource.GroupBy(ti => ti.material).ToList();

        materialUsedInDifferentSource = sameMat.Any(group => group.Key == tiList[0].material);

        needValidateMaterial = false;
    }
}
}
