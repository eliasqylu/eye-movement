using UnityEngine;

/// <summary>
/// Reconstructed head-gaze gesture component (guid 61582f2c...). Drives the
/// cursor under combined gaze/head pointing and integrates nod confirmation.
/// Field surface restored from the scene; logic implemented in P3.
/// </summary>
public class HeadGazeGesture : MonoBehaviour
{
    [Header("Object References")]
    public GameObject cursor;
    public GameObject nodButton;
    public GameObject gazeCircle;

    [Header("Feedback / Gains")]
    public bool confirmFeedback = true;
    public float positionGain;
    public float rotationGain = 0.5f;
    public bool useOffset;

    [Header("Fixation (I-DT)")]
    public float minimumFixationDuration = 0.02f;
    public float dispersionThreshold = 2f;
    public bool useCentroidForFixation = true;

    [Header("Bimodal Gaze")]
    public bool useBimodalGaze = true;
    public bool hideCursorInSaccade = true;
    public int nodSignificantSample = 9;
}
