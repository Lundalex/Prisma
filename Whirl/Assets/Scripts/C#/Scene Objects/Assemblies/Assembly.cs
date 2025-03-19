public abstract class Assembly : EditorLifeCycle
{
    #if UNITY_EDITOR
        public override void OnEditorUpdate() => AssemblyUpdate();
    #endif

    private void OnValidate() => AssemblyUpdate();
    
    public abstract void AssemblyUpdate();
}