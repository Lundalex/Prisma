using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using Unity.VisualScripting;

namespace Michsky.MUIP
{
    public class WindowManager : MonoBehaviour
    {
        public List<WindowItem> windows = new List<WindowItem>();

        public int currentWindowIndex = 0;
        private int currentButtonIndex = 0;
        private int newWindowIndex;
        public bool cullWindows = true;
        public bool initializeButtons = true;
        bool isInitialized = false;

        [System.Serializable] public class WindowChangeEvent : UnityEvent<int> { }
        public WindowChangeEvent onWindowChange;

        private GameObject currentWindow;
        private GameObject nextWindow;
        private GameObject currentButton;
        private GameObject nextButton;
        private Animator currentWindowAnimator;
        private Animator nextWindowAnimator;
        private Animator currentButtonAnimator;
        private Animator nextButtonAnimator;

        string windowFadeIn = "In";
        string windowFadeOut = "Out";
        string buttonFadeIn = "Hover to Pressed";
        string buttonFadeOut = "Pressed to Normal";
        float cachedStateLength;
        public bool altMode;

        [System.Serializable]
        public class WindowItem
        {
            public string windowName = "My Window";
            public GameObject windowObject;
            public GameObject buttonObject;
            public GameObject firstSelected;
        }

        void Awake()
        {
            if (windows.Count == 0)
                return;

            InitializeWindows();
        }

        void OnEnable()
        {
            if (isInitialized == true && nextWindowAnimator == null)
            {
                // Ensure active before playing
                if (currentWindow != null && !currentWindow.activeSelf) currentWindow.SetActive(true);
                if (currentButton != null && !currentButton.activeSelf) currentButton.SetActive(true);

                if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeIn);
                if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) { currentButtonAnimator.Play(buttonFadeIn); }
            }

