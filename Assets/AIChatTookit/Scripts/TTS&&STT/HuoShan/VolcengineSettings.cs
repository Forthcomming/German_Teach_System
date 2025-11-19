using UnityEngine;

[CreateAssetMenu(fileName = "VolcengineSettings", menuName = "TTS/Volcengine Settings")]
public class VolcengineSettings : MonoBehaviour
{
    [Header("火山引擎 TTS 配置")]
    public string appId = "7925191101";
    public string accessToken = "jC8xF0tlz9_OESYM5mVsxf3wTtzj0tDe";
    public string cluster = "volcano_tts"; // 默认集群，可根据控制台配置修改

    [Header("TTS 参数")]
    public string voiceType = "zh_female_yingyujiaoyu_mars_bigtts"; // 发音人 ID
    public float speedRatio = 1.0f;
    public float volumeRatio = 1.0f;
    public float pitchRatio = 0.0f;

    [Header("采样参数")]
    public int sampleRate = 24000;
    public int channels = 1;
}
