public abstract class Assembly : EditorLifeCycle
{
    #if UNITY_EDITOR
        private void OnValidate() => AssemblyUpdate();

        public override void OnEditorUpdate() => AssemblyUpdate();
    #endif

    public abstract void AssemblyUpdate();
}