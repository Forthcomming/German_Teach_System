using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System.Globalization;
using WebGLSupport;
using System;
using UnityEngine.SceneManagement; 
using System.Text.RegularExpressions;


public class ChatSample : MonoBehaviour
{


    /// <summary>
    /// 聊天配置
    /// </summary>
    [SerializeField] private ChatSetting m_ChatSettings;
    #region UI定义
    /// <summary>
    /// 聊天UI窗
    /// </summary>
    [SerializeField] private GameObject m_ChatPanel;
    /// <summary>
    /// 输入的信息
    /// </summary>
    [SerializeField] public InputField m_InputWord;
    /// <summary>
    /// 返回的信息
    /// </summary>
    [SerializeField] private Text m_TextBack;
    /// <summary>
    /// 播放声音
    /// </summary>
    [SerializeField] private AudioSource m_AudioSource;
    /// <summary>
    /// 发送信息按钮
    /// </summary>
    [SerializeField] private Button m_CommitMsgBtn;
    /// <summary>
    /// 保存设置按钮
    /// </summary>
    [SerializeField] private Button m_SaveSettingBtn;
    /// 标题
    /// </summary>
    [SerializeField] private TextMeshProUGUI titleToSync;
    /// 输入框文字
    /// </summary>
    [SerializeField] private TMP_InputField titleToType;
    /// 你的 Dropdown 组件
    /// </summary>
    [SerializeField] private TMP_Dropdown dropdown;
    /// <summary>
    ///离开菜单
    /// </summary>
    [SerializeField] private CanvasGroup exitPanelCanvasGroup;  // 指向 exitPanel 的 CanvasGroup 组件的引用
    /// <summary>
    ///火山tts组件
    /// </summary>
    //[SerializeField] private VoiceCloneTTS voiceCloneTTS;
    //[SerializeField] private VolcengineTextToSpeech volcengineTextToSpeech;
    [SerializeField] private VolcengineVoiceCloneTTS volcengineVoiceCloneTTS;
    #endregion

    #region 参数定义
    /// <summary>
    /// 动画控制器
    /// </summary>
    [SerializeField] private Animator[] m_Animators;
    /// <summary>
    /// 动画控制器
    /// </summary>
    [SerializeField] private Animator m_Animator;
    /// <summary>
    /// 语音模式，设置为false,则不通过语音合成
    /// </summary>
    [Header("设置是否通过语音合成播放文本")]
    [SerializeField] private bool m_IsVoiceMode = true;
    [Header("勾选则不发送LLM，直接合成输入文字")]
    [SerializeField] private bool m_CreateVoiceMode = false;
    /// <summary>
    /// 说话动画状态的编号
    /// </summary>
    private int[] speakingStates = new int[] { 1, 2, 3,4,5,6 };
    private int lastPlayedState = -1; // 记录上一次播放的动画状态
    /// <summary>
    /// 协程引用，用于停止协程
    /// </summary>
    private Coroutine speakingCoroutine;
    private bool m_IsSpeaking;

    /// <summary>
    /// AI回复结束之后，回调
    /// </summary>
    public Action OnAISpeakDone;
    private Queue<string> m_VoiceQueue = new Queue<string>(); // 语音播放队列
    private bool isPlayingVoice = false; // 是否正在播放语音
    private string m_UnfinishedBuffer = string.Empty; // 用于缓存未完成的语音内容
    private string accumulatedResponse = string.Empty; // 用于累积AI的回复内容
   
    /* 【LYF新增】：打断AI上一段语音 */
    private bool isNewInput; // 记录是否有新回复
    private Coroutine voicePlayWaitingCoroutine;
    public Button stopPlayAudioButton;
    public int isLoadingAnswer = 0; // 是否处于思考状态,0表示应用刚启动，1表示正在思考，2表示思考完毕

    #endregion




