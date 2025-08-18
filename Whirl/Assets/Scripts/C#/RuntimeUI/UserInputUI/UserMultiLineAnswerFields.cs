using UnityEngine;

public class UserMultiLineAnswerField : UserAnswerField
{
    [Header("TODO: ADD SOPHISTICAD, AI-POWERED COMPARISON CHECK TO UserMultiLineAnswerField"), SerializeField]
    float t = 0;
    protected override bool CompareWithAnswerKey(string answer, string answerKey)
    {
        if (t == 1) return true; // Temporary garbage code to avoid warnings in the console

        // TODO: Use the smart assistant to evaluate the answer
        return base.CompareWithAnswerKey(answer, answerKey);
    }
}