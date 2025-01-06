using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Diagnostics;

[CustomEditor(typeof(Main))]
public class MainEditor : Editor
{
    public VisualTreeAsset m_UXML;
    public Stopwatch warningCooldownStopwatch;

    public override VisualElement CreateInspectorGUI()
    {
        // Initialize stopwatch
        warningCooldownStopwatch = new();
        warningCooldownStopwatch.Start();

        // Create a root VisualElement
        var root = new VisualElement();

        // Clone UXML layout into the root
        m_UXML.CloneTree(root);

        // Find the existing PropertyFields and TextField in the UXML by their name or binding path
        var maxSpringsPerParticleField = root.Q<PropertyField>("MaxSpringsPerParticle");
        var maxParticlesNumField = root.Q<PropertyField>("MaxParticlesNum");
        var maxStartingParticlesNumField = root.Q<PropertyField>("MaxStartingParticlesNum");
        var totalParticleSpringsField = root.Q<TextField>("TotalParticleSprings");

        // Find the properties in the serialized object
        var serializedMaxSpringsPerParticle = serializedObject.FindProperty("MaxSpringsPerParticle");
        var serializedMaxParticlesNum = serializedObject.FindProperty("MaxParticlesNum");
        var serializedMaxStartingParticlesNum = serializedObject.FindProperty("MaxStartingParticlesNum");

        // Bind the PropertyFields to the serialized properties
        maxSpringsPerParticleField.BindProperty(serializedMaxSpringsPerParticle);
        maxParticlesNumField.BindProperty(serializedMaxParticlesNum);
        maxStartingParticlesNumField.BindProperty(serializedMaxStartingParticlesNum);

        // Make the TextField read-only (so it behaves like a display field)
        totalParticleSpringsField.isReadOnly = true;

        // Create a function to update the result TextField dynamically
        void UpdateResult()
        {
            // Update the serialized object to get the latest values
            serializedObject.Update();

            int numSpringsPerParticle = serializedMaxSpringsPerParticle.intValue;
            int numParticles = serializedMaxParticlesNum.intValue;
            int numParticleSprings = numSpringsPerParticle * numParticles;

            totalParticleSpringsField.value = numParticleSprings.ToString(); // Display the result

            // Ensure MaxStartingParticlesNum does not exceed MaxParticlesNum
            if (serializedMaxStartingParticlesNum.intValue > numParticles)
            {
                if (warningCooldownStopwatch.Elapsed.TotalSeconds > 1.0f)
                {
                    warningCooldownStopwatch.Restart();
                    UnityEngine.Debug.LogWarning("MaxStartingParticlesNum cannot exceed MaxParticlesNum");
                }
                serializedMaxStartingParticlesNum.intValue = numParticles;
                serializedObject.ApplyModifiedProperties(); // Apply the change to enforce the limit
            }
        }

        // Subscribe to value changes to update the TextField and enforce validation dynamically
        maxSpringsPerParticleField.RegisterCallback<ChangeEvent<int>>(evt => UpdateResult());
        maxParticlesNumField.RegisterCallback<ChangeEvent<int>>(evt => UpdateResult());
        maxStartingParticlesNumField.RegisterCallback<ChangeEvent<int>>(evt => UpdateResult());

        // Initialize the TextField value and validation on creation
        UpdateResult();

        // --- Control the visibility of the AdvancedSettings foldout based on the ShowAdvanced toggle ---

        // Find the toggle and foldout in the UXML by their names
        var showAdvancedToggle = root.Q<Toggle>("ShowAdvanced");
        var advancedSettingsFoldout = root.Q<Foldout>("AdvancedSettings");

        var serializedShowAdvanced = serializedObject.FindProperty("ShowAdvanced");
        if (serializedShowAdvanced != null)
        {
            showAdvancedToggle.BindProperty(serializedShowAdvanced);
        }

        // Toggle the visibility of the foldout based of the toggle
        advancedSettingsFoldout.style.display = showAdvancedToggle.value ? DisplayStyle.Flex : DisplayStyle.None;
        showAdvancedToggle.RegisterValueChangedCallback(evt =>
        {
            advancedSettingsFoldout.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        });

        return root;
    }
}
