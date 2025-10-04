// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using LeTai.Common.Editor;
using UnityEditor;
using UnityEngine;

namespace LeTai.Paraform.Scaffold.Editor
{
public class ParaformEditor
{
    readonly SerializedProperty cornerRadii;
    readonly SerializedProperty isCornersLinked;
    readonly SerializedProperty cornerCurvature;
    readonly SerializedProperty filletCurvature;
    readonly SerializedProperty edgeWidth;
    readonly SerializedProperty elevation;

    readonly GUIContent labelCornerRadii;
    readonly GUIContent labelTR;
    readonly GUIContent labelBR;
    readonly GUIContent labelTL;
    readonly GUIContent labelBL;
    readonly GUIContent labelBtnLink;

    GUIStyle styleBtnLink;
    GUIStyle styleCornerLabelTop;
    GUIStyle styleCornerLabelBot;

    public ParaformEditor(SerializedProperty property)
    {
        cornerRadii     = property.FindPropertyRelative(nameof(ParaformConfig.cornerRadii));
        isCornersLinked = property.FindPropertyRelative(nameof(ParaformConfig.isCornersLinked));
        cornerCurvature = property.FindPropertyRelative(nameof(ParaformConfig.cornerCurvature));
        filletCurvature = property.FindPropertyRelative(nameof(ParaformConfig.filletCurvature));
        edgeWidth       = property.FindPropertyRelative(nameof(ParaformConfig.edgeWidth));
        elevation       = property.FindPropertyRelative(nameof(ParaformConfig.elevation));

        labelCornerRadii = new GUIContent(cornerRadii.displayName);
        labelTR          = new GUIContent(Assets.Find<Texture>("round_corner_tr"));
        labelBR          = new GUIContent(Assets.Find<Texture>("round_corner_br"));
        labelTL          = new GUIContent(Assets.Find<Texture>("round_corner_tl"));
        labelBL          = new GUIContent(Assets.Find<Texture>("round_corner_bl"));
        labelBtnLink     = new GUIContent(EditorGUIUtility.IconContent("Linked"));
    }

    void InitStyles()
    {
        if (styleBtnLink != null)
            return;

        styleBtnLink = new GUIStyle(EditorStyles.miniButton) {
            fixedHeight   = 0,
            margin        = new RectOffset(0, 0, 0,  0),
            padding       = new RectOffset(0, 0, -8, -8),
            contentOffset = new Vector2(0, -1 * EditorGUIUtility.pixelsPerPoint),
        };
        styleCornerLabelTop = new GUIStyle(EditorStyles.label) {
            contentOffset = new Vector2(0, -1f * EditorGUIUtility.pixelsPerPoint)
        };
        styleCornerLabelBot = new GUIStyle(styleCornerLabelTop) {
            contentOffset = new Vector2(0, .5f * EditorGUIUtility.pixelsPerPoint)
        };
    }

    public void DrawShapeControls()
    {
        using var changeScope = new EditorGUI.ChangeCheckScope();

        DrawCornerRadiiControls();

        EditorGUILayout.Space();

        EditorGUILayout.PropertyField(cornerCurvature);
        EditorGUILayout.PropertyField(filletCurvature);

        var radii        = cornerRadii.vector4Value;
        var maxEdgeWidth = Mathf.Min(radii.x, radii.y, radii.z, radii.w);
        EditorCustom.ComputedPropertyField(
            edgeWidth,
            p =>
            {
                var width = p.floatValue;
                return width <= maxEdgeWidth
                    ? width.ToString("0.##")
                    : $"{maxEdgeWidth:0.##} (clamped by radii)";
            });

        EditorGUILayout.PropertyField(elevation);
    }

