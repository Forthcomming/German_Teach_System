using UnityEngine;
using System;

public class PracticeModule : MonoBehaviour
{
    public string[] questions;
    public string[] answers;

    private int currentIndex;
    private float startTime;
    private Action onFinished;

    public void StartPractice(Action finishedCallback)
    {
        onFinished = finishedCallback;
        currentIndex = 0;
        ShowQuestion();
    }

    void ShowQuestion()
    {
        if (currentIndex >= questions.Length)
        {
            onFinished?.Invoke();
            return;
        }

        Debug.Log("练习题: " + questions[currentIndex]);
        startTime = Time.time;
    }

    // 由按钮 / XR 输入调用
    public void OnUserAnswer(string userAnswer)
    {
        float reactionTime = Time.time - startTime;

        bool correct = userAnswer == answers[currentIndex];

        Debug.Log($"答案: {userAnswer} 是否正确: {correct} RT: {reactionTime}");

        DataLogger.Instance.LogPractice(
            questions[currentIndex],
            userAnswer,
            correct,
            reactionTime
        );

        currentIndex++;
        ShowQuestion();
    }
}