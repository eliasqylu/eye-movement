using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using j_utils; // Assuming this contains eyeHead_utils and other dependencies

public class Fitts_wink : MonoBehaviour
{
    // Enum to manage the state of the experiment for a clearer workflow
    private enum ExperimentState { Idle, Running, Completed }
    private ExperimentState _currentState = ExperimentState.Idle;

    [Header("Object References")]
    public GameObject cursor;
    public GameObject targetPrefab;
    public Camera mainCamera;
    public GameObject referenceTransform;
    public AudioSource errorSoundEffect;
    public AudioSource alertSoundEffect;

    [Header("Fitts' Law Settings")]
    [Tooltip("The angular amplitudes (in degrees) for each block of targets.")]
    public float[] fittsAmplitudes = { 10.0f, 30.0f };
    // NEW: Added target sizes in visual degrees
    [Tooltip("The target widths (visual angle in degrees) for each block.")]
    public float[] fittsTargetSizes = { 2.5f, 5.0f };

    private const float TrialTimeoutDuration = 10.0f;
    private const int TargetsPerBlock = 11;
    // NEW: Constants for calculating scale from visual angle
    private const float BaseTargetAngle = 5.0f;    // The visual angle (in deg) of the base prefab
    private const float BasePrefabScale = 0.0873f; // The localScale of the base prefab

    [Header("Head Movement Settings")]
    [Tooltip("The threshold for detecting significant head roll velocity.")]
    public float rollVelocityThreshold;

    // --- Private State Variables ---
    private List<Vector3> _targetSequenceHmdPositions = new List<Vector3>();
    // NEW: Parallel list to store the visual angle size for each corresponding target
    private List<float> _targetSequenceSizes = new List<float>(); 
    private List<float> _targetSequenceAmplitudes = new List<float>(); 

    private List<GameObject> _instantiatedTargets = new List<GameObject>();
    private int _currentTrialIndex = 0;
    private int _currentBlockIndex = 0;
    private float _timeInDwell = 0;
    private float _timeAtTargetOnset;
    private Quaternion _previousHmdRotation;
    private bool isInDrag_prev;

    private static readonly Color DefaultColor = new Color(1, 1, 1, 0.5f);
    private static readonly Color HoverColor = Color.cyan;
    private static readonly Color ActiveColor = Color.black;

    bool hasDragged;

    // --- Configuration ---
    public float winkCanConfirmThresholdSeconds = 0.25f;
    private const int STABILITY_FRAME_THRESHOLD = 3; // How many frames a state must be held to be stable

    // --- Stability State Variables ---
    private int leftOpenConsecutiveFrames = 0;
    private int rightOpenConsecutiveFrames = 0;
    private int bothOpenConsecutiveFrames = 0;

    // --- Per-Eye Wink Tracking Variables ---
    private int leftWinkFrameCount = 0;
    private int rightWinkFrameCount = 0;
    private bool singleEyeIntentionalWink_prev = false; // For detecting when a wink ends
    Vector3 cursorOrignalScale = new Vector3();

    public float eyeOpennessThreshold = 0.3f;


    #region --- Unity Lifecycle & State ---

    void Start()
    {
        fittsAmplitudes = fittsAmplitudes.Select(a => a * 0.5f).ToArray();
        cursor.SetActive(true);
        cursor.GetComponent<MeshRenderer>().enabled = true;
        cursorOrignalScale = cursor.transform.localScale;

        if (mainCamera == null) mainCamera = Camera.main;
        // The SetupExperiment method is now just the sequence generation
        GenerateFittsLawSequence();
    }

    void Update()
    {
        gameStates.failureMode = "";
        gameStates.targetSize = 0;
        gameStates.targetAmp = 0;

        switch (_currentState)
        {
            case ExperimentState.Idle:
                HandleIdleState();
                break;
            case ExperimentState.Running:
                HandleRunningState();
                break;
            case ExperimentState.Completed:
                cursor.SetActive(false);
                break;
        }
    }
    #endregion

    #region --- Core Interaction Logic (Unchanged) ---

