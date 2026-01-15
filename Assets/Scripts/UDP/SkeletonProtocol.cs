using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

[Serializable]
public class SkeletonProtocol
{
    // 全身 91 點與辨識用 14 點 (13點 + 補上1號Hip點)
    public const int JointCount = 91;
    public const int ReducedJointCount = 14; 

    // Position (12 bytes) + Rotation (16 bytes) = 28 bytes per joint
    private const int BytesPerJoint = 28; 

    [Serializable]
    public struct JointData
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    // 根據您的需求對應的 Unity 關節索引
    // 包含：51(頭), 19,63(肩), 21,65(肘), 22,66(腕), 1(腰), 2,7(髖), 3,8(膝), 4,9(踝)
    public static readonly int[] ReducedIndices = new int[ReducedJointCount] 
    {
        51, 19, 63, 21, 65, 22, 66, 1, 2, 7, 3, 8, 4, 9
    };

    #region 關鍵點模式 (Reduced 14 點)
    public static byte[] PackReduced(ARHumanBody body)
    {
        var joints = body.joints;
        JointData[] reducedData = new JointData[ReducedJointCount];
        
        for (int i = 0; i < ReducedJointCount; i++)
        {
            int unityIndex = ReducedIndices[i];
            if (unityIndex < joints.Length)
            {
                reducedData[i].position = joints[unityIndex].anchorPose.position;
                reducedData[i].rotation = joints[unityIndex].anchorPose.rotation;
            }
        }
        return Serialize(reducedData, ReducedJointCount);
    }
    #endregion

    #region 全傳送模式 (91 點)
    public static byte[] PackFull(ARHumanBody body)
    {
        var joints = body.joints;
        JointData[] fullData = new JointData[JointCount];
        for (int i = 0; i < JointCount; i++)
        {
            if (i < joints.Length)
            {
                fullData[i].position = joints[i].anchorPose.position;
                fullData[i].rotation = joints[i].anchorPose.rotation;
            }
        }
        return Serialize(fullData, JointCount);
    }
    #endregion

    private static byte[] Serialize(JointData[] joints, int count)
    {
        int packetSize = 4 + (count * BytesPerJoint);
        byte[] packet = new byte[packetSize];
        Buffer.BlockCopy(BitConverter.GetBytes(count), 0, packet, 0, 4);

        for (int i = 0; i < count; i++)
        {
            int offset = 4 + (i * BytesPerJoint);
            // Position
            Buffer.BlockCopy(BitConverter.GetBytes(joints[i].position.x), 0, packet, offset, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(joints[i].position.y), 0, packet, offset + 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(joints[i].position.z), 0, packet, offset + 8, 4);
            // Rotation
            Buffer.BlockCopy(BitConverter.GetBytes(joints[i].rotation.x), 0, packet, offset + 12, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(joints[i].rotation.y), 0, packet, offset + 16, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(joints[i].rotation.z), 0, packet, offset + 20, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(joints[i].rotation.w), 0, packet, offset + 24, 4);
        }
        return packet;
    }
}