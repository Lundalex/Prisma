// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Collections.Generic;
using LeTai.Common.Editor;
using UnityEditor;
using UnityEngine;

namespace LeTai.Asset.Paraform.Editor
{
public static class ScriptingDefine
{
    const string KEY_STEALTH_MODE = "LeTai_Paraform_StealthModeEnabled";

    internal static readonly HashSet<string> SYMBOLS = new HashSet<string> { "LETAI_PARAFORM" };

    [InitializeOnLoadMethod]
    static void Init()
    {
        var stealth = EditorPrefs.GetBool(KEY_STEALTH_MODE);
        if (stealth)
            EditorUtils.RemoveDefines(SYMBOLS);
        else
            EditorUtils.AddDefines(SYMBOLS);
    }

#if LETAI_DEBUG
    [MenuItem("Tools/Paraform/Toggle Stealth Mode")]
    static void ToggleStealthMode()
    {
        var stealth = !EditorPrefs.GetBool(KEY_STEALTH_MODE);
        EditorPrefs.SetBool(KEY_STEALTH_MODE, stealth);

        Debug.Log($"Stealth mode: {stealth}");

        if (stealth)
            EditorUtils.RemoveDefines(SYMBOLS);
        else
            EditorUtils.AddDefines(SYMBOLS);
    }
#endif
}
}
