using UnityEngine;
using System.Collections.Generic;
using System;

// Helper class for storing data between scene reloads
public class DataStorage : MonoBehaviour
{
    [SerializeField] private string uniqueKey;

    private static readonly Dictionary<string, object> cache = new();
    [NonSerialized] public static bool hasValue = false;

    public void SetValue<T>(T value)
    {
        EnsureKey();

        cache[uniqueKey] = value;
        hasValue = true;
    }

    public T GetValue<T>()
    {
        EnsureKey();

        if (cache.TryGetValue(uniqueKey, out object val))
        {
            if (val is T variable)
            {
                return variable;
            }
            else
            {
                Debug.LogError($"Type conversion failed for key '{uniqueKey}'. Expected type: {typeof(T)}, Actual type: {val.GetType()}");
                return default;
            }
        }
        return default;
    }

    private void EnsureKey()
    {
        // Ensure uniqueKey is set; generate one if not
        if (string.IsNullOrEmpty(uniqueKey)) uniqueKey = Guid.NewGuid().ToString();
    }
}
