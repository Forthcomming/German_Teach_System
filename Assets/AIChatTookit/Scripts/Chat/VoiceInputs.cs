using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VoiceInputs : MonoBehaviour
{

    /// <summary>
    /// 录制的音频长度
    /// </summary>
    public int m_RecordingLength = 10;

    public AudioClip recording;

    /// <summary>
    /// 检测录音状态的协程
    /// </summary>
    private Coroutine detectRecordingCoroutine;

    /// <summary>
    /// WebGL辅助类
    /// </summary>
    [SerializeField]private SignalManager signalManager;
    /// <summary>
    /// 开始录制声音
    /// </summary>
    public void StartRecordAudio()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        signalManager.onAudioClipDone = null;
        signalManager.StartRecordBinding();
#else
        recording = Microphone.Start(null, false, m_RecordingLength, 16000);

        // 【LYF新增】启动录制状态检测协程
        m_SilenceTimer = 0.0f;
        while (Microphone.GetPosition(null) <= 0) { }
        detectRecordingCoroutine = StartCoroutine(DetectRecording());
       
        #endif
    }

    /*【LYF新增】：录音同时检测音量，实现与对话模式一样自动发送的功能 */
    
    /// <summary>
    /// 音量大于这个值，就开始录制
    /// </summary>
    private float m_SilenceThreshold = 0.01f;
    /// <summary>
    /// 沉默限制时长
    /// </summary>
    [Header("设置几秒没声音，就停止录制")]
    public float m_RecordingTimeLimit = 2.0f;
    /// <summary>
    /// 沉默计时器
    /// </summary>
    [SerializeField]private float m_SilenceTimer = 0.0f;
    public bool isAutoStopRecording = false;


    /// <summary>
    /// 【LYF】协程：监听语音模式录音，当结束录音时，等待x秒自动结束录制
    /// </summary>
    private IEnumerator DetectRecording()
    {
        /* 【LYF】要将m_RecordingLength数值调大一点，我这里是设置为10 */
        while(true)
        {
            // 获取当前录音位置
            int position = Microphone.GetPosition(null);

            // 如果录音位置无效（例如麦克风未准备好），跳过当前循环
            if (position <= 0)
            {
                yield return null;
                continue;
            }

            // 创建一个临时采样缓冲区
            float[] samples = new float[128]; // 选择合适的样本大小

            // 确保读取范围合法
            int startSample = Mathf.Max(0, position - samples.Length); // 避免负数索引
            try
            {
                // 尝试从 AudioClip 获取音频数据
                recording.GetData(samples, startSample);
            }
            catch (System.Exception ex)
            {
                // 捕获异常并记录
                Debug.LogWarning($"Error accessing audio data: {ex.Message}");
                //yield return null;
                continue;
            }

            float rms = 0.0f;
            foreach(float sample in samples){
                rms += sample * sample;
            }

            rms = Mathf.Sqrt(rms / samples.Length);

            // 当检测到声音时，重置静默计时器
            if(rms > m_SilenceThreshold){
                m_SilenceTimer = 0.0f;
            }
            else{
                m_SilenceTimer += Time.deltaTime;
                if(m_SilenceTimer > m_RecordingTimeLimit){
                        isAutoStopRecording = true;
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// 结束录制，返回audioClip
    /// </summary>
    /// <param name="_callback"></param>
    public void StopRecordAudio(Action<AudioClip> _callback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        signalManager.onAudioClipDone += _callback;
        signalManager.StopRecordBinding();
#else
        Microphone.End(null);

        /* 【LYF新增】：停止监听音量协程 */
        //isAutoStopRecording = false;
        StopCoroutine(detectRecordingCoroutine);
        
        _callback(recording);
        
#endif

    }

}