    private void Awake()
    {
        titleToType.text = titleToSync.text;
        m_CommitMsgBtn.onClick.AddListener(delegate { SendData(); });
        m_SaveSettingBtn.onClick.AddListener(delegate { SaveSetting(); });
        RegistButtonEvent();
        InputSettingWhenWebgl();
    }
    private void Start()
    {
        // 为Dropdown的valueChange事件添加监听器
        dropdown.onValueChanged.AddListener(delegate {
            ChangeAnimator();
        });
        m_InputWord.onEndEdit.AddListener(HandleEndEdit);
        // 确保 exitPanel 初始不可见
        exitPanelCanvasGroup.alpha = 0;
        exitPanelCanvasGroup.blocksRaycasts = false;  // 初始时不阻挡点击

    }
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ShowExitPanel(); // 使用渐显方法显示面板
        }
        if (m_AudioSource.isPlaying == false && m_IsSpeaking)
        {
            m_IsSpeaking = false;
            SetAnimator("state", 0); // 切换到静止状态
        }
        /* 【LYF新增】检测录音自动上传 */
        if(m_VoiceInputs.isAutoStopRecording){
            m_VoiceInputs.isAutoStopRecording = false;
            ToggleRecord();
        }
        // 【LYF新增】播放语音时，显示暂停播放按钮；否则隐藏按钮
        stopPlayAudioButton.gameObject.SetActive(isPlayingVoice);

    }
    private void HandleEndEdit(string text)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendData();
        }
    }
    public void OnConfirmExit()
    {
        Application.Quit(); // Quit the game
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false; // Stop play mode in the editor
#endif
    }

    public void OnCancelExit()
    {
        HideExitPanel(); // 使用渐隐方法隐藏面板
    }
    IEnumerator FadeElement(CanvasGroup canvasGroup, float targetAlpha, float duration)
    {
        float startAlpha = canvasGroup.alpha;
        float time = 0f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, time / duration);
            yield return null;
        }

        canvasGroup.alpha = targetAlpha;

        // 在渐变结束后更新 CanvasGroup 的 blocksRaycasts 属性，以允许或阻止点击
        canvasGroup.blocksRaycasts = targetAlpha > 0;
    }

    public void ShowExitPanel()
    {
        StartCoroutine(FadeElement(exitPanelCanvasGroup, 1.0f, 0.5f));  // 渐显
        Time.timeScale = 0; // 暂停游戏
    }

    public void HideExitPanel()
    {
        StartCoroutine(FadeElement(exitPanelCanvasGroup, 0.0f, 0.5f));  // 渐隐
        Time.timeScale = 1; // 恢复游戏时间
    }


    /// <summary>
    /// 更换animator
    /// </summary>
    void ChangeAnimator()
{
    // 确保索引在有效范围内，避免数组越界错误
    if (dropdown.value >= 0 && dropdown.value < m_Animators.Length)
    {
        m_Animator = m_Animators[dropdown.value];
    }
}

    #region 消息发送

    /// <summary>
    /// webgl时处理，支持中文输入
    /// </summary>
    private void InputSettingWhenWebgl()
    {
#if UNITY_WEBGL
        m_InputWord.gameObject.AddComponent<WebGLSupport.WebGLInput>();
#endif
    }

    /// <summary>
    /// 保存标题
    /// </summary>
     public void SaveSetting()
    {
        string temp = titleToType.text;
        titleToSync.text = temp;
    }


    /// <summary>
    /// 发送信息
    /// </summary>
    public void SendData()
    {
        if (m_InputWord.text.Equals(""))
            return;

        if (m_CreateVoiceMode)//合成输入为语音
        {
            CallBack(m_InputWord.text);
            m_InputWord.text = "";
            return;
        }

        StartCoroutine(GetSendChatInfo(m_InputWord.text));
        //添加记录聊天
        //m_ChatHistory.Add(m_InputWord.text);
        //提示词
        string _msg = m_InputWord.text;

        //发送数据
        m_ChatSettings.m_ChatModel.PostMsg(_msg, CallBack);

        m_InputWord.text = "";
        m_TextBack.text = "正在思考中...";

        //切换思考动作
        //SetAnimator("state", 1);
    }
    /// <summary>
    /// 带文字发送
    /// </summary>
    /// <param name="_postWord"></param>
    public void SendData(string _postWord)
    {
        if (_postWord.Equals(""))
            return;

        if (m_CreateVoiceMode)//合成输入为语音
        {
            CallBack(_postWord);
            m_InputWord.text = "";
            return;
        }

        StartCoroutine(GetSendChatInfo(_postWord));
        //添加记录聊天
        //m_ChatHistory.Add(_postWord);
        //提示词
        string _msg = _postWord;

        //发送数据
        m_ChatSettings.m_ChatModel.PostMsg(_msg, CallBack);

        m_InputWord.text = "";
        m_TextBack.text = "正在思考中...";

        //切换思考动作
        //SetAnimator("state", 1);
    }

    /// <summary>
    /// AI回复的信息的回调
    /// </summary>
    /// <param name="_response"></param>
    private void CallBack(string _response)
    {
        if (string.IsNullOrEmpty(_response))
            return;

        _response = _response.Trim();
        Debug.Log("收到AI增量回复：" + _response);

        // 1. 无论什么模式，先追加到显示文本
        AppendText(_response);

        // 2. 拼接到缓冲区
        m_UnfinishedBuffer += _response;

        // 3. 检查缓冲区是否含有“强停顿标点”
        // 包含：句号、感叹号、问号、省略号、或者换行符
        if (ContainsStrongPunctuation(m_UnfinishedBuffer))
        {
            SendBufferToVoiceQueue();
        }

        // 4. 启动随机动画
        if (!m_IsSpeaking)
        {
            m_IsSpeaking = true;
            StartSpeakingSequenceRandomly();
        }
    }

    /// <summary>
    /// 检查字符串是否包含强停顿标点
    /// </summary>
    private bool ContainsStrongPunctuation(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;

        // 定义强停顿符号
        char[] strongPunctuations = { '。', '！', '？', '!', '?', '\n' };

        // 检查是否包含上述字符，或者包含省略号
        if (text.IndexOfAny(strongPunctuations) != -1 || text.Contains("……") || text.Contains("..."))
        {
            return true;
        }
        return false;
    }

    /// <summary>
    /// 将当前缓冲区内容送入语音队列并清空
    /// </summary>
    private void SendBufferToVoiceQueue()
    {
        string toSpeak = m_UnfinishedBuffer.Trim();
        if (string.IsNullOrEmpty(toSpeak)) return;

        // 【打断逻辑】：如果是新的一轮对话的第一句
        if (isNewInput)
        {
            isNewInput = false;
            StopPlayAudio();
            m_VoiceQueue.Clear(); // 清空旧队列，防止新老对话混在一起
        }

        // 加入队列
        m_VoiceQueue.Enqueue(toSpeak);
        Debug.Log($"[强标点断句] 加入语音队列：{toSpeak}");

        // 清空缓存
        m_UnfinishedBuffer = string.Empty;

        // 尝试播放
        TryPlayNextVoice();
    }

    // 定义接收完整回复参数的方法
    public IEnumerator ReceiveFullResponse(string fullResponse)
    {
        yield return new WaitForEndOfFrame();

        /* 【LYF新增】：打断AI上一段语音 */
        isNewInput = true;
        m_UnfinishedBuffer = "";

        ChatPrefab _receiveChat = Instantiate(m_RobotChatPrefab, m_rootTrans.transform);
        m_TempChatBox.Add(_receiveChat.gameObject);
        _receiveChat.SetText(fullResponse);
        //重新计算容器尺寸
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rootTrans);
        StartCoroutine(TurnToLastLine());
    }
   

    private bool IsSentenceComplete(string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;

        char lastChar = text[text.Length - 1];
        return lastChar == '。' || lastChar == '！' || lastChar == '？' || lastChar == '，' || lastChar == ' ' || char.IsPunctuation(lastChar);
    }
    private void TryPlayNextVoice()
    {
        // 【LYF新增】优化等待AI思考时的UI,此处AI已返回语音消息
        m_VoiceBottonText.text = "点击按钮，开始录音"; 
        isLoadingAnswer = 2;
        m_RecordTips.text = "";
        m_VoiceInputBotton.interactable = true;

        // 如果已经在播放语音，或者语音队列为空，则直接返回
        if (isPlayingVoice || m_VoiceQueue.Count == 0)
        {
            Debug.Log("当前没有需要播放的语音或正在播放中...");
            return;
        }

        // 从队列中取出下一段语音
        string nextVoice = m_VoiceQueue.Dequeue();
        string cleanVoice=CleanTextForTTS(nextVoice);
        // 【关键新增】：检查清理后的文本是否为空
        if (string.IsNullOrWhiteSpace(cleanVoice))
        {
            Debug.LogWarning("检测到空文本语音，跳过并尝试播放下一条...");
            // 递归调用，尝试播放队列中的下一个
            TryPlayNextVoice();
            return;
        }
        isPlayingVoice = true;

        Debug.Log($"开始播放语音：{cleanVoice}");

        // 调用语音合成播放
        //voiceCloneTTS.VoiceTTS(nextVoice,PlayVoice);
        volcengineVoiceCloneTTS.VoiceTTS1(cleanVoice, PlayVoice);

        // 调用语音合成播放
        //m_ChatSettings.m_TextToSpeech.Speak(nextVoice, PlayVoice);
    }
    private string CleanTextForTTS(string input)
    {
        if (string.IsNullOrEmpty(input))
            return string.Empty;

        //// 1. 将所有空白字符（换行、回车、制表符、多余空格）统一替换为一个空格
        //// \s 匹配 [ \f\n\r\t\v]，这能直接处理掉所有形式的“空行”
        //input = Regex.Replace(input, @"\s+", " ");

        // 2. 移除特殊字符
        // 保留：中文、英文、数字、常用中英文标点
        input = Regex.Replace(
            input,
            @"[^a-zA-Z0-9\u4e00-\u9fa5，。！？、,.!? ""“”: ：]+",
        ""
        );

        //// 3. 再次确保没有因为特殊字符被删掉后留下的双空格
        //input = Regex.Replace(input, @"\s{2,}", " ");

        return input.Trim();
    }

    /* 【LYF新增】中断语音播放 */
    public void StopPlayAudio(){
        if(voicePlayWaitingCoroutine!=null){
            StopCoroutine(voicePlayWaitingCoroutine);
        }
        m_VoiceQueue.Clear();
        m_AudioSource.Stop();
        isPlayingVoice = false; // 标记当前播放结束
        StopSpeakingSequence(); // 停止当前动画并切换到静止状态
    }

    private void AppendText(string _response)
    {
        if (string.IsNullOrEmpty(_response))
            return;

        // 增量追加到显示文本
        m_TextBack.text += _response;
        Debug.Log("m_TextBack.text"+m_TextBack.text);
        // 如果逐字显示未完成，则继续逐字显示
        if (!m_WriteState)
        {
            StartTypeWords(m_TextBack.text);
        }
    }

    #endregion

    #region 语音输入
    /// <summary>
    /// 语音识别返回的文本是否直接发送至LLM
    /// </summary>
    [SerializeField] private bool m_AutoSend = true;
    /// <summary>
    /// 语音输入的按钮
    /// </summary>
    [SerializeField] private Button m_VoiceInputBotton;
    /// <summary>
    /// 录音按钮的文本
    /// </summary>
    [SerializeField]private Text m_VoiceBottonText;
    /// <summary>
    /// 录音的提示信息
    /// </summary>
    [SerializeField] private Text m_RecordTips;
    /// <summary>
    /// AI思考时的文本信息
    /// </summary>
    [SerializeField] private string m_WatingText;
    /// <summary>
    /// 语音输入处理类
    /// </summary>
    [SerializeField] private VoiceInputs m_VoiceInputs;
    /// <summary>
    /// 注册按钮事件
    /// </summary>

    private bool isRecording = false; // 用于跟踪录音状态
    private void RegistButtonEvent()
    {
        if (m_VoiceInputBotton == null || m_VoiceInputBotton.GetComponent<EventTrigger>())
            return;

        EventTrigger _trigger = m_VoiceInputBotton.gameObject.AddComponent<EventTrigger>();

        // 添加按钮点击事件
        EventTrigger.Entry clickEntry = new EventTrigger.Entry();
        clickEntry.eventID = EventTriggerType.PointerClick;
        clickEntry.callback = new EventTrigger.TriggerEvent();
        clickEntry.callback.AddListener((data) => ToggleRecord());

        _trigger.triggers.Add(clickEntry);
    }


    /// <summary>
    /// 切换录音状态
    /// </summary>
    private void ToggleRecord()
    {
        if (isRecording)
        {
            StopRecord();
        }
        else
        {
            StartRecord();
        }
        isRecording = !isRecording; // 更新录音状态
    }
    /// <summary>
    /// 开始录制
    /// </summary>
    public void StartRecord()
    {
        /* 【LYF新增】 */
        StopPlayAudio();

        m_VoiceBottonText.text = "正在录音中...";
        m_VoiceInputs.StartRecordAudio();
    }
    /// <summary>
    /// 结束录制
    /// </summary>
    public void StopRecord()
    {
        m_RecordTips.text = "录音结束，正在识别...";

        // 【LYF新增】UI优化：临时禁用录音按钮防止出现回复插队的情况
        m_VoiceInputBotton.interactable = false;
        m_VoiceBottonText.text = ""; 

        m_VoiceInputs.StopRecordAudio(AcceptClip);
    }

    /// <summary>
    /// 处理录制的音频数据
    /// </summary>
    /// <param name="_data"></param>
    private void AcceptData(byte[] _data)
    {
        if (m_ChatSettings.m_SpeechToText == null)
            return;

        m_ChatSettings.m_SpeechToText.SpeechToText(_data, DealingTextCallback);
    }

    /// <summary>
    /// 处理录制的音频数据
    /// </summary>
    /// <param name="_data"></param>
    public void AcceptClip(AudioClip _audioClip)
    {
        if (m_ChatSettings.m_SpeechToText == null)
            return;

        m_ChatSettings.m_SpeechToText.SpeechToText(_audioClip, DealingTextCallback);
    }
    /// <summary>
    /// 处理识别到的文本
    /// </summary>
    /// <param name="_msg"></param>
    private void DealingTextCallback(string _msg)
    {
        m_RecordTips.text = _msg;
        StartCoroutine(SetTextVisible(m_RecordTips));
        //自动发送
        if (m_AutoSend)
        {
            SendData(_msg);
            return;
        }

        m_InputWord.text = _msg;
    }

    private IEnumerator SetTextVisible(Text _textbox)
    {
        yield return new WaitForSeconds(2f);
        // 【LYF新增】优化等待AI思考时的UI，此处AI刚上传语音消息
        if(isPlayingVoice == false){
            _textbox.text = m_WatingText;
            isLoadingAnswer = 1;
        }else{
            _textbox.text = ""; 
        }
        
    }

    #endregion

    #region 语音合成

    private IEnumerator TriggerSpeakingActionsRandomly()
    {
        while (m_IsSpeaking)
        {
            // 获取随机动画状态
            int randomState = GetRandomState();

            // 如果随机状态与上次播放的相同，则跳过
            if (randomState == lastPlayedState)
            {
                yield return null;
                continue;
            }

            // 播放动画
            PlayAnimator("talk" + randomState);
            lastPlayedState = randomState;

            Debug.Log($"播放动画：talk{randomState}");

            // 等待动画完成
            yield return new WaitForSeconds(6f); // 根据动画时长调整
        }
    }

    public void StartSpeakingSequenceRandomly()
    {
        if (speakingCoroutine != null)
        {
            StopCoroutine(speakingCoroutine);
        }

        // 启动动画播放协程
        speakingCoroutine = StartCoroutine(TriggerSpeakingActionsRandomly());
    }
    private int GetRandomState()
    {
        int randomIndex;

        do
        {
            randomIndex = UnityEngine.Random.Range(1, speakingStates.Length);
        }
        while (randomIndex == lastPlayedState); // 确保随机状态不重复

        lastPlayedState = randomIndex;
        return randomIndex;
    }



    public void StopSpeakingSequence()
    {
        m_IsSpeaking = false;

        // 切换到默认静止状态
        SetAnimator("state", 0);
        Debug.Log("动画已停止，切换到静止状态");
    }
    private void PlayVoice(AudioClip _clip, string _response)
    {
        if (_clip == null)
        {
            Debug.LogError("语音合成失败：AudioClip 为空，无法播放语音。");
            isPlayingVoice = false; // 标记为未播放
            TryPlayNextVoice(); // 尝试播放下一个语音
            return;
        }

        // 设置音频剪辑并播放
        m_AudioSource.clip = _clip;
        m_AudioSource.Play();

        Debug.Log($"播放语音：{_response}，时长：{_clip.length} 秒");

        // 触发一个随机动画
        if (!m_IsSpeaking)
        {
            m_IsSpeaking = true;

            // 选择一个随机的说话动画
            int randomState = GetRandomState();
            PlayAnimator("talk" + randomState); // 触发动画
            Debug.Log($"触发动画：talk{randomState}");
        }

        // 等待语音播放完成
        voicePlayWaitingCoroutine = StartCoroutine(HandleVoicePlaybackCompletion(_clip.length));
    }


    private IEnumerator HandleVoicePlaybackCompletion(float duration)
    {
        yield return new WaitForSeconds(duration);

        Debug.Log("语音播放完成");

        isPlayingVoice = false; // 标记当前播放结束

        // 停止动画播放
        if (m_IsSpeaking)
        {
            StopSpeakingSequence(); // 停止当前动画并切换到静止状态
        }

        // 尝试播放队列中的下一个语音
        TryPlayNextVoice();

        // 如果语音播放完成且没有更多流式数据，停止动画
        if (!m_IsSpeaking && m_VoiceQueue.Count == 0)
        {
            StopSpeakingSequence();
        }
    }

    #endregion

    #region 文字逐个显示
    //逐字显示的时间间隔
    [SerializeField] private float m_WordWaitTime = 0.2f;
    //是否显示完成
    [SerializeField] private bool m_WriteState = false;

    /// <summary>
    /// 开始逐个打印
    /// </summary>
    /// <param name="_msg"></param>
    private void StartTypeWords(string _msg)
    {
        if (_msg == "")
            return;

        m_WriteState = true;
        StartCoroutine(SetTextPerWord(_msg));
        // 自动加载聊天历史
        //StartCoroutine(GetHistoryChatInfo());
    }



    private IEnumerator SetTextPerWord(string _msg)
    {
        int currentPos = 0;
        float originalWordWaitTime = m_WordWaitTime; // 保存原始的等待时间
        while (m_WriteState)
        {

            if (currentPos < _msg.Length && CharUnicodeInfo.GetUnicodeCategory(_msg[currentPos]) == UnicodeCategory.OtherLetter)
            {
                // 中文或其他非拉丁字母
                m_WordWaitTime = originalWordWaitTime;
                //Debug.Log("other" + m_WordWaitTime);
            }
            else if (currentPos < _msg.Length && char.IsLetter(_msg[currentPos]) && _msg[currentPos] != ' ')
            {
                // 英文字符
                m_WordWaitTime = 0.03f;
                //Debug.Log("en" + m_WordWaitTime);
            }
            else
            {
                // 非字母字符
                m_WordWaitTime = originalWordWaitTime;
                //Debug.Log(m_WordWaitTime);
            }
            yield return new WaitForSeconds(m_WordWaitTime);
            currentPos++;
            //更新显示的内容
            m_TextBack.text = _msg.Substring(0, currentPos);

            m_WriteState = currentPos < _msg.Length;

        }
        m_IsSpeaking = false;
        //切换到等待动作
        //SetAnimator("state",0);
        // 恢复原始的等待时间
        m_WordWaitTime = originalWordWaitTime;

        //回复结束
        if (OnAISpeakDone != null)
        {
            OnAISpeakDone();
        }
    }

