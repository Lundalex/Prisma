// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using UnityEditor;
using UnityEngine;

namespace LeTai.Asset.TranslucentImage.Editor
{
public static class MaterialEditorGUI
{
    internal static float Slider(string label, float value, float left, float right)
    {
        var labelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 0;
        var newValue = EditorGUILayout.Slider(label, value, left, right);
        EditorGUIUtility.labelWidth = labelWidth;
        return newValue;
    }

    public class PropertyScope : GUI.Scope
    {
        public bool Changed => _changeScope.changed;

        private readonly EditorGUI.ChangeCheckScope _changeScope;

        public PropertyScope(MaterialProperty prop)
        {
            _changeScope             = new EditorGUI.ChangeCheckScope();
            EditorGUI.showMixedValue = prop.hasMixedValue;
        }

        protected override void CloseScope()
        {
            EditorGUI.showMixedValue = false;
            _changeScope.Dispose();
        }
    }
}
}
