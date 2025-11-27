using System.Collections.Generic;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(Animator))]
public class BoneMonitor : MonoBehaviour
{
    [Header("Settings")]
    public float recordFrameRate = 30f;
    [Tooltip("是否開啟播放平滑插值 (逐幀操作時會自動暫時忽略此設定)")]
    public bool enableInterpolation = true;
    
    [Header("Playback Status (Read Only)")]
    public bool isPaused = false;
    public int currentFrameIndex = 0;
    public int totalFrames = 0;

    [Header("Debug Controls")]
    [Tooltip("開啟後可用鍵盤控制：空白鍵(暫停), 左右鍵(進退幀)")]
    public bool enableKeyboardControl = true;

    // 核心組件
    private Animator animator;
    private Dictionary<HumanBodyBones, Transform> boneMap;
    
    // 速度計算用
    private Dictionary<HumanBodyBones, Vector3> lastPositions = new Dictionary<HumanBodyBones, Vector3>();
    private Dictionary<HumanBodyBones, float> currentVelocities = new Dictionary<HumanBodyBones, float>();

    // 錄製與播放變數
    private bool isRecording = false;
    private bool isPlaying = false;
    private float recordingTimer = 0f;
    private BoneAnimationData currentSessionData;
    private float playbackTime = 0f;
    private BoneAnimationData playbackData;

    void Awake()
    {
        animator = GetComponent<Animator>();
        InitializeBoneMap();
    }

    void Update()
    {
        // 1. 持續計算速度 (功能 6)
        CalculateVelocities();

        // 2. 處理錄製 (功能 4)
        if (isRecording)
        {
            HandleRecording();
        }

        // 3. 處理播放 (功能 5)
        if (isPlaying && playbackData != null)
        {
            // 鍵盤控制輸入
            if (enableKeyboardControl) HandleInput();
            
            // 播放邏輯
            HandlePlayback();
        }
    }

