using UnityEngine;
using System;

public class QuizModule : MonoBehaviour
{
    [Header("??????")]
    [Tooltip("??????????????????????????")]
    public string[] questions;

    [Tooltip("??????????????????? questions ????\n- ??????????????? \"0\"??\"1\"??\"2\"??\n- ??????????????????????????? \"Kaffee\"???????????????????? Trim??")]
    public string[] answers;

    private int currentIndex;
    private int score;
    private float startTime;
    private Action<int> onFinished;

    /// <summary>
    /// ?????????????? 0 ???????
    /// </summary>
    public int CurrentIndex => currentIndex;

    /// <summary>
    /// ???????????
    /// </summary>
    public string CurrentQuestion
    {
        get
        {
            if (questions == null || currentIndex < 0 || currentIndex >= questions.Length)
            {
                return null;
            }

            return questions[currentIndex];
        }
    }

    /// <summary>
    /// ??????????????????????????????? UI ????
    /// </summary>
    public event Action<int> QuestionShown;

    public void StartQuiz(Action<int> finishedCallback)
    {
        onFinished = finishedCallback;
        currentIndex = 0;
        score = 0;
        startTime = 0f;

        if (!ValidateData())
        {
            Debug.LogError("[QuizModule] ???????????????????????????");
            onFinished?.Invoke(0);
            return;
        }

        ShowQuestion();
    }

    private bool ValidateData()
    {
        if (questions == null || answers == null)
        {
            Debug.LogError("[QuizModule] questions ?? answers ? null??");
            return false;
        }

        if (questions.Length == 0)
        {
            Debug.LogWarning("[QuizModule] questions ????");
            return false;
        }

        if (answers.Length != questions.Length)
        {
            Debug.LogError($"[QuizModule] questions.Length ({questions.Length}) ?? answers.Length ({answers.Length}) ??????");
            return false;
        }

        return true;
    }

    private void ShowQuestion()
    {
        if (currentIndex >= questions.Length)
        {
            onFinished?.Invoke(score);
            return;
        }

        Debug.Log("??????: " + questions[currentIndex]);
        startTime = Time.time;
        QuestionShown?.Invoke(currentIndex);
    }

    /// <summary>
    /// ???????????????????????????????????????
    /// </summary>
    public void OnUserAnswer(string userAnswer)
    {
        if (!ValidateData())
        {
            Debug.LogWarning("[QuizModule] ??????????????????");
            return;
        }

        if (currentIndex < 0 || currentIndex >= questions.Length)
        {
            Debug.LogWarning($"[QuizModule] OnUserAnswer ? currentIndex({currentIndex}) ????");
            return;
        }

        float reactionTime = Time.time - startTime;

        bool correct = CompareAnswer(userAnswer, answers[currentIndex]);
        if (correct)
        {
            score++;
        }

        DataLogger.Instance.LogQuiz(
            questions[currentIndex],
            userAnswer,
            correct,
            reactionTime
        );

        currentIndex++;
        ShowQuestion();
    }

    /// <summary>
    /// ??????????????????????????Trim ?? Ordinal????
    /// ????????/??????Trim ??????????????
    /// </summary>
    public static bool CompareAnswer(string userAnswer, string expectedAnswer)
    {
        string user = userAnswer != null ? userAnswer.Trim() : string.Empty;
        string expected = expectedAnswer != null ? expectedAnswer.Trim() : string.Empty;

        if (IsNumericChoiceAnswer(expected))
        {
            return string.Equals(user, expected, StringComparison.Ordinal);
        }

        return string.Equals(user, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNumericChoiceAnswer(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return false;
        }

        for (int i = 0; i < s.Length; i++)
        {
            if (!char.IsDigit(s[i]))
            {
                return false;
            }
        }

        return true;
    }
}
