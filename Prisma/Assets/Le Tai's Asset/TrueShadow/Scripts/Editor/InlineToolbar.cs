// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEditor;
using UnityEngine;

namespace LeTai.TrueShadow.Editor
{
public class InlineToolbar : PropertyDrawer
{
    protected static Texture[] textures;

    static GUIStyle _labelStyle;
    static GUIStyle LabelStyle
    {
        get
        {
            if (_labelStyle == null)
            {
                GUIStyle baseStyle = null;
                try { baseStyle = EditorStyles.label; } catch {}
                _labelStyle = baseStyle != null ? new GUIStyle(baseStyle) : new GUIStyle();
                _labelStyle.alignment = TextAnchor.MiddleLeft;
            }
            return _labelStyle;
        }
    }

    public override void OnGUI(Rect       position, SerializedProperty property,
                               GUIContent label)
    {
        using (var propScope = new EditorGUI.PropertyScope(position, label, property))
        {
            int id        = GUIUtility.GetControlID(FocusType.Keyboard, position);
            var labelRect = position;
            labelRect.y      += (labelRect.height - EditorGUIUtility.singleLineHeight) / 2;
            labelRect.height =  EditorGUIUtility.singleLineHeight;
            var toolbarRect = EditorGUI.PrefixLabel(labelRect, id, propScope.content, LabelStyle);
            toolbarRect.width  = EditorGUIUtility.singleLineHeight * 4f;
            toolbarRect.height = position.height;
            toolbarRect.y      = position.y;

            if (textures == null || textures.Length == 0)
            {
                bool newVal = EditorGUI.Toggle(toolbarRect, property.boolValue);
                if (newVal != property.boolValue) property.boolValue = newVal;
                return;
            }

            using (var changeScope = new EditorGUI.ChangeCheckScope())
            {
                var isOn    = GUI.Toolbar(toolbarRect, property.boolValue ? 1 : 0, textures) == 1;
                var changed = changeScope.changed;

                if (Event.current.type == EventType.KeyDown &&
                    GUIUtility.keyboardControl == id)
                {
                    if (Event.current.keyCode == KeyCode.Return ||
                        Event.current.keyCode == KeyCode.KeypadEnter ||
                        Event.current.keyCode == KeyCode.Space)
                    {
                        changed = GUI.changed = true;
                        isOn    = !isOn;
                    }
                }

                if (changed)
                    property.boolValue = isOn;
            }
        }
    }
}
}