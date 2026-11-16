using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using j_utils;
using System; // Assuming this contains eyeHead_utils and other dependencies

public class Fitts_align : MonoBehaviour
{
    int headMovedCount = 0;
    eyetracking _eyetracking;
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
    [Tooltip("The target widths (visual angle in degrees) for each block.")]
    public float[] fittsTargetSizes = { 1.0f, 3.0f };

    private const float TrialTimeoutDuration = 10.0f;
    private const int TargetsPerBlock = 11;
    private const float BaseTargetAngle = 5.0f;    // The visual angle (in deg) of the base prefab
    private const float BasePrefabScale = 0.0873f; // The localScale of the base prefab

    [Header("Head Movement Settings")]
    public float rollVelocityThreshold;

    // --- Private State Variables ---
    private List<Vector3> _targetSequenceHmdPositions = new List<Vector3>();
    private List<float> _targetSequenceSizes = new List<float>();
    private List<float> _targetSequenceAmplitudes = new List<float>();

    private List<GameObject> _instantiatedTargets = new List<GameObject>();
    private int _currentTrialIndex = 0;
    private int _currentBlockIndex = 0;
    private float _timeInDwell = 0;
    private float _timeAtTargetOnset;
    private Quaternion _previousHmdRotation;
    bool isInDrag_prev;

    private GameObject _startSignalTarget;

    // --- Complex Gaze/Head Interaction State ---
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

    private static readonly Color DefaultColor = new Color(1, 1, 1, 0.5f);
    private static readonly Color HoverColor = Color.cyan;
    private static readonly Color ActiveColor = Color.black;

    #region --- Unity Lifecycle & Setup ---

