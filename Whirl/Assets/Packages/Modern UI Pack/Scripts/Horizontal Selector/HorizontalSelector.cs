using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using TMPro;

namespace Michsky.MUIP
{
    [RequireComponent(typeof(Animator))]
    public class HorizontalSelector : MonoBehaviour
    {
        // Resources
        public TextMeshProUGUI label;
        public TextMeshProUGUI labelHelper;
        public Image labelIcon;
        public Image labelIconHelper;
        public Transform indicatorParent;
        public GameObject indicatorObject;
        public Animator selectorAnimator;
        public HorizontalLayoutGroup contentLayout;
        public HorizontalLayoutGroup contentLayoutHelper;
        string newItemTitle;

        // Saving
        public bool enableIcon = true;
        public bool saveSelected = false;
        public string saveKey = "My Selector";

        // Settings
        public bool enableIndicators = true;
        public bool invokeAtStart;
        public bool invertAnimation;
        public bool loopSelection;
        [Range(0.25f, 2.5f)] public float iconScale = 1;
        [Range(1, 50)] public int contentSpacing = 15;
        public int defaultIndex = 0;
        [HideInInspector] public int index = 0;

        // Items
        public List<Item> items = new List<Item>();

        // Events
        [System.Serializable] public class SelectorEvent : UnityEvent<int> { }
        public SelectorEvent onValueChanged;
        [System.Serializable] public class ItemTextChangedEvent : UnityEvent<TMP_Text> { }
        public ItemTextChangedEvent onItemTextChanged;

        [System.Serializable]
        public class Item
        {
            public string itemTitle = "Item Title";
            public Sprite itemIcon;
            public UnityEvent onItemSelect = new UnityEvent();
        }

        void Awake()
        {
            if (selectorAnimator == null) selectorAnimator = gameObject.GetComponent<Animator>();
            if (label == null || labelHelper == null)
            {
                Debug.LogError("<b>[Horizontal Selector]</b> Missing label references.", this);
                return;
            }

            SetupSelector();
            UpdateContentLayout();

            if (invokeAtStart)
            {
                items[index].onItemSelect.Invoke();
                onValueChanged.Invoke(index);
            }
        }

        void OnEnable()
        {
            if (gameObject.activeInHierarchy) StartCoroutine("DisableAnimator");
        }

        public void SetupSelector()
        {
            if (items.Count == 0) return;

            if (saveSelected)
            {
                if (PlayerPrefs.HasKey("HorizontalSelector_" + saveKey)) defaultIndex = PlayerPrefs.GetInt("HorizontalSelector_" + saveKey);
                else PlayerPrefs.SetInt("HorizontalSelector_" + saveKey, defaultIndex);
            }

            label.text = items[defaultIndex].itemTitle;
            labelHelper.text = label.text;
            onItemTextChanged?.Invoke(label);

            if (labelIcon != null && enableIcon)
            {
                labelIcon.sprite = items[defaultIndex].itemIcon;
                labelIconHelper.sprite = labelIcon.sprite;
            }
            else
            {
                if (labelIcon != null) labelIcon.gameObject.SetActive(false);
                if (labelIconHelper != null) labelIconHelper.gameObject.SetActive(false);
            }

            index = defaultIndex;

            if (enableIndicators) UpdateIndicators();
            else if (indicatorParent != null) Destroy(indicatorParent.gameObject);
        }

        public void PreviousItem()
        {
            if (items.Count == 0) return;

            StopCoroutine("DisableAnimator");
            selectorAnimator.enabled = true;

            if (!loopSelection)
            {
                if (index != 0)
                {
                    labelHelper.text = label.text;
                    if (labelIcon != null && enableIcon) labelIconHelper.sprite = labelIcon.sprite;

                    index = Mathf.Max(0, index - 1);

                    label.text = items[index].itemTitle;
                    onItemTextChanged?.Invoke(label);
                    if (labelIcon != null && enableIcon) labelIcon.sprite = items[index].itemIcon;

                    items[index].onItemSelect.Invoke();
                    onValueChanged.Invoke(index);

                    selectorAnimator.Play(null);
                    selectorAnimator.StopPlayback();
                    selectorAnimator.Play(invertAnimation ? "Forward" : "Previous");
                }
            }
            else
            {
                labelHelper.text = label.text;
                if (labelIcon != null && enableIcon) labelIconHelper.sprite = labelIcon.sprite;

                index = (index == 0) ? items.Count - 1 : index - 1;

                label.text = items[index].itemTitle;
                onItemTextChanged?.Invoke(label);
                if (labelIcon != null && enableIcon) labelIcon.sprite = items[index].itemIcon;

                items[index].onItemSelect.Invoke();
                onValueChanged.Invoke(index);

                selectorAnimator.Play(null);
                selectorAnimator.StopPlayback();
                selectorAnimator.Play(invertAnimation ? "Forward" : "Previous");
            }

            if (saveSelected) PlayerPrefs.SetInt("HorizontalSelector_" + saveKey, index);
            if (gameObject.activeInHierarchy) StartCoroutine("DisableAnimator");
            if (enableIndicators)
            {
                for (int i = 0; i < items.Count; ++i)
                {
                    GameObject go = indicatorParent.GetChild(i).gameObject;
                    Transform onObj = go.transform.Find("On");
                    Transform offObj = go.transform.Find("Off");
                    bool active = (i == index);
                    if (onObj != null) onObj.gameObject.SetActive(active);
                    if (offObj != null) offObj.gameObject.SetActive(!active);
                }
            }
        }

