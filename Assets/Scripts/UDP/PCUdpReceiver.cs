using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Threading;

[RequireComponent(typeof(PCBoneController))]
public class PCUdpReceiver : MonoBehaviour
{
    public int port = 8080; // 監聽 Port
    
    private UdpClient udpClient;
    private Thread receiveThread;
    private bool isRunning = true;
    private PCBoneController boneController;

    // 用來在主執行緒儲存接收到的數據
    private SkeletonProtocol.JointData[] latestJointData = null;
    private object dataLock = new object(); // 執行緒鎖

    void Start()
    {
        boneController = GetComponent<PCBoneController>();
        StartReceiver();
    }

    void StartReceiver()
    {
        udpClient = new UdpClient(port);
        receiveThread = new Thread(new ThreadStart(ReceiveData));
        receiveThread.IsBackground = true;
        receiveThread.Start();
        Debug.Log($"PC UDP Receiver started on port {port}");
    }

    void ReceiveData()
    {
        IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, port);
        while (isRunning)
        {
            try
            {
                // 接收 UDP 封包
                byte[] data = udpClient.Receive(ref remoteEndPoint);

                // 解包數據
                var joints = SkeletonProtocol.Unpack(data);

                // 存入變數供 Update 使用 (確保執行緒安全)
                lock (dataLock)
                {
                    latestJointData = joints;
                }
            }
            catch (System.Exception e)
            {
                if(isRunning) Debug.LogError("UDP Receive Error: " + e.Message);
            }
        }
    }

    void Update()
    {
        // 在 Main Thread 套用動畫
        if (latestJointData != null)
        {
            lock (dataLock)
            {
                boneController.ApplyRemotePose(latestJointData);
                latestJointData = null; // 清空以避免重複套用
            }
        }
    }

    void OnDestroy()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null) receiveThread.Abort();
    }
}