#endregion

#region 聊天记录

    //缓存已创建的聊天气泡
    [SerializeField] private List<GameObject> m_TempChatBox;
    //聊天记录显示层
    [SerializeField] private GameObject m_HistoryPanel;
    //聊天文本放置的层
    [SerializeField] private RectTransform m_rootTrans;
    //发送聊天气泡
    [SerializeField] private ChatPrefab m_PostChatPrefab;
    //回复的聊天气泡
    [SerializeField] private ChatPrefab m_RobotChatPrefab;
    //滚动条
    [SerializeField] private ScrollRect m_ScroTectObject;
    //获取聊天记录


    private IEnumerator TurnToLastLine()
    {
        yield return new WaitForEndOfFrame();
        //滚动到最近的消息
        m_ScroTectObject.verticalNormalizedPosition = 0;
    }


    private IEnumerator GetSendChatInfo(string _sendmsg)
    {
        yield return new WaitForEndOfFrame();

        ChatPrefab _sendChat = Instantiate(m_PostChatPrefab, m_rootTrans.transform);
        _sendChat.SetText(_sendmsg);  // 发送方的消息立即显示
        m_TempChatBox.Add(_sendChat.gameObject);

        //重新计算容器尺寸
        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rootTrans);
        StartCoroutine(TurnToLastLine());

    }
  






    #endregion

    private void SetAnimator(string _para,int _value)
    {
        if (m_Animator == null)
            return;

        m_Animator.SetInteger(_para, _value);
    }
    private void PlayAnimator(string name)
    {
        if (m_Animator == null)
            return;

        m_Animator.Play(name);
    }

    public void ChangeScene()
    {
        // 切换场景
        SceneManager.LoadScene("desktopscene");
    }
}
