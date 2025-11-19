using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using TMPro;
using static VolcengineTextToSpeech; // 这样可以直接用 PhonemeData
public class Lipsync3 : MonoBehaviour
{
    [Tooltip("Which lip sync provider to use for viseme computation.")]
    public OVRLipSync.ContextProviders provider = OVRLipSync.ContextProviders.Enhanced;
    [Tooltip("Enable DSP offload on supported Android devices.")]
    public bool enableAcceleration = true;
    [SerializeField] private uint Context = 0;
    [SerializeField] public float gain = 1.0f;

    [SerializeField] private AudioSource m_AudioSource;
    [SerializeField] private TMP_Dropdown dropdown; // 你的 Dropdown 组件

    [Tooltip("Array of Skinned Mesh Renderer Components for the character.")]
    public SkinnedMeshRenderer[] HeadMeshRenderers;
    [Tooltip("Skinned Mesh Renderer Component for the head of the character.")]
    public SkinnedMeshRenderer HeadSkinnedMeshRenderer;

    //[Tooltip("Skinned Mesh Renderer Component for the teeth of the character, if available. Leave empty if not.")]
    //public SkinnedMeshRenderer TeethSkinnedMeshRenderer;

    [Tooltip("Array of Skinned Mesh Renderer Components for the character.")]
    public SkinnedMeshRenderer[] TongueMeshRenderers;
    [Tooltip("Skinned Mesh Renderer Component for the tongue of the character, if available. Leave empty if not.")]
    public SkinnedMeshRenderer TongueSkinnedMeshRenderer;

    [Tooltip("Array of Skinned Mesh Renderer Components for the character.")]
    public GameObject[] jawBones;
    [Tooltip("Game object with the bone of the jaw for the character, if available. Leave empty if not.")]
    public GameObject jawBone;


    [Tooltip("Array of Skinned Mesh Renderer Components for the character.")]
    public GameObject[] tongueBones;
    [Tooltip("Game object with the bone of the tongue for the character, if available. Leave empty if not.")]
    public GameObject tongueBone; // even though actually tongue doesn't have a bone

    [Tooltip("Set a custom position for the tongue bone so that it looks natural.")]
    [SerializeField]
    private Vector3 tongueBoneOffset = new(-0.01f, 0.015f, 0f);

    [Tooltip("The index of the first blendshape that will be manipulated.")]
    public int firstIndex;



   //public float blendWeightMultiplier = 100f;

    [Header("设置元音对应的blendershape的索引值")]
    public VisemeBlenderShapeIndexMap m_VisemeIndex;

    private OVRLipSync.Frame frame = new OVRLipSync.Frame();
    protected OVRLipSync.Frame Frame
    {
        get { return frame; }
    }
    public class PhonemeData
    {
        public string phoneme;
        public float startTime;
        public float endTime;
    }
    private PhonemeData currentPhoneme;
    //加一个队列来接收火山返回的 phoneme + 时间戳
    private Queue<PhonemeData> volcenginePhonemeQueue = new Queue<PhonemeData>();
    // 每段音频的起始偏移（这段 clip 在整句话里的起点时间）
    private float audioClipOffset = 0f;

    // 播放音频时，传入偏移量

    //接收 phoneme
    public void EnqueuePhoneme(PhonemeData data)
    {
        volcenginePhonemeQueue.Enqueue(data);

    }
    //每句播放完后清空音素
    public void LoadNewSentencePhonemes(List<PhonemeData> newPhonemes)
    {
        volcenginePhonemeQueue.Clear(); // 清空上一句的
        foreach (var p in newPhonemes)
            volcenginePhonemeQueue.Enqueue(p);

        currentPhoneme = null; // 重置当前音素
        Debug.Log($"已加载新句子的音素 {newPhonemes.Count} 个");
    }


    public AudioSource audioSource;