    void Start()
    {
        _eyetracking = GameObject.Find("eyetracking").GetComponent<eyetracking>();
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
                HandleWaitingForStartSignal();
                break;
            case ExperimentState.Running:
                HandleRunningState();
                break;
            case ExperimentState.Completed:
                gazeCursor.SetActive(false);
                break;
        }
    }

    #endregion

    #region --- State Handlers & Core Logic ---

    private void HandleIdleState()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartExperiment();
        }
    }

    private void HandleWaitingForStartSignal()
    {
        // Use the exact same interaction logic as the main trials
        RunCoreInteractionLogic(out bool justStartedDrag, out bool justReleasedDrag, out bool justSelected);

        // Check if the selection was successful on our start target
        if (justSelected)
        {
            Vector3 cursorDir = headCursor.transform.position - xrCamera.transform.position;
            Vector3 targetDir = _startSignalTarget.transform.position - xrCamera.transform.position;
            float angle = Vector3.Angle(cursorDir, targetDir);

            if (angle < 2.5f) // Generous threshold for the start target
            {
                BeginMainExperiment();
            }
            else
            {
                errorSoundEffect.Play();
            }
        }
    }

    private void HandleRunningState()
    {
        gameStates.targetSize = _targetSequenceSizes[_currentTrialIndex];
        gameStates.targetAmp = _targetSequenceAmplitudes[_currentTrialIndex];

        // Call the shared interaction logic to get the user's actions for this frame
        RunCoreInteractionLogic(out bool justStartedDrag, out bool justReleasedDrag, out bool justSelected);

        // --- Fitts' Law Trial Logic ---
        float cursorTargetAngle = HandleDwellSelection();
        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;

        bool selectionSuccess = justSelected && (cursorTargetAngle < currentSelectionThreshold);
        bool selectionFailed = justSelected && (cursorTargetAngle > currentSelectionThreshold);
        bool hasTimedOut = Time.time - _timeAtTargetOnset > TrialTimeoutDuration;

        if (selectionFailed || justStartedDrag || hasTimedOut)
        {
            errorSoundEffect.Play();
            if (selectionFailed) gameStates.failureMode = "offTarget";
            else if (justStartedDrag) gameStates.failureMode = "dragged";
            else if (hasTimedOut) gameStates.failureMode = "timedOut";
            AdvanceToNextTrial();
        }
        else if (selectionSuccess)
        {
            gameStates.failureMode = "success";
            alertSoundEffect.Play();
            AdvanceToNextTrial();
        }

        float rollVelocity = ProcessHeadMovement();

        gameStates.eventType = "";
        if (justStartedDrag) gameStates.eventType = "justStartedDrag";
        else if (justReleasedDrag) gameStates.eventType = "justReleasedDrag";
        else if (justSelected) gameStates.eventType = "justSelected";
        UpdateGameState(cursorTargetAngle, rollVelocity);
    }

    // ADDED: This new function contains all the complex interaction logic,
    // ensuring it's identical for the start signal and main trials.
    private void RunCoreInteractionLogic(out bool justStartedDrag, out bool justReleasedDrag, out bool justSelected)
    {
        // Initialize output variables
        justStartedDrag = false;
        justReleasedDrag = false;
        justSelected = false;

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
            return;
        }

        Vector3 gazeCursorRingInHeadVec = xrCamera.transform.InverseTransformPoint(gazeCursorRing.transform.position);
        float gazeRingHeadAng = Vector3.Angle(gazeCursorRingInHeadVec, Vector3.forward);
        headInGazeRing = Vector3.Angle(gazeCursorRingInHeadVec, Vector3.forward) < gazeRingSize / 2f;
        float gaze2gazeRingAng = Vector3.Angle(gazeVector, gazeCursorRingInHeadVec);
        bool eyeMoving = gaze2gazeRingAng > gazeRingSize * 0.75f || new Vector2(eyetracking.eyeInWorldVel_Az, eyetracking.eyeInWorldVel_Pol).magnitude > 160f;
        float headVel = new Vector2(headtracking.headVel_Az, headtracking.headVel_Pol).magnitude;
        bool headMoving = !headtracking.isHeadInFixation;
        bool eyeHeadCloseEnoughforVisual = Vector3.Angle(gazeVector, Vector3.forward) < 100;
        gazeHeadAngle = Vector3.Angle(gazeVector, Vector3.forward);
        int frameThreshold = 5;
        bool headMovingToGaze = IsHeadMovingTowardsGaze2(eyeMoving, headVel > 5, gazeHeadAngle < gazeHeadAngle_prev, frameThreshold);

        if (headMovingToGaze && eyeHeadCloseEnoughforVisual)
        {
            if (gazeCursorRing != null && !(!gazeCursorRing.activeSelf && headInGazeRing))
            {
                headMovedCount++;
                if (headMovedCount >= 0)
                {
                    gazeCursorRing.SetActive(true);
                    if (ringUpdated == false)
                    {
                        // gazeVector = GetSmoothedGazePoint();
                        headGazeDistance_start = gazeRingHeadAng;
                        Vector3 vectorGaze2Head = gazeVector - Vector3.forward;
                        float rawAngleGaze2HeadDeg = CalculateAngleFromEast(vectorGaze2Head);
                        float segment1CenterAngleDegForShader = WrapAngleDeg(rawAngleGaze2HeadDeg + 180.0f);
                        UpdateGazeRingDirection(segment1CenterAngleDegForShader);
                        freezeGaze = true;
                    }
                    ringUpdated = true;
                }
            }
            if (customRingAlpha && headGazeDistance_start > 10)
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
                headMovedCount = 0;
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
                headMovedCount = 0;
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
            headCursor.GetComponent<Renderer>().material.color = Color.white;
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
        
    }
    #endregion

    #region --- Experiment Setup & Control ---

    private void SetupExperiment()
    {
        GenerateFittsLawSequence();
    }

    private void StartExperiment()
    {
        referenceTransform.transform.SetPositionAndRotation(xrCamera.transform.position, xrCamera.transform.rotation);
        DestroyAllTargets();

        InstantiateAllTargets();
        foreach (var target in _instantiatedTargets)
        {
            target.SetActive(false);
        }

        Vector3 startPos = referenceTransform.transform.TransformPoint(Vector3.forward);
        _startSignalTarget = Instantiate(targetPrefab, startPos, Quaternion.identity);
        _startSignalTarget.SetActive(true);
        SetTargetColor(_startSignalTarget, ActiveColor);

        _currentState = ExperimentState.WaitingForStartSignal;
        Debug.Log("Waiting for start signal...");
    }

    private void BeginMainExperiment()
    {
        Debug.Log("Start signal received! Beginning experiment.");
        alertSoundEffect.Play();

        Destroy(_startSignalTarget);

        _currentState = ExperimentState.Running;
        _currentTrialIndex = 0;
        _timeAtTargetOnset = Time.time;

        UpdateTargetActivation();
        SetTargetColor(_instantiatedTargets[_currentTrialIndex], ActiveColor);
    }

    private void AdvanceToNextTrial()
    {
        SetTargetColor(_currentTrialIndex, DefaultColor);
        _currentTrialIndex++;

        if (_currentTrialIndex >= _instantiatedTargets.Count)
        {
            EndSession();
            return;
        }

        if (_currentTrialIndex > 0 && _currentTrialIndex % TargetsPerBlock == 0)
        {
            _currentBlockIndex++;
            UpdateTargetActivation();
        }

        _timeAtTargetOnset = Time.time;
        _timeInDwell = 0f;
        SetTargetColor(_currentTrialIndex, ActiveColor);
    }

    private void EndSession()
    {
        _currentState = ExperimentState.Completed;
        headCursor.SetActive(false);
        DestroyAllTargets();
        Debug.Log("Session Completed :)");
    }
    #endregion

    #region --- Target & Sequence Generation ---

    private void GenerateFittsLawSequence()
    {
        _targetSequenceHmdPositions.Clear();
        _targetSequenceSizes.Clear();
        _targetSequenceAmplitudes.Clear(); // <-- ADD THIS LINE: Clear the new list
        const float distance = 1.0f;

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

                // ... (code to generate ringPositions remains the same) ...
                var ringPositions = new List<Vector3>();
                for (int i = 0; i < TargetsPerBlock; i++)
                {
                    float angleOnRing = i * (360.0f / TargetsPerBlock);
                    float azimuth = amplitude * Mathf.Cos(angleOnRing * Mathf.Deg2Rad);
                    float polar = amplitude * Mathf.Sin(angleOnRing * Mathf.Deg2Rad);
                    Vector3 targetDirection = eyeHead_utils.ficks2vec(azimuth, polar);
                    ringPositions.Add(targetDirection * distance);
                }


                int currentIndex = 0;
                int jump = TargetsPerBlock / 2;
                for (int i = 0; i < TargetsPerBlock; i++)
                {
                    _targetSequenceHmdPositions.Add(ringPositions[currentIndex]);
                    _targetSequenceSizes.Add(visualAngle);
                    _targetSequenceAmplitudes.Add(amplitude); // <-- ADD THIS LINE: Store the amplitude
                    currentIndex = (currentIndex + jump) % TargetsPerBlock;
                }
            }
        }
    }

    private void InstantiateAllTargets()
    {
        targetPrefab.SetActive(false);
        for (int i = 0; i < _targetSequenceHmdPositions.Count; i++)
        {
            Vector3 worldPos = referenceTransform.transform.TransformPoint(_targetSequenceHmdPositions[i]);
            GameObject targetInstance = Instantiate(targetPrefab, worldPos, Quaternion.identity);

            float requiredAngle = _targetSequenceSizes[i];
            float newScale = (requiredAngle / BaseTargetAngle) * BasePrefabScale;
            targetInstance.transform.localScale = Vector3.one * newScale;

            SetTargetColor(targetInstance, DefaultColor);
            _instantiatedTargets.Add(targetInstance);
        }
    }

    private void UpdateTargetActivation()
    {
        int blockStart = _currentBlockIndex * TargetsPerBlock;
        int blockEnd = blockStart + TargetsPerBlock;

        for (int i = 0; i < _instantiatedTargets.Count; i++)
        {
            GameObject target = _instantiatedTargets[i];
            bool isActiveBlock = i >= blockStart && i < blockEnd;
            target.SetActive(isActiveBlock);

            if (isActiveBlock)
            {
                target.transform.LookAt(xrCamera.transform);
                target.transform.Rotate(0, 180, 0);
            }
        }
    }
    #endregion

    #region --- Per-Frame Calculations ---

    private float HandleDwellSelection()
    {
        if (_currentTrialIndex >= _instantiatedTargets.Count) return -1;

        GameObject currentTarget = _instantiatedTargets[_currentTrialIndex];
        Vector3 cursorDir = headCursor.transform.position - xrCamera.transform.position;
        Vector3 targetDir = currentTarget.transform.position - xrCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);

        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;

        if (angle < currentSelectionThreshold && headInGazeRing)
        {
            SetTargetColor(currentTarget, HoverColor);
            _timeInDwell += Time.deltaTime;
        }
        else
        {
            SetTargetColor(currentTarget, ActiveColor);
            _timeInDwell = 0;
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

    private void UpdateGameState(float cursorTargetAngle, float rollVelocity)
    {
        GameStateManager.Instance.frameOfCentering = false;
        gameStates.trialIndex = _currentTrialIndex;
        gameStates.cursorInWorldPosition = headCursor.transform.position;
        gameStates.cursorInHmdPosition = xrCamera.transform.InverseTransformPoint(headCursor.transform.position);

        if (_currentTrialIndex < _instantiatedTargets.Count)
        {
            GameObject currentTarget = _instantiatedTargets[_currentTrialIndex];
            gameStates.targetInWorldPosition = currentTarget.transform.position;
            gameStates.targetInHmdPosition = xrCamera.transform.InverseTransformPoint(currentTarget.transform.position);
        }

        gameStates.cursorTargetAngle = cursorTargetAngle;
        GameStateManager.Instance.hmdRollVelocity = rollVelocity;
        GameStateManager.Instance.hmdAngularDeltaFromNeutral = GetLocalAngularDeltaVectorFromNeutral(xrCamera.transform.rotation, xrCamera.transform);
        isInDrag_prev = dragEnabled;
        GameStateManager.Instance.UpdateAllPreviousStates();
    }

    #endregion

    #region --- Utility & Helper Functions ---

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
        // Destroy the start signal target if it exists
        if (_startSignalTarget != null)
        {
            Destroy(_startSignalTarget);
        }
        // Destroy all the main trial targets
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
    
    public Vector3 GetSmoothedGazePoint()
    {
        // 1. Call your function to get the last 20 historical points.#endregion
        List<Vector3> recentPoints = _eyetracking.GetLastGazePoints(20);

        // 2. We need at least 10 points to calculate a meaningful average.
        // If there are fewer, return a default value.
        if (recentPoints.Count < 10)
        {
            Debug.LogWarning("Not enough gaze points to calculate a stable average. Returning the last known point or zero.");
            // LastOrDefault() returns the last item, or the default value (Vector3.zero) if the list is empty.
            return recentPoints.LastOrDefault();
        }

        var firstTen = recentPoints.Take(10);

        // 4. Calculate the mean by summing the vectors and dividing by the count.
        Vector3 sum = Vector3.zero;
        foreach (Vector3 point in firstTen)
        {
            sum += point;
        }

        // Divide the summed vector by 10 to get the average position.
        Vector3 meanPosition = sum / 10f;

        return meanPosition;
    }
    #endregion
}