    public void DrawCornerRadiiControls()
    {
        InitStyles();

        var spacing       = EditorGUIUtility.standardVerticalSpacing;
        var lineHeight    = EditorGUIUtility.singleLineHeight;
        var labelWidth    = EditorGUIUtility.singleLineHeight * 1f;
        var btnLinkWidth  = lineHeight * 1.5f;
        var btnLinkHeight = lineHeight * 1f;
        var totalRect     = EditorGUILayout.GetControlRect(true, lineHeight * 2 + spacing);

        using var prop = new EditorGUI.PropertyScope(totalRect, labelCornerRadii, cornerRadii);

        var controlRect = EditorGUI.PrefixLabel(totalRect, prop.content);

        var floatControlWidth = controlRect.width * .4f;

        var btnLinkSpace   = controlRect.width - floatControlWidth * 2;
        var btnLinkMarginX = (btnLinkSpace - btnLinkWidth) / 2;
        var btnLinkMarginY = (controlRect.height - btnLinkHeight) / 2;

        var rectLink = new Rect(controlRect.x + floatControlWidth + btnLinkMarginX,
                                controlRect.y + btnLinkMarginY,
                                btnLinkWidth,
                                btnLinkHeight);
        using (var changes = new EditorGUI.ChangeCheckScope())
        {
            isCornersLinked.boolValue = GUI.Toggle(rectLink, isCornersLinked.boolValue, labelBtnLink, styleBtnLink);
            if (changes.changed && isCornersLinked.boolValue)
            {
                var newValue = cornerRadii.vector4Value.z;
                cornerRadii.vector4Value = new Vector4(newValue, newValue, newValue, newValue);
            }
        }


        using var labelWidthScope = new EditorCustom.LabelWidthScope(labelWidth);

        var rectFloatLabel = new Rect(0, 0, labelWidth,                     lineHeight);
        var rectFloatField = new Rect(0, 0, floatControlWidth - labelWidth, lineHeight);
        var gridH = new[] {
            controlRect.x,
            controlRect.x + labelWidth,
            controlRect.xMax - floatControlWidth,
            controlRect.xMax - labelWidth,
        };
        var gridV = new[] {
            controlRect.y,
            controlRect.yMax - lineHeight,
        };
        var rectLbTL = new Rect(rectFloatLabel) { x = gridH[0], y = gridV[0] };
        var rectTL   = new Rect(rectFloatField) { x = gridH[1], y = gridV[0] };
        var rectTR   = new Rect(rectFloatField) { x = gridH[2], y = gridV[0] };
        var rectLbTR = new Rect(rectFloatLabel) { x = gridH[3], y = gridV[0] };
        var rectLbBL = new Rect(rectFloatLabel) { x = gridH[0], y = gridV[1] };
        var rectBL   = new Rect(rectFloatField) { x = gridH[1], y = gridV[1] };
        var rectBR   = new Rect(rectFloatField) { x = gridH[2], y = gridV[1] };
        var rectLbBR = new Rect(rectFloatLabel) { x = gridH[3], y = gridV[1] };

        using var changeCheck = new EditorGUI.ChangeCheckScope();
        var       value       = cornerRadii.vector4Value;

        var tl = Mathf.Max(0, EditorCustom.LabelFloatField(rectTL, rectLbTL, labelTL, value.z, styleCornerLabelTop));
        var tr = Mathf.Max(0, EditorCustom.LabelFloatField(rectTR, rectLbTR, labelTR, value.x, styleCornerLabelTop));
        var bl = Mathf.Max(0, EditorCustom.LabelFloatField(rectBL, rectLbBL, labelBL, value.w, styleCornerLabelBot));
        var br = Mathf.Max(0, EditorCustom.LabelFloatField(rectBR, rectLbBR, labelBR, value.y, styleCornerLabelBot));

        if (changeCheck.changed)
        {
            var newRadii = new Vector4(tr, br, tl, bl);

            if (isCornersLinked.boolValue)
            {
                int changedIndex = 0;
                for (int i = 1; i < 4; i++)
                {
                    if (!Mathf.Approximately(newRadii[i], value[i]))
                    {
                        changedIndex = i;
                        break;
                    }
                }
                for (int i = 0; i < 4; i++)
                {
                    newRadii[i] = newRadii[changedIndex];
                }
            }

            cornerRadii.vector4Value = newRadii;
        }
    }

    public static void MaybeGetParaformLinkBtn()
    {
#if !LETAI_PARAFORM
        // DisabledGroupScope and co don't work here !?
        var wasEnabled = GUI.enabled;
        GUI.enabled = true;
        if (EditorCustom.LinkButton("Get Paraform ↗", GUILayout.ExpandWidth(true)))
            Application.OpenURL("");
        GUI.enabled = wasEnabled;
#endif
    }
}
}
