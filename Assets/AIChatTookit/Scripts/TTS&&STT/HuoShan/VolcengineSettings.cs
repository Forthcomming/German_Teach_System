using UnityEngine;

/// <summary>
/// ?????????????TTS??HTTP openspeech??????? ASR??WebSocket openspeech v2?????????��?
/// ???? Project ??????? Create ?? TTS ?? Volcengine Settings ????????????????????? Settings ???????
/// </summary>
[CreateAssetMenu(fileName = "VolcengineSettings", menuName = "TTS/Volcengine Settings")]
public class VolcengineSettings : ScriptableObject
{
    [Header("??????? TTS??HTTP??")]
    [Tooltip("???????? AppID")]
    public string appId = "";

    [Tooltip("????? Access Token")]
    public string accessToken = "";

    [Header("ASR 鉴权（WebSocket）")]
    [Tooltip("ASR 使用的 App Key（火山控制台）")]
    public string appKey = "";

    [Tooltip("ASR 使用的 Access Key（新版控制台可留空）")]
    public string accessKey = "";

    [Tooltip("ASR WebSocket 地址")]
    public string asrWsUrl = "wss://openspeech.bytedance.com/api/v3/sauc/bigmodel_nostream";

    [Tooltip("ASR 资源ID，默认与 Python 示例一致")]
    public string asrResourceId = "volc.bigasr.sauc.duration";

    [Header("ASR 请求参数")]
    [Tooltip("ASR 模型名，例如 bigmodel")]
    public string asrModelName = "bigmodel";

    [Tooltip("识别语言，例如 zh-CN、en-US、de-DE")]
    public string asrLanguage = "zh-CN";

    [Tooltip("每包发送的音频时长（毫秒）")]
    [Min(40)]
    public int asrSegmentDurationMs = 200;

    [Tooltip("是否启用逆文本规范化 ITN")]
    public bool enableItn = true;

    [Tooltip("是否启用标点恢复")]
    public bool enablePunc = true;

    [Tooltip("是否启用数字/日期规范化等增强")]
    public bool enableDdc = true;

    [Tooltip("是否输出分句信息")]
    public bool showUtterances = true;

    [Tooltip("TTS ?????????? volcano_tts")]
    public string cluster = "volcano_tts";

    [Header("TTS ????")]
    public string voiceType = "zh_female_yingyujiaoyu_mars_bigtts";
    public float speedRatio = 1.0f;
    public float volumeRatio = 1.0f;
    public float pitchRatio = 0.0f;

    [Header("TTS ????")]
    public int sampleRate = 24000;
    public int channels = 1;


}
