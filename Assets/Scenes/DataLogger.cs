using UnityEngine;
using System.IO;

public class DataLogger : MonoBehaviour
{
    public static DataLogger Instance;

    private StreamWriter writer;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        string path = Application.persistentDataPath + "/experiment_log.csv";
        writer = new StreamWriter(path, false);
        writer.WriteLine("Type,Question,UserAnswer,Correct,ReactionTime");
    }

    public void LogPractice(string question, string answer, bool correct, float rt)
    {
        writer.WriteLine($"Practice,{question},{answer},{correct},{rt}");
        writer.Flush();
    }

    public void LogQuiz(string question, string answer, bool correct, float rt)
    {
        writer.WriteLine($"Quiz,{question},{answer},{correct},{rt}");
        writer.Flush();
    }

    void OnApplicationQuit()
    {
        writer?.Close();
    }
}