using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections.Generic;

[System.Serializable]

public class BoneToPythonSender : MonoBehaviour
{
    public string ip = "127.0.0.1";
    public int port = 5005;
    private UdpClient client;

    // 包含：51(頭), 19,63(肩), 21,65(肘), 22,66(腕), 1(腰), 2,7(髖), 3,8(膝), 4,9(踝)
    public static readonly int[] ReducedIndices = new int[14] 
    {
        51, 19, 63, 21, 65, 22, 66, 1, 2, 7, 3, 8, 4, 9
    };

    public Transform[] jointObjects = new Transform[14]; 

    void Start() => client = new UdpClient();

    public void SendBodyData() {
        try
        {
            SkeletonProtocol.JointData[] data = new SkeletonProtocol.JointData[ReducedIndices.Length];
            for (int i = 0 ; i < ReducedIndices.Length; i++)
            {
                Transform joint = jointObjects[i];
                if (joint == null) continue;

                // 取得關節的本地位置
                data[i].position = joint.position;
                data[i].rotation = joint.rotation;
            }
            // 使用非同步發送，確保不影響 AR 渲染效能 (避免掉幀)
            byte[] bytes = SkeletonProtocol.Pack(data);
            client.BeginSend(bytes, bytes.Length, ip, port, null, null);
            Debug.Log("Sent UDP packet with " + data.Length + " joints.");

        }
        catch (System.Exception e)
        {
            Debug.LogError("UDP Send Error: " + e.Message);
        }   
    }

    void OnApplicationQuit() => client.Close();
}