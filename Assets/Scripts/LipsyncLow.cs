using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text.RegularExpressions;
using TMPro;

public class LipsyncLow : MonoBehaviour
{
    [Tooltip("Which lip sync provider to use for viseme computation.")]
    public OVRLipSync.ContextProviders provider = OVRLipSync.ContextProviders.Enhanced;
    [Tooltip("Enable DSP offload on supported Android devices.")]
    public bool enableAcceleration = true;
    [SerializeField] private uint Context = 0;
    [SerializeField] public float gain = 1.0f;

    [SerializeField] private AudioSource m_AudioSource;
 //   [SerializeField] private TMP_Dropdown dropdown; // 你的 Dropdown 组件

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

    public class BlendShapeData
    {
        public int Index;
        public float Weight;

        public BlendShapeData(int index, float weight)
        {
            Index = index;
            Weight = weight;
        }
    }
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

        // regex search for SkinnedMeshRenderers: head, teeth, tongue
        if (HeadSkinnedMeshRenderer == null)
            HeadSkinnedMeshRenderer = GetHeadSkinnedMeshRendererWithRegex(transform);

        if (TongueSkinnedMeshRenderer == null)
            TongueSkinnedMeshRenderer = GetTongueSkinnedMeshRendererWithRegex(transform);


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

    public void Update()
    {
        if (tongueBone != null) tongueBone.transform.localPosition = tongueBoneOffset;
        if (this.Frame != null)
        {
                SetBlenderShapes2();
        }
        //if (this.Frame != null)
        //{
        //    if ( dropdown.value == 1)
        //    {
        //        SetBlenderShapes3();
        //    }
        //    if (dropdown.value == 0 || dropdown.value == 2)
        //    {
        //        SetBlenderShapes2();
        //    }
        //    if (dropdown.value==3|| dropdown.value == 4 || dropdown.value == 5|| dropdown.value == 6)
        //    {
        //        SetBlenderShapes1();
        //    }

        if (this.Frame == null)
        {
            StopLipSync2();
            
        }
            //
            //if (this.Frame == null)
            //{
            //    if (dropdown.value == 1)
            //    {
            //        StopLipSync3();
            //    }
            //    if (dropdown.value == 0|| dropdown.value == 2)
            //    {
            //        StopLipSync2();
            //    }
            //    if (dropdown.value == 3 || dropdown.value == 4 || dropdown.value == 5 || dropdown.value == 6)
            //    {
            //        StopLipSync1();
            //    }

            //}


        }