    private void Awake()
    {
        m_AudioSource = this.GetComponent<AudioSource>();
        m_VisemeIndex = new VisemeBlenderShapeIndexMap();

        if (Context == 0)
        {
            if (OVRLipSync.CreateContext(ref Context, provider, 0, enableAcceleration) != OVRLipSync.Result.Success)
            {
                Debug.LogError("OVRLipSyncContextBase.Start ERROR: Could not create Phoneme context.");
                return;
            }
        }
    }

    /// <summary>
    ///     This function will automatically set any of the unassigned skinned mesh renderers
    ///     to appropriate values using regex based functions.
    ///     It also invokes the LipSyncCharacter() function every one hundredth of a second.
    /// </summary>
    private void Start()
    {
        // 为Dropdown的valueChange事件添加监听器
        dropdown.onValueChanged.AddListener(delegate {
            ChangePeople();
        });
        // regex search for SkinnedMeshRenderers: head, teeth, tongue
        if (HeadSkinnedMeshRenderer == null)
            HeadSkinnedMeshRenderer = GetHeadSkinnedMeshRendererWithRegex(transform);

        if (TongueSkinnedMeshRenderer == null)
            TongueSkinnedMeshRenderer = GetTongueSkinnedMeshRendererWithRegex(transform);


    }

    void ChangePeople()
    {
        // 获取选中的索引
        int index = dropdown.value;

        // 检查索引是否在所有数组的有效范围内
        if (index >= 0 &&
            index < HeadMeshRenderers.Length &&
            index < TongueMeshRenderers.Length &&
            index < jawBones.Length &&
            index < tongueBones.Length)
        {
            // 直接使用索引赋值，避免重复的条件判断
            HeadSkinnedMeshRenderer = HeadMeshRenderers[index];
            TongueSkinnedMeshRenderer = TongueMeshRenderers[index];
            jawBone = jawBones[index];
            tongueBone = tongueBones[index];
        }
    }

    /// <summary>
    ///     This function finds the Head skinned mesh renderer components, if present,
    ///     in the children of the parentTransform using regex.
    /// </summary>
    /// <param name="parentTransform">The parent transform whose children are searched.</param>
    /// <returns>The SkinnedMeshRenderer component of the Head, if found; otherwise, null.</returns>
    private SkinnedMeshRenderer GetHeadSkinnedMeshRendererWithRegex(Transform parentTransform)
    {
        // Initialize a variable to store the found SkinnedMeshRenderer.
        SkinnedMeshRenderer findFaceSkinnedMeshRenderer = null;

        // Define a regular expression pattern for matching child object names.
        Regex regexPattern = new("(.*_Head|CC_Base_Body)");

        // Iterate through each child of the parentTransform.
        foreach (Transform child in parentTransform)
            // Check if the child's name matches the regex pattern.
            if (regexPattern.IsMatch(child.name) && child.gameObject.activeInHierarchy)
            {
                // If a match is found, get the SkinnedMeshRenderer component of the child.
                findFaceSkinnedMeshRenderer = child.GetComponent<SkinnedMeshRenderer>();

                // If a SkinnedMeshRenderer is found, break out of the loop.
                if (findFaceSkinnedMeshRenderer != null) break;
            }

        // Return the found SkinnedMeshRenderer (or null if none is found).
        return findFaceSkinnedMeshRenderer;
    }


    /// <summary>
    ///     This function finds the Tongue skinned mesh renderer components, if present,
    ///     in the children of the parentTransform using regex.
    /// </summary>
    /// <param name="parentTransform">The parent transform whose children are searched.</param>
    /// <returns>The SkinnedMeshRenderer component of the Tongue, if found; otherwise, null.</returns>
    private SkinnedMeshRenderer GetTongueSkinnedMeshRendererWithRegex(Transform parentTransform)
    {
        // Initialize a variable to store the found SkinnedMeshRenderer for the tongue.
        SkinnedMeshRenderer findTongueSkinnedMeshRenderer = null;

        // Define a regular expression pattern for matching child object names.
        Regex regexPattern = new("(.*_Tongue|CC_Base_Body)");

        // Iterate through each child of the parentTransform.
        foreach (Transform child in parentTransform)
            // Check if the child's name matches the regex pattern.
            if (regexPattern.IsMatch(child.name) && child.gameObject.activeInHierarchy)
            {
                // If a match is found, get the SkinnedMeshRenderer component of the child.
                findTongueSkinnedMeshRenderer = child.GetComponent<SkinnedMeshRenderer>();

                // If a SkinnedMeshRenderer is found, break out of the loop.
                if (findTongueSkinnedMeshRenderer != null) break;
            }

        // Return the found SkinnedMeshRenderer for the tongue (or null if none is found).
        return findTongueSkinnedMeshRenderer;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        ProcessAudioSamplesRaw(data, channels);
    }

