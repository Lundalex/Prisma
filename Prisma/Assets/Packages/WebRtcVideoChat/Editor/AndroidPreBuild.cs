/* 
 * Copyright (C) 2025 because-why-not.com Limited
 * 
 * Please refer to the license.txt for license information
 */


using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Byn.Unity
{
    public class AndroidPreBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
#if UNITY_2022 || UNITY_2021
            if (report.summary.platform == BuildTarget.Android)
            {
                /* WebRTC Video Chat 1.1 and higher trigger a bug in the old R8 compiler 
                 * packaged with Android Gradle Plugin.
                 * To workaround the issue you can either copy the file 
                 * from Assets/WebRtcVideoChat/Plugins/Android/baseProjectTemplate.gradle
                 * to Assets/Plugins/Android/baseProjectTemplate.gradle
                 * OR manually update the gradle plugin to 8.3 or higher. 
                 * 
                 * In both cases you should use the "Export Project" option.
                 * 
                 * Delete this .cs file to get rid of the warning.
                 */
                Debug.LogWarning(
                    "WebRTC Video Chat: Android builds in Unity 2021/2022 may fail without a custom Gradle config.\n" +
                    "Copy the example Gradle file from:\n" +
                    "Assets/WebRtcVideoChat/Plugins/Android/baseProjectTemplate.gradle\n" +
                    "to Assets/Plugins/Android/baseProjectTemplate.gradle\n" +
                    "Or update your Android Gradle Plugin to 8.3+.\n" +
                    "Use 'Export Project' for both options.\n" +
                    "Delete AndroidPreBuild.cs to remove this warning."
                );

            }
#endif
        }
    }
}
