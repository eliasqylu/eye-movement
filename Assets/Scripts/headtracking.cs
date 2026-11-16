using System.Collections.Generic;
using UnityEngine;
using j_utils;

public class headtracking : MonoBehaviour
{
    [Header("Object References")]
    public GameObject debugObject_Cursor;
    public GameObject debugObject_HeadFov;
    public GameObject cursor;
    public GameObject headCursor;
    private Camera xrCamera;

    [Header("Fixation Detection Settings")]
    public bool useAdaptiveRms = false;
    public float dispersionThresholdValue = 0.4f;
    public float dispersionMultiplier = 1.5f;
    public float minimumFixationDuration = 0.08f;
    public int rmsWinLen = 1800;
    public bool useCentroidForCursor = true;
    public bool useCentroidForHeadFov = true;

    [Header("Enable/Disable Features")]
    public bool detectCursorFixation = true;
    public bool detectHeadFovFixation = true;

    // --- Public Static Head Tracking Data ---
    public static Quaternion hmdQuaternion;
    public static Vector2 hmdAzPol;
    public static float headAz_neutral;
    public static float headPol_neutral;
    public static float headVel_Az;
    public static float headVel_Pol;
    public static float headAccel_Az;
    public static float headAccel_Pol;
    public static float headJolt_Az;
    public static float headJolt_Pol;
    public static Vector3 angularVelocity;
    public static Vector3 angularAcceleration;
    public static Vector3 positionVelocity;
    public static Vector3 positionAcceleration;
    public static bool isHeadInFixation = false;

    // --- Original Fixation Variables for CURSOR (Preserved for other scripts) ---
    private IDTSession cursorIdtSession = new IDTSession();
    private List<Vector3> cursorPositionHistory = new List<Vector3>();
    private bool isCursorFixationDetected;
    private bool isCursorFixationDetected_prev;
    public static Vector3 currentCursorCentroid;
    public static List<Vector3> previousCursorCentroids = new List<Vector3>();
    public static List<float> previousFixationEndTime = new List<float>(); // THIS IS THE ORIGINAL VARIABLE

    // --- NEW Duplicated Fixation Variables for HEAD FOV ---
    private IDTSession headFovIdtSession = new IDTSession();
    private List<Vector3> headFovPositionHistory = new List<Vector3>();
    private bool isHeadFovFixationDetected;
    private bool isHeadFovFixationDetected_prev;
    public static Vector3 currentHeadFovCentroid;
    public static List<Vector3> previousHeadFovCentroids = new List<Vector3>();
    public static List<float> previousHeadFovFixationEndTime = new List<float>();


    // --- Private Fields for general head kinematics ---
    private List<float> Azs = new List<float>();
    private List<float> Pols = new List<float>();
    public static Vector2 hmdAzPol_0 = Vector2.zero;
    private Queue<Vector3> rotationHistory_5pt = new Queue<Vector3>();
    private Queue<Vector3> positionHistory_5pt = new Queue<Vector3>();


    void Start()
    {
        xrCamera = Camera.main;
        if (xrCamera == null)
        {
            Debug.LogError("Main Camera not found! Headtracking requires a Camera with 'MainCamera' tag.");
            this.enabled = false;
        }
    }