    public void ProcessAudioSamplesRaw(float[] data, int channels)
    {
        lock (this)
        {
            if (OVRLipSync.IsInitialized() != OVRLipSync.Result.Success)
            {
                return;
            }
            var frame = this.Frame;
            OVRLipSync.ProcessFrame(Context, data, frame, channels == 2);
        }
    }
    // 声明计时器变量（记录距离上次调用的时间）
    private float _printTimer = 0f;
    // 调用间隔（秒）
    private const float PRINT_INTERVAL = 0.3f;

    public void Update()
    {

        if (tongueBone != null) tongueBone.transform.localPosition = tongueBoneOffset;


        float audioTime = audioSource != null ? audioSource.time : 0f;
        // 只有播放中才处理时间信息
        //if (audioSource.isPlaying)
        //{
        //    Debug.Log($"当前音频播放时间: {audioSource.time:F2} 秒");

        //    // 先判断currentPhoneme是否为null，再访问它的属性
        //    if (currentPhoneme != null)
        //    {
        //        Debug.Log($"当前音素 startTime: {currentPhoneme.startTime:F2} 秒, endTime: {currentPhoneme.endTime:F2} 秒");
        //    }
        //    else
        //    {
        //        // 当currentPhoneme为null时，输出提示信息（非错误）
        //        Debug.Log("当前没有需要处理的音素（currentPhoneme为null）");
        //    }
        //}

        if (currentPhoneme == null || audioTime > currentPhoneme.endTime)
        {

            if (volcenginePhonemeQueue.Count > 0)
                currentPhoneme = volcenginePhonemeQueue.Dequeue();
            else
                currentPhoneme = null;
        }


        if (currentPhoneme != null &&
        audioTime >= currentPhoneme.startTime &&
        audioTime <= currentPhoneme.endTime)
        {
            string visemeName = MapVolcenginePhonemeToViseme(currentPhoneme.phoneme);
            SetBlenderShapes2(visemeName);
        }
        else
        {
            StopLipSync2();
        }

    }


