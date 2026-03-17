using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UI
{
    /// <summary>
    /// 控制测验 UI：负责显示题干、图片选项，并把玩家点击的选项传给 QuizModule。
    /// 使用方式：
    /// 1. 将本脚本挂到一个管理对象（例如 Canvas 下的 QuizPanel）。
    /// 2. 在 Inspector 里绑定 QuizModule、题目文本、4 个选项图片，以及每题对应的 4 张 Sprite。
    /// 3. 在开始测验时调用 BeginQuiz（可由按钮或其它脚本触发）。
    /// 4. 在每个选项按钮上配置 QuizAnswerButton，并将其连接到本脚本的 OnOptionSelected。
    /// </summary>
    [DisallowMultipleComponent]
    public class QuizUIController : MonoBehaviour
    {
        [Header("核心逻辑引用")]
        [Tooltip("测验逻辑模块 QuizModule。")]
        [SerializeField] private QuizModule quizModule;

        [Header("题干 UI")]
        [Tooltip("用于显示题干文本的 TextMeshProUGUI 组件。")]
        [SerializeField] private TextMeshProUGUI questionText;

        [Header("选项图片 UI")]
        [Tooltip("用于显示选项图片的 Image 数组，例如长度为 4 对应四个选项。")]
        [SerializeField] private Image[] optionImages;

        [Serializable]
        public class QuestionSprites
        {
            [Tooltip("该题目对应的选项图片数组，顺序需与“选项编号”一致。")]
            public Sprite[] optionSprites;
        }

        [Header("每题对应的选项图片")]
        [Tooltip("长度需与 QuizModule.questions 一致，每个元素包含该题的所有选项图片。")]
        [SerializeField] private QuestionSprites[] questionSprites;

        private void OnEnable()
        {
            if (quizModule != null)
            {
                quizModule.QuestionShown += OnQuestionShown;
            }
        }

        private void OnDisable()
        {
            if (quizModule != null)
            {
                quizModule.QuestionShown -= OnQuestionShown;
            }
        }

        /// <summary>
        /// 由外部（如按钮）调用，开始整个测验流程。
        /// </summary>
        public void BeginQuiz()
        {
            if (quizModule == null)
            {
                Debug.LogError("[QuizUIController] quizModule 未绑定，无法开始测验。", this);
                return;
            }

            quizModule.StartQuiz(OnQuizFinished);
        }

        /// <summary>
        /// 选项按钮点击时调用，将选中的选项索引传递给 QuizModule。
        /// 例如：第 0 个选项按钮调用 OnOptionSelected(0)。
        /// </summary>
        public void OnOptionSelected(int optionIndex)
        {
            if (quizModule == null)
            {
                Debug.LogWarning("[QuizUIController] quizModule 为 null，无法处理选项点击。", this);
                return;
            }

            // 将选项索引转为字符串，与 QuizModule.answers 中的编号字符串对应，例如 "0"、"1"。
            quizModule.OnUserAnswer(optionIndex.ToString());
        }

        private void OnQuestionShown(int questionIndex)
        {
            UpdateQuestionText(questionIndex);
            UpdateOptionImages(questionIndex);
        }

        private void UpdateQuestionText(int questionIndex)
        {
            if (questionText == null)
            {
                return;
            }

            string question = quizModule != null ? quizModule.CurrentQuestion : null;
            questionText.text = string.IsNullOrEmpty(question) ? string.Empty : question;
        }

        private void UpdateOptionImages(int questionIndex)
        {
            if (optionImages == null || optionImages.Length == 0)
            {
                return;
            }

            if (questionSprites == null || questionIndex < 0 || questionIndex >= questionSprites.Length)
            {
                Debug.LogWarning($"[QuizUIController] questionSprites 未正确配置或索引越界（index={questionIndex}）。", this);
                return;
            }

            QuestionSprites spritesForQuestion = questionSprites[questionIndex];
            if (spritesForQuestion == null || spritesForQuestion.optionSprites == null)
            {
                Debug.LogWarning($"[QuizUIController] 第 {questionIndex} 题的 optionSprites 未配置。", this);
                return;
            }

            int count = Mathf.Min(optionImages.Length, spritesForQuestion.optionSprites.Length);
            for (int i = 0; i < count; i++)
            {
                if (optionImages[i] == null)
                {
                    continue;
                }

                optionImages[i].sprite = spritesForQuestion.optionSprites[i];
                optionImages[i].enabled = optionImages[i].sprite != null;
            }

            // 多余的 Image 隐藏（如果有的话）
            for (int i = count; i < optionImages.Length; i++)
            {
                if (optionImages[i] != null)
                {
                    optionImages[i].enabled = false;
                }
            }
        }

        private void OnQuizFinished(int finalScore)
        {
            Debug.Log("[QuizUIController] 测验结束，最终得分：" + finalScore, this);
            // 如有需要，可在这里切换到结果面板、显示分数等。
        }
    }
}

