

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ViveSR.anipal.Eye;
using j_utils;
using Unity.VisualScripting;

public class eyetracking : MonoBehaviour
{
    public bool useRawGaze;
    public GameObject debugObject;
    public GameObject gazeCursor;
    public bool useAdaptiveRms = false;
    public float dispersionThresholdValue = 1.0f;
    public int rmsWinLen = 1800; //around 20s of time (90hz) for calculating rms for idt fixation detection
    public bool useCentroidForFixation = true;
    public static bool isEyeInFixation = false;
    public static float eyeVel_Az;
    public static float eyeVel_Pol;
    public static float eyeInWorldVel_Az;
    public static float eyeInWorldVel_Pol;

    public static Vector3 rawGazeVector = new Vector3();
    public static Vector3 combinedEyeLocalForward_processed = new Vector3();
    public static VerboseData eyeData;
    public static bool readVerboseDataSuccess;

    Vector3 combinedEyeLocalForward;
    public static Vector3 leftEyeLocalForward;
    public static Vector3 rightEyeLocalForward;
    public static bool isLeftEyeGazeRayValid;
    public static bool isRightEyeGazeRayValid;
    public static Vector3 leftEyeLocalOrigin;
    public static Vector3 rightEyeLocalOrigin;
    Vector3 combinedEyeWorldForward;
    Queue<Vector3> combinedEyeLocalForward_raw = new Queue<Vector3>();
    int maxSize_combinedEyeLocalForward_raw = 10;
    // The maximum number of gaze points to store in the history.
    private readonly int maxGazeHistorySize = 1000;

    // Use a Queue to efficiently store the recent gaze points.
    private Queue<Vector3> combinedEyeLocalForward_processed_history = new Queue<Vector3>();
    private Camera xrCamera;

    //eye fixation
    // Parameters for fixation detection
    public float dispersionMultiplier = 1.5f; // Dispersion threshold is this multiplier * gaze RMS
    public float minimumFixationDuration = 0.08f; // Minimum fixation duration in seconds

    public static Vector2 eyeAzPol;

    private List<Vector2> angleHistory = new List<Vector2>(); // To store x, y Euler angles (x, y)
    private List<float> Azs = new List<float>();
    private List<float> Pols = new List<float>();
    private List<float> Azs_world = new List<float>();
    private List<float> Pols_world = new List<float>();

    private float fixationStartTime = -1f; // Time when fixation started
    private bool isFixating = false; // Whether the head is currently in fixation

    private IDTSession idtSession = new IDTSession();
    private List<Vector3> gazePositionHistory = new List<Vector3>();
    public static Vector3 currentCursorCentroid;
    public static List<Vector3> previousGazeCursorCentroids = new List<Vector3>();
    public static List<float> previousGazeFixationEndTime = new List<float>();

    bool isFixationDetected_prev;
    bool isFixationDetected;

    //smoothing compensation
    private Queue<Quaternion> hmdRotationHistory = new Queue<Quaternion>();
    public int historyBufferSize = 8; // Corresponding to your "8th previous quaternion"
    public bool compensateHeadSmoothing;

