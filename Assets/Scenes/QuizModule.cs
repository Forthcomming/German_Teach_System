using UnityEngine;
using System;

public class QuizModule : MonoBehaviour
{
    public string[] questions;
    public string[] answers;

    private int currentIndex;
    private int score;
    private float startTime;
    private Action<int> onFinished;

    public void StartQuiz(Action<int> finishedCallback)
    {
        onFinished = finishedCallback;
        currentIndex = 0;
        score = 0;
        ShowQuestion();
    }

    void ShowQuestion()
    {
        if (currentIndex >= questions.Length)
        {
            onFinished?.Invoke(score);
            return;
        }

        Debug.Log("≤‚—ÈÃ‚: " + questions[currentIndex]);
        startTime = Time.time;
    }

    public void OnUserAnswer(string userAnswer)
    {
        float reactionTime = Time.time - startTime;

        bool correct = userAnswer == answers[currentIndex];
        if (correct) score++;

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