    private void HandleIdleState()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartExperiment();
        }
    }

    private void HandleRunningState()
    {
        gameStates.targetSize = _targetSequenceSizes[_currentTrialIndex];
        gameStates.targetAmp =  _targetSequenceAmplitudes[_currentTrialIndex];
        cursor.transform.LookAt(mainCamera.transform);
        cursor.transform.Rotate(new Vector3(0, 180, 0));
        

        bool justStartedDrag = false;
        bool justReleasedDrag = false;
        bool justSelected = false;
         
        // ====================================================================
        // 1. GET RAW EYE STATE & CALCULATE STABLE STATES (3-frame check)
        // ====================================================================

        // Get raw data for this frame (assuming lower value means more closed)
        bool isLeftEyeRawOpen = eyetracking.eyeData.left.eye_openness > eyeOpennessThreshold;
        bool isRightEyeRawOpen = eyetracking.eyeData.right.eye_openness > eyeOpennessThreshold;

        // Update consecutive frame counters for stability
        leftOpenConsecutiveFrames = isLeftEyeRawOpen ? leftOpenConsecutiveFrames + 1 : 0;
        rightOpenConsecutiveFrames = isRightEyeRawOpen ? rightOpenConsecutiveFrames + 1 : 0;
        bothOpenConsecutiveFrames = (isLeftEyeRawOpen && isRightEyeRawOpen) ? bothOpenConsecutiveFrames + 1 : 0;

        // The final, stable booleans are only true if the state was held for enough frames
        bool isLeftEyeStableOpen = leftOpenConsecutiveFrames >= STABILITY_FRAME_THRESHOLD;
        bool isRightEyeStableOpen = rightOpenConsecutiveFrames >= STABILITY_FRAME_THRESHOLD;
        bool areBothEyesStableOpen = bothOpenConsecutiveFrames >= STABILITY_FRAME_THRESHOLD;

        // ====================================================================
        // 2. UPDATE PER-EYE WINK COUNTERS
        // ====================================================================

        if (!isLeftEyeStableOpen && isRightEyeStableOpen) // Left wink detected
        {
            leftWinkFrameCount++;
            rightWinkFrameCount = 0; // Reset other eye's counter
        }
        else if (isLeftEyeStableOpen && !isRightEyeStableOpen) // Right wink detected
        {
            rightWinkFrameCount++;
            leftWinkFrameCount = 0; // Reset other eye's counter
        }
        else // Both eyes are open or both are closed
        {
            leftWinkFrameCount = 0;
            rightWinkFrameCount = 0;
        }

        // ====================================================================
        // 3. DETERMINE IF AN INTENTIONAL WINK OCCURRED
        // ====================================================================

        float winkFrameThreshold = winkCanConfirmThresholdSeconds * (1 / Time.deltaTime);
        bool leftEyeIntentionalClosed = leftWinkFrameCount >= winkFrameThreshold;
        bool rightEyeIntentionalClosed = rightWinkFrameCount >= winkFrameThreshold;

        // An intentional single wink is one or the other, but not both.
        bool singleEyeIntentionalWink = leftEyeIntentionalClosed ^ rightEyeIntentionalClosed;
        
        if (singleEyeIntentionalWink)
        {
            cursor.GetComponent<Renderer>().material.color = Color.black;
            cursor.transform.localScale = cursorOrignalScale * 2;
        }
        else
        {
            cursor.GetComponent<Renderer>().material.color = Color.white;
            cursor.transform.localScale = cursorOrignalScale;
        }

        // ====================================================================
        // 4. APPLY DRAG AND SELECT LOGIC
        // ====================================================================

        bool isHeadMoving = new Vector2(headtracking.headVel_Az, headtracking.headVel_Pol).magnitude > 15;

        // Start dragging if a confirmed wink is held and the head is moving.
        if (singleEyeIntentionalWink && isHeadMoving)
        {
            if (!GameStateManager.Instance.isInDrag)
            {
                justStartedDrag = true;
                GameStateManager.Instance.SetDrag(true);    
            }
        }
        if (areBothEyesStableOpen)
        {
            if (singleEyeIntentionalWink_prev) // If we were just winking on the previous frame...
            {
                if (GameStateManager.Instance.isInDrag)
                {
                    // If we were dragging, just stop the drag. Don't select.
                    GameStateManager.Instance.SetDrag(false);
                    justReleasedDrag = true;
                }
                else
                {
                    // If we were not dragging, this was a wink-to-select action.
                    justSelected = true;
                }
            }
            else if (GameStateManager.Instance.isInDrag)
            {
                // General case to stop dragging if eyes open, even if it wasn't a confirmed "wink"
                GameStateManager.Instance.SetDrag(false);
                justReleasedDrag = true;
            }
        }
        
        // Update previous frame state for the next cycle
        singleEyeIntentionalWink_prev = singleEyeIntentionalWink;

        // if (justStartedDrag || justReleasedDrag || justSelected)
        // {
        //     Vector3 prevHeadFix = headtracking.previousHeadFovCentroids[headtracking.previousHeadFovCentroids.Count - 1];
        //     if (Vector3.Angle(cursor.transform.position, prevHeadFix) < 2) cursor.transform.position = prevHeadFix;
        // }

        float cursorTargetAngle = HandleDwellSelection();
        
        // MODIFIED: The selection threshold is now calculated dynamically for the current trial
        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;
        bool selectionSuccess = justSelected && (cursorTargetAngle <= currentSelectionThreshold);
        bool selectionFailed_offTarget = justSelected && (cursorTargetAngle > currentSelectionThreshold);
        bool selectionFailed_dragged = justReleasedDrag;
        bool hasTimedOut = Time.time - _timeAtTargetOnset > TrialTimeoutDuration;


        if (selectionFailed_offTarget || selectionFailed_dragged || hasTimedOut)
        {
            errorSoundEffect.Play();
            if (selectionFailed_offTarget) gameStates.failureMode = "offTarget";
            else if (selectionFailed_dragged) gameStates.failureMode = "dragged";
            else if (hasTimedOut) gameStates.failureMode = "timedOut";
            AdvanceToNextTrial();
        }
        else if (selectionSuccess && !justReleasedDrag)
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
    #endregion

    #region --- Experiment Setup & Control ---

    private void StartExperiment()
    {
        referenceTransform.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
        
        DestroyAllTargets();
        InstantiateAllTargets(); // MODIFIED: Renamed for clarity
        UpdateTargetActivation();

        _currentTrialIndex = 0;
        _timeAtTargetOnset = Time.time;
        _currentState = ExperimentState.Running;
        
        SetTargetColor(_currentTrialIndex, ActiveColor);
        Debug.Log("Experiment started.");
    }
    
    private void AdvanceToNextTrial()
    {
        hasDragged = false;
        GameStateManager.Instance.SetDrag(false);
        SetTargetColor(_currentTrialIndex, DefaultColor);
        _currentTrialIndex++;

        if (_currentTrialIndex >= _targetSequenceHmdPositions.Count)
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
        cursor.SetActive(false);
        DestroyAllTargets();
        Debug.Log("Session Completed :)");
    }
    #endregion

    #region --- Target & Sequence Generation (Modified) ---

    /// <summary>
    /// Generates a sequence for all combinations of amplitude and size.
    /// </summary>
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

            var randomizedBlocks = blockConditions.OrderBy(b => Random.value).ToList();

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
    
    /// <summary>
    /// Instantiates all targets and sets their scale based on the sequence data.
    /// </summary>
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
    #endregion

    #region --- Per-Frame Logic & Calculations (Unchanged) ---

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
                target.transform.LookAt(mainCamera.transform);
                target.transform.Rotate(0, 180, 0);
            }
        }
    }
    
    private float HandleDwellSelection()
    {
        if (_currentTrialIndex >= _instantiatedTargets.Count) return -1;
        
        GameObject currentTarget = _instantiatedTargets[_currentTrialIndex];
        Vector3 cursorDir = cursor.transform.position - mainCamera.transform.position;
        Vector3 targetDir = currentTarget.transform.position - mainCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);
        
        // MODIFIED: The threshold for changing color is now dynamic
        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;

        if (angle < currentSelectionThreshold)
        {
            SetTargetColor(currentTarget, HoverColor);
            _timeInDwell += Time.deltaTime; // This logic is preserved
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
        Quaternion currentHmdRotation = mainCamera.transform.rotation;
        float rollVelocity = GetRollVelocity(currentHmdRotation, _previousHmdRotation, mainCamera.transform);
        _previousHmdRotation = currentHmdRotation;
        
        GameStateManager.Instance.rollDirection = DetectRollDirection(rollVelocity, rollVelocityThreshold);
        return rollVelocity;
    }

    private void UpdateGameState(float cursorTargetAngle, float rollVelocity)
    {
        GameStateManager.Instance.frameOfCentering = false;
        gameStates.trialIndex = _currentTrialIndex;
        gameStates.cursorInWorldPosition = cursor.transform.position;
        gameStates.cursorInHmdPosition = mainCamera.transform.InverseTransformPoint(cursor.transform.position);

        if (_currentTrialIndex < _instantiatedTargets.Count)
        {
            GameObject currentTarget = _instantiatedTargets[_currentTrialIndex];
            gameStates.targetInWorldPosition = currentTarget.transform.position;
            gameStates.targetInHmdPosition = mainCamera.transform.InverseTransformPoint(currentTarget.transform.position);
        }

        gameStates.cursorTargetAngle = cursorTargetAngle;
        GameStateManager.Instance.hmdRollVelocity = rollVelocity;
        GameStateManager.Instance.hmdAngularDeltaFromNeutral = GetLocalAngularDeltaVectorFromNeutral(mainCamera.transform.rotation, mainCamera.transform);

        isInDrag_prev = GameStateManager.Instance.isInDrag;
        
        
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
        if (rend != null)
        {
            rend.material.color = color;
        }
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
    
    #endregion
}