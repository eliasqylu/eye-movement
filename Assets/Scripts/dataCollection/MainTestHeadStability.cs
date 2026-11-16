using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using j_utils;
using Unity.Mathematics;

public class MainTestHeadStability : MonoBehaviour
{
    public GameObject cursor;
    public GameObject target;
    private Camera xrCamera;
    public GameObject xrCam0_proxy;

    [Tooltip("The radius for the target's inner circle, used by the shader.")]
    public float targetSize = 0.0625f*2;

    private List<Vector3> stimInHmdPosList = new List<Vector3>();
    private int s = 0; // Current trial index
    private bool sessionCompleted;
    private float timeAtTargetOnset;

    float cursorTargetAngleThreshold;
    Quaternion hmdRotation;
    Quaternion hmdRotation_prev;
    public float rollVelocityThreshold;
    public AudioSource errorSoundEffect;

    public float dwellThreshold = 0.8f;
    float timeInDwell = 0;



    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
        
        GenerateCustomTargetSequence();
        cursor.transform.Find("gazeCircle").gameObject.SetActive(false);
        cursor.transform.Find("nodButton").gameObject.SetActive(true);

        // Hide target until the user starts the sequence
        target.SetActive(false);
    }

   
    void GenerateCustomTargetSequence()
    {
        stimInHmdPosList.Clear();
        float distance = 1.0f; // Targets are 1m away

        // A. Define the 9 unique target positions in degrees (azimuth, polar)
        var targetAngles = new List<(float azimuth, float polar)>
        {
            (0, 0),
            (0, 20),
            (0, -20),
            (20, 0),
            (-20, 0),
            (-20, 20),
            (-20, -20),
            (20, 20),
            (20, -20)
        };

        var targetPositions = new List<Vector3>();

        // B. Convert angular coordinates to 3D world positions
        foreach (var angles in targetAngles)
        {
            Vector3 targetDirection = eyeHead_utils.ficks2vec(angles.azimuth, angles.polar);
            targetPositions.Add(targetDirection * distance);
        }

        // C. Randomize the trial sequence and add it to the final list
        // A simple and effective way to shuffle is to order by a new random Guid.
        stimInHmdPosList.AddRange(targetPositions.OrderBy(pos => System.Guid.NewGuid()));
    }





    // Update is called once per frame
    void Update()
    {
        
        cursor.transform.position = xrCamera.transform.TransformPoint(Vector3.forward);
        cursor.transform.LookAt(xrCamera.transform);
        cursor.transform.Rotate(new Vector3(0, 180, 0));
       

        GameStateManager.Instance.frameOfCentering = false;
        gameStates.trialIndex = s;
        if (sessionCompleted)
        {
            if (target.activeSelf) target.SetActive(false);
            Debug.Log("Session Completed :)");
            return;
        }

        // Set reference camera and spawn the first target
        if (Input.GetKeyDown(KeyCode.Space) && s == 0)
        {
            xrCam0_proxy.transform.position = xrCamera.transform.position;
            xrCam0_proxy.transform.rotation = xrCamera.transform.rotation;

            if (s < stimInHmdPosList.Count)
            {
                Vector3 stimInHmdPos = stimInHmdPosList[s];
                sprawnTarget(target, stimInHmdPos, xrCam0_proxy);
                GameStateManager.Instance.frameOfCentering = true;
            }
        }

        // Do nothing if the task hasn't started yet
        if (!target.activeSelf) return;

        hmdRotation = xrCamera.transform.rotation;
        Vector3 targetInHmdDir = xrCamera.transform.InverseTransformPoint(target.transform.position);

        // Calculate cursor-target angle
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
        GameStateManager.Instance.rollDirection = DetectRollDirection(rollVelocity, rollVelocityThreshold);

        bool timedOut = Time.time - timeAtTargetOnset > 5;
        if (timeInDwell >= dwellThreshold || timedOut)
        {
            if (timedOut) errorSoundEffect.Play();

            s++;
            // Check for session completion against the total number of targets
            if (s >= stimInHmdPosList.Count)
            {
                sessionCompleted = true;
                cursor.SetActive(false);
            }
            else // Spawn next target
            {
                Vector3 stimInHmdPos = stimInHmdPosList[s];
                sprawnTarget(target, stimInHmdPos, xrCam0_proxy);
            }
        }

        hmdRotation_prev = hmdRotation;

        // Update Game States
        gameStates.cursorInWorldPosition = cursor.transform.position;
        gameStates.cursorInHmdPosition = xrCamera.transform.InverseTransformPoint(cursor.transform.position);
        gameStates.targetInWorldPosition = target.transform.position;
        gameStates.targetInHmdPosition = xrCamera.transform.InverseTransformPoint(target.transform.position);
        gameStates.cursorTargetAngle = cursorTargetAngle;
        GameStateManager.Instance.hmdRollVelocity = rollVelocity;
        GameStateManager.Instance.hmdAngularDeltaFromNeutral = GetLocalAngularDeltaVectorFromNeutral(hmdRotation, xrCamera.transform);
    }

    /// <summary>
    /// Spawns a target at the specified position relative to the reference camera.
    /// </summary>
    void sprawnTarget(GameObject target, Vector3 stimInHmdPos, GameObject xrCam)
    {
        if (!target.activeSelf) target.SetActive(true);

        target.transform.position = xrCam.transform.TransformPoint(stimInHmdPos);
        target.transform.LookAt(xrCam.transform);
        target.transform.Rotate(new Vector3(0, 180, 0));

        // Set target properties
        cursorTargetAngleThreshold = targetSize * 4;
        target.GetComponent<RawImage>().material.SetFloat("_RadiusInnerCircle", targetSize);

        Debug.Log("Spawning Target #" + s);
        timeAtTargetOnset = Time.time;
        timeInDwell = 0f;
    }

    // --- Utility Functions ---

    public static float GetRollVelocity(Quaternion currentRotation, Quaternion previousRotation, Transform trackedTransform)
    {
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
        deltaRotation.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
        Vector3 localAngularVelocityVector = (trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees) / Time.deltaTime;
        GameStateManager.Instance.hmdAngularDeltaVector = localAngularVelocityVector * Time.deltaTime;
        return localAngularVelocityVector.z;
    }

    public Vector3 GetLocalAngularDeltaVectorFromNeutral(Quaternion currentRotation, Transform trackedTransform)
    {
        Quaternion rotaionDelta = currentRotation * xrCam0_proxy.transform.rotation;
        rotaionDelta.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
        return trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees;
    }

    public int DetectRollDirection(float currentRollVelocity, float rollVelocityThreshold)
    {
        if (Mathf.Abs(currentRollVelocity) <= rollVelocityThreshold) return 0;
        return currentRollVelocity > 0 ? 1 : -1; // Right : Left
    }
}