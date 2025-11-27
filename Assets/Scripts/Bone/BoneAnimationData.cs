using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BoneAnimationData
{
    public string clipName;
    public float frameRate;
    public List<FrameData> frames = new List<FrameData>();
}

[Serializable]
public class FrameData
{
    public float timeStamp;
    // 儲存該幀所有骨架的 Transform 資訊
    public List<BoneTransformData> boneDataList = new List<BoneTransformData>();
}

[Serializable]
public class BoneTransformData
{
    public HumanBodyBones boneType;
    public Vector3 localPosition;
    public Quaternion localRotation;

    public BoneTransformData(HumanBodyBones type, Transform t)
    {
        boneType = type;
        localPosition = t.localPosition;
        localRotation = t.localRotation;
    }
}