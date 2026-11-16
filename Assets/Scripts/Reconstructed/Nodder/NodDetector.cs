using UnityEngine;

/// <summary>
/// Reconstructed nod detector (guid d772f943...). Runs heuristic/SVM/HMM nod
/// detection and optionally connects to the Python calibration server. Field
/// surface restored from the scene; detection logic implemented in P3.
/// </summary>
public class NodDetector : MonoBehaviour
{
    [Header("Detection")]
    public int detectionMode;
    public float nodVelThresh = -38f;
    public float nodAccelThresh = 2000f;
    public float nodJoltThresh = 200000f;
    public float nodFastTime_short = 0.14f;
    public float nodFastVel_short = -28f;
    public float dragReleaseNodVel_short = -38f;
    public float eyeSacVelThresh = 100f;
    public int frameSinceLastEyeSacThresh = 30;

    [Header("Python Server")]
    public string serverIP = "127.0.0.1";
    public int serverPort = 65432;

    [Header("Latest Outputs")]
    public int latestHeuristicsNod;
    public int latestSVMCondition;
    public int latestHMMCondition;
    public int latestNodDetected;
    public int isConnectedToServer;
}
