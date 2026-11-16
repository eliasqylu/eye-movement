using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using j_utils; // Assuming this contains eyeHead_utils and other dependencies
using System;

public class drag_align : MonoBehaviour
{
    // Enum to manage the state of the experiment for a clearer workflow
    private enum ExperimentState { Idle, WaitingForStartSignal, Running, Completed }
    private ExperimentState _currentState = ExperimentState.Idle;

    [Header("Object References")]
    public GameObject gazeCursor;
    public GameObject headCursor;
    public GameObject progressBar;
    GameObject gazeCursorRing;
    Vector3 gazeCursorRingScale_orig;
    Material gazeCursorRingMaterial;
    Vector3 gazeVector;
    public bool freezeCursorForSelecting;
    public bool customRingAlpha = true;
    public bool hidGazeRing = true;
    public GameObject targetPrefab;
    public Camera xrCamera;
    public GameObject referenceTransform;
    public AudioSource errorSoundEffect;
    public AudioSource alertSoundEffect;

    [Header("Fitts' Law Settings")]
    [Tooltip("The angular amplitudes (in degrees) for each block of targets.")]
    public float[] fittsAmplitudes = { 10.0f, 30.0f };
    // NEW: Added target sizes in visual degrees
    [Tooltip("The target widths (visual angle in degrees) for each block.")]
    public float[] fittsTargetSizes = { 2.5f };
    public float dropZoneThreshold = 0.5f;

    private const float TrialTimeoutDuration = 10.0f;
    private const int TargetsPerBlock = 7;
    // NEW: Constants for calculating scale from visual angle
    private const float BaseTargetAngle = 5.0f;    // The visual angle (in deg) of the base prefab
    private const float BasePrefabScale = 0.0873f; // The localScale of the base prefab

    [Header("Head Movement Settings")]
    public float rollVelocityThreshold;

    // --- Private State Variables ---
    private List<GameObject> _instantiatedTargets = new List<GameObject>();

    // These lists define the sequence of trials by pointing to indices in _instantiatedTargets.
    private List<int> _object2DragIndices = new List<int>();
    private List<int> _dropzoneIndices = new List<int>();

    // These lists store the parameters for each trial, parallel to the index lists.
    private List<float> _targetSequenceSizes = new List<float>();
    private List<float> _targetSequenceAmplitudes = new List<float>();
    private List<Vector3> _initialTargetPositions = new List<Vector3>();

    // References to the currently active objects for this trial.
    GameObject thisObject2Drag;
    GameObject thisDropZone;
    private int _currentTrialIndex = 0;
    private int _currentBlockIndex = 0;
    private float _timeInDropZoneDwell = 0;
    private float _timeAtTargetOnset;
    private Quaternion _previousHmdRotation;
    bool isInDrag_prev;

    // --- Complex Gaze/Head Interaction State (Unchanged) ---
    bool freezeGaze;
    bool ringUpdated;
    float gazeRingSize = 4.5f;
    float gazeHeadAngle;
    float gazeHeadAngle_prev;
    float gazeRingHeadAng;
    float gazeRingHeadAng_prev;
    int headToGazePossibleFrames;
    float headGazeDistance_start;
    bool headInGazeRing;
    bool headInGazeRing_prev;
    bool lookForDwell;
    bool isHeadFixation_prev;
    float dwelledTime_head = -1f;
    public float dwellTimeThreshold = 0.1f;
    bool dragEnabled;
    public enum RingSegment { None, Selecting, Dragging }
    public RingSegment lastEnteredRingSegment = RingSegment.None;

    private static readonly Color DefaultColor = new Color(1, 1, 1, 0.1f);
    private static readonly Color HoverColor = Color.red;
    private static readonly Color ActiveColor = Color.white;
    private static readonly Color DropZoneColor = Color.blue;

    bool hasPickedup;

    // --- Start-signal targets (transient) ---
    private GameObject startWhiteTarget;
    private GameObject startBlueTarget;
    private const float startDistance = 1.0f;
    private const float startSelectionAngleDeg = 8.0f; // threshold for "selection" in start-signal phase
    private const float startDropThresholdDeg = 8.0f; // threshold for dropping to start blue
    Vector3 cursorOrignalScale = new Vector3();

    #region --- Unity Lifecycle & Setup (Unchanged) ---

    void Start()
    {
        fittsAmplitudes = fittsAmplitudes.Select(a => a * 0.5f).ToArray();
        if (xrCamera == null) xrCamera = Camera.main;
        if (gazeCursor != null)
        {
            Transform ringTransform = gazeCursor.transform.Find("eyeGestureControl");
            if (ringTransform != null)
            {
                gazeCursorRing = ringTransform.gameObject;
                gazeCursorRingScale_orig = gazeCursorRing.transform.localScale;
                Renderer ringRenderer = gazeCursorRing.GetComponent<Renderer>();
                if (ringRenderer != null) gazeCursorRingMaterial = ringRenderer.material;
                else Debug.LogError("GazeCursorRing (eyeGestureControl) does not have a Renderer component.", this);
            }
            else Debug.LogError("Could not find 'eyeGestureControl' child object under GazeCursor.", this);
        }
        else Debug.LogError("Gaze Cursor GameObject is not assigned in the inspector.", this);

        headCursor.GetComponent<MeshRenderer>().enabled = true;
        cursorOrignalScale = headCursor.transform.localScale;
        SetupExperiment();
    }

