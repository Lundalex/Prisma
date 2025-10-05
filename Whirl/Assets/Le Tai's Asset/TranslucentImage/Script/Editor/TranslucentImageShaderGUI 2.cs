using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using LeTai.Common.Editor;
using LeTai.Paraform.Scaffold;
using LeTai.Paraform.Scaffold.Editor;
using UnityEditor;
using UnityEngine;
using EGU = UnityEditor.EditorGUIUtility;

namespace LeTai.Asset.TranslucentImage.Editor
{
public class TranslucentImageShaderGUI : ShaderGUI
{
    static readonly EditorPrefValue<bool> SHOW_PARAFORM_MATERIAL_CONTROLS = new("TranslucentImage_ShowParaformMaterialControls", true);

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        DrawProperties(materialEditor, properties, false);
    }

    [SuppressMessage("ReSharper", "ConditionIsAlwaysTrueOrFalse")]
    public static void DrawProperties(MaterialEditor materialEditor, MaterialProperty[] properties, bool skipUnimportants)
    {
        float oldLabelWidth = EGU.labelWidth;
        float oldFieldWidth = EGU.fieldWidth;
        materialEditor.SetDefaultGUIWidths();

        // ReSharper disable once ConvertToConstant.Local
        bool haveParaform = false;
#if LETAI_PARAFORM
        haveParaform = true;
#endif

        var useParaform = ((Material)materialEditor.target).shader.name == "UI/TranslucentImage-Paraform";

        using var propIter = properties.AsEnumerable().GetEnumerator();

        var reflectionModeProp = DrawDefaultUntil("_REFRACTION_MODE");

        EditorGUILayout.Space();

        var paraformLabel = "Paraform";
        if (!haveParaform)
            paraformLabel += " (not installed)";
        SHOW_PARAFORM_MATERIAL_CONTROLS.Value = EditorGUILayout.Foldout(
            SHOW_PARAFORM_MATERIAL_CONTROLS, paraformLabel, true, EditorStyles.foldoutHeader);
        if (!SHOW_PARAFORM_MATERIAL_CONTROLS)
        {
            while (propIter.Current.name != "_StencilComp")
                propIter.MoveNext();
        }
        else
        {
            using (new EditorGUI.DisabledGroupScope(!haveParaform))
            {
                if (haveParaform && !useParaform)
                    EditorGUILayout.HelpBox("Change the material's shader to UI/TranslucentImage-Paraform to enable", MessageType.Info);

                using (new EditorGUI.DisabledGroupScope(!useParaform))
                {
                    var refractiveIndexProp       = ConsumeProp();
                    var chromaticDispersionProp   = ConsumeProp();
                    var refractiveIndexRatiosProp = ConsumeProp();
                    using (var changes = new EditorGUI.ChangeCheckScope())
                    {
                        DrawDefault(reflectionModeProp);
                        var refractionMode = (RefractionMode)(int)reflectionModeProp.floatValue;

                        using (new EditorGUI.DisabledScope(refractionMode == RefractionMode.Off))
                            DrawDefault(refractiveIndexProp);

                        using (new EditorGUI.DisabledScope(refractionMode != RefractionMode.Chromatic))
                            DrawDefault(chromaticDispersionProp);

                        if (changes.changed)
                            refractiveIndexRatiosProp.vectorValue = ParaformMaterial
                               .GetRefractiveIndexRatios(
                                    refractiveIndexProp.floatValue,
                                    refractionMode == RefractionMode.Chromatic ? chromaticDispersionProp.floatValue : 0
                                );
                    }


                    var useEdgeGlintProp = ConsumeProp();
                    DrawDefault(useEdgeGlintProp);
                    using (new EditorGUI.DisabledScope(useEdgeGlintProp.floatValue == 0))
                    {
                        var edgeGlintDirectionsProp = ConsumePropName("_EdgeGlintDirections");
                        DrawEdgeGlintDirections(edgeGlintDirectionsProp);

                        var edgeGlintWrapProp = DrawDefaultUntil("_EdgeGlintWrap");
                        DrawEdgeGlintWrap(edgeGlintWrapProp);

                        var edgeGlintSharpnessProp = ConsumePropName("_EdgeGlintSharpness");
                        DrawEdgeGlintSharpness(edgeGlintSharpnessProp);
                    }
                }

                if (!haveParaform)
                    ParaformEditor.MaybeGetParaformLinkBtn();

                ConsumeProp();
            }
        }

        if (!skipUnimportants)
        {
            do
            {
                DrawDefault(propIter.Current);
            } while (propIter.MoveNext());
        }

        EditorGUILayout.Space();

        if (!skipUnimportants)
        {
            EditorGUILayout.Space();
            if (UnityEngine.Rendering.SupportedRenderingFeatures.active.editableMaterialRenderQueue)
                materialEditor.RenderQueueField();
            materialEditor.EnableInstancingField();
            materialEditor.DoubleSidedGIField();
        }

        EGU.labelWidth = oldLabelWidth;
        EGU.fieldWidth = oldFieldWidth;

        return;

        MaterialProperty ConsumeProp()
        {
            return propIter.MoveNext() ? propIter.Current : null;
        }

        MaterialProperty ConsumePropName(string name)
        {
            var next = ConsumeProp();
            if (next.name != name)
                throw new ArgumentException($"Expect {name} but got {next.name}");
            return next;
        }

        MaterialProperty DrawDefaultUntil(string name)
        {
            while (propIter.MoveNext() && propIter.Current.name != name)
                DrawDefault(propIter.Current);

            return propIter.Current;
        }

        void DrawDefault(MaterialProperty prop)
        {
            if ((prop.flags & MaterialProperty.PropFlags.HideInInspector) != 0)
                return;

            if (skipUnimportants && (prop.flags & MaterialProperty.PropFlags.PerRendererData) != 0)
                return;

            float h = materialEditor.GetPropertyHeight(prop, prop.displayName);
            Rect  r = EditorGUILayout.GetControlRect(true, h);
            materialEditor.ShaderProperty(r, prop, GetPropGUIContent(prop));
        }
    }

