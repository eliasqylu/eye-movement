using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using j_utils;
using Unity.VisualScripting;

public class headtracking_v1 : MonoBehaviour
{
    public GameObject debugObject;
    private Camera xrCamera;
    public GameObject cursor;
    public GameObject headCursor;
    public bool useAdaptiveRms = false;
    public float dispersionThresholdValue = 0.4f;
    public int rmsWinLen = 1800; //around 20s of time (90hz) for calculating rms for idt fixation detection

    // private Queue<Quaternion> hmdQuaternions_5pt = new Queue<Quaternion>();
    private Queue<Vector3> rotationHistory_5pt = new Queue<Vector3>();
    private Queue<Vector3> positionHistory_5pt = new Queue<Vector3>();
    public static Quaternion hmdQuaternion;
    public static Vector2 hmdAzPol;
    public static float headVel_Az;
    public static float headVel_Pol;
    public static float headAccel_Az;
    public static float headAccel_Pol;
    public static float headJolt_Az;
    public static float headJolt_Pol;
    public float dispersionMultiplier = 1.5f; // Dispersion threshold is this multiplier * gaze RMS
    public float minimumFixationDuration = 0.08f; // Minimum fixation duration in seconds
    public static bool isHeadInFixation = false;
    private IDTSession idtSession = new IDTSession();


    public static Vector3 angularVelocity;
    public static Vector3 angularAcceleration;

    public static Vector3 positionVelocity;
    public static Vector3 positionAcceleration;

    public bool fixationCentroid;


    private List<float> Azs = new List<float>();
    private List<float> Pols = new List<float>();

    public static Vector2 hmdAzPol_0 = new Vector2(0, 0);
    private List<Vector3> cursorPositionHistory = new List<Vector3>();
    public static Vector3 currentCursorCentroid;
    public static List<Vector3> previousCursorCentroids = new List<Vector3>();
    public static List<float> previousFixationEndTime = new List<float>();
    bool isFixationDetected_prev;
    bool isFixationDetected;
    public bool detectFOVFixation;


    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        headCursor.transform.position = xrCamera.transform.TransformPoint(0, 0, 1);
        hmdQuaternion = xrCamera.transform.rotation;

        hmdAzPol = eyeHead_utils.vec2ficks(xrCamera.transform.forward.x, xrCamera.transform.forward.y, xrCamera.transform.forward.z);
        if (gameStates.frameOfCentering)
        {
            hmdAzPol_0 = new Vector2(hmdAzPol.x, hmdAzPol.y);
        }
        hmdAzPol -= hmdAzPol_0;

        Azs.Add(hmdAzPol.x);
        Pols.Add(hmdAzPol.y);
        if (Azs.Count > rmsWinLen)
        {
            Azs.RemoveAt(0);
            Pols.RemoveAt(0);
        }
        headVel_Az = eyeHead_utils.Compute5ptVelocity(Azs);
        headVel_Pol = eyeHead_utils.Compute5ptVelocity(Pols);
        headAccel_Az = eyeHead_utils.Compute5ptAcceleration(Azs);
        headAccel_Pol = eyeHead_utils.Compute5ptAcceleration(Pols);
        headJolt_Az = eyeHead_utils.Compute5ptJolt(Azs);
        headJolt_Pol = eyeHead_utils.Compute5ptJolt(Pols);