    private List<Vector2> headAzPolList = new List<Vector2>();
    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
        InitializeHmdQuaternionQueue(xrCamera.transform.rotation);
    }

    // Update is called once per frame
    void Update()
    {
        // --- Data acquisition from the hardware-agnostic provider ---
        IEyeTrackerProvider provider = EyeTrackingBootstrap.Active;
        readVerboseDataSuccess = provider.IsTracking;

        isLeftEyeGazeRayValid = provider.TryGetGazeRay(EyeSide.Left, out leftEyeLocalOrigin, out leftEyeLocalForward);
        isRightEyeGazeRayValid = provider.TryGetGazeRay(EyeSide.Right, out rightEyeLocalOrigin, out rightEyeLocalForward);

        eyeData = BuildVerboseData(provider);

        if (isLeftEyeGazeRayValid == false && isRightEyeGazeRayValid == false)
        {
            Debug.LogError("no both eyes");
            // gameStates.isEyeFixation = false;
            return;
        }
        if (isLeftEyeGazeRayValid && isRightEyeGazeRayValid)
        {
            combinedEyeLocalForward = 0.5f * (leftEyeLocalForward + rightEyeLocalForward);
        }
        else if (isLeftEyeGazeRayValid && !isRightEyeGazeRayValid)
        {
            Debug.LogError("no right eye");
            combinedEyeLocalForward = leftEyeLocalForward;
        }
        else if (!isLeftEyeGazeRayValid && isRightEyeGazeRayValid)
        {
            Debug.LogError("no left eye");
            combinedEyeLocalForward = rightEyeLocalForward;
        }


        if (compensateHeadSmoothing)
        {
            bool headMoved = new Vector2(headtracking.headVel_Az, headtracking.headVel_Pol).magnitude > 10;
            // combinedEyeLocalForward = xrCamera.transform.TransformDirection(combinedEyeLocalForward);
            // combinedEyeLocalForward = CompensateGazeSmoothing(combinedEyeLocalForward, xrCamera.transform.rotation);
            // combinedEyeLocalForward = xrCamera.transform.InverseTransformDirection(combinedEyeLocalForward);

            headAzPolList.Add(headtracking.hmdAzPol);
            if (headAzPolList.Count > historyBufferSize)
            {
                headAzPolList.RemoveAt(0);
            }
            if (headMoved)
            {
                Vector2 gazeAzPol = eyeHead_utils.vec2ficks(combinedEyeLocalForward.x, combinedEyeLocalForward.y, combinedEyeLocalForward.z);
                gazeAzPol -= headAzPolList[headAzPolList.Count - 1] - headAzPolList[0];
                combinedEyeLocalForward = eyeHead_utils.ficks2vec(gazeAzPol.x, gazeAzPol.y);
            }

        }
        rawGazeVector = combinedEyeLocalForward;

        // Debug.LogError("combinedEyeLocalForward: " + combinedEyeLocalForward);
        combinedEyeWorldForward = xrCamera.transform.TransformDirection(combinedEyeLocalForward);
        combinedEyeLocalForward_raw.Enqueue(combinedEyeLocalForward);
        if (combinedEyeLocalForward_raw.Count > maxSize_combinedEyeLocalForward_raw)
        {
            combinedEyeLocalForward_raw.Dequeue();
        }
        combinedEyeLocalForward_processed = eyeHead_utils.GetMeanOfQueue(combinedEyeLocalForward_raw);
        gameStates.combinedEyeLocalForward = combinedEyeLocalForward_processed;
        AddGazePointToHistory(combinedEyeLocalForward_processed);

        eyeAzPol = eyeHead_utils.vec2ficks(combinedEyeLocalForward.x, combinedEyeLocalForward.y, combinedEyeLocalForward.z);
        eyeAzPol = eyeHead_utils.vec2ficks(combinedEyeLocalForward_processed.x, combinedEyeLocalForward_processed.y, combinedEyeLocalForward_processed.z);

        Vector3 combinedEyeInWorldForward_processed = xrCamera.transform.TransformDirection(combinedEyeLocalForward_processed);
        if (useRawGaze)
        {
            combinedEyeInWorldForward_processed = rawGazeVector;
        }

        Vector2 eyeInWorldAzPol = eyeHead_utils.vec2ficks(combinedEyeInWorldForward_processed.x, combinedEyeInWorldForward_processed.y, combinedEyeInWorldForward_processed.z);

        if (debugObject != null && debugObject.activeSelf)
        {
            debugObject.transform.position = xrCamera.transform.TransformPoint(combinedEyeLocalForward);
            debugObject.transform.LookAt(xrCamera.transform);
            debugObject.transform.Rotate(0, 180, 0);
        }


        Azs.Add(eyeAzPol.x);
        Pols.Add(eyeAzPol.y);
        if (Azs.Count > rmsWinLen)
        {
            Azs.RemoveAt(0);
            Pols.RemoveAt(0);
        }
        eyeVel_Az = eyeHead_utils.Compute5ptVelocity(Azs);
        eyeVel_Pol = eyeHead_utils.Compute5ptVelocity(Pols);

        Azs_world.Add(eyeInWorldAzPol.x);
        Pols_world.Add(eyeInWorldAzPol.y);
        if (Azs_world.Count > rmsWinLen)
        {
            Azs_world.RemoveAt(0);
            Pols_world.RemoveAt(0);
        }
        eyeInWorldVel_Az = eyeHead_utils.Compute5ptVelocity(Azs_world);
        eyeInWorldVel_Pol = eyeHead_utils.Compute5ptVelocity(Pols_world);

        bool detectFixation = true;
        if (detectFixation)
        {
            float dispersionThreshold = dispersionThresholdValue;
            if (useAdaptiveRms)
            {
                float rms = eyeHead_utils.RMS(Azs, Pols);
                dispersionThreshold = dispersionMultiplier * rms;
            }

            // Debug.LogWarning("rms: " + rms);
            // Debug.LogWarning("dispersionThreshold: " + dispersionThreshold);
            string dispersionType = "disp";
            isFixationDetected = idtSession.IDT(eyeAzPol.x, eyeAzPol.y, dispersionThreshold, minimumFixationDuration, dispersionType);
            // if (isFixationDetected){
            //     gazeCursor.GetComponent<Renderer>().material.SetColor("_Color", Color.red);
            //     gameStates.isEyeFixation=true;
            // }
            // else{
            //     gazeCursor.GetComponent<Renderer>().material.SetColor("_Color", Color.white);
            //     gameStates.isEyeFixation=false;
            // }
            isEyeInFixation = isFixationDetected;

            //get cursor centroid
            if (idtSession.IsCurrentlyFixating())
            {
                gazePositionHistory.Add(combinedEyeLocalForward_processed);
                if (isFixationDetected)
                {
                    currentCursorCentroid = useCentroidForFixation
                        ? CalculateCentroid(gazePositionHistory)
                        : combinedEyeLocalForward_processed;

                    // debugObject.transform.position = currentCursorCentroid;
                }
            }
            else if (isFixationDetected_prev)
            {
                previousGazeCursorCentroids.Add(currentCursorCentroid);
                if (previousGazeCursorCentroids.Count > 1) previousGazeCursorCentroids.RemoveAt(0);

                // debugObject.transform.position = previousGazeCursorCentroids[previousGazeCursorCentroids.Count-1];

                previousGazeFixationEndTime.Add(Time.time);
                if (previousGazeFixationEndTime.Count > 1) previousGazeFixationEndTime.RemoveAt(0);

                gazePositionHistory.Clear();
                // currentCursorCentroid = Vector3.zero;
            }

            isFixationDetected_prev = isFixationDetected;
        }



    }

    private static VerboseData BuildVerboseData(IEyeTrackerProvider provider)
    {
        var data = new VerboseData();
        data.left = BuildSingleEye(provider, EyeSide.Left);
        data.right = BuildSingleEye(provider, EyeSide.Right);
        return data;
    }

    private static SingleEyeData BuildSingleEye(IEyeTrackerProvider provider, EyeSide side)
    {
        provider.TryGetGazeRay(side, out Vector3 origin, out Vector3 direction);
        return new SingleEyeData
        {
            eye_data_validata_bit_mask = ulong.MaxValue,
            gaze_origin_mm = origin * 1000f, // provider uses metres, SRanipal uses mm
            gaze_direction_normalized = direction,
            pupil_diameter_mm = 3f,
            eye_openness = provider.GetEyeOpenness(side),
            pupil_position_in_sensor_area = Vector2.zero
        };
    }

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

    public void InitializeHmdQuaternionQueue(Quaternion initialHmdRotation)
    {
        hmdRotationHistory.Clear();
        for (int i = 0; i < historyBufferSize; i++)
        {
            hmdRotationHistory.Enqueue(initialHmdRotation);
        }
    }

    public Vector3 CompensateGazeSmoothing(Vector3 currentGazeVector, Quaternion currentHmdRotation)
    {
        // 1. Get the oldest stored HMD rotation
        Quaternion oldestHmdRotation = hmdRotationHistory.Peek(); // Get the oldest without removing

        // 2. Calculate the rotation difference from the oldest to the current HMD rotation
        // The rotation from oldest to current: (current * Inverse(oldest))
        Quaternion rotationDifference = currentHmdRotation * Quaternion.Inverse(oldestHmdRotation);

        // 3. Apply the *inverse* of this rotation difference to the current gaze vector.
        // This is because if the head rotated by 'R', to cancel that out, you apply 'R_inverse'.
        Vector3 compensatedGazeVector = Quaternion.Inverse(rotationDifference) * currentGazeVector;

        // 4. Update the history buffer: remove the oldest and add the current
        hmdRotationHistory.Dequeue();
        hmdRotationHistory.Enqueue(currentHmdRotation);

        return compensatedGazeVector;
    }

    public void AddGazePointToHistory(Vector3 newPoint)
    {
        // Add the new point to the back of the queue.
        combinedEyeLocalForward_processed_history.Enqueue(newPoint);

        // If the queue exceeds the max size, remove the oldest item from the front.
        // This is a very fast O(1) operation.
        if (combinedEyeLocalForward_processed_history.Count > maxGazeHistorySize)
        {
            combinedEyeLocalForward_processed_history.Dequeue();
        }
    }

    /// <summary>
    /// Retrieves the last N items from the gaze history.
    /// </summary>
    /// <param name="count">The number of items to return.</param>
    /// <returns>A new List containing the last N items.</returns>
    public List<Vector3> GetLastGazePoints(int count)
    {
        // You can still use TakeLast() directly on a Queue.
        // It correctly returns the most recently added items.
        return combinedEyeLocalForward_processed_history.TakeLast(count).ToList();
    }

    // // Function to detect fixation based on current x and y Euler angles
    // public bool IDT(float currentXAngle, float currentYAngle, float thresh_disp, float thresh_dur, string dispersionType)
    // {
    //     // Add current angle to history
    //     angleHistory.Add(new Vector2(currentXAngle, currentYAngle));

    //     // Keep the history within a reasonable size (for example, last 10 frames)
    //     if (angleHistory.Count < 7)
    //     {
    //         return false;
    //     }

    //     // Calculate dispersion (how much the angles are spreading)
    //     float dispersion = CalculateDispersion(dispersionType);
    //     Debug.LogWarning("dispersion: " + dispersion);

    //     // Check if the dispersion is below the threshold and duration is sufficient for a fixation
    //     if (dispersion < thresh_disp)
    //     {
    //         if (!isFixating)
    //         {
    //             // If we're starting a fixation, note the time
    //             fixationStartTime = Time.time;
    //             isFixating = true;
    //         }
    //         else
    //         {
    //             // If the fixation has lasted long enough
    //             if (Time.time - fixationStartTime >= thresh_dur)
    //             {
    //                 Debug.Log("eye Fixation Detected!");
    //                 return true; // Fixation detected
    //             }
    //         }
    //     }
    //     else
    //     {
    //         // If dispersion is above the threshold, reset the fixation state
    //         if (isFixating){
    //             angleHistory = new List<Vector2>
    //             {
    //                 new Vector2(currentXAngle, currentYAngle)
    //             };
    //         }
    //         else{
    //             angleHistory.RemoveAt(0);
    //         }
    //         isFixating = false;
    //         fixationStartTime = -1f;
    //     }
    //     return false; // No fixation detected
    // }

    // float CalculateDispersion(string dispersionType){
    //     if (dispersionType == "disp"){
    //         if (angleHistory.Count <= 1) return 0f;
    //         else{
    //             float maxX = float.MinValue, minX = float.MaxValue;
    //             float maxY = float.MinValue, minY = float.MaxValue;

    //             // Iterate through angle history to find the max and min values for x and y
    //             foreach (var angle in angleHistory)
    //             {
    //                 maxX = Mathf.Max(maxX, angle.x);
    //                 minX = Mathf.Min(minX, angle.x);
    //                 maxY = Mathf.Max(maxY, angle.y);
    //                 minY = Mathf.Min(minY, angle.y);
    //             }

    //             // Calculate dispersion as the sum of (max - min) for both x and y
    //             float dispersion = (maxX - minX) + (maxY - minY);
    //             return dispersion;
    //         }
    //     }
    //     else if (dispersionType == "var"){
    //         if (angleHistory.Count <= 1) return 0f;

    //         float xVariance = 0f, yVariance = 0f;
    //         int count = angleHistory.Count;

    //         // Calculate the mean (average) x and y angles
    //         float meanX = 0f, meanY = 0f;
    //         foreach (var angle in angleHistory)
    //         {
    //             meanX += angle.x;
    //             meanY += angle.y;
    //         }
    //         meanX /= count;
    //         meanY /= count;

    //         // Calculate the variance of x and y angles
    //         foreach (var angle in angleHistory)
    //         {
    //             xVariance += Mathf.Pow(angle.x - meanX, 2);
    //             yVariance += Mathf.Pow(angle.y - meanY, 2);
    //         }

    //         xVariance /= count;
    //         yVariance /= count;

    //         // Dispersion is the square root of the sum of the variances (this is essentially the standard deviation)
    //         float dispersion = Mathf.Sqrt(xVariance + yVariance);
    //         return dispersion;
    //     }
    //     else{
    //         Debug.LogError("dispersion type is:" + dispersionType + " should be either disp or var");
    //         return 0f;
    //     }
    // }

    // public float RMS(List<float> x, List<float> y)
    // {
    //     // Handle case when there are less than 2 elements in the arrays
    //     if (x.Count < 2)
    //     {
    //         return 0f;
    //     }

    //     float sumSquaredDiff = 0f;

    //     for (int i = 0; i < x.Count - 1; i++)
    //     {
    //         sumSquaredDiff += Mathf.Pow(x[i + 1] - x[i], 2);
    //         sumSquaredDiff += Mathf.Pow(y[i + 1] - y[i],2);
    //     }

    //    // Calculate the mean of the squared differences
    //     float meanSquaredDiff = sumSquaredDiff / (x.Count - 1);

    //     // Return the square root of the mean squared differences (RMS)
    //     return Mathf.Sqrt(meanSquaredDiff);
    // }

    // Vector3  GetMeanOfQueue(Queue<Vector3> items){
    //     Vector3 sum = new Vector3(0f,0f,0f);
    //     foreach (Vector3 item in items)
    //     {
    //         sum += item;
    //     }
    //     Vector3 mean = sum / items.Count;
    //     return mean;
    // }

    // private float Compute5ptVelocity(List<float> xs)
    // {
    //     int endInd = xs.Count-1;
    //     return (-xs[endInd] + 8 * xs[endInd-1] - 8 * xs[endInd-3] + xs[endInd-4]) / (12 * Time.deltaTime);
    // }
}
