#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "ParticleBoolData", menuName = "ParticleBoolData")]
public class ParticleBoolData : ScriptableObject
{
    private static ParticleBoolData _instance;
    public static ParticleBoolData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ParticleBoolData>("ParticleBoolData");
                if (_instance == null)
                {
                    _instance = CreateInstance<ParticleBoolData>();
                    #if UNITY_EDITOR
                    AssetDatabase.CreateAsset(_instance, "Assets/Resources/ParticleBoolData.asset");
                    #endif
                }
            }
            return _instance;
        }
    }

    public List<bool> containedFlags;
    public PData[] PDatas;

    // Call this after generating new particles to force Unity to save the asset:
    public void SaveData()
    {
        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        #endif
    }
}