    static void DrawEdgeGlintDirections(MaterialProperty prop)
    {
        Vector4 value = prop.vectorValue;

        var angle1 = MathCustom.VecToAngle360(Vector2.right, new Vector2(value.x, -value.y));
        var angle2 = MathCustom.VecToAngle360(Vector2.right, new Vector2(value.z, -value.w));

        EditorGUI.showMixedValue = prop.hasMixedValue;
        using var changeScope = new EditorGUI.ChangeCheckScope();

        LABEL.text = "Edge Glint 1 Direction";
        angle1     = EditorGUICustom.KnobField(LABEL, angle1, Vector2.right);
        LABEL.text = "Edge Glint 2 Direction";
        angle2     = EditorGUICustom.KnobField(LABEL, angle2, Vector2.right);

        EditorGUI.showMixedValue = false;
        if (changeScope.changed)
        {
            var dir1 = MathCustom.Angle360ToVec(angle1, Vector2.right);
            var dir2 = MathCustom.Angle360ToVec(angle2, Vector2.right);
            prop.vectorValue = new Vector4(dir1.x, -dir1.y, dir2.x, -dir2.y);
        }
    }

    static void DrawEdgeGlintWrap(MaterialProperty prop)
    {
        using var scope = new MaterialEditorGUI.PropertyScope(prop);

        var normalized = ParaformMaterial.EdgeGlintWrapFromRaw(prop.floatValue);
        normalized = MaterialEditorGUI.Slider("Edge Glint Wrap", normalized, 0, 1);

        if (scope.Changed)
            prop.floatValue = ParaformMaterial.EdgeGlintWrapToRaw(normalized);
    }

    static void DrawEdgeGlintSharpness(MaterialProperty prop)
    {
        using var scope = new MaterialEditorGUI.PropertyScope(prop);

        var normalized = ParaformMaterial.EdgeGlintSharpnessFromRaw(prop.floatValue);
        normalized = MaterialEditorGUI.Slider("Edge Glint Sharpness", normalized, 0.05f, .8f);

        if (scope.Changed)
            prop.floatValue = ParaformMaterial.EdgeGlintSharpnessToRaw(normalized);
    }

    static readonly GUIContent LABEL = new GUIContent();

    static GUIContent GetPropGUIContent(MaterialProperty prop)
    {
        switch (prop.name)
        {
        case "_Vibrancy":
            LABEL.tooltip = "(De)Saturate the image, 1 is normal, 0 is black and white, below zero make the image negative";
            break;
        case "_Brightness":
            LABEL.tooltip = "Brighten/darken the image";
            break;
        case "_Flatten":
            LABEL.tooltip = "Flatten the color behind to help keep contrast on varying background";
            break;
        default:
            LABEL.tooltip = "";
            break;
        }

        LABEL.text = prop.displayName;

        return LABEL;
    }

    enum RefractionMode
    {
        Off,
        On,
        Chromatic
    }
}
}