        public void NextItem()
        {
            if (items.Count == 0) return;

            StopCoroutine("DisableAnimator");
            selectorAnimator.enabled = true;

            if (!loopSelection)
            {
                if (index != items.Count - 1)
                {
                    labelHelper.text = label.text;
                    if (labelIcon != null && enableIcon) labelIconHelper.sprite = labelIcon.sprite;

                    index = Mathf.Min(items.Count - 1, index + 1);

                    label.text = items[index].itemTitle;
                    onItemTextChanged?.Invoke(label);
                    if (labelIcon != null && enableIcon) labelIcon.sprite = items[index].itemIcon;

                    items[index].onItemSelect.Invoke();
                    onValueChanged.Invoke(index);

                    selectorAnimator.Play(null);
                    selectorAnimator.StopPlayback();
                    selectorAnimator.Play(invertAnimation ? "Previous" : "Forward");
                }
            }
            else
            {
                labelHelper.text = label.text;
                if (labelIcon != null && enableIcon) labelIconHelper.sprite = labelIcon.sprite;

                index = (index + 1 >= items.Count) ? 0 : index + 1;

                label.text = items[index].itemTitle;
                onItemTextChanged?.Invoke(label);
                if (labelIcon != null && enableIcon) labelIcon.sprite = items[index].itemIcon;

                items[index].onItemSelect.Invoke();
                onValueChanged.Invoke(index);

                selectorAnimator.Play(null);
                selectorAnimator.StopPlayback();
                selectorAnimator.Play(invertAnimation ? "Previous" : "Forward");
            }

            if (saveSelected) PlayerPrefs.SetInt("HorizontalSelector_" + saveKey, index);

            if (enableIndicators)
            {
                for (int i = 0; i < items.Count; ++i)
                {
                    GameObject go = indicatorParent.GetChild(i).gameObject;
                    Transform onObj = go.transform.Find("On");
                    Transform offObj = go.transform.Find("Off");
                    bool active = (i == index);
                    if (onObj != null) onObj.gameObject.SetActive(active);
                    if (offObj != null) offObj.gameObject.SetActive(!active);
                }
            }

            if (gameObject.activeInHierarchy) StartCoroutine("DisableAnimator");
        }

        // Obsolete
        public void PreviousClick() { PreviousItem(); }
        public void ForwardClick() { NextItem(); }

        public void CreateNewItem(string title)
        {
            Item item = new Item { itemTitle = title };
            newItemTitle = title;
            items.Add(item);
        }

        public void CreateNewItem(string title, Sprite icon)
        {
            Item item = new Item { itemTitle = title, itemIcon = icon };
            newItemTitle = title;
            items.Add(item);
        }

        public void RemoveItem(string itemTitle)
        {
            var item = items.Find(x => x.itemTitle == itemTitle);
            items.Remove(item);
            SetupSelector();
        }

        public void UpdateUI()
        {
            selectorAnimator.enabled = true;

            label.text = items[index].itemTitle;
            onItemTextChanged?.Invoke(label);

            if (labelIcon != null && enableIcon) labelIcon.sprite = items[index].itemIcon;
            if (gameObject.activeInHierarchy) StartCoroutine("DisableAnimator");

            UpdateContentLayout();
            UpdateIndicators();
        }

        public void UpdateIndicators()
        {
            if (!enableIndicators || indicatorParent == null || indicatorObject == null) return;

            int targetCount = items.Count;
            int currentCount = indicatorParent.childCount;

            for (int i = currentCount - 1; i >= targetCount; i--)
                Destroy(indicatorParent.GetChild(i).gameObject);

            for (int i = currentCount; i < targetCount; i++)
            {
                var go = Instantiate(indicatorObject);
                go.transform.SetParent(indicatorParent, false);
            }

            for (int i = 0; i < targetCount; i++)
            {
                var child = indicatorParent.GetChild(i).gameObject;
                child.name = items[i].itemTitle;

                var onObj = child.transform.Find("On");
                var offObj = child.transform.Find("Off");
                bool active = (i == index);

                if (onObj != null) onObj.gameObject.SetActive(active);
                if (offObj != null) offObj.gameObject.SetActive(!active);
            }
        }

        public void UpdateContentLayout()
        {
            if (contentLayout != null) contentLayout.spacing = contentSpacing;
            if (contentLayoutHelper != null) contentLayoutHelper.spacing = contentSpacing;
            if (labelIcon != null)
            {
                labelIcon.transform.localScale = new Vector3(iconScale, iconScale, iconScale);
                labelIconHelper.transform.localScale = new Vector3(iconScale, iconScale, iconScale);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(label.transform.GetComponent<RectTransform>());
            LayoutRebuilder.ForceRebuildLayoutImmediate(label.transform.parent.GetComponent<RectTransform>());
        }

        IEnumerator DisableAnimator()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            selectorAnimator.enabled = false;
        }
    }
}