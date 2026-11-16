using UnityEngine;

/// <summary>
/// Reconstructed GazeSwitch component (guid 543c7ad9...). Bayesian-style
/// gaze/head mode switching based on VOR compensation and head velocity.
/// Field surface restored from the scene; switching logic implemented in P3.
/// </summary>
public class GazeSwitch : MonoBehaviour
{
    [Header("Stability / Smoothing")]
    public int stabilityThreshold = 3;
    public int smoothingWindow = 1;
    public int velocitySmoothingWindow = 3;
    public int maxHistory = 2;

    [Header("Saccade / VOR")]
    public float eyeSacVelThresh = 50f;
    public int frameSinceLastEyeSacThresh = 30;
    public float vorProbThresh = 0.4f;

    [Header("Head Refine Velocity Range (deg/s)")]
    public float minRefineHeadVel = 3.04f;
    public float maxRefineHeadVel = 17.14f;

    [Header("Latest Outputs")]
    public float latestVORCompensation;
    public Vector3 latestHeadAngularVelocity;
    public Vector3 latestEyeAngularVelocity;
}