    void Update()
    {
        // 1. Update Head Position and Kinematics (Done once per frame)
        UpdateHeadKinematics();

        // Calculate the shared dispersion threshold once
        float dispersionThreshold = useAdaptiveRms ? dispersionMultiplier * eyeHead_utils.RMS(Azs, Pols) : dispersionThresholdValue;

        // 2. Process Fixation for CURSOR
        if (detectCursorFixation)
        {
            Vector3 cursorInHeadPos = xrCamera.transform.InverseTransformPoint(cursor.transform.position);
            Vector2 cursorAzPol = eyeHead_utils.vec2ficks(cursorInHeadPos.x, cursorInHeadPos.y, cursorInHeadPos.z);

            isCursorFixationDetected = cursorIdtSession.IDT(cursorAzPol.x, cursorAzPol.y, dispersionThreshold, minimumFixationDuration, "disp");

            if (cursorIdtSession.IsCurrentlyFixating())
            {
                cursorPositionHistory.Add(cursor.transform.position);
                if (isCursorFixationDetected)
                {
                    currentCursorCentroid = useCentroidForCursor
                        ? CalculateCentroid(cursorPositionHistory)
                        : cursor.transform.position;

                    if (debugObject_Cursor != null)
                        debugObject_Cursor.transform.position = currentCursorCentroid;
                }
            }
            else if (isCursorFixationDetected_prev)
            {
                previousCursorCentroids.Add(currentCursorCentroid);
                if (previousCursorCentroids.Count > 1) previousCursorCentroids.RemoveAt(0);

                previousFixationEndTime.Add(Time.time); // Using original variable name
                if (previousFixationEndTime.Count > 1) previousFixationEndTime.RemoveAt(0);

                cursorPositionHistory.Clear();
            }
            isCursorFixationDetected_prev = isCursorFixationDetected;
        }


        // 3. Process Fixation for HEAD FOV (Duplicated Logic)
        if (detectHeadFovFixation)
        {
            isHeadFovFixationDetected = headFovIdtSession.IDT(hmdAzPol.x, hmdAzPol.y, dispersionThreshold, minimumFixationDuration, "disp");
            // isHeadFovFixationDetected = headFovIdtSession.IDT(0, hmdAzPol.y, dispersionThreshold, minimumFixationDuration, "disp");

            isHeadInFixation = isHeadFovFixationDetected; // Update global flag

            if (headFovIdtSession.IsCurrentlyFixating())
            {
                headFovPositionHistory.Add(headCursor.transform.position);
                if (isHeadFovFixationDetected)
                {
                    currentHeadFovCentroid = useCentroidForHeadFov
                        ? CalculateCentroid(headFovPositionHistory)
                        : headCursor.transform.position;

                    if (debugObject_HeadFov != null)
                        debugObject_HeadFov.transform.position = currentHeadFovCentroid;
                }
            }
            else if (isHeadFovFixationDetected_prev)
            {
                previousHeadFovCentroids.Add(currentHeadFovCentroid);
                if (previousHeadFovCentroids.Count > 1) previousHeadFovCentroids.RemoveAt(0);

                previousHeadFovFixationEndTime.Add(Time.time);
                if (previousHeadFovFixationEndTime.Count > 1) previousHeadFovFixationEndTime.RemoveAt(0);

                headFovPositionHistory.Clear();
            }
            isHeadFovFixationDetected_prev = isHeadFovFixationDetected;
        }
    }

    // Helper to avoid writing the centroid loop twice
    Vector3 CalculateCentroid(List<Vector3> positions)
    {
        if (positions == null || positions.Count == 0) return Vector3.zero;
        Vector3 centroid = Vector3.zero;
        foreach (var pos in positions)
        {
            centroid += pos;
        }
        return centroid / positions.Count;
    }

    /// <summary>
    /// Handles all calculations related to head movement.
    /// </summary>
    void UpdateHeadKinematics()
    {
        // Update cursors and get rotation
        headCursor.transform.position = xrCamera.transform.TransformPoint(0, 0, 1);
        hmdQuaternion = xrCamera.transform.rotation;

        // Calculate Azimuth/Polar coordinates
        hmdAzPol = eyeHead_utils.vec2ficks(xrCamera.transform.forward.x, xrCamera.transform.forward.y, xrCamera.transform.forward.z);
        if (gameStates.frameOfCentering)
        {
            hmdAzPol_0 = hmdAzPol;
        }
        hmdAzPol -= hmdAzPol_0;

        // Maintain history buffer for derivatives
        Azs.Add(hmdAzPol.x);
        Pols.Add(hmdAzPol.y);
        if (Azs.Count > rmsWinLen)
        {
            Azs.RemoveAt(0);
            Pols.RemoveAt(0);
        }

        // Compute derivatives from Az/Pol data
        headVel_Az = eyeHead_utils.Compute5ptVelocity(Azs);
        headVel_Pol = eyeHead_utils.Compute5ptVelocity(Pols);
        headAccel_Az = eyeHead_utils.Compute5ptAcceleration(Azs);
        headAccel_Pol = eyeHead_utils.Compute5ptAcceleration(Pols);
        headJolt_Az = eyeHead_utils.Compute5ptJolt(Azs);
        headJolt_Pol = eyeHead_utils.Compute5ptJolt(Pols);

        // Maintain history for 5-point differentiation of Vector3s
        rotationHistory_5pt.Enqueue(xrCamera.transform.eulerAngles);
        if (rotationHistory_5pt.Count > 5)
        {
            rotationHistory_5pt.Dequeue();
        }

        positionHistory_5pt.Enqueue(xrCamera.transform.position);
        if (positionHistory_5pt.Count > 5)
        {
            positionHistory_5pt.Dequeue();
        }

        // Compute derivatives from Vector3 data if history is sufficient
        if (rotationHistory_5pt.Count == 5)
        {
            angularVelocity = eyeHead_utils.ComputeAngularVelocity(rotationHistory_5pt.ToArray());
            angularAcceleration = eyeHead_utils.ComputeAngularAcceleration(rotationHistory_5pt.ToArray());
        }
        if (positionHistory_5pt.Count == 5)
        {
            positionVelocity = eyeHead_utils.ComputeAngularVelocity(positionHistory_5pt.ToArray()); // Assuming same differentiation logic
            positionAcceleration = eyeHead_utils.ComputeAngularAcceleration(positionHistory_5pt.ToArray());
        }
    }

    public List<float> GetAzsCopy()
    {
        return new List<float>(Azs);
    }
    public List<float> GetPolsCopy()
    {
        return new List<float>(Pols);
    }
}