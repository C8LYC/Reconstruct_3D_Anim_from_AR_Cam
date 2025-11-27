using System.IO;
using UnityEngine;

// 這是 AR 端與 PC 端溝通的共同語言
public class SkeletonProtocol
{
    // 根據你的 Enum，總共有 91 個關節
    public const int JointCount = 91;

    // 定義單一關節的資料
    public struct JointData
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    // 將骨架資料打包成 Byte 陣列 (序列化)
    public static byte[] Pack(JointData[] joints)
    {
        using (var ms = new MemoryStream())
        using (var writer = new BinaryWriter(ms))
        {
            // 寫入資料長度檢查 (Optional)
            writer.Write(joints.Length);

            foreach (var joint in joints)
            {
                // 寫入位置 (3 floats)
                writer.Write(joint.position.x);
                writer.Write(joint.position.y);
                writer.Write(joint.position.z);

                // 寫入旋轉 (4 floats)
                writer.Write(joint.rotation.x);
                writer.Write(joint.rotation.y);
                writer.Write(joint.rotation.z);
                writer.Write(joint.rotation.w);
            }
            return ms.ToArray();
        }
    }

    // 將 Byte 陣列還原成骨架資料 (反序列化)
    public static JointData[] Unpack(byte[] data)
    {
        using (var ms = new MemoryStream(data))
        using (var reader = new BinaryReader(ms))
        {
            int count = reader.ReadInt32();
            JointData[] joints = new JointData[count];

            for (int i = 0; i < count; i++)
            {
                Vector3 pos = new Vector3(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );

                Quaternion rot = new Quaternion(
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle(),
                    reader.ReadSingle()
                );

                joints[i] = new JointData { position = pos, rotation = rot };
            }
            return joints;
        }
    }
}