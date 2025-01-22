#if UNITY_EDITOR
using System;
using UnityEditor;
#endif
using UnityEngine;

[CreateAssetMenu(fileName = "DeterministicData", menuName = "DeterministicData")]
public class DeterministicData : ScriptableObject
{
    private static DeterministicData _instance;
    public static DeterministicData Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<DeterministicData>("DeterministicData");
                if (_instance == null)
                {
                    _instance = CreateInstance<DeterministicData>();
                    #if UNITY_EDITOR
                    AssetDatabase.CreateAsset(_instance, "Assets/Resources/DeterministicData.asset");
                    #endif
                }
            }
            return _instance;
        }
    }

    public PData[] PDatas;

    public void SaveData()
    {
        #if UNITY_EDITOR
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        #endif
    }
}