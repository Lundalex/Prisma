using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways]
public class MultiImageColorController : MonoBehaviour
{
    // -----------------------------
    // SIMPLE (UNCONDITIONAL) GROUPS
    // -----------------------------
    [System.Serializable]
    public class ImageColorGroup
    {
        [Tooltip("Optional label to help you identify this group in the inspector.")]
        public string groupName = "Group";

        [Tooltip("The HDR color applied to every Image in this group.")]
        [ColorUsage(true, true)]
        public Color color = Color.white;

        [Tooltip("All UI Images that will receive the color above.")]
        public List<Image> images = new();
    }

    // -----------------------------------
    // CONDITIONAL (BOOLEAN-DRIVEN) GROUPS
    // -----------------------------------
    [System.Serializable]
    public class ConditionalImageColorGroup
    {
        [Tooltip("Optional label to help you identify this conditional group in the inspector.")]
        public string groupName = "Conditional Group";

        [Header("Colors")]
        [Tooltip("Color used when 'state' is TRUE.")]
        [ColorUsage(true, true)]
        public Color trueColor = Color.white;

        [Tooltip("Color used when 'state' is FALSE.")]
        [ColorUsage(true, true)]
        public Color falseColor = Color.gray;

        [Tooltip("Determines which color is applied (trueColor when true, falseColor when false).")]
        public bool state = true;

        [Header("Targets")]
        [Tooltip("All UI Images that will receive either the trueColor or falseColor based on 'state'.")]
        public List<Image> images = new();
    }

    [Header("Groups")]
    [Tooltip("Add one element per group. Each group holds a color and any number of Images.")]
    public List<ImageColorGroup> groups = new();

    [Tooltip("Add one element per conditional group. Each one has two colors selected by a boolean state.")]
    public List<ConditionalImageColorGroup> conditionalGroups = new();

    [Header("Theme Button Icons")]
    [Tooltip("One or more icon Images associated with this theme button.")]
    public Image[] icons;

    [Header("Behavior")]
    [Tooltip("Apply colors automatically when the scene starts (Play Mode).")]
    public bool applyOnStart = true;

    [Tooltip("Apply colors automatically whenever you edit values in the Inspector (Edit Mode).")]
    public bool autoApplyInEditor = true;

    private void OnValidate()
    {
        if (!isActiveAndEnabled) return;

        if (autoApplyInEditor)
            ApplyAll();
    }

    // -----------------
    // APPLY ENTRY POINT
    // -----------------
    /// <summary>
    /// Applies the configured colors to all Images in all simple and conditional groups.
    /// </summary>
    [ContextMenu("Apply Now")]
    public void ApplyAll()
    {
        if (groups != null)
        {
            for (int gi = 0; gi < groups.Count; gi++)
                ApplyGroup(groups[gi]);
        }

        if (conditionalGroups != null)
        {
            for (int ci = 0; ci < conditionalGroups.Count; ci++)
                ApplyConditionalGroup(conditionalGroups[ci]);
        }
    }

    // --------------------------
    // SIMPLE GROUPS - APPLY HELP
    // --------------------------
    /// <summary>
    /// Applies the configured color to all Images in a specific simple group by index.
    /// </summary>
    public void ApplyGroup(int groupIndex)
    {
        if (groups == null || groupIndex < 0 || groupIndex >= groups.Count) return;
        ApplyGroup(groups[groupIndex]);
    }

    private static void ApplyGroup(ImageColorGroup group)
    {
        if (group == null || group.images == null) return;

        for (int i = 0; i < group.images.Count; i++)
        {
            var img = group.images[i];
            if (!img) continue;

            img.color = group.color;
        }
    }

    // --------------------------------
    // CONDITIONAL GROUPS - APPLY/SETTER
    // --------------------------------
    /// <summary>
    /// Applies either trueColor or falseColor (based on 'state') to all Images in a specific conditional group by index.
    /// </summary>
    public void ApplyConditionalGroup(int conditionalGroupIndex)
    {
        if (conditionalGroups == null ||
            conditionalGroupIndex < 0 ||
            conditionalGroupIndex >= conditionalGroups.Count)
            return;

        ApplyConditionalGroup(conditionalGroups[conditionalGroupIndex]);
    }

    private static void ApplyConditionalGroup(ConditionalImageColorGroup group)
    {
        if (group == null || group.images == null) return;

        Color selected = group.state ? group.trueColor : group.falseColor;

        for (int i = 0; i < group.images.Count; i++)
        {
            var img = group.images[i];
            if (!img) continue;

            img.color = selected;
        }
    }

    /// <summary>
    /// Sets the boolean state for a given conditional group by index. Optionally applies immediately.
    /// </summary>
    public void SetConditionalGroupState(int conditionalGroupIndex, bool state, bool applyNow = true)
    {
        if (conditionalGroups == null ||
            conditionalGroupIndex < 0 ||
            conditionalGroupIndex >= conditionalGroups.Count)
            return;

        var group = conditionalGroups[conditionalGroupIndex];
        if (group == null) return;

        group.state = state;

        if (applyNow)
            ApplyConditionalGroup(conditionalGroupIndex);
    }

    /// <summary>
    /// Sets the boolean state for a given conditional group by name (case-insensitive). Optionally applies immediately.
    /// </summary>
    public void SetConditionalGroupState(string groupName, bool state, bool applyNow = true)
    {
        if (string.IsNullOrEmpty(groupName) || conditionalGroups == null) return;

        int index = TryGetConditionalGroupIndexByName(groupName);
        if (index < 0) return;

        SetConditionalGroupState(index, state, applyNow);
    }

    /// <summary>
    /// Sets the boolean state for all conditional groups. Optionally applies immediately.
    /// </summary>
    public void SetAllConditionalGroupStates(bool state, bool applyNow = true)
    {
        if (conditionalGroups == null || conditionalGroups.Count == 0) return;

        for (int i = 0; i < conditionalGroups.Count; i++)
        {
            var group = conditionalGroups[i];
            if (group == null) continue;

            group.state = state;

            if (applyNow)
                ApplyConditionalGroup(group);
        }
    }

    /// <summary>
    /// Helper: returns the index of a conditional group by name (case-insensitive). Returns -1 if not found.
    /// </summary>
    private int TryGetConditionalGroupIndexByName(string groupName)
    {
        if (conditionalGroups == null) return -1;

        for (int i = 0; i < conditionalGroups.Count; i++)
        {
            var g = conditionalGroups[i];
            if (g != null && string.Equals(g.groupName, groupName, System.StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }
}