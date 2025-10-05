using UnityEngine;

public class FeedbackButton : MonoBehaviour
{
    [Header("(Set surveyUrl in code instead of in the inspector)")]
    private const string surveyUrl = "https://forms.office.com/e/AexHqGV0rF";

    public void OpenFeedbackSurvey() => LinkHandler.OpenURL(surveyUrl);
}