    // --- 鍵盤控制 ---
    void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isPaused) ResumePlayback(); else PausePlayback();
        }
        if (Input.GetKeyDown(KeyCode.RightArrow)) StepForward();
        if (Input.GetKeyDown(KeyCode.LeftArrow)) StepBackward();
    }

    // --- 初始化與骨架存取 (功能 1) ---
    void InitializeBoneMap()
    {
        boneMap = new Dictionary<HumanBodyBones, Transform>();
        foreach (HumanBodyBones bone in System.Enum.GetValues(typeof(HumanBodyBones)))
        {
            if (bone == HumanBodyBones.LastBone) continue;
            
            Transform t = animator.GetBoneTransform(bone);
            if (t != null)
            {
                boneMap.Add(bone, t);
                lastPositions.Add(bone, t.position);
                currentVelocities.Add(bone, 0f);
            }
        }
    }

    public Transform GetBone(HumanBodyBones bone)
    {
        if (boneMap.ContainsKey(bone)) return boneMap[bone];
        return null; // 若該模型沒有此骨骼 (例如沒有手指) 則回傳 null
    }

    // --- 數學計算 (功能 2 & 3) ---
    public float GetAngle(HumanBodyBones start, HumanBodyBones center, HumanBodyBones end)
    {
        Transform tStart = GetBone(start);
        Transform tCenter = GetBone(center);
        Transform tEnd = GetBone(end);

        if (!tStart || !tCenter || !tEnd) return 0f;

        Vector3 vectorA = tStart.position - tCenter.position;
        Vector3 vectorB = tEnd.position - tCenter.position;

        return Vector3.Angle(vectorA, vectorB);
    }

    public float GetAxisDistance(HumanBodyBones boneA, HumanBodyBones boneB, string axis)
    {
        Transform tA = GetBone(boneA);
        Transform tB = GetBone(boneB);

        if (!tA || !tB) return 0f;

        Vector3 diff = tA.position - tB.position;
        
        switch (axis.ToLower())
        {
            case "x": return Mathf.Abs(diff.x);
            case "y": return Mathf.Abs(diff.y);
            case "z": return Mathf.Abs(diff.z);
            default: return Vector3.Distance(tA.position, tB.position);
        }
    }

    // --- 速度計算 (功能 6) ---
    void CalculateVelocities()
    {
        if (Time.deltaTime <= 0) return;

        foreach (var kvp in boneMap)
        {
            HumanBodyBones bone = kvp.Key;
            Transform t = kvp.Value;

            float distance = Vector3.Distance(t.position, lastPositions[bone]);
            float speed = distance / Time.deltaTime;
            
            currentVelocities[bone] = speed;
            lastPositions[bone] = t.position;
        }
    }

    public float GetVelocity(HumanBodyBones bone)
    {
        if (currentVelocities.ContainsKey(bone)) return currentVelocities[bone];
        return 0f;
    }

    // --- 錄製系統 (功能 4) ---
    [ContextMenu("Start Recording")]
    public void StartRecording()
    {
        currentSessionData = new BoneAnimationData();
        currentSessionData.frameRate = recordFrameRate;
        currentSessionData.clipName = "Record_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
        isRecording = true;
        recordingTimer = 0f;
        Debug.Log("Recording Started...");
    }

    [ContextMenu("Stop Recording")]
    public void StopRecording()
    {
        // 預設儲存路徑 (可在這裡修改)
        string path = Path.Combine(Application.persistentDataPath, "bone_motion.json");
        StopRecordingAndSave(path);
    }

    public void StopRecordingAndSave(string filePath)
    {
        isRecording = false;
        string json = JsonUtility.ToJson(currentSessionData, true);
        File.WriteAllText(filePath, json);
        Debug.Log($"Recording Saved to: {filePath}");
    }

    void HandleRecording()
    {
        recordingTimer += Time.deltaTime;
        if (recordingTimer >= (1f / recordFrameRate))
        {
            recordingTimer = 0f;
            FrameData frame = new FrameData();
            frame.timeStamp = Time.time;

            foreach (var kvp in boneMap)
            {
                frame.boneDataList.Add(new BoneTransformData(kvp.Key, kvp.Value));
            }
            currentSessionData.frames.Add(frame);
        }
    }

    // --- 播放與控制系統 (功能 5 + 逐幀控制) ---
    
    [ContextMenu("Load & Play Last File")]
    public void LoadLastFile()
    {
        string path = Path.Combine(Application.persistentDataPath, "bone_motion.json");
        LoadAndPlay(path);
    }

    public void LoadAndPlay(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Debug.LogError($"File not found: {filePath}");
            return;
        }

        string json = File.ReadAllText(filePath);
        playbackData = JsonUtility.FromJson<BoneAnimationData>(json);
        
        animator.enabled = false; // 關閉 Animator 以便控制 Transform
        isPlaying = true;
        isPaused = false;
        playbackTime = 0f;
        currentFrameIndex = 0;
        totalFrames = playbackData.frames.Count;
        
        Debug.Log($"Playing... Total Frames: {totalFrames}");
    }

    public void StopPlaying()
    {
        isPlaying = false;
        animator.enabled = true; // 恢復 Animator
        Debug.Log("Playback Stopped.");
    }

    [ContextMenu("Pause")]
    public void PausePlayback()
    {
        isPaused = true;
        Debug.Log("Paused");
    }

    [ContextMenu("Resume")]
    public void ResumePlayback()
    {
        isPaused = false;
        Debug.Log("Resumed");
    }

    [ContextMenu("Step Forward")]
    public void StepForward()
    {
        if (!isPlaying || playbackData == null) return;
        isPaused = true;
        
        int nextIndex = currentFrameIndex + 1;
        if (nextIndex >= totalFrames) nextIndex = totalFrames - 1;
        
        SetFrameIndex(nextIndex);
    }

    [ContextMenu("Step Backward")]
    public void StepBackward()
    {
        if (!isPlaying || playbackData == null) return;
        isPaused = true;

        int prevIndex = currentFrameIndex - 1;
        if (prevIndex < 0) prevIndex = 0;

        SetFrameIndex(prevIndex);
    }

    // 跳轉到特定幀並同步時間
    private void SetFrameIndex(int index)
    {
        currentFrameIndex = index;
        playbackTime = currentFrameIndex / playbackData.frameRate;
        ApplyFrame(playbackData.frames[currentFrameIndex]);
    }

    void HandlePlayback()
    {
        if (isPaused) return;

        playbackTime += Time.deltaTime;
        float exactFrameIndex = playbackTime * playbackData.frameRate;

        // 更新 UI 顯示用的索引
        currentFrameIndex = Mathf.FloorToInt(exactFrameIndex);

        // 平滑插值模式 (且非暫停狀態)
        if (enableInterpolation)
        {
            int prevIndex = currentFrameIndex;
            int nextIndex = prevIndex + 1;

            if (nextIndex >= totalFrames)
            {
                StopPlaying();
                return;
            }

            float t = exactFrameIndex - prevIndex;
            ApplyInterpolatedFrame(playbackData.frames[prevIndex], playbackData.frames[nextIndex], t);
        }
        else // Snapping 模式
        {
            if (currentFrameIndex >= totalFrames)
            {
                StopPlaying();
                return;
            }
            ApplyFrame(playbackData.frames[currentFrameIndex]);
        }
    }

    // 套用單一幀 (Snapping)
    void ApplyFrame(FrameData frame)
    {
        foreach (var data in frame.boneDataList)
        {
            Transform t = GetBone(data.boneType);
            if (t != null)
            {
                t.localPosition = data.localPosition;
                t.localRotation = data.localRotation;
            }
        }
    }

    // 套用插值幀 (Smoothing)
    void ApplyInterpolatedFrame(FrameData frameA, FrameData frameB, float t)
    {
        // 假設 boneDataList 順序一致
        for (int i = 0; i < frameA.boneDataList.Count; i++)
        {
            var dataA = frameA.boneDataList[i];
            var dataB = frameB.boneDataList[i];

            if (dataA.boneType != dataB.boneType) continue;

            Transform trans = GetBone(dataA.boneType);
            if (trans != null)
            {
                trans.localPosition = Vector3.Lerp(dataA.localPosition, dataB.localPosition, t);
                trans.localRotation = Quaternion.Slerp(dataA.localRotation, dataB.localRotation, t);
            }
        }
    }
}