using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using UnityEngine.UI;
using System.Globalization;
using j_utils;
using Unity.Mathematics;
using System.Runtime.InteropServices.WindowsRuntime;



public class MainDataCollection_markingGesture : MonoBehaviour
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

    public enum GestureType
    {
        Nod, // First option
        Roll  // Second option
    }

    // Now, declare a public variable of your Enum type.
    // This will appear as a dropdown in the Unity Inspector.
    public GestureType gestureType;



    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
        

        //read stim-in-HMD Positions
        var tarPosPath_2dGrid = Path.Combine(Application.dataPath, "Resources", "2dGrid", stimPosCsvName + ".csv");
        var tarPosPath_1dGrid = Path.Combine(Application.dataPath, "Resources", stimPosCsvName + ".csv");

        if (gestureType == GestureType.Roll)
        {
            tarPosPath_2dGrid = Path.Combine(Application.dataPath, "Resources", "roll_targets", stimPosCsvName + ".csv");
        }

        var initialTargets = new List<Vector3>
        {
            eyeHead_utils.ficks2vec(0, 0),
            eyeHead_utils.ficks2vec(0, 15),
            eyeHead_utils.ficks2vec(0, -15),
            eyeHead_utils.ficks2vec(15, 0),
            eyeHead_utils.ficks2vec(15, 15),
            eyeHead_utils.ficks2vec(15, -15),
            eyeHead_utils.ficks2vec(-15, 0),
            eyeHead_utils.ficks2vec(-15, 15),
            eyeHead_utils.ficks2vec(-15, -15),
        };
        
        

        List<Vector3> stimInHmdPosList_2d = new List<Vector3>();
        StreamReader reader = new StreamReader(@tarPosPath_2dGrid);
        stimInHmdPosList = CSVRead2Vector3List(stimInHmdPosList, reader).GetRange(0, 60);

        // stimInHmdPosList = CSVRead2Vector3List(stimInHmdPosList, reader).GetRange(0, 600);

        if (gestureType == GestureType.Nod)
        {
            List<Vector3> stimInHmdPosList_1d = new List<Vector3>();
            reader = new StreamReader(@tarPosPath_1dGrid);
            stimInHmdPosList_1d = CSVRead2Vector3List(stimInHmdPosList_1d, reader).GetRange(0, 60);
            stimInHmdPosList.InsertRange(0, stimInHmdPosList_1d);
        }

        stimInHmdPosList = RemoveConsecutiveDuplicates(stimInHmdPosList);

        stimInHmdPosList.InsertRange(0, initialTargets);

        
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
        GameStateManager.Instance.overwriteValue = -1;
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
        }
        else
        {
            target.GetComponent<RawImage>().material.SetColor("_ColorInnerCircle", Color.black);
        }

        controller_utils.GetRightControllerTrackPadClicked(out controllerDown);
        float rollVelocity = GetRollVelocity(hmdRotation, hmdRotation_prev, xrCamera.transform);
        int rollDetected = DetectRollDirection(rollVelocity, rollVelocityThreshold);
        GameStateManager.Instance.rollDirection = rollDetected;

        if (gestureType==GestureType.Nod && controllerDown && headtracking.headVel_Pol < -2f)
        {
            Debug.LogWarning("hey!!1");
            GameStateManager.Instance.overwriteValue = 2;
        }
        else if (gestureType==GestureType.Roll && controllerDown && (rollDetected == -1 || rollDetected == 1))
        {
            GameStateManager.Instance.overwriteValue = 2;
        }

        if (!controllerDown && controllerDown_prev)
        {
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
                GameStateManager.Instance.overwriteValue = 0;
            }
        }

        controllerDown_prev = controllerDown;
        hmdRotation_prev = hmdRotation;

        gameStates.cursorInWorldPosition = cursor.transform.position;
        gameStates.cursorInHmdPosition = xrCamera.transform.InverseTransformPoint(cursor.transform.position);
        gameStates.targetInWorldPosition = target.transform.position;
        gameStates.targetInHmdPosition = xrCamera.transform.InverseTransformPoint(target.transform.position);
        gameStates.cursorTargetAngle = cursorTargetAngle;
        gameStates.trackPadClicked = controllerDown;
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
        // isRefineTarget = random.Next(2) == 0;
        isRefineTarget = true;
        if (isRefineTarget)
        {
            gameStates.targetType = "refine";
            cursorTargetAngleThreshold = 0.32f;
            artificialGazeTargetOffsetDeg = -1 * UnityEngine.Random.Range(2.5f, 5.0f); ;
            gameStates.artificialGazeTargetOffsetDeg = artificialGazeTargetOffsetDeg;
            if (gestureType == GestureType.Nod)
            {
                GameStateManager.Instance.CursorOffsetQuaternion = Quaternion.Euler(artificialGazeTargetOffsetDeg, 0f, 0f);
            }
            else if (gestureType == GestureType.Roll)
            {
                GameStateManager.Instance.CursorOffsetQuaternion = GetRandomDiagonalRotation(artificialGazeTargetOffsetDeg);
            }

            target.GetComponent<RawImage>().material.SetFloat("_RadiusInnerCircle", 0.125f / 2f);
        }
        else
        {
            gameStates.targetType = "nonRefine";
            cursorTargetAngleThreshold = 0.65f * 4;
            artificialGazeTargetOffsetDeg = 0;
            gameStates.artificialGazeTargetOffsetDeg = artificialGazeTargetOffsetDeg;
            GameStateManager.Instance.CursorOffsetQuaternion = Quaternion.Euler(artificialGazeTargetOffsetDeg, 0f, 0f);
            target.GetComponent<RawImage>().material.SetFloat("_RadiusInnerCircle", 0.125f * 4);
        }
        // gameStates.targetType = isRefineTarget ? "refine" : "nod";
        Debug.Log("targetNumber: " + targetCount + ", s: " + s);
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

    
    public Quaternion GetRandomDiagonalRotation(float angle)
    {
        // Define the four normalized diagonal axes
        Vector3[] diagonalAxes = new Vector3[]
        {
            new Vector3(1f, 0f, 1f).normalized,  // 0: North-East
            new Vector3(-1f, 0f, 1f).normalized, // 1: North-West
            new Vector3(1f, 0f, -1f).normalized, // 2: South-East
            new Vector3(-1f, 0f, -1f).normalized // 3: South-West
        };

        // Get a random integer between 0 and 3 (inclusive)
        int randomIndex = UnityEngine.Random.Range(0, diagonalAxes.Length); // Random.Range(min, max) for int is exclusive of max

        // Select the corresponding diagonal axis
        Vector3 selectedAxis = diagonalAxes[randomIndex];

        // Create the Quaternion rotation around the selected axis
        Quaternion rotation = Quaternion.AngleAxis(angle, selectedAxis);

        return rotation;
    }
 
}