    private void SetBlenderShapes1()
    {
        float alpha = 1.0f;
        float weightMultiplier = 110f;
        for (int i = 0; i < this.Frame.Visemes.Length; i++)
        {
            string _name = ((OVRLipSync.Viseme)i).ToString();
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
    private void SetBlenderShapes2()
    {
        float alpha = 1.0f;
        float weightMultiplier = 110f;
        for (int i = 0; i < this.Frame.Visemes.Length; i++)
        {
            string _name = ((OVRLipSync.Viseme)i).ToString();
            int blendShapeIndex = GetBlenderShapeIndexByName(_name);

            if (blendShapeIndex == 999)
                continue;
            float blendWeight = this.Frame.Visemes[i] * weightMultiplier * alpha;
            //float weight;

            jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
            tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);
            // 根据音素名称设置特定的混合形状权重
            switch (_name)
            {
                case "PP":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, blendWeight);
                    break;
                case "FF":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(40 + firstIndex, blendWeight); // V_Dental_Lip
                    break;
                case "TH":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.5f); // Mouth_Drop_Lower
                    break;
                case "DD":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.2f / 0.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(155)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight * 0.5f / 0.7f); // Mouth_Shrug_Upper
                    break;
                case "kk":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.5f / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(155)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    break;
                case "CH":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.7f / 2.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(155)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight / 2.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(45 + firstIndex, blendWeight / 2.7f); // V_Lip_Open
                    break;
                case "SS":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.5f / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(155)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    break;
                case "nn":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.5f / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(155)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    break;
                case "RR":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight * 0.5f / 0.9f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    break;
                case "aa":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    break;
                case "E":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(155)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight * 0.3f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    break;
                case "I":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(155 + firstIndex, blendWeight * 0.7f / 1.2f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(155)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(152 + firstIndex, blendWeight / 1.2f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(152)); // Mouth_Shrug_Upper
                    break;
                case "O":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(41 + firstIndex, blendWeight * 1.2f); // V_Tight_O
                    break;
                case "U":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(41 + firstIndex, blendWeight + HeadSkinnedMeshRenderer.GetBlendShapeWeight(41)); // V_Tight_O
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
    private void SetBlenderShapes3()
    {
        float alpha = 1.0f;
        float weightMultiplier = 110f;
        for (int i = 0; i < this.Frame.Visemes.Length; i++)
        {
            string _name = ((OVRLipSync.Viseme)i).ToString();
            int blendShapeIndex = GetBlenderShapeIndexByName(_name);

            if (blendShapeIndex == 999)
                continue;
            float blendWeight = this.Frame.Visemes[i] * weightMultiplier * alpha;
            //float weight;

            jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
            tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);
            // 根据音素名称设置特定的混合形状权重
            switch (_name)
            {
                case "PP":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, blendWeight);
                    break;
                case "FF":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(40 + firstIndex, blendWeight); // V_Dental_Lip
                    break;
                case "TH":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.5f); // Mouth_Drop_Lower
                    break;
                case "DD":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.2f / 0.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(178)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight * 0.5f / 0.7f); // Mouth_Shrug_Upper
                    break;
                case "kk":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.5f / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(178)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    break;
                case "CH":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.7f / 2.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(178)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight / 2.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(45 + firstIndex, blendWeight / 2.7f); // V_Lip_Open
                    break;
                case "SS":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.5f / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(178)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight / 1.5f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    break;
                case "nn":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.5f / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(178)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    break;
                case "RR":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight * 0.5f / 0.9f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    break;
                case "aa":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight / 2.0f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    break;
                case "E":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.7f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(178)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight * 0.3f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    break;
                case "I":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, blendWeight * 0.7f / 1.2f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(178)); // Mouth_Drop_Lower
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, blendWeight / 1.2f + HeadSkinnedMeshRenderer.GetBlendShapeWeight(175)); // Mouth_Shrug_Upper
                    break;
                case "O":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(41 + firstIndex, blendWeight * 1.2f); // V_Tight_O
                    break;
                case "U":
                    HeadSkinnedMeshRenderer.SetBlendShapeWeight(41 + firstIndex, blendWeight + HeadSkinnedMeshRenderer.GetBlendShapeWeight(41)); // V_Tight_O
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
    }
        private void StopLipSync1()
    {
        jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
        tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(1 + firstIndex, 0f); // V_Explosive

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(2 + firstIndex, 0f); // V_Dental_Lip

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(117 + firstIndex, 0f); // Mouth_Drop_Lower

        TongueSkinnedMeshRenderer.SetBlendShapeWeight(1 + firstIndex, 0f); // V_Tongue_Out

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(114 + firstIndex, 0f); // Mouth_Shrug_Upper

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(7 + firstIndex, 0f); // V_Lip_Open

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(76 + firstIndex, 0f); // Mouth_Press_L

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(77 + firstIndex, 0f); // Mouth_Press_R

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(3 + firstIndex, 0f); // V_Tight_O

        // Jaw Bone
        jawBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -90.0f);

        // Tongue Bone
        tongueBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -5f);
    }
    private void StopLipSync2()
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

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(41 + firstIndex, 0f); // V_Tight_O

        // Jaw Bone
        jawBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -90.0f);

        // Tongue Bone
        tongueBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -5f);
    }

    private void StopLipSync3()
    {
        jawBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -90.0f);
        tongueBone.transform.localEulerAngles = new Vector3(0.0f, 0.0f, -5.0f);

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, 0f); // V_Explosive

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(40 + firstIndex, 0f); // V_Dental_Lip

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(178 + firstIndex, 0f); // Mouth_Drop_Lower

        TongueSkinnedMeshRenderer.SetBlendShapeWeight(39 + firstIndex, 0f); // V_Tongue_Out

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(175 + firstIndex, 0f); // Mouth_Shrug_Upper

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(45 + firstIndex, 0f); // V_Lip_Open

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(137 + firstIndex, 0f); // Mouth_Press_L

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(138 + firstIndex, 0f); // Mouth_Press_R

        HeadSkinnedMeshRenderer.SetBlendShapeWeight(41 + firstIndex, 0f); // V_Tight_O

        // Jaw Bone
        jawBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -90.0f);

        // Tongue Bone
        tongueBone.transform.localEulerAngles
            = new Vector3(0.0f, 0.0f, -5f);
    }


    /// <summary>
    /// 简单判断下，返回a i u e o 的索引
    /// </summary>
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
   
}