    void Update()
    {
        gameStates.failureMode = "";
        gameStates.targetSize = 0;
        gameStates.targetAmp = 0;
        headCursor.transform.LookAt(xrCamera.transform);
        headCursor.transform.Rotate(new Vector3(0, 180, 0));
        switch (_currentState)
        {
            case ExperimentState.Idle:
                HandleIdleState();
                break;
            case ExperimentState.WaitingForStartSignal:
                HandleWaitingForStartSignalState();
                break;
            case ExperimentState.Running:
                targetPrefab.SetActive(false);
                HandleRunningState();
                break;
            case ExperimentState.Completed:
                gazeCursor.SetActive(false);
                break;
        }
    }

    #endregion

    #region --- Core Interaction Logic (Refactored) ---

    private void HandleIdleState()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            referenceTransform.transform.SetPositionAndRotation(xrCamera.transform.position, xrCamera.transform.rotation);
            // Move to start-signal waiting state
            EnterWaitingForStartSignal();
        }
    }

    private void EnterWaitingForStartSignal()
    {
        _currentState = ExperimentState.WaitingForStartSignal;
        SpawnStartSignalTargets();
        // Ensure not interfering with any main targets
        hasPickedup = false;
        resetDragState();
        Debug.Log("Entered WaitingForStartSignal. Please drag/select the white start object onto the blue drop target to begin.");
    }

    private void HandleWaitingForStartSignalState()
    {
        // Use same core interaction as Running but routed to start targets
        GameStateManager.Instance.overwriteValue = -1;

        // Guarantee start targets exist
        if (startWhiteTarget == null || startBlueTarget == null)
        {
            Debug.LogWarning("Start-signal targets missing. Re-spawning.");
            SpawnStartSignalTargets();
        }

        // Run common interaction frame using active start targets
        bool justSelected, justStartedDrag, justReleasedDrag;
        float cursor2ObjAngle, cursor2DropAngle;
        float roll = RunCoreInteractionFrame(out justSelected, out justStartedDrag, out justReleasedDrag, out cursor2ObjAngle, out cursor2DropAngle, startWhiteTarget, startBlueTarget, startSelectionAngleDeg, startDropThresholdDeg);

        // Evaluate start-signal success conditions:
        // - selected the white start object (justSelected)
        // - OR drag+release success: justReleasedDrag && cursor2DropAngle <= startDropThresholdDeg
        bool startSelectionFailed = false;
        bool startPickupFailed = justStartedDrag && (cursor2ObjAngle > startSelectionAngleDeg);
        bool startDropSuccess = justReleasedDrag && (cursor2DropAngle <= startDropThresholdDeg);
        bool startSelectionSuccess = justSelected;

        // if (hasPickedup && dragEnabled && startWhiteTarget!=null && _currentState == ExperimentState.WaitingForStartSignal)
        // {
        //     // Only move the real trial object while running (start-signal object movement is allowed above if desired)
        //     startWhiteTarget.transform.position = headCursor.transform.TransformPoint(0, 0, 0.01f);
        //     startWhiteTarget.transform.LookAt(xrCamera.transform);
        //     startWhiteTarget.transform.Rotate(new Vector3(0, 180, 0));
        // }

        if (startPickupFailed || startSelectionFailed)
        {
            errorSoundEffect.Play();
            gameStates.failureMode = "startSignal_failed";
            // reset start-signal objects to their original positions
            ResetStartSignalPositions();
        }
        else if (startDropSuccess || startSelectionSuccess)
        {
            alertSoundEffect.Play();
            // Clear start targets and start the main experiment
            ClearStartSignalTargets();
            StartExperiment();
        }
        // else keep waiting in this state until a success or failure is triggered or user times out (we keep it simple)
    }

    private void HandleRunningState()
    {
        GameStateManager.Instance.overwriteValue = -1;
        if (thisObject2Drag != null)
        {
            thisObject2Drag.transform.LookAt(xrCamera.transform);
            thisObject2Drag.transform.Rotate(new Vector3(0, 180, 0));
        }
        if (thisDropZone != null)
        {
            thisDropZone.transform.LookAt(xrCamera.transform);
            thisDropZone.transform.Rotate(new Vector3(0, 180, 0));
        }

        gameStates.targetSize = _targetSequenceSizes[_currentTrialIndex];
        gameStates.targetAmp = _targetSequenceAmplitudes[_currentTrialIndex];

        bool justSelected, justStartedDrag, justReleasedDrag;
        float cursor2ObjAngle, cursor2DropAngle;
        float roll = RunCoreInteractionFrame(out justSelected, out justStartedDrag, out justReleasedDrag, out cursor2ObjAngle, out cursor2DropAngle, thisObject2Drag, thisDropZone, _targetSequenceSizes[_currentTrialIndex] / 2.0f, dropZoneThreshold);

        bool pickupFailed = justStartedDrag && (cursor2ObjAngle > (_targetSequenceSizes[_currentTrialIndex] / 2.0f)); // Changed to '>' for failed pickup
        if (!hasPickedup) hasPickedup = justStartedDrag && (cursor2ObjAngle <= (_targetSequenceSizes[_currentTrialIndex] / 2.0f));

        bool dwelledInDropZoneEnough = false;
        if (thisDropZone != null && thisDropZone.transform.Find("feedbackRing") != null)
        {
            dwelledInDropZoneEnough = _timeInDropZoneDwell > 0.1f;
            thisDropZone.transform.Find("feedbackRing").gameObject.SetActive(dwelledInDropZoneEnough);
        }

        bool dropFailed_offTarget = justReleasedDrag && (cursor2DropAngle > dropZoneThreshold);
        bool dropSuccess = justReleasedDrag && (cursor2DropAngle <= dropZoneThreshold);
        bool selectionFailed_selected = justSelected;
        bool hasTimedOut = Time.time - _timeAtTargetOnset > TrialTimeoutDuration;

        if (pickupFailed || dropFailed_offTarget || selectionFailed_selected || hasTimedOut)
        {
            errorSoundEffect.Play();
            if (pickupFailed) gameStates.failureMode = "offTarget_pickup";
            else if (selectionFailed_selected) gameStates.failureMode = "selected";
            else if (dropFailed_offTarget) gameStates.failureMode = "offTarget_drop";
            else if (hasTimedOut) gameStates.failureMode = "timedOut";
            AdvanceToNextTrial();
        }
        else if (dropSuccess)
        {
            gameStates.failureMode = "success";
            alertSoundEffect.Play();
            AdvanceToNextTrial();
        }

        if (hasPickedup && dragEnabled && thisObject2Drag != null)
        {
            thisObject2Drag.transform.position = headCursor.transform.TransformPoint(0, 0, 0.01f);
            thisObject2Drag.transform.LookAt(xrCamera.transform);
            thisObject2Drag.transform.Rotate(new Vector3(0, 180, 0));
        }

        GameStateManager.Instance.SetDrag(dragEnabled);
        gameStates.eventType = "";
        if (justStartedDrag) gameStates.eventType = "justStartedDrag";
        else if (justReleasedDrag) gameStates.eventType = "justReleasedDrag";
        else if (justSelected) gameStates.eventType = "justSelected";

        UpdateGameState(cursor2ObjAngle, cursor2DropAngle, roll);
    }

    /// <summary>
    /// Runs the core interaction frame using the provided "active" drag object and drop zone.
    /// Returns roll velocity. Also outputs justSelected, justStartedDrag, justReleasedDrag, cursor2ObjAngle, cursor2DropAngle.
    /// This consolidates the ring/head/gaze/dwell logic used both by the start-signal and the main experiment.
    /// </summary>
    private float RunCoreInteractionFrame(out bool justSelected, out bool justStartedDrag, out bool justReleasedDrag, out float cursor2ObjectAngle, out float cursor2DropZoneAngle, GameObject activeDragObject, GameObject activeDropZone, float selectionThresholdAngleForThis, float dropThresholdForThis)
    {
        justSelected = false;
        justStartedDrag = false;
        justReleasedDrag = false;
        cursor2ObjectAngle = -1f;
        cursor2DropZoneAngle = -1f;

        gazeVector = eyetracking.combinedEyeLocalForward_processed;

        if (gazeCursor != null)
        {
            if (freezeCursorForSelecting == false || (freezeCursorForSelecting && freezeGaze == false))
            {
                gazeCursor.transform.position = xrCamera.transform.TransformPoint(gazeVector);
            }
            gazeCursor.transform.LookAt(xrCamera.transform);
            gazeCursor.transform.Rotate(0, 180, 0);
        }
        else
        {
            Debug.LogError("Gaze Cursor is null, cannot update its transform.", this);
            return 0f;
        }

        Vector3 gazeCursorRingInHeadVec = xrCamera.transform.InverseTransformPoint(gazeCursorRing.transform.position);
        float gazeRingHeadAng = Vector3.Angle(gazeCursorRingInHeadVec, Vector3.forward);
        headInGazeRing = Vector3.Angle(gazeCursorRingInHeadVec, Vector3.forward) < gazeRingSize / 2f;
        float gaze2gazeRingAng = Vector3.Angle(gazeVector, gazeCursorRingInHeadVec);
        bool eyeMoving = gaze2gazeRingAng > gazeRingSize * 0.75f || new Vector2(eyetracking.eyeInWorldVel_Az, eyetracking.eyeInWorldVel_Pol).magnitude > 160f;
        float headVel = new Vector2(headtracking.headVel_Az, headtracking.headVel_Pol).magnitude;
        bool headMoving = !headtracking.isHeadInFixation;
        bool eyeHeadCloseEnoughforVisual = Vector3.Angle(gazeVector, Vector3.forward) < 50;
        gazeHeadAngle = Vector3.Angle(gazeVector, Vector3.forward);
        int frameThreshold = 5;
        bool headMovingToGaze = IsHeadMovingTowardsGaze2(eyeMoving, headVel > 5, gazeHeadAngle < gazeHeadAngle_prev, frameThreshold);

        if (headMovingToGaze && eyeHeadCloseEnoughforVisual)
        {
            if (gazeCursorRing != null && !(!gazeCursorRing.activeSelf && headInGazeRing))
            {
                gazeCursorRing.SetActive(true);
                if (ringUpdated == false)
                {
                    headGazeDistance_start = gazeRingHeadAng;
                    Vector3 vectorGaze2Head = gazeVector - Vector3.forward;
                    float rawAngleGaze2HeadDeg = CalculateAngleFromEast(vectorGaze2Head);
                    float segment1CenterAngleDegForShader = WrapAngleDeg(rawAngleGaze2HeadDeg + 180.0f);
                    UpdateGazeRingDirection(segment1CenterAngleDegForShader);
                    freezeGaze = true;
                }
                ringUpdated = true;
            }
            if (customRingAlpha && headGazeDistance_start>10)
            {
                float ratioCustomAlpha = 1 - ((gazeRingHeadAng) / headGazeDistance_start);
                // ratioCustomAlpha = SigmoidIsh(ratioCustomAlpha - 0.25f);
                ratioCustomAlpha = SigmoidIsh(ratioCustomAlpha);
                // if (ratioCustomAlpha > 0.1f) ratioCustomAlpha = 1;
                gazeCursorRingMaterial.SetFloat("_customAlpha", ratioCustomAlpha);
            }
        }
        else if (eyeMoving)
        {
            if (gazeCursorRing != null)
            {
                gazeCursorRing.SetActive(false);
                headGazeDistance_start = -1;
                gazeCursorRingMaterial.SetFloat("_customAlpha", 1);
            }
            ringUpdated = false;
            freezeGaze = false;
        }

        if (headInGazeRing && gazeCursorRing.activeSelf)
        {
            if (headInGazeRing_prev == false)
            {
                lookForDwell = true;
                lastEnteredRingSegment = DetermineSegmentEntered();
                Debug.Log($"Head cursor entered the ring via: {lastEnteredRingSegment}");

                if (lastEnteredRingSegment == RingSegment.Selecting)
                {
                    gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.green);
                    gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.green);
                }
                else if (lastEnteredRingSegment == RingSegment.Dragging)
                {
                    gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.blue);
                    gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.blue);
                    freezeGaze = false;
                    gazeRingSize = 13.8f;
                    gazeCursorRing.transform.localScale = gazeCursorRingScale_orig * 2;
                }
            }
        }
        else
        {
            if (dragEnabled) justReleasedDrag = true;
            lookForDwell = false;
            dragEnabled = false;
            dwelledTime_head = -1;
            lastEnteredRingSegment = RingSegment.None;
            gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.green);
            gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.blue);
            if (headInGazeRing_prev)
            {
                gazeRingSize = 6.9f;
                gazeCursorRing.transform.localScale = gazeCursorRingScale_orig;
                gazeCursorRing.SetActive(false);
                ringUpdated = false;
            }
        }

        if (lookForDwell && isHeadFixation_prev == false && headtracking.isHeadInFixation)
        {
            dwelledTime_head = 0;
            progressBar.SetActive(false);
            progressBar.GetComponent<Renderer>().material.SetFloat("_Progress", 0);

        }
        if (lookForDwell && headtracking.isHeadInFixation)
        {
            dwelledTime_head += Time.deltaTime;
            progressBar.SetActive(true);
            progressBar.GetComponent<Renderer>().material.SetFloat("_Progress", dwelledTime_head / dwellTimeThreshold);
        }

        if (lastEnteredRingSegment == RingSegment.Selecting && lookForDwell && dwelledTime_head > dwellTimeThreshold)
        {
            justSelected = true;
            lookForDwell = false;
        }
        else if (lastEnteredRingSegment == RingSegment.Dragging && lookForDwell && dwelledTime_head > dwellTimeThreshold)
        {
            if (!dragEnabled)
            {
                dragEnabled = true;
                justStartedDrag = true;
            }
        }
        if (dragEnabled)
        {
            freezeGaze = false;

            headCursor.GetComponent<Renderer>().material.color = Color.black;
            headCursor.transform.localScale = cursorOrignalScale * 2;
        }
        else
        {
            headCursor.GetComponent<Renderer>().material.color = Color.white;
            headCursor.transform.localScale = cursorOrignalScale; 
        }

        isHeadFixation_prev = headtracking.isHeadInFixation;
        headInGazeRing_prev = headInGazeRing;
        gazeHeadAngle_prev = gazeHeadAngle;
        gazeRingHeadAng_prev = gazeRingHeadAng;

        if (hidGazeRing)
        {
            gazeCursorRing.GetComponent<MeshRenderer>().enabled = dragEnabled;
            if (lastEnteredRingSegment == RingSegment.Selecting) headCursor.GetComponent<Renderer>().material.color = Color.green;
            else if (lastEnteredRingSegment == RingSegment.Dragging) headCursor.GetComponent<Renderer>().material.color = Color.blue;
            else headCursor.GetComponent<Renderer>().material.color = Color.white;
        }
        else
        {
            gazeCursorRing.GetComponent<MeshRenderer>().enabled = true;
            if (!dragEnabled) headCursor.GetComponent<Renderer>().material.color = Color.white;
            else{headCursor.GetComponent<Renderer>().material.color = Color.black;}
        }

        // Use the passed activeDragObject and activeDropZone for the selection/pick/drop checks
        cursor2ObjectAngle = HandleStartDrag(activeDragObject, selectionThresholdAngleForThis);
        cursor2DropZoneAngle = HandleEndDrag(activeDropZone, dropThresholdForThis);

        float rollVelocity = ProcessHeadMovement();

        if (hasPickedup && dragEnabled && activeDragObject != null && _currentState == ExperimentState.Running)
        {
            // Only move the real trial object while running (start-signal object movement is allowed above if desired)
            thisObject2Drag.transform.position = headCursor.transform.TransformPoint(0, 0, 0.01f);
            thisObject2Drag.transform.LookAt(xrCamera.transform);
            thisObject2Drag.transform.Rotate(new Vector3(0, 180, 0));
        }

        if (!gazeCursorRing.activeSelf || dragEnabled || justSelected || justStartedDrag)
        {
            progressBar.SetActive(false);
            progressBar.GetComponent<Renderer>().material.SetFloat("_Progress", 0);
        }

        if (gazeCursorRing.activeSelf)
        {
            gameStates.gazeCursorRingInWorldPos = gazeCursorRing.transform.position;
            gameStates.gazeCursorRingInHmdPos = xrCamera.transform.InverseTransformPoint(gazeCursorRing.transform.position);
        }
        else
        {
            gameStates.gazeCursorRingInWorldPos = new Vector3(Single.NaN, Single.NaN, Single.NaN);
            gameStates.gazeCursorRingInHmdPos = new Vector3(Single.NaN, Single.NaN, Single.NaN);
        }

        return rollVelocity;
    }

    #endregion

    #region --- Experiment Setup & Control ---

    private void SetupExperiment()
    {
        // DELETED: Obsolete fixed threshold calculation removed.
        GenerateFittsLawSequence();
    }

    private void StartExperiment()
    {
        // Called when the start-signal phase is successfully cleared (or you press space originally in old flow).
        // referenceTransform.transform.SetPositionAndRotation(xrCamera.transform.position, xrCamera.transform.rotation);
        DestroyAllTargets();
        InstantiateAllTargets(); // MODIFIED: Renamed for clarity
        UpdateTargetActivation();

        _currentTrialIndex = 0;
        _timeAtTargetOnset = Time.time;
        _currentState = ExperimentState.Running;

        SetTargetColor(_currentTrialIndex, ActiveColor);
        Debug.Log("Experiment started.");
    }

    void resetDragState()
    {
        if (gazeCursorRing != null)
        {
            gazeCursorRing.SetActive(false);
            headGazeDistance_start = -1;
            gazeCursorRingMaterial.SetFloat("_customAlpha", 1);
        }
        ringUpdated = false;
        freezeGaze = false;
        lookForDwell = false;
        dragEnabled = false;
        dwelledTime_head = -1;
        lastEnteredRingSegment = RingSegment.None;
        if (gazeCursorRingMaterial != null)
        {
            gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.green);
            gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.blue);
        }
        if (headInGazeRing_prev)
        {
            gazeRingSize = 6.9f;
            gazeCursorRing.transform.localScale = gazeCursorRingScale_orig;
            gazeCursorRing.SetActive(false);
            ringUpdated = false;
        }
    }

    private void AdvanceToNextTrial()
    {
        hasPickedup = false;
        resetDragState();
        GameStateManager.Instance.SetDrag(false);

        if (thisObject2Drag != null)
        {
            int dragIndex = _object2DragIndices[_currentTrialIndex];
            thisObject2Drag.transform.position = _initialTargetPositions[dragIndex];
            thisObject2Drag.transform.LookAt(referenceTransform.transform);
            thisObject2Drag.transform.Rotate(new Vector3(0, 180, 0));
        }

        _currentTrialIndex++;

        if (_currentTrialIndex >= _object2DragIndices.Count)
        {
            EndSession();
            return;
        }

        // This logic correctly identifies the start of a new block based on the trial count
        if (_currentTrialIndex > 0 && _currentTrialIndex % TargetsPerBlock == 0)
        {
            _currentBlockIndex++;
        }

        // This is the key: update the active targets for the new trial
        UpdateTargetActivation();

        _timeAtTargetOnset = Time.time;
        _timeInDropZoneDwell = 0f;
    }

    private void EndSession()
    {
        _currentState = ExperimentState.Completed;
        headCursor.SetActive(false);
        DestroyAllTargets();
        Debug.Log("Session Completed :)");
    }

    #endregion

    #region --- Target & Sequence Generation (Modified) ---

    private void GenerateFittsLawSequence()
    {
        _object2DragIndices.Clear();
        _dropzoneIndices.Clear();
        _targetSequenceSizes.Clear();
        _targetSequenceAmplitudes.Clear();
        
        const float distance = 1.0f;
        int blockOffset = 0; 

        var blockConditions = new List<(float Amplitude, float Size)>();
        foreach (float amplitude in fittsAmplitudes)
        {
            foreach (float size in fittsTargetSizes)
            {
                blockConditions.Add((amplitude, size));
            }
        }

        int repetitions = 2; // 2 blocks

        for (int rep = 0; rep < repetitions; rep++)
        {

            var randomizedBlocks = blockConditions.OrderBy(b => UnityEngine.Random.value).ToList();

            foreach (var condition in randomizedBlocks)
            {
                float amplitude = condition.Amplitude;
                float visualAngle = condition.Size;

                var ringPositions = new List<Vector3>();
                for (int i = 0; i < TargetsPerBlock; i++)
                {
                    float angleOnRing = i * (360.0f / TargetsPerBlock);
                    float azimuth = amplitude * Mathf.Cos(angleOnRing * Mathf.Deg2Rad);
                    float polar = amplitude * Mathf.Sin(angleOnRing * Mathf.Deg2Rad);
                    Vector3 targetDirection = eyeHead_utils.ficks2vec(azimuth, polar);
                    ringPositions.Add(targetDirection * distance);
                }

                //generate normal fitts sequence
                int currentIndex = 0;
                int jump = TargetsPerBlock / 2;
                List<int> targetIndices = new List<int>();
                for (int i = 0; i < TargetsPerBlock; i++)
                {
                    targetIndices.Add(currentIndex);
                    currentIndex = (currentIndex + jump) % TargetsPerBlock;
                }

                var startIndicesInBlock = new List<int>();
                var endIndicesInBlock = new List<int>();

                for (int i = 0; i < TargetsPerBlock; i++)
                {
                    if (i % 2 == 0)
                    {
                        startIndicesInBlock.Add(targetIndices[i]);
                        endIndicesInBlock.Add(targetIndices[(i + 1) % TargetsPerBlock]);
                    }
                }
                for (int i = 0; i < TargetsPerBlock; i++)
                {
                    if (i % 2 != 0)
                    {
                        startIndicesInBlock.Add(targetIndices[i]);
                        endIndicesInBlock.Add(targetIndices[(i + 1) % TargetsPerBlock]);
                    }
                }

                for (int i = 0; i < startIndicesInBlock.Count; i++)
                {
                    int startIndexInBlock = startIndicesInBlock[i];
                    int endIndexInBlock = endIndicesInBlock[i];

                    _object2DragIndices.Add(blockOffset + startIndexInBlock);
                    _dropzoneIndices.Add(blockOffset + endIndexInBlock);
                    _targetSequenceSizes.Add(visualAngle);
                    _targetSequenceAmplitudes.Add(amplitude);
                }

                blockOffset += TargetsPerBlock;
            }
        }
    }

    /// <summary>
    /// Creates the master pool of all possible target GameObjects and stores their
    /// initial world positions in a parallel list.
    /// </summary>
    private void InstantiateAllTargets()
    {
        DestroyAllTargets(); // Clear any existing targets first
        _initialTargetPositions.Clear(); // Clear the position list as well
        
        const float distance = 1.0f;

        // FIXED: Match the structure used in GenerateFittsLawSequence
        var blockConditions = new List<(float Amplitude, float Size)>();
        foreach (float amplitude in fittsAmplitudes)
        {
            foreach (float size in fittsTargetSizes)
            {
                blockConditions.Add((amplitude, size));
            }
        }

        int repetitions = 2; // 2 blocks

        for (int rep = 0; rep < repetitions; rep++)
        {
            // Use the same randomization as in GenerateFittsLawSequence
            var randomizedBlocks = blockConditions.OrderBy(b => UnityEngine.Random.value).ToList();

            foreach (var condition in randomizedBlocks)
            {
                float amplitude = condition.Amplitude;
                float size = condition.Size;

                // Create all targets for this condition
                for (int i = 0; i < TargetsPerBlock; i++)
                {
                    float angleOnRing = i * (360.0f / TargetsPerBlock);
                    float azimuth = amplitude * Mathf.Cos(angleOnRing * Mathf.Deg2Rad);
                    float polar = amplitude * Mathf.Sin(angleOnRing * Mathf.Deg2Rad);
                    Vector3 targetDirection = eyeHead_utils.ficks2vec(azimuth, polar);
                    
                    // Calculate and STORE the initial world position
                    Vector3 worldPos = referenceTransform.transform.TransformPoint(targetDirection * distance);
                    _initialTargetPositions.Add(worldPos);
                    
                    GameObject targetInstance = Instantiate(targetPrefab, worldPos, Quaternion.identity);
                    targetInstance.transform.LookAt(referenceTransform.transform);
                    targetInstance.transform.Rotate(new Vector3(0, 180, 0));

                    float newScale = (size / BaseTargetAngle) * BasePrefabScale;
                    targetInstance.transform.localScale = Vector3.one * newScale;
                    
                    targetInstance.SetActive(false); // All targets start as inactive
                    SetTargetColor(targetInstance, DefaultColor);
                    _instantiatedTargets.Add(targetInstance);
                }
            }
        }
        
        Debug.Log($"Instantiated {_instantiatedTargets.Count} total targets");
    }
    #endregion

    #region --- Per-Frame Logic & Calculations ---

    /// <summary>
    /// Activates all targets within the current block. It sets the specific
    /// draggable object and drop zone to their unique colors, while all other
    /// targets in the block are set to a default color.
    /// </summary>
    private void UpdateTargetActivation()
    {
        // Ensure the trial index is valid before proceeding.
        if (_currentTrialIndex >= _object2DragIndices.Count) return;

        // 1. Determine the index range for all targets in the current block.
        int blockStart = _currentBlockIndex * TargetsPerBlock;
        int blockEnd = blockStart + TargetsPerBlock;

        // 2. Get the specific master indices for the current trial's active objects.
        int dragIndex = _object2DragIndices[_currentTrialIndex];
        int dropIndex = _dropzoneIndices[_currentTrialIndex];

        // 3. Loop through the entire pool of instantiated targets.
        for (int i = 0; i < _instantiatedTargets.Count; i++)
        {
            GameObject currentTarget = _instantiatedTargets[i];
            if (currentTarget == null) continue;

            // Check if this target is part of the current active block.
            if (i >= blockStart && i < blockEnd)
            {
                // Activate the target and make it face the camera.
                currentTarget.SetActive(true);
                // Set the color based on its role in the current trial.
                if (i == dragIndex)
                {
                    SetTargetColor(currentTarget, ActiveColor);
                }
                else if (i == dropIndex)
                {
                    SetTargetColor(currentTarget, DropZoneColor);
                }
                else
                {
                    // It's a background target in the active block.
                    SetTargetColor(currentTarget, DefaultColor);
                }
            }
            else
            {
                // Deactivate targets that are not in the current block.
                currentTarget.SetActive(false);
            }
        }

        // 4. Assign the correct GameObjects to the member variables for this trial.
        Debug.Log($"Setting dragIndex: {dragIndex} (Trial {_currentTrialIndex})");
        thisObject2Drag = _instantiatedTargets[dragIndex];
        if (thisDropZone != null && thisDropZone.transform.Find("feedbackRing") != null) 
        {
            thisDropZone.transform.Find("feedbackRing").gameObject.SetActive(false);
        }
        thisDropZone = _instantiatedTargets[dropIndex];
        if (thisDropZone != null && thisDropZone.transform.Find("feedbackRing") != null)
        {
            thisDropZone.transform.Find("feedbackRing").gameObject.SetActive(false);
        }
    }

    #endregion

    #region --- Per-Frame Logic & Calculations (Unchanged but overloaded) ---

    private float HandleStartDrag()
    {
        // Backwards-compatible default: use current trial object / threshold
        if (thisObject2Drag == null) return -1;
        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;
        return HandleStartDrag(thisObject2Drag, currentSelectionThreshold);
    }

    // Overload: works with provided object + selection angle threshold (degrees)
    private float HandleStartDrag(GameObject target, float selectionAngleThreshold)
    {
        if (target == null) return -1;

        Vector3 cursorDir = headCursor.transform.position - xrCamera.transform.position;
        Vector3 targetDir = target.transform.position - xrCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);

        // For start-signal objects, if their renderer exists we highlight on hover
        if (angle < selectionAngleThreshold)
        {
            SetTargetColor(target, HoverColor);
        }
        else
        {
            // Choose a sensible default color depending on whether this is start or main
            if (_currentState == ExperimentState.WaitingForStartSignal)
            {
                SetTargetColor(target, ActiveColor); // white default for start draggable
            }
            else
            {
                SetTargetColor(target, ActiveColor);
            }
        }
        return angle;
    }

    private float HandleEndDrag()
    {
        if (thisDropZone == null) return -1;
        return HandleEndDrag(thisDropZone, dropZoneThreshold);
    }

    // Overload: works with provided dropzone + threshold angle
    private float HandleEndDrag(GameObject dropZone, float dropAngleThreshold)
    {
        if (dropZone == null) return -1;

        Vector3 cursorDir = headCursor.transform.position - xrCamera.transform.position;
        Vector3 targetDir = dropZone.transform.position - xrCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);

        if (angle < dropAngleThreshold)
        {
            _timeInDropZoneDwell += Time.deltaTime;
        }
        else
        {
            // Reset color to the standard drop zone color, not active color
            SetTargetColor(dropZone, DropZoneColor);
            _timeInDropZoneDwell = 0;
        }
        return angle;
    }

    private float ProcessHeadMovement()
    {
        Quaternion currentHmdRotation = xrCamera.transform.rotation;
        float rollVelocity = GetRollVelocity(currentHmdRotation, _previousHmdRotation, xrCamera.transform);
        _previousHmdRotation = currentHmdRotation;

        GameStateManager.Instance.rollDirection = DetectRollDirection(rollVelocity, rollVelocityThreshold);
        return rollVelocity;
    }

    private void UpdateGameState(float cursor2ObjectAngle, float cursor2DropZoneAngle, float rollVelocity)
    {
        GameStateManager.Instance.frameOfCentering = false;
        gameStates.trialIndex = _currentTrialIndex;
        gameStates.cursorInWorldPosition = headCursor.transform.position;
        gameStates.cursorInHmdPosition = xrCamera.transform.InverseTransformPoint(headCursor.transform.position);

        if (thisObject2Drag != null)
        {
            gameStates.object2DragInWorldPosition = thisObject2Drag.transform.position;
            gameStates.object2DragInHmdPosition = xrCamera.transform.InverseTransformPoint(thisObject2Drag.transform.position);
        }

        if (thisDropZone != null)
        {
            gameStates.DropzoneInWorldPosition = thisDropZone.transform.position;
            gameStates.DropzoneInHmdPosition = xrCamera.transform.InverseTransformPoint(thisDropZone.transform.position);
        }

        gameStates.cursorDropzoneAngle = cursor2DropZoneAngle;
        gameStates.cursorObjectAngle = cursor2ObjectAngle;
        GameStateManager.Instance.hmdRollVelocity = rollVelocity;
        GameStateManager.Instance.hmdAngularDeltaFromNeutral = GetLocalAngularDeltaVectorFromNeutral(xrCamera.transform.rotation, xrCamera.transform);

        isInDrag_prev = dragEnabled;

        GameStateManager.Instance.UpdateAllPreviousStates();
    }

    #endregion

    #region --- Utility & Helper Functions (Unchanged) ---

    private void SetTargetColor(int targetIndex, Color color)
    {
        if (targetIndex < 0 || targetIndex >= _instantiatedTargets.Count) return;
        SetTargetColor(_instantiatedTargets[targetIndex], color);
    }

    private void SetTargetColor(GameObject target, Color color)
    {
        if (target == null) return;
        Renderer rend = target.GetComponent<Renderer>();
        if (rend != null) rend.material.color = color;
    }

    private void DestroyAllTargets()
    {
        foreach (GameObject target in _instantiatedTargets)
        {
            if (target != null) Destroy(target);
        }
        _instantiatedTargets.Clear();
    }

    public static float GetRollVelocity(Quaternion currentRotation, Quaternion previousRotation, Transform trackedTransform)
    {
        if (Time.deltaTime == 0) return 0f;
        Quaternion deltaRotation = currentRotation * Quaternion.Inverse(previousRotation);
        deltaRotation.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
        Vector3 localAngularVelocityVector = (trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees) / Time.deltaTime;
        GameStateManager.Instance.hmdAngularDeltaVector = localAngularVelocityVector * Time.deltaTime;
        return localAngularVelocityVector.z;
    }

    public Vector3 GetLocalAngularDeltaVectorFromNeutral(Quaternion currentRotation, Transform trackedTransform)
    {
        Quaternion rotationDelta = currentRotation * referenceTransform.transform.rotation;
        rotationDelta.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
        return trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees;
    }

    public int DetectRollDirection(float currentRollVelocity, float rollVelocityThreshold)
    {
        if (Mathf.Abs(currentRollVelocity) <= rollVelocityThreshold) return 0;
        return currentRollVelocity > 0 ? 1 : -1;
    }

    private void UpdateGazeRingDirection(float segment1CenterAngleDeg)
    {
        if (gazeCursorRingMaterial == null)
        {
            Debug.LogError("Gaze Cursor Ring Material is not assigned.", this);
            return;
        }
        gazeCursorRingMaterial.SetFloat("_Segment1CenterAngleDeg", segment1CenterAngleDeg);
    }

    private float CalculateAngleFromEast(Vector3 vector)
    {
        if (vector.x == 0f && vector.y == 0f) return 0f;
        float angleRad = Mathf.Atan2(vector.y, vector.x);
        float angleDeg = angleRad * Mathf.Rad2Deg;
        return WrapAngleDeg(angleDeg);
    }

    private RingSegment DetermineSegmentEntered()
    {
        if (gazeCursorRingMaterial == null) return RingSegment.None;
        float segment1CenterAngleFromShaderDeg = WrapAngleDeg(gazeCursorRingMaterial.GetFloat("_Segment1CenterAngleDeg"));
        Vector3 gazeRingToHeadVec = xrCamera.transform.InverseTransformPoint(gazeCursorRing.transform.position);
        Vector3 currentVectorGaze2Head = gazeRingToHeadVec.normalized - Vector3.forward;
        float currentAngleGaze2Head = CalculateAngleFromEast(-1 * currentVectorGaze2Head);
        float currentSegmentRatio = gazeCursorRingMaterial.GetFloat("_SegmentRatio");
        float segment1HalfArcDeg = (currentSegmentRatio * 360.0f) / 2.0f;
        float angleDiffDeg = Mathf.Abs(currentAngleGaze2Head - segment1CenterAngleFromShaderDeg);
        angleDiffDeg = Mathf.Min(angleDiffDeg, 360.0f - angleDiffDeg);
        return (angleDiffDeg <= segment1HalfArcDeg) ? RingSegment.Selecting : RingSegment.Dragging;
    }

    private float WrapAngleDeg(float angle)
    {
        return (angle % 360.0f + 360.0f) % 360.0f;
    }

    public bool IsHeadMovingTowardsGaze2(bool isEyeMoving, bool isHeadMoving, bool closerToGaze, int frameThreshold)
    {
        if (isEyeMoving || !isHeadMoving) return false;
        if (closerToGaze) headToGazePossibleFrames += 1;
        else headToGazePossibleFrames = 0;
        return headToGazePossibleFrames > frameThreshold;
    }

    public static float SigmoidIsh(float x)
    {
        // return Mathf.SmoothStep(0f, 1f, x);
        return 1.0f / (1.0f + Mathf.Exp(-(x - 0.1f) * 30));

    }

    #endregion

    #region --- Start-Signal Helpers (New) ---

    private void SpawnStartSignalTargets()
    {
        ClearStartSignalTargets();

        Vector3 whitePos = eyeHead_utils.ficks2vec(-5, 0);
        whitePos = referenceTransform.transform.TransformPoint(whitePos);
        Vector3 bluePos = eyeHead_utils.ficks2vec(5, 0);
        bluePos = referenceTransform.transform.TransformPoint(bluePos);


        float size = fittsTargetSizes[0];
        float newScale = (size / BaseTargetAngle) * BasePrefabScale;

        startWhiteTarget = Instantiate(targetPrefab, whitePos, Quaternion.identity);
        startWhiteTarget.transform.LookAt(referenceTransform.transform);
        startWhiteTarget.transform.Rotate(new Vector3(0, 180, 0));
        startWhiteTarget.transform.localScale = Vector3.one * newScale;
        SetTargetColor(startWhiteTarget, ActiveColor); // white

        startBlueTarget = Instantiate(targetPrefab, bluePos, Quaternion.identity);
        startBlueTarget.transform.LookAt(referenceTransform.transform);
        startBlueTarget.transform.Rotate(new Vector3(0, 180, 0));
        startBlueTarget.transform.localScale = Vector3.one * newScale;

    }

    private void ResetStartSignalPositions()
    {
        if (startWhiteTarget != null)
        {
            // push it back to original spawn location (for simplicity just reset rotation/position relative to reference)
            Vector3 whitePos = eyeHead_utils.ficks2vec(-5, 0);
            whitePos = referenceTransform.transform.TransformPoint(whitePos);
            startWhiteTarget.transform.position = whitePos;
            startWhiteTarget.transform.LookAt(referenceTransform.transform);
            startWhiteTarget.transform.Rotate(new Vector3(0, 180, 0));
            SetTargetColor(startWhiteTarget, ActiveColor);
        }
        if (startBlueTarget != null)
        {
            Vector3 bluePos = eyeHead_utils.ficks2vec(5, 0);
            bluePos = referenceTransform.transform.TransformPoint(bluePos);
            startBlueTarget.transform.position = bluePos;
            startBlueTarget.transform.LookAt(referenceTransform.transform);
            startBlueTarget.transform.Rotate(new Vector3(0, 180, 0));
            SetTargetColor(startBlueTarget, DropZoneColor);
        }
    }

    private void ClearStartSignalTargets()
    {
        if (startWhiteTarget != null)
        {
            Destroy(startWhiteTarget);
            startWhiteTarget = null;
        }
        if (startBlueTarget != null)
        {
            Destroy(startBlueTarget);
            startBlueTarget = null;
        }
    }

    #endregion
}
