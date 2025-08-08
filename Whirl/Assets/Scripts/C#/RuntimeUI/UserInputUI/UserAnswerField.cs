using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserAnswerField : MonoBehaviour
{
    [Header("Answer Settings")]
    [SerializeField] private string answerKey;
    [SerializeField] private bool caseSensitive = false;

    [SerializeField] private Color successColor = Color.green;
    [SerializeField] private Color failColor = Color.red;

    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject outlineObject;
    [SerializeField] private TMP_InputField inputField;

    public void ProcessAnswer()
    {
        string answer = inputField.text;
        bool answerIsCorrect = CompareWithAnswerKey(answer, answerKey);

        if (answerIsCorrect)
        {
            outlineImage.color = successColor;
            outlineObject.SetActive(true);
        }
        else
        {
            outlineImage.color = failColor;
            outlineObject.SetActive(true);
        }
    }

    private bool CompareWithAnswerKey(string answer, string answerKey)
    {
        if (caseSensitive) return answer == answerKey;
        else return string.Equals(answer, answerKey, System.StringComparison.OrdinalIgnoreCase);
    }
}