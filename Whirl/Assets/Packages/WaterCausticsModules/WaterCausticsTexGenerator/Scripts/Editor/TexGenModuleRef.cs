// WaterCausticsModules
// Copyright (c) 2021 Masataka Hakozaki

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace MH.WaterCausticsModules {
#if WCE_DEVELOPMENT
    [CreateAssetMenu]
#endif
    public class TexGenModuleRef : ScriptableObject {
        // WaterCausticsModulesフォルダ
        [SerializeField] private Object m_assetFolder;
        internal Object assetFolder => m_assetFolder;

        // WaterCausticsTexGeneratorフォルダ
        [SerializeField] private Object m_texGenModule;
        internal Object texGenModule => m_texGenModule;

        [SerializeField] internal Texture m_iconUnlock;
        [SerializeField] internal Texture m_iconLock;
        [SerializeField] internal Texture m_iconMenu;
        [SerializeField] internal Shader m_iconShader;

        static internal bool findAsset<T> (out T asset, out string path) where T : Object {
            asset = null;
            path = null;
            var guids = AssetDatabase.FindAssets ($"t:{typeof (T).ToString()}", new [] { "Assets" });
            if (guids.Length == 0) return false;
            path = AssetDatabase.GUIDToAssetPath (guids [0]);
            asset = AssetDatabase.LoadAssetAtPath<T> (path);
            return asset != null;
        }
    }
}
#endif
