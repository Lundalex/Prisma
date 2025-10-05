// ─────────────────────────────────────────────────────────────────────────────
// MultiThemeButtonController.cs
// - Uses currentSelectedIndex as the initial selection.
// - Updates in Edit Mode by detecting changes each frame.
// - Exposes GetSelectedIndex() for UserSettingsRelayHelper UI->Field sync.
// - Icons are provided by each MultiImageColorController (array) and updated here.
// ─────────────────────────────────────────────────────────────────────────────
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

[ExecuteInEditMode]
public class MultiThemeButtonController : MonoBehaviour
{
    [Header("Theme Buttons")]
    [Tooltip("List of theme button controllers; each should live on a GameObject that also has a Button component.")]
    public List<MultiImageColorController> themeButtons = new();

    [Header("Icons")]
    [Tooltip("Sprite used for the selected theme button's icons.")]
    public Sprite selectedIcon;

    [Tooltip("Sprite used for non-selected theme button icons.")]
    public Sprite normalIcon;

    [Header("Behavior")]
    [Tooltip("If true, clicking a theme button will select it.")]
    public bool hookUpClickHandlers = true;

    [SerializeField, Tooltip("Current selected index at runtime or in the Inspector. -1 if none.")]
    private int currentSelectedIndex = -1;

    [Header("Events")]
    public UnityEvent onValueChanged;

    // Private
    private int _lastSelectedIndex = int.MinValue;  // change detector (edit mode safe)
    private bool setupFinished = false;

    private void Start()
    {
        if (hookUpClickHandlers)
            WireClickHandlers();

        // Respect currentSelectedIndex as the initial selection
        RefreshAll();

        // Initialize change tracking
        _lastSelectedIndex = currentSelectedIndex;
    }

    private void Update()
    {
        if (!setupFinished)
        {
            // Ensure visuals are in sync when first enabled/compiled in Edit Mode
            RefreshAll();
            setupFinished = true;
        }

        // Detect inspector or runtime changes to currentSelectedIndex without using OnValidate
        if (_lastSelectedIndex != currentSelectedIndex)
        {
            // Update visuals first for all buttons
            RefreshAll();

            _lastSelectedIndex = currentSelectedIndex;
            onValueChanged?.Invoke();
        }
    }

    private void WireClickHandlers()
    {
        if (themeButtons == null) return;

        for (int i = 0; i < themeButtons.Count; i++)
        {
            var theme = themeButtons[i];
            if (theme == null) continue;

            var btn = theme.GetComponent<Button>();
            if (btn == null) continue;

            int captured = i;
            btn.onClick.RemoveListener(() => SetSelectedIndex(captured));
            btn.onClick.AddListener(() => SetSelectedIndex(captured));
        }
    }

    /// <summary>
    /// Public API: selects the theme button at the given index and updates all visuals.
    /// </summary>
    public void SetSelectedIndex(int index)
    {
        if (themeButtons == null || themeButtons.Count == 0) return;
        if (index < -1 || index >= themeButtons.Count) return;

        currentSelectedIndex = index;

        // Force the change detector to fire on next Update (covers Edit Mode changes)
        _lastSelectedIndex = index - 1;

        // Update visuals immediately so the user sees the change without waiting a frame
        RefreshAll();
    }

    /// <summary>
    /// Public API: returns the current selected index (-1 if none).
    /// </summary>
    public int GetSelectedIndex()
    {
        return currentSelectedIndex;
    }

    private void RefreshAll()
    {
        if (themeButtons == null) return;

        for (int i = 0; i < themeButtons.Count; i++)
        {
            bool isSelected = (i == currentSelectedIndex);
            var theme = themeButtons[i];
            if (theme == null) continue;

            // 1) Update icons (icons live on MultiImageColorController, not on the Button)
            if (theme.icons != null)
            {
                for (int k = 0; k < theme.icons.Length; k++)
                {
                    var img = theme.icons[k];
                    if (img == null) continue;

                    if (isSelected && selectedIcon != null)
                        img.sprite = selectedIcon;
                    else if (!isSelected && normalIcon != null)
                        img.sprite = normalIcon;
                }
            }

            // 2) Update theme colors via conditional groups
            theme.SetAllConditionalGroupStates(isSelected, applyNow: true);
        }
    }
}