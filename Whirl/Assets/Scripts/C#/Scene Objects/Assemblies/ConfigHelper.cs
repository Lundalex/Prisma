using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

public class ConfigHelper : MonoBehaviour
{
    [SerializeField] private bool onlyRunWhilePlaying;
    [SerializeField] [Min(-1)] private int collectionIndex;
    [SerializeField] [Min(-1)] private int configIndex;
    [SerializeField] private ConfigCollection[] collections;

    // Automatically update the active config when a value changes in the Inspector
#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (collections == null || collections.Length == 0) return;

        // Clamp collectionIndex
        if (collectionIndex >= collections.Length)
        {
            Debug.LogWarning("collectionIndex cannot exceed collections length");
            collectionIndex = collections.Length - 1;
        }

        // Clamp configIndex
        if (collections[collectionIndex].configs != null && 
            collections[collectionIndex].configs.Length > 0)
        {
            if (configIndex >= collections[collectionIndex].configs.Length)
            {
                Debug.LogWarning("configIndex cannot exceed chosen collection configs length");
                configIndex = collections[collectionIndex].configs.Length - 1;
            }
        }
        else return;

        // Delay the execution when not playing to avoid SendMessage warnings
        if (Application.isPlaying) SetActiveConfigByIndex(collectionIndex, configIndex);
        else EditorApplication.delayCall += () => SetActiveConfigByIndex(collectionIndex, configIndex);
    }
#endif

    public void UpdateActiveConfig() => SetActiveConfigByIndex(collectionIndex, configIndex);
    public void SetActiveConfigByIndex(int configIndex) => SetActiveConfigByIndex(collectionIndex, configIndex);
    public void SetActiveConfigByIndex(int collectionIndex, int configIndex)
    {
        if (onlyRunWhilePlaying && !Application.isPlaying) return;
        if (collectionIndex >= collections.Length)
        {
            Debug.LogWarning("configIndex out of range. ConfigHelper: " + this.name);
            return;
        }
        if (collections[collectionIndex] == null) return; // May be null if not yet initiated

        Config[] collectionConfigs = collections[collectionIndex].configs;

        if (configIndex >= collectionConfigs.Length || configIndex < -1)
        {
            Debug.LogWarning("configIndex out of range. ConfigHelper: " + this.name);
            return;
        }
        if (collectionConfigs == null) return; // May be null if not yet initiated
        
        // Create a hash set
        GameObject[] objectsToBeActivated = configIndex == -1 ? new GameObject[0] : collectionConfigs[configIndex].gameObjects;
        HashSet<GameObject> toActivateSet = new(objectsToBeActivated);

        // Activate the objects which are found in the hashset
        for (int i = 0; i < collectionConfigs.Length; i++)
        {
            if (collectionConfigs[i] == null) continue;
            foreach (GameObject obj in collectionConfigs[i].gameObjects)
            {
                if (obj == null) continue;
                obj.SetActive(toActivateSet.Contains(obj));
            }
        }
    }

    public void SetActiveConfigByName(string collectionName, string configName)
    {
        int collectionIndex = -1;
        for (int i = 0; i < collections.Length; i++)
        {
            if (collections[i].name == collectionName)
            {
                collectionIndex = i;
                break;
            }
        }

        if (collectionIndex == -1)
        {
            Debug.LogWarning("ConfigCollection with name '" + collectionName + "' not found. ConfigHelper: " + this.name);
            return;
        }

        Config[] collectionConfigs = collections[collectionIndex].configs;
        int configIndex = -1;
        for (int i = 0; i < collectionConfigs.Length; i++)
        {
            if (collectionConfigs[i] == null) continue;

            if (collectionConfigs[i].name == configName)
            {
                configIndex = i;
                break;
            }
        }

        if (configIndex == -1)
        {
            Debug.LogWarning("Config with name '" + configName + "' not found. ConfigHelper: " + this.name);
            return;
        }

        // Delay the execution when not playing to avoid SendMessage warnings
        if (Application.isPlaying) SetActiveConfigByIndex(collectionIndex, configIndex);
        #if UNITY_EDITOR
            else EditorApplication.delayCall += () => SetActiveConfigByIndex(collectionIndex, configIndex);
        #endif
    }
}