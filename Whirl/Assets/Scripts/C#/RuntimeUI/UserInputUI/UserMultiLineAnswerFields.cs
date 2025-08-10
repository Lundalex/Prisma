using UnityEngine;

public class UserMultiLineAnswerField : UserAnswerField
{
    protected override bool CompareWithAnswerKey(string answer, string answerKey)
    {
        // TODO: Use the smart assistant to evaluate the answer
        return base.CompareWithAnswerKey(answer, answerKey);
    }
}