using UnityEngine;

public class LessonManager : MonoBehaviour
{
    public enum LessonState
    {
        Intro,
        Explain,
        Practice,
        Transfer,
        Quiz,
        End
    }

    public LessonState currentState;

    [Header("阶段时长")]
    public float introDuration = 8f;

    [Header("模块引用")]
    public PracticeModule practiceModule;
    public QuizModule quizModule;

    void Start()
    {
        ChangeState(LessonState.Intro);
    }

    void ChangeState(LessonState newState)
    {
        currentState = newState;
        Debug.Log("进入阶段: " + newState);

        switch (newState)
        {
            case LessonState.Intro:
                StartCoroutine(IntroRoutine());
                break;

            case LessonState.Explain:
                StartCoroutine(ExplainRoutine());
                break;

            case LessonState.Practice:
                practiceModule.StartPractice(OnPracticeFinished);
                break;

            case LessonState.Transfer:
                StartCoroutine(TransferRoutine());
                break;

            case LessonState.Quiz:
                quizModule.StartQuiz(OnQuizFinished);
                break;

            case LessonState.End:
                Debug.Log("课程结束");
                break;
        }
    }

    #region 各阶段逻辑

    System.Collections.IEnumerator IntroRoutine()
    {
        Debug.Log("导入阶段开始");
        yield return new WaitForSeconds(introDuration);
        ChangeState(LessonState.Explain);
    }

    System.Collections.IEnumerator ExplainRoutine()
    {
        Debug.Log("讲解阶段开始");

        // 示例内容
        yield return new WaitForSeconds(3f);
        Debug.Log("Kaffee - gro?");

        yield return new WaitForSeconds(3f);
        Debug.Log("Milch - klein");

        ChangeState(LessonState.Practice);
    }

    System.Collections.IEnumerator TransferRoutine()
    {
        Debug.Log("迁移阶段开始");

        yield return new WaitForSeconds(5f);

        ChangeState(LessonState.Quiz);
    }

    #endregion

    void OnPracticeFinished()
    {
        ChangeState(LessonState.Transfer);
    }

    void OnQuizFinished(int score)
    {
        Debug.Log("最终得分: " + score);
        ChangeState(LessonState.End);
    }
}