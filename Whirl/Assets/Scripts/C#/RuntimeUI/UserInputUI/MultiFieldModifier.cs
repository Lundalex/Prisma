using UnityEngine;

public class MultiFieldModifier : FieldModifierBase
{
    [SerializeField] FieldReference[] fieldReferences;

    public void ModifyClassFieldByIndex(int index, string innerFieldName, object newInnerValue)
        => base.ModifyClassField(fieldReferences[index], innerFieldName, newInnerValue);

    public void ModifyFieldByIndex(int index, object newValue)
        => base.ModifyField(fieldReferences[index], newValue);

    public void ModifyClassFieldByFieldName(string fieldName, string innerFieldName, object newInnerValue)
        => base.ModifyClassField(fieldReferences[FindFieldIndexByName(fieldName)], innerFieldName, newInnerValue);

    public void ModifyFieldByFieldName(string fieldName, object newValue)
        => base.ModifyField(fieldReferences[FindFieldIndexByName(fieldName)], newValue);

    private int FindFieldIndexByName(string fieldName)
    {
        for (int i = 0; i < fieldReferences.Length; i++)
        {
            if (fieldName == fieldReferences[i].fieldName) return i;
        }

        Debug.LogWarning("No fieldName found with the value '" + fieldName + "'. MultiFieldModifier: " + this.name);
        return -1;
    }
}