    private void SetBlenderShapes1()
    {
        float alpha = 1.0f;
        float weightMultiplier = 110f;
        for (int i = 0; i < this.Frame.Visemes.Length; i++)
        {
            string _name = ((OVRLipSync.Viseme)i).ToString();
            // 如果是火山引擎返回的音素，就先映射到Viseme
            //string _name = MapVolcenginePhonemeToViseme(currentPhonemeFromVolcengine);
            int blendShapeIndex = GetBlenderShapeIndexByName(_name);
            
            if (blendShapeIndex == 999)
                continue;
            float blendWeight = this.Frame.Visemes[i]* weightMultiplier*alpha;
            //float weight;
                
            jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
            tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);
            // 根据音素名称设置特定的混合形状权重
            switch (_name)
            {
                case "PP":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(1 + firstIndex, blendWeight);
                    break;
                case "FF":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(2 + firstIndex, blendWeight); // V_Dental_Lip
                    break;
                case "TH":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.5f); // Mouth_Drop_Lower
                    break;
                case "DD":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.2f / 0.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(117)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight * 0.5f / 0.7f); // Mouth_Shrug_Upper
                    break;
                case "kk":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.5f / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(117)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    break;
                case "CH":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.7f / 2.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(117)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight / 2.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(7 + firstIndex, blendWeight / 2.7f); // V_Lip_Open
                    break;
                case "SS":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.5f / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(117)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    break;
                case "nn":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.5f / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(117)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    break;
                case "RR":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight * 0.5f / 0.9f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    break;
                case "aa":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    break;
                case "E":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(117)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight * 0.3f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    break;
                case "I":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, blendWeight * 0.7f / 1.2f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(117)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, blendWeight / 1.2f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(114)); // Mouth_Shrug_Upper
                    break;
                case "O":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(3 + firstIndex, blendWeight * 1.2f); // V_Tight_O
                    break;
                case "U":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(3 + firstIndex, blendWeight + HeadSkinnedMeshRenderer.GetBlendShapeWeight(41)); // V_Tight_O
                    break;
            }
            
            // Adjust the jaw and tongue bone rotations based on the specific viseme values.
            jawBone.transform.localEulerAngles
                = new Vector3(0.0f, 0.0f, -90.0f - (
                        0.2f * this.Frame.Visemes[3]
                        + 0.1f * this.Frame.Visemes[4]
                        + 0.5f * this.Frame.Visemes[5]
                        + 0.2f * this.Frame.Visemes[8]
                        + 0.2f * this.Frame.Visemes[9]
                        + 1.0f * this.Frame.Visemes[10]
                        + 0.2f * this.Frame.Visemes[11]
                        + 0.3f * this.Frame.Visemes[12]
                        + 0.8f * this.Frame.Visemes[13]
                        + 0.3f * this.Frame.Visemes[14])
                    / (0.2f + 0.1f + 0.5f + 0.2f + 0.2f + 1.0f + 0.2f + 0.3f + 0.8f + 0.3f)
                    * 50f);
            //Debug.Log("Jaw Bone Local Rotation: " + jawBone.transform.localEulerAngles);
            // Tongue Bone
            tongueBone.transform.localEulerAngles
                = new Vector3(0.0f, 0.0f, (
                        0.1f * this.Frame.Visemes[3]
                        + 0.2f * this.Frame.Visemes[8]
                        + 0.15f * this.Frame.Visemes[9]
                    )
                    / (0.1f + 0.2f + 0.15f)
                    * 80f - 5f);

        } 
            //foreach (BlendShapeData blendShape in blendShapeIndexes)
            //{
            //    if (blendShape.Index != 999 && blendShape.Index < meshRenderer.sharedMesh.blendShapeCount)
            //    {
            //        meshRenderer.SetBlendShapeWeight(blendShape.Index, blendShape.Weight * blendWeight);
            //    }
            //}

        
    }
    private void SetBlenderShapes2(string currentPhonemeFromVolcengine, float phonemeDuration = 0.1f)
    {
        float targetWeight = 90f; // 最大嘴型强度

        // 持续时间越短 → 过渡速度越快；持续时间越长 → 过渡慢一些
        // 比如 0.1 秒的爆破音 → lerpSpeed ≈ 20
        // 比如 0.4 秒的元音 → lerpSpeed ≈ 5
        float lerpSpeed = Mathf.Clamp(1f / phonemeDuration * 2f, 5f, 20f);

        // 先把所有目标值初始化为 0
        Dictionary<int, float> targetBlendShapes = new Dictionary<int, float>
    {
        { 39 + firstIndex, 0f },  // V_Explosive
        { 40 + firstIndex, 0f },  // V_Dental_Lip
        { 155 + firstIndex, 0f }, // Mouth_Drop_Lower
        { 152 + firstIndex, 0f }, // Mouth_Shrug_Upper
        { 45 + firstIndex, 0f },  // V_Lip_Open
        { 114 + firstIndex, 0f }, // Mouth_Press_L
        { 115 + firstIndex, 0f }, // Mouth_Press_R
        { 41 + firstIndex, 0f }   // V_Tight_O
    };

        // 根据当前音素，设置目标值
        switch (currentPhonemeFromVolcengine)
        {
            case "PP":
                targetBlendShapes[39 + firstIndex] = targetWeight;
                break;
            case "FF":
                targetBlendShapes[40 + firstIndex] = targetWeight;
                break;
            case "TH":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.5f;
                break;
            case "DD":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.2f;
                targetBlendShapes[152 + firstIndex] = targetWeight * 0.5f;
                break;
            case "kk":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.5f;
                targetBlendShapes[152 + firstIndex] = targetWeight;
                break;
            case "CH":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.7f;
                targetBlendShapes[152 + firstIndex] = targetWeight;
                targetBlendShapes[45 + firstIndex] = targetWeight;
                break;
            case "SS":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.5f;
                targetBlendShapes[152 + firstIndex] = targetWeight;
                break;
            case "nn":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.5f;
                targetBlendShapes[152 + firstIndex] = targetWeight;
                break;
            case "RR":
                targetBlendShapes[152 + firstIndex] = targetWeight * 0.5f;
                break;
            case "aa":
                targetBlendShapes[152 + firstIndex] = targetWeight;
                break;
            case "E":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.7f;
                targetBlendShapes[152 + firstIndex] = targetWeight * 0.3f;
                break;
            case "I":
                targetBlendShapes[155 + firstIndex] = targetWeight * 0.7f;
                targetBlendShapes[152 + firstIndex] = targetWeight;
                break;
            case "O":
                targetBlendShapes[41 + firstIndex] = targetWeight * 1.2f;
                break;
            case "U":
                targetBlendShapes[41 + firstIndex] = targetWeight;
                break;
        }

        // 平滑过渡到目标值
        foreach (var kvp in targetBlendShapes)
        {
            int index = kvp.Key;
            float desired = kvp.Value;
            float current = HeadSkinnedMeshRenderer.GetBlendShapeWeight(index);
            float newWeight = Mathf.Lerp(current, desired, Time.deltaTime * lerpSpeed);
            HeadSkinnedMeshRenderer.SetBlendShapeWeight(index, newWeight);
        }

        // 下颚和舌头控制保持
        jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
        tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);
    }



    private void StopLipSync1()
    {
        jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
        tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, 0f); // V_Explosive

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(40 + firstIndex, 0f); // V_Dental_Lip

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, 0f); // Mouth_Drop_Lower

        TongueSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, 0f); // V_Tongue_Out

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, 0f); // Mouth_Shrug_Upper

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(45 + firstIndex, 0f); // V_Lip_Open

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, 0f); // Mouth_Press_L

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(115 + firstIndex, 0f); // Mouth_Press_R

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(42 + firstIndex, 0f); // V_Tight_O

        // Jaw Bone
        jawBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -90.0f);

        // Tongue Bone
        //tongueBone.transform.localEulerAngles
        //    = new Vector3(0.0f, 0.0f, -5f);
    }
    private void StopLipSync2()
    {
        jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
        tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, 0f); // V_Explosive

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(40 + firstIndex, 0f); // V_Dental_Lip

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, 0f); // Mouth_Drop_Lower

        //TongueSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, 0f); // V_Tongue_Out

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, 0f); // Mouth_Shrug_Upper

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(45 + firstIndex, 0f); // V_Lip_Open

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, 0f); // Mouth_Press_L

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(115 + firstIndex, 0f); // Mouth_Press_R

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(41 + firstIndex, 0f); // V_Tight_O

        // Jaw Bone
        jawBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -90.0f);

        // Tongue Bone
        tongueBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -5f);
    }



    /// <param name="_name"></param>
    /// <returns></returns>
    private int GetBlenderShapeIndexByName(string _name)
    {
        switch (_name)
        {
            case "sil": return 999; // 返回空列表
            case "PP": return m_VisemeIndex.P;
            case "FF": return m_VisemeIndex.F;
            case "TH": return m_VisemeIndex.T;
            case "DD": return m_VisemeIndex.D;
            case "kk": return m_VisemeIndex.K;
            case "CH": return m_VisemeIndex.C;
            case "SS": return m_VisemeIndex.S;
            case "nn": return m_VisemeIndex.N;
            case "RR": return m_VisemeIndex.R;
            case "aa": return m_VisemeIndex.A;
            case "E": return m_VisemeIndex.E;
            case "I": return m_VisemeIndex.I;
            case "O": return m_VisemeIndex.O;
            case "U": return m_VisemeIndex.U;
            default: return m_VisemeIndex.U;
        }
    }

    [System.Serializable]
    public class VisemeBlenderShapeIndexMap
    {
        public int P;
        public int F;
        public int T;
        public int D;
        public int K;
        public int C;
        public int S;
        public int N;
        public int R;
        public int A;
        public int E;
        public int I;
        public int O;
        public int U;

    }
    /// <summary>
    /// 将火山引擎返回的中英文音素映射到现有Viseme
    /// </summary>
    private string MapVolcenginePhonemeToViseme(string phoneme)
    {
        // 去掉前缀 C0 / E0
        string p = phoneme.Length > 2 ? phoneme.Substring(2).ToLower() : phoneme;

        // 中文拼音映射（补充后）
        switch (p)
        {
            // 双唇音、唇齿音
            case "p":
            case "b":
            case "m":
                return "PP";
            case "f":
                return "FF";

            // 舌尖音（d/t/n/l）
            case "d":
            case "t":
            case "n":
            case "l":
                return "DD";

            // 舌根音（k/g/h）
            case "k":
            case "g":
            case "h":
                return "kk";

            // 舌尖后音（zh/ch/sh/r）
            case "zh":
            case "ch":
            case "sh":
            case "r":
                return "CH";

            // 舌尖前音（s/z/c）
            case "s":
            case "z":
            case "c":
                return "SS";

            // 开口呼（a组）
            case "a":
            case "ai":
            case "an":
            case "ang":
            case "ao":  // 补充：ao（如"奥"）
                return "aa";

            // 开口呼（e组）
            case "e":
            case "ei":
            case "en":
            case "eng":
            case "er":  // 补充：er（如"儿"）
                return "E";

            // 齐齿呼（i组）
            case "i":
            case "ia":  // 补充：ia（如"呀"）
            case "ian": // 补充：ian（如"烟"）
            case "iang":// 补充：iang（如"央"）
            case "iao": // 补充：iao（如"腰"）
            case "ie":  // 补充：ie（如"耶"）
            case "in":  // 补充：in（如"因"）
            case "ing": // 补充：ing（如"英"）
            case "iou":
                return "I";

            // 合口呼（o/u组）
            case "o":
            case "ou":
            case "u":
            case "ua":  // 补充：ua（如"哇"）
            case "uai": // 补充：uai（如"歪"）
            case "uan": // 补充：uan（如"弯"）
            case "uang":// 补充：uang（如"汪"）
            case "uei":
            case "un":  // 补充：un（如"温"）
            case "uo":  // 补充：uo（如"窝"）
                return "O";

            // 撮口呼（ü组，拼音中通常写作v）
            case "v":   // 对应ü
            case "van": // 补充：üan（如"冤"）
            case "ve":  // 补充：üe（如"约"）
            case "vn":  // 补充：ün（如"晕"）
                return "U"; // 可根据实际唇形调整为O或U
        }
        // 英文 ARPAbet/IPA 映射
        switch (p)
        {
            case "p":
            case "b":
            case "m":
                return "PP";
            case "f":
            case "v":
                return "FF";
            case "th":
            case "dh":
                return "TH";
            case "d":
            case "t":
            case "n":
            case "l":
                return "DD";
            case "k":
            case "g":
                return "kk";
            case "ch":
            case "jh":
            case "sh":
            case "zh":
            case "r":
                return "CH";
            case "s":
            case "z":
                return "SS";
            case "ng":
                return "nn";
            case "er":
            case "rr":
                return "RR";
            case "aa":
            case "ah":
            case "ae":
                return "aa";
            case "eh":
            case "ey":
                return "E";
            case "ih":
            case "iy":
                return "I";
            case "ow":
            case "ao":
                return "O";
            case "uw":
            case "uh":
                return "U";
        }

        // 默认返回静音
        return "sil";
    }



}