        bool detectFixation = true;
        float dispersionThreshold = dispersionThresholdValue;
        if (detectFixation)
        {
            if (useAdaptiveRms)
            {
                float rms = eyeHead_utils.RMS(Azs, Pols);
                dispersionThreshold = dispersionMultiplier * rms;
            }

            // Debug.LogWarning("rms: " + rms);
            // Debug.LogWarning("dispersionThreshold: " + dispersionThreshold);
            string dispersionType = "disp";
            isFixationDetected = idtSession.IDT(hmdAzPol.x, hmdAzPol.y, dispersionThreshold, minimumFixationDuration, dispersionType);
            // if (isFixationDetected){
            //     headCursor.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
            //     gameStates.isHeadFixation=true;
            // }
            // else{
            //     headCursor.GetComponent<Renderer>().material.SetColor("_Color", Color.white);
            //     gameStates.isHeadFixation=false;
            // }
            isHeadInFixation = isFixationDetected;

            //get cursor centroid
            if (idtSession.IsCurrentlyFixating())
            {
                cursorPositionHistory.Add(cursor.transform.position);
                if (isFixationDetected)
                {
                    Vector3 centroid = Vector3.zero;
                    foreach (var pos in cursorPositionHistory)
                    {
                        centroid += pos;
                    }
                    if (fixationCentroid)
                    {
                        currentCursorCentroid = centroid /= cursorPositionHistory.Count;
                    }
                    else
                    {
                        currentCursorCentroid = cursor.transform.position;
                    }

                    debugObject.transform.position = currentCursorCentroid;
                }
            }
            else if (isFixationDetected_prev)
            {
                previousCursorCentroids.Add(currentCursorCentroid);
                if (previousCursorCentroids.Count > 5)
                {
                    previousCursorCentroids.RemoveAt(0);
                }
                cursorPositionHistory.Clear();
                // currentCursorCentroid = Vector3.zero;

                previousFixationEndTime.Add(Time.time);
                if (previousFixationEndTime.Count > 5)
                {
                    previousFixationEndTime.RemoveAt(0);
                }
            }

            isFixationDetected_prev = isFixationDetected;
        }


        Vector3 currentRotation = xrCamera.transform.eulerAngles;
        // Normalize yaw and pitch for consistency (ensure yaw is between -180 and 180)
        float currentYaw = eyeHead_utils.NormalizeAngle(currentRotation.y);
        float currentPitch = eyeHead_utils.NormalizeAngle(currentRotation.x);


        // Store the current rotation in the rotation history array
        if (rotationHistory_5pt.Count >= 5)
        {//5 point central differnce differntiation
            rotationHistory_5pt.Dequeue();
        }
        rotationHistory_5pt.Enqueue(currentRotation);
        // Compute angular velocity and acceleration using the 5-point method
        angularVelocity = eyeHead_utils.ComputeAngularVelocity(rotationHistory_5pt.ToArray());
        angularAcceleration = eyeHead_utils.ComputeAngularAcceleration(rotationHistory_5pt.ToArray());

        if (positionHistory_5pt.Count >= 5)
        {//5 point central differnce differntiation
            positionHistory_5pt.Dequeue();
        }
        positionHistory_5pt.Enqueue(xrCamera.transform.position);
        positionVelocity = eyeHead_utils.ComputeAngularVelocity(positionHistory_5pt.ToArray());
        positionAcceleration = eyeHead_utils.ComputeAngularAcceleration(positionHistory_5pt.ToArray());


    }

    // private float Compute5ptVelocity(List<float> xs)
    // {
    //     int endInd = xs.Count-1;
    //     return (-xs[endInd] + 8 * xs[endInd-1] - 8 * xs[endInd-3] + xs[endInd-4]) / (12 * Time.deltaTime);
    // }

    // private float NormalizeAngle(float angle)
    // {
    //     if (angle > 180f) return angle - 360f;
    //     else if (angle < -180f) return angle + 360f;
    //     return angle;
    // }

    // Method to compute angular velocity using the 5-point method
    // private Vector3 ComputeAngularVelocity(Vector3[] rotations)
    // {
    //     return new Vector3(
    //         (-rotations[4].x + 8 * rotations[3].x - 8 * rotations[1].x + rotations[0].x) / (12 * Time.deltaTime),
    //         (-rotations[4].y + 8 * rotations[3].y - 8 * rotations[1].y + rotations[0].y) / (12 * Time.deltaTime),
    //         (-rotations[4].z + 8 * rotations[3].z - 8 * rotations[1].z + rotations[0].z) / (12 * Time.deltaTime)
    //     );
    // }

    // // Method to compute angular acceleration using the 5-point method
    // private Vector3 ComputeAngularAcceleration(Vector3[] rotations)
    // {
    //     return new Vector3(
    //         (-rotations[4].x + 16 * rotations[3].x - 30 * rotations[2].x + 16 * rotations[1].x - rotations[0].x) / (12 * Mathf.Pow(Time.deltaTime, 2)),
    //         (-rotations[4].y + 16 * rotations[3].y - 30 * rotations[2].y + 16 * rotations[1].y - rotations[0].y) / (12 * Mathf.Pow(Time.deltaTime, 2)),
    //         (-rotations[4].z + 16 * rotations[3].z - 30 * rotations[2].z + 16 * rotations[1].z - rotations[0].z) / (12 * Mathf.Pow(Time.deltaTime, 2))
    //     );
    // }


}
