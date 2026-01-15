using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System; // 為了使用 Buffer.BlockCopy

[RequireComponent(typeof(PCBoneController))]
public class PCUdpReceiver : MonoBehaviour
{
    public int port = 8080;
    
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;
    private PCBoneController boneController;

    private SkeletonProtocol.JointData[] latestJointData = null;
    private object dataLock = new object();

    void Start()
    {
        boneController = GetComponent<PCBoneController>();
        StartReceiver();
    }

    void StartReceiver()
    {
        try {
            udpClient = new UdpClient(port);
            receiveThread = new Thread(new ThreadStart(ReceiveData));
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Debug.Log($"PC UDP Receiver started on port {port}");
        } catch (Exception e) {
            Debug.LogError($"無法啟動 UDP Receiver: {e.Message}");
        }
    }

    void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, port);
        while (isRunning)
        {
            try
            {
                byte[] data = udpClient.Receive(ref remoteEndPoint);
                
                // 根據你的 SkeletonProtocol 結構進行手動解包
                var joints = UnpackFromProtocol(data);

                if (joints != null)
                {
                    lock (dataLock)
                    {
                        latestJointData = joints;
                    }
                }
            }
            catch (Exception e)
            {
                if(isRunning) Debug.LogWarning("UDP Receive Error: " + e.Message);
            }
        }
    }

    // 符合 SkeletonProtocol 序列化邏輯的解包程式
    private SkeletonProtocol.JointData[] UnpackFromProtocol(byte[] data)
    {
        if (data.Length < 4) return null;

        // 讀取前 4 bytes 取得點數 (int)
        int count = BitConverter.ToInt32(data, 0);
        SkeletonProtocol.JointData[] joints = new SkeletonProtocol.JointData[count];
        
        int bytesPerJoint = 28; // 3 floats (pos) + 4 floats (rot)

        for (int i = 0; i < count; i++)
        {
            int offset = 4 + (i * bytesPerJoint);
            
            // Position
            joints[i].position.x = BitConverter.ToSingle(data, offset);
            joints[i].position.y = BitConverter.ToSingle(data, offset + 4);
            joints[i].position.z = BitConverter.ToSingle(data, offset + 8);

            // Rotation
            joints[i].rotation.x = BitConverter.ToSingle(data, offset + 12);
            joints[i].rotation.y = BitConverter.ToSingle(data, offset + 16);
            joints[i].rotation.z = BitConverter.ToSingle(data, offset + 20);
            joints[i].rotation.w = BitConverter.ToSingle(data, offset + 24);
        }
        return joints;
    }

    void Update()
    {
        if (latestJointData != null)
        {
            lock (dataLock)
            {
                boneController.ApplyRemotePose(latestJointData);
                latestJointData = null; 
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null) receiveThread.Join(500);
    }
}