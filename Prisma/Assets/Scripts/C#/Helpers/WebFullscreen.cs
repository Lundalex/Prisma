using System.Runtime.InteropServices;
using UnityEngine;

public static class WebFullscreen
{
#if !UNITY_EDITOR && (UNITY_WEBGL || UNITY_WEBGPU || UNITY_WEB)
    [DllImport("__Internal")] private static extern int WF_IsViewportLikelyFullscreen();
    public static bool IsViewportLikelyFullscreen() => WF_IsViewportLikelyFullscreen() != 0;
#else
    public static bool IsViewportLikelyFullscreen()
    {
        // In Editor / non-Web builds, just mirror Screen.fullScreen as a harmless fallback.
        return Screen.fullScreen;
    }
#endif
}