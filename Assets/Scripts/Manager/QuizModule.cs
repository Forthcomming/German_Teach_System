using UnityEngine;
using System;

public class QuizModule : MonoBehaviour
{
    [Header("题目与答案")]
    [Tooltip("题干文本数组，每个元素对应一道题目。")]
    public string[] questions;

    [Tooltip("正确答案编号字符串数组，例如 \"0\"、\"1\"、\"2\"，表示第几个选项是正确答案。长度需与 questions 一致。")]
    public string[] answers;

    private int currentIndex;
    private int score;
    private float startTime;
    private Action<int> onFinished;

    /// <summary>
    /// ???????????
    /// </summary>
    public int CurrentIndex => currentIndex;

    /// <summary>
    /// ?????????????
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
    /// ?????/????????????????
    /// UI ????????????????
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
            Debug.LogError("[QuizModule] ??????????????????");
            onFinished?.Invoke(0);
            return;
        }

        ShowQuestion();
    }

    private bool ValidateData()
    {
        if (questions == null || answers == null)
        {
            Debug.LogError("[QuizModule] questions ? answers ? null?");
            return false;
        }

        if (questions.Length == 0)
        {
            Debug.LogWarning("[QuizModule] questions ??????????");
            return false;
        }

        if (answers.Length != questions.Length)
        {
            Debug.LogError($"[QuizModule] questions.Length ({questions.Length}) ? answers.Length ({answers.Length}) ????");
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

        Debug.Log("???: " + questions[currentIndex]);
        startTime = Time.time;
        QuestionShown?.Invoke(currentIndex);
    }

    /// <summary>
    /// UI ?????????????
    /// ???????????????????????? \"0\"?\"1\"?\"2\"?
    /// </summary>
    /// <param name="userAnswer">???????????????</param>
    public void OnUserAnswer(string userAnswer)
    {
        if (!ValidateData())
        {
            Debug.LogWarning("[QuizModule] ???????????");
            return;
        }

        if (currentIndex < 0 || currentIndex >= questions.Length)
        {
            Debug.LogWarning($"[QuizModule] OnUserAnswer ???? currentIndex({currentIndex}) ??????");
            return;
        }

        float reactionTime = Time.time - startTime;

        bool correct = string.Equals(userAnswer, answers[currentIndex], StringComparison.Ordinal);
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
}