            else if (isInitialized == true && nextWindowAnimator != null)
            {
                if (nextWindow != null && !nextWindow.activeSelf) nextWindow.SetActive(true);
                if (nextButton != null && !nextButton.activeSelf) nextButton.SetActive(true);

                if (nextWindowAnimator != null && nextWindowAnimator.gameObject.activeInHierarchy) nextWindowAnimator.Play(windowFadeIn);
                if (nextButtonAnimator != null && nextButtonAnimator.gameObject.activeInHierarchy) { nextButtonAnimator.Play(buttonFadeIn); }
            }
        }

        public void InitializeWindows()
        {
            if (windows.Count == 0)
                return;

            // Make sure the current window/button are active before any animations
            if (currentWindowIndex < 0 || currentWindowIndex >= windows.Count) currentWindowIndex = 0;

            if (windows[currentWindowIndex].windowObject != null && !windows[currentWindowIndex].windowObject.activeSelf)
                windows[currentWindowIndex].windowObject.SetActive(true);

            if (windows[currentWindowIndex].buttonObject != null && !windows[currentWindowIndex].buttonObject.activeSelf)
                windows[currentWindowIndex].buttonObject.SetActive(true);

            if (windows[currentWindowIndex].firstSelected != null)
                EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected;

            if (windows[currentWindowIndex].buttonObject != null)
            {
                currentButton = windows[currentWindowIndex].buttonObject;
                currentButtonAnimator = currentButton.GetComponent<Animator>();
                if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeIn);
            }

            currentWindow = windows[currentWindowIndex].windowObject;
            currentWindowAnimator = currentWindow != null ? currentWindow.GetComponent<Animator>() : null;
            if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeIn);
            onWindowChange.Invoke(currentWindowIndex);

            if (altMode == true) { cachedStateLength = 0.3f; }
            else { cachedStateLength = MUIPInternalTools.GetAnimatorClipLength(currentWindowAnimator, MUIPInternalTools.windowManagerStateName); }

            isInitialized = true;

            for (int i = 0; i < windows.Count; i++)
            {
                if (i != currentWindowIndex && cullWindows == true && windows[i].windowObject != null)
                    windows[i].windowObject.SetActive(false);

                if (windows[i].buttonObject != null && initializeButtons == true)
                {
                    string tempName = windows[i].windowName;
                    ButtonManager tempButton = windows[i].buttonObject.GetComponent<ButtonManager>();

                    if (tempButton != null)
                    {
                        tempButton.onClick.RemoveAllListeners();
                        tempButton.onClick.AddListener(() => OpenPanel(tempName));
                    }
                }
            }
        }

        public void OpenFirstTab()
        {
            if (currentWindowIndex != 0)
            {
                currentWindow = windows[currentWindowIndex].windowObject;
                currentWindowAnimator = currentWindow.GetComponent<Animator>();
                if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeOut);

                if (windows[currentWindowIndex].buttonObject != null)
                {
                    currentButton = windows[currentWindowIndex].buttonObject;
                    currentButtonAnimator = currentButton.GetComponent<Animator>();
                    if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeOut);
                }

                currentWindowIndex = 0;
                currentButtonIndex = 0;

                currentWindow = windows[currentWindowIndex].windowObject;
                if (currentWindow != null && !currentWindow.activeSelf) currentWindow.SetActive(true);
                currentWindowAnimator = currentWindow.GetComponent<Animator>();
                if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeIn);

                if (windows[currentWindowIndex].firstSelected != null) { EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected; }
                if (windows[currentButtonIndex].buttonObject != null)
                {
                    currentButton = windows[currentButtonIndex].buttonObject;
                    if (currentButton != null && !currentButton.activeSelf) currentButton.SetActive(true);
                    currentButtonAnimator = currentButton.GetComponent<Animator>();
                    if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeIn);
                }

                onWindowChange.Invoke(currentWindowIndex);
            }

            else if (currentWindowIndex == 0)
            {
                currentWindow = windows[currentWindowIndex].windowObject;
                if (currentWindow != null && !currentWindow.activeSelf) currentWindow.SetActive(true);
                currentWindowAnimator = currentWindow.GetComponent<Animator>();
                if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeIn);

                if (windows[currentWindowIndex].firstSelected != null) { EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected; }
                if (windows[currentButtonIndex].buttonObject != null)
                {
                    currentButton = windows[currentButtonIndex].buttonObject;
                    if (currentButton != null && !currentButton.activeSelf) currentButton.SetActive(true);
                    currentButtonAnimator = currentButton.GetComponent<Animator>();
                    if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeIn);
                }
            }
        }

        public void OpenWindow(string newWindow)
        {
            for (int i = 0; i < windows.Count; i++)
            {
                if (windows[i].windowName == newWindow)
                {
                    newWindowIndex = i;
                    break;
                }
            }

            if (newWindowIndex != currentWindowIndex)
            {
                // If we're inactive or not in a good state to animate, just record the target.
                if (!gameObject || !gameObject.activeInHierarchy || !isInitialized)
                {
                    currentWindowIndex = newWindowIndex;
                    return;
                }

                if (cullWindows == true && this)
                    StopCoroutine("DisablePreviousWindow");

                currentWindow = windows[currentWindowIndex].windowObject;

                if (windows[currentWindowIndex].buttonObject != null)
                    currentButton = windows[currentWindowIndex].buttonObject;

                currentWindowIndex = newWindowIndex;
                nextWindow = windows[currentWindowIndex].windowObject;
                if (nextWindow != null && !nextWindow.activeSelf) nextWindow.SetActive(true);

                currentWindowAnimator = currentWindow != null ? currentWindow.GetComponent<Animator>() : null;
                nextWindowAnimator = nextWindow != null ? nextWindow.GetComponent<Animator>() : null;

                if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeOut);
                if (nextWindowAnimator != null && nextWindowAnimator.gameObject.activeInHierarchy) nextWindowAnimator.Play(windowFadeIn);

                if (cullWindows == true && gameObject.activeInHierarchy)
                    StartCoroutine("DisablePreviousWindow");

                currentButtonIndex = newWindowIndex;

                if (windows[currentWindowIndex].firstSelected != null) { EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected; }
                if (windows[currentButtonIndex].buttonObject != null)
                {
                    nextButton = windows[currentWindowIndex].buttonObject;

                    currentButtonAnimator = currentButton != null ? currentButton.GetComponent<Animator>() : null;
                    nextButtonAnimator = nextButton != null ? nextButton.GetComponent<Animator>() : null;

                    if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeOut);
                    if (nextButtonAnimator != null && nextButtonAnimator.gameObject.activeInHierarchy) nextButtonAnimator.Play(buttonFadeIn);
                }

                onWindowChange.Invoke(currentWindowIndex);
            }
        }

        public void OpenPanel(string newPanel)
        {
            OpenWindow(newPanel);
        }

        public void OpenWindowByIndex(int windowIndex)
        {
            if (windowIndex < 0 || windowIndex >= windows.Count) return;
            OpenWindow(windows[windowIndex].windowName);
        }

        public void NextWindow()
        {
            if (currentWindowIndex <= windows.Count - 2)
            {
                if (cullWindows == true)
                    StopCoroutine("DisablePreviousWindow");

                currentWindow = windows[currentWindowIndex].windowObject;
                if (currentWindow != null && !currentWindow.activeSelf) currentWindow.SetActive(true);

                if (windows[currentButtonIndex].buttonObject != null)
                {
                    currentButton = windows[currentButtonIndex].buttonObject;
                    nextButton = windows[currentButtonIndex + 1].buttonObject;

                    currentButtonAnimator = currentButton != null ? currentButton.GetComponent<Animator>() : null;
                    if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeOut);
                }

                currentWindowAnimator = currentWindow != null ? currentWindow.GetComponent<Animator>() : null;
                if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeOut);

                currentWindowIndex += 1;
                currentButtonIndex += 1;

                nextWindow = windows[currentWindowIndex].windowObject;
                if (nextWindow != null && !nextWindow.activeSelf) nextWindow.SetActive(true);

                nextWindowAnimator = nextWindow != null ? nextWindow.GetComponent<Animator>() : null;
                if (nextWindowAnimator != null && nextWindowAnimator.gameObject.activeInHierarchy) nextWindowAnimator.Play(windowFadeIn);

                if (cullWindows == true) { StartCoroutine("DisablePreviousWindow"); }
                if (windows[currentWindowIndex].firstSelected != null) { EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected; }
                if (nextButton != null)
                {
                    nextButtonAnimator = nextButton.GetComponent<Animator>();
                    if (nextButtonAnimator != null && nextButtonAnimator.gameObject.activeInHierarchy) nextButtonAnimator.Play(buttonFadeIn);
                }

                onWindowChange.Invoke(currentWindowIndex);
            }
        }

        public void PrevWindow()
        {
            if (currentWindowIndex >= 1)
            {
                if (cullWindows == true)
                    StopCoroutine("DisablePreviousWindow");

                currentWindow = windows[currentWindowIndex].windowObject;
                if (currentWindow != null && !currentWindow.activeSelf) currentWindow.SetActive(true);

                if (windows[currentButtonIndex].buttonObject != null)
                {
                    currentButton = windows[currentButtonIndex].buttonObject;
                    nextButton = windows[currentButtonIndex - 1].buttonObject;

                    currentButtonAnimator = currentButton != null ? currentButton.GetComponent<Animator>() : null;
                    if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeOut);
                }

                currentWindowAnimator = currentWindow != null ? currentWindow.GetComponent<Animator>() : null;
                if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeOut);

                currentWindowIndex -= 1;
                currentButtonIndex -= 1;

                nextWindow = windows[currentWindowIndex].windowObject;
                if (nextWindow != null && !nextWindow.activeSelf) nextWindow.SetActive(true);

                nextWindowAnimator = nextWindow != null ? nextWindow.GetComponent<Animator>() : null;
                if (nextWindowAnimator != null && nextWindowAnimator.gameObject.activeInHierarchy) nextWindowAnimator.Play(windowFadeIn);

                if (cullWindows == true) { StartCoroutine("DisablePreviousWindow"); }
                if (windows[currentWindowIndex].firstSelected != null) { EventSystem.current.firstSelectedGameObject = windows[currentWindowIndex].firstSelected; }
                if (nextButton != null)
                {
                    nextButtonAnimator = nextButton.GetComponent<Animator>();
                    if (nextButtonAnimator != null && nextButtonAnimator.gameObject.activeInHierarchy) nextButtonAnimator.Play(buttonFadeIn);
                }

                onWindowChange.Invoke(currentWindowIndex);
            }
        }

        public void ShowCurrentWindow()
        {
            if (nextWindowAnimator == null) { if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeIn); }
            else { if (nextWindowAnimator.gameObject.activeInHierarchy) nextWindowAnimator.Play(windowFadeIn); }
        }

        public void HideCurrentWindow()
        {
            if (nextWindowAnimator == null) { if (currentWindowAnimator != null && currentWindowAnimator.gameObject.activeInHierarchy) currentWindowAnimator.Play(windowFadeOut); }
            else { if (nextWindowAnimator.gameObject.activeInHierarchy) nextWindowAnimator.Play(windowFadeOut); }
        }

        public void ShowCurrentButton()
        {
            if (nextButtonAnimator == null) { if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeIn); }
            else { if (nextButtonAnimator.gameObject.activeInHierarchy) nextButtonAnimator.Play(buttonFadeIn); }
        }

        public void HideCurrentButton()
        {
            if (nextButtonAnimator == null) { if (currentButtonAnimator != null && currentButtonAnimator.gameObject.activeInHierarchy) currentButtonAnimator.Play(buttonFadeOut); }
            else { if (nextButtonAnimator.gameObject.activeInHierarchy) nextButtonAnimator.Play(buttonFadeOut); }
        }

        public void AddNewItem()
        {
            WindowItem window = new WindowItem();

            if (windows.Count != 0 && windows[windows.Count - 1].windowObject != null)
            {
                int tempIndex = windows.Count - 1;

                GameObject tempWindow = windows[tempIndex].windowObject.transform.parent.GetChild(tempIndex).gameObject;
                GameObject newWindow = Instantiate(tempWindow, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;

                newWindow.transform.SetParent(windows[tempIndex].windowObject.transform.parent, false);
                newWindow.gameObject.name = "New Window " + tempIndex.ToString();

                window.windowName = "New Window " + tempIndex.ToString();
                window.windowObject = newWindow;

                if (windows[tempIndex].buttonObject != null)
                {
                    GameObject tempButton = windows[tempIndex].buttonObject.transform.parent.GetChild(tempIndex).gameObject;
                    GameObject newButton = Instantiate(tempButton, new Vector3(0, 0, 0), Quaternion.identity) as GameObject;

                    newButton.transform.SetParent(windows[tempIndex].buttonObject.transform.parent, false);
                    newButton.gameObject.name = "New Window " + tempIndex.ToString();

                    window.buttonObject = newButton;
                }
            }

            windows.Add(window);
        }

        IEnumerator DisablePreviousWindow()
        {
            yield return new WaitForSecondsRealtime(cachedStateLength);

            for (int i = 0; i < windows.Count; i++)
            {
                if (i == currentWindowIndex)
                    continue;

                if (windows[i].windowObject != null)
                    windows[i].windowObject.SetActive(false);
            }
        }
    }
}