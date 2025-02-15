using System;
using System.Reflection;
using UnityEngine;

public abstract class FieldModifierBase : MonoBehaviour
{
    [Serializable]
    public class FieldReference
    {
        public MonoBehaviour targetScript;
        public string fieldName;
    }

    virtual public void ModifyClassField(FieldReference fieldReference, string innerFieldName, object newInnerValue)
    {
        if (fieldReference.targetScript == null || string.IsNullOrEmpty(fieldReference.fieldName))
        {
            Debug.LogError("FieldReference is not properly set");
            return;
        }

        var targetType = fieldReference.targetScript.GetType();
        var fieldInfo = targetType.GetField(fieldReference.fieldName, BindingFlags.Public | BindingFlags.Instance);

        if (fieldInfo == null)
        {
            Debug.LogError($"Field '{fieldReference.fieldName}' not found in {targetType.Name}");
            return;
        }

        object classInstance = fieldInfo.GetValue(fieldReference.targetScript);

        if (classInstance == null)
        {
            Debug.LogError("Class instance is null.");
            return;
        }

        // Get the inner field info
        Type classType = classInstance.GetType();
        var innerFieldInfo = classType.GetField(innerFieldName, BindingFlags.Public | BindingFlags.Instance);

        if (innerFieldInfo == null)
        {
            Debug.LogError($"Inner field '{innerFieldName}' not found in {classType.Name}");
            return;
        }

        // Modify the inner field directly
        innerFieldInfo.SetValue(classInstance, newInnerValue);
    }

    virtual public void ModifyField(FieldReference fieldReference, object newValue)
    {
        if (fieldReference.targetScript == null || string.IsNullOrEmpty(fieldReference.fieldName))
        {
            Debug.LogWarning("FieldReference is not set. No field will be modified. FieldModifier: " + this.name);
            return;
        }

        // Get the type of the target script
        var targetType = fieldReference.targetScript.GetType();

        // Get the field info
        var fieldInfo = targetType.GetField(fieldReference.fieldName, BindingFlags.Public | BindingFlags.Instance);

        if (fieldInfo == null)
        {
            Debug.LogError($"Field '{fieldReference.fieldName}' not found in {targetType.Name}");
            return;
        }

        // Set the new value
        fieldInfo.SetValue(fieldReference.targetScript, newValue);
    }
}