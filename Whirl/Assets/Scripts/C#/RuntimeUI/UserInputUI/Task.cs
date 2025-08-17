using TMPro;
using UnityEngine;

[ExecuteInEditMode]
public class Task : MonoBehaviour
{
    [SerializeField] private string headerText;

    [TextArea(6, 40)]
    [SerializeField] private string bodyText;
    [SerializeField] private string answerKey;

    [SerializeField] private bool prevActive;
    [SerializeField] private bool nextActive;

    [SerializeField] private GameObject prevButton;
    [SerializeField] private GameObject nextButton;

    [SerializeField] private TMP_Text headerTextObj;
    [SerializeField] private TMPBulletListFormatter bodyTextFormatter;
    [SerializeField] private UserMultiLineAnswerField answerField;

    void OnEnable()
    {
        // Set header text
        if (headerTextObj != null) headerTextObj.text = headerText;
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
        {
            // Enable/disable buttons
            if (prevButton != null && prevButton.activeSelf != prevActive) prevButton.SetActive(prevActive);
            if (nextButton != null && nextButton.activeSelf != nextActive) nextButton.SetActive(nextActive);

            // Set header text
            if (headerTextObj != null) headerTextObj.text = headerText;

            // Set body text
            if (bodyTextFormatter != null) bodyTextFormatter.sourceText = bodyText;

            // Set answerKey
            if (answerField != null) answerField.answerKey = answerKey;
        }
    }
#endif
}