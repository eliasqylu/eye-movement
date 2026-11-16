using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System.Globalization;
using j_utils;
using Unity.Mathematics;
using System.Runtime.InteropServices.WindowsRuntime;



public class MainTestBimodalGaze : MonoBehaviour
{
    public GameObject cursor;
    public GameObject target;
    private Camera xrCamera;
    public GameObject xrCam0_proxy;
    public int seqIndex = 0; //this is the 0th target sequence
    public int seqSize = 25; //there are 25 targets per sequence
    public string stimPosCsvName;
    private List<Vector3> stimInHmdPosList = new List<Vector3>();
    Vector3 nativeEyeTargetOffset = new Vector3(0, 0, 0); //native eye-target offset at end of gazeshift
    private bool isNewTarget = true;
    bool isRefineTarget = false;
    float artificialGazeTargetOffsetDeg;
    private System.Random random = new System.Random();
    private int targetCount = 0;
    private int s = 0;
    private bool sessionCompleted;
    private bool isOnset;
    float cursorTargetAngleThreshold;
    Quaternion gazeOffsetQuaternion;
    bool controllerDown;
    bool controllerDown_prev;
    Quaternion hmdRotation;
    Quaternion hmdRotation_prev;
    public float rollVelocityThreshold;
    public AudioSource errorSoundEffect;

    public float dwellThreshold = 0.8f;
    float timeInDwell = 0;
    float timeAtTargetOnset;


    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
        

        //read stim-in-HMD Positions
        var tarPosPath = Path.Combine(Application.dataPath, "Resources", stimPosCsvName + ".csv");


        var initialTargets = new List<Vector3>
        {
            eyeHead_utils.ficks2vec(0, 0),
            eyeHead_utils.ficks2vec(15, 15),
            eyeHead_utils.ficks2vec(-15, -15),
            eyeHead_utils.ficks2vec(-15, 15),
            eyeHead_utils.ficks2vec(15, -15)
        };
        
        StreamReader reader = new StreamReader(@tarPosPath);
        stimInHmdPosList = CSVRead2Vector3List(stimInHmdPosList, reader).GetRange(0, 600);

        stimInHmdPosList.InsertRange(0, initialTargets);

    
        stimInHmdPosList = RemoveConsecutiveDuplicates(stimInHmdPosList);
        if (stimInHmdPosList.Count < seqSize)
        {
            seqSize = stimInHmdPosList.Count;
            Debug.LogError("not enough non-consetuvely-duplicated target positions, new seqSize: " + seqSize);
        }

        //break a long list of stimInHmdPosList into parts to perform
        if ((seqIndex + 1) * seqSize <= stimInHmdPosList.Count)
        {
            if (seqIndex == 0)
            {
                stimInHmdPosList = stimInHmdPosList.GetRange(seqIndex * seqSize, seqSize);
            }
            else
            {
                stimInHmdPosList = stimInHmdPosList.GetRange(seqIndex * seqSize - 1, seqSize + 1);
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        GameStateManager.Instance.frameOfCentering = false;
        gameStates.trialIndex = s;
        if (sessionCompleted)
        {
            target.SetActive(false);
            Debug.LogError("sessionCompleted :)");
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            xrCam0_proxy.transform.position = xrCamera.transform.position;
            xrCam0_proxy.transform.rotation = xrCamera.transform.rotation;
            Vector3 stimInHmdPos = stimInHmdPosList[s];
            sprawnTarget(target, stimInHmdPos, xrCam0_proxy);
            GameStateManager.Instance.frameOfCentering = true; // Assuming GameStateManager exists
        }

        hmdRotation = xrCamera.transform.rotation;

        Vector3 targetInHmdDir = xrCamera.transform.InverseTransformPoint(target.transform.position);

        //calculate gaze-target angle
        float gazeTargetAngle = Vector3.Angle(
            eyetracking.combinedEyeLocalForward_processed,
            targetInHmdDir);

        float cursorTargetAngle = Vector3.Angle(cursor.transform.position - xrCamera.transform.position, target.transform.position - xrCamera.transform.position);

        if (cursorTargetAngle < cursorTargetAngleThreshold)
        {
            target.GetComponent<RawImage>().material.SetColor("_ColorInnerCircle", Color.cyan);
            timeInDwell += Time.deltaTime;
        }
        else
        {
            timeInDwell = 0;
            target.GetComponent<RawImage>().material.SetColor("_ColorInnerCircle", Color.black);
        }

        
        float rollVelocity = GetRollVelocity(hmdRotation, hmdRotation_prev, xrCamera.transform);
        int rollDetected = DetectRollDirection(rollVelocity, rollVelocityThreshold);
        GameStateManager.Instance.rollDirection = rollDetected;

        // controller_utils.GetRightControllerTrackPadClicked(out controllerDown);
        // if (controllerDown)
        // {
        //     GameStateManager.Instance.controllerState = 1;
        // }
        // else
        // {
        //     GameStateManager.Instance.controllerState = 0;
        // }

        // if (!controllerDown && controllerDown_prev)
        bool timedOut = Time.time - timeAtTargetOnset > 5;
        if (timeInDwell >= dwellThreshold || timedOut)
        {
            if (timedOut) errorSoundEffect.Play();

            s += 1;
            if (s >= seqSize)
            {
                sessionCompleted = true;
                cursor.SetActive(false);
            }
            else
            {
                Vector3 stimInHmdPos = stimInHmdPosList[s];
                sprawnTarget(target, stimInHmdPos, xrCam0_proxy);
            }
        }
        
        // controllerDown_prev = controllerDown;
        hmdRotation_prev = hmdRotation;

        gameStates.cursorInWorldPosition = cursor.transform.position;
        gameStates.cursorInHmdPosition = xrCamera.transform.InverseTransformPoint(cursor.transform.position);
        gameStates.targetInWorldPosition = target.transform.position;
        gameStates.targetInHmdPosition = xrCamera.transform.InverseTransformPoint(target.transform.position);
        gameStates.cursorTargetAngle = cursorTargetAngle;
        // gameStates.trackPadClicked = controllerDown;
        GameStateManager.Instance.hmdRollVelocity = rollVelocity;
        GameStateManager.Instance.hmdAngularDeltaFromNeutral = GetLocalAngularDeltaVectorFromNeutral(hmdRotation, xrCamera.transform);


    }

    private static float NormalizeAngle(float angle)
    {
        angle = angle % 360;
        if (angle > 180)
            angle -= 360;
        else if (angle < -180)
            angle += 360;
        return angle;
    }

    List<Vector3> CSVRead2Vector3List(List<Vector3> stimSP, StreamReader reader)
    {
        using (reader)
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                var values = line.Split(',');

                stimSP.Add(new Vector3(
                    float.Parse(values[0], CultureInfo.InvariantCulture),
                    float.Parse(values[1], CultureInfo.InvariantCulture),
                    float.Parse(values[2], CultureInfo.InvariantCulture)));
            }
        }
        return stimSP;
    }

    void sprawnTarget(GameObject target, Vector3 stimInHmdPos, GameObject xrCam)
    {
        target.transform.position = xrCam.transform.TransformPoint(stimInHmdPos);
        target.transform.LookAt(xrCam.transform);
        target.transform.Rotate(new Vector3(0, 180, 0));
        isNewTarget = true;
        nativeEyeTargetOffset = new Vector3(0, 0, 0);
        random = new System.Random();

        gameStates.targetType = "refine";
        cursorTargetAngleThreshold = 0.32f;
        target.GetComponent<RawImage>().material.SetFloat("_RadiusInnerCircle", 0.125f / 2f);

        Debug.Log("targetNumber: " + targetCount + ", s: " + s);
        timeAtTargetOnset = Time.time;
    }

    List<Vector3> RemoveConsecutiveDuplicates(List<Vector3> list)
    {
        if (list == null || list.Count <= 1)
        {
            return list; // No duplicates or empty list
        }

        List<Vector3> result = new List<Vector3>();
        result.Add(list[0]); // Add the first element

        for (int i = 1; i < list.Count; i++)
        {
            if (list[i] != result[result.Count - 1])
            {
                result.Add(list[i]);
            }
        }

        return result;
    }

    public static float GetRollVelocity(Quaternion currentRotation, Quaternion previousRotation, Transform trackedTransform)
    {

        // Calculate the rotational difference between frames
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);

        // Convert the delta rotation to an angle and axis
        // 'angleInDegrees' is the total magnitude of rotation, 'rotationAxis' is the axis of that rotation in world space.
        deltaRotation.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);

        // Transform the world-space rotation axis into the local space of the tracked transform.
        // Then multiply by the angle and divide by Time.deltaTime to get the local angular velocity vector.
        Vector3 localAngularDeltaVector = trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees;
        GameStateManager.Instance.hmdAngularDeltaVector = localAngularDeltaVector;
        Vector3 localAngularVelocityVector = (trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees) / Time.deltaTime;
        // The Z-component of the local angular velocity vector represents the roll velocity.
        float currentRollVelocity = localAngularVelocityVector.z;

        return currentRollVelocity;
    }

    public Vector3 GetLocalAngularDeltaVectorFromNeutral(Quaternion currentRotation, Transform trackedTransform)
    {
        Quaternion rotaionDelta = currentRotation * xrCam0_proxy.transform.rotation;
        rotaionDelta.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
        Vector3 localAngularDeltaVector = trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees;
        return localAngularDeltaVector;
    }     


    public int DetectRollDirection(float currentRollVelocity, float rollVelocityThreshold)
    {
        if (Mathf.Abs(currentRollVelocity) > rollVelocityThreshold)
        {
            if (currentRollVelocity > 0) // Positive local Z rotation typically corresponds to rolling right (clockwise from behind)
            {
                return 1; // Rolling right
            }
            else // Negative local Z rotation typically corresponds to rolling left (counter-clockwise from behind)
            {
                return -1; // Rolling left
            }
        }
        else
        {
            return 0; // No significant roll velocity
        }
    }

    
    public Quaternion GetRandomCompassRotation(float angle)
    {
        // Define all eight normalized compass axes (cardinal and diagonal)
        Vector3[] compassAxes = new Vector3[]
        {
            // Cardinal Directions
            new Vector3(0f, 0f, 1f).normalized,  // 0: North
            new Vector3(0f, 0f, -1f).normalized, // 1: South
            new Vector3(1f, 0f, 0f).normalized,  // 2: East
            new Vector3(-1f, 0f, 0f).normalized, // 3: West

            // Diagonal Directions
            new Vector3(1f, 0f, 1f).normalized,  // 4: North-East
            new Vector3(-1f, 0f, 1f).normalized, // 5: North-West
            new Vector3(1f, 0f, -1f).normalized, // 6: South-East
            new Vector3(-1f, 0f, -1f).normalized // 7: South-West
        };

        // Get a random integer between 0 and 7 (inclusive)
        int randomIndex = UnityEngine.Random.Range(0, compassAxes.Length); 

        // Select the corresponding axis
        Vector3 selectedAxis = compassAxes[randomIndex];

        // Create the Quaternion rotation around the selected axis
        Quaternion rotation = Quaternion.AngleAxis(angle, selectedAxis);

        return rotation;
    }
 
}
