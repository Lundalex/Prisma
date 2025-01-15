public class FieldModifier : FieldModifierBase
{
    public FieldReference fieldReference;

    public void ModifyClassField(string innerFieldName, object newInnerValue) => base.ModifyClassField(fieldReference, innerFieldName, newInnerValue);

    public void ModifyField(object newValue) => base.ModifyField(fieldReference, newValue);
}