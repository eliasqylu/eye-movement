using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;
using j_utils; // Assuming this contains eyeHead_utils and other dependencies

public class drag_wink : MonoBehaviour
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
    [Tooltip("The target widths (visual angle in degrees) for each block.")]
    public float[] fittsTargetSizes = { 2.5f };
    public float dropZoneThreshold = 0.5f;

    private const float TrialTimeoutDuration = 10.0f;
    private const int TargetsPerBlock = 7;
    private const float BaseTargetAngle = 5.0f;      // The visual angle (in deg) of the base prefab
    private const float BasePrefabScale = 0.0873f; // The localScale of the base prefab

    [Header("Head Movement Settings")]
    [Tooltip("The threshold for detecting significant head roll velocity.")]
    public float rollVelocityThreshold;

    // --- Private State Variables ---
    
    // A single master list of all possible target objects that can be displayed.
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
    private bool isInDrag_prev;
    
    // MODIFIED: Flag to control trial transitions
    private bool _isTransitioning = false;

    private static readonly Color DefaultColor = new Color(1, 1, 1, 0.1f);
    private static readonly Color HoverColor = Color.red;
    private static readonly Color ActiveColor = Color.white;
    private static readonly Color DropZoneColor = Color.blue;

    bool hasDragged;
    bool hasPickedup;

    // --- Wink Detection Configuration ---
    public float winkCanConfirmThresholdSeconds = 0.25f;
    private const int STABILITY_FRAME_THRESHOLD = 3; // How many frames a state must be held to be stable

    // --- Stability State Variables ---
    private int leftOpenConsecutiveFrames = 0;
    private int rightOpenConsecutiveFrames = 0;
    private int bothOpenConsecutiveFrames = 0;

    // --- Per-Eye Wink Tracking Variables ---
    private int leftWinkFrameCount = 0;
    private int rightWinkFrameCount = 0;
    bool areBothEyesStableOpen;
    private bool singleEyeIntentionalWink_prev = false; // For detecting when a wink ends
    Vector3 cursorPositionAtWink = new Vector3();
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
                targetPrefab.SetActive(false);
                HandleRunningState();
                break;
            case ExperimentState.Completed:
                cursor.SetActive(false);
                break;
        }
    }
    #endregion

    #region --- Core Interaction Logic ---

    private void HandleIdleState()
    {
        if (Input.GetKeyDown(KeyCode.Space) || AnyTriggerPressed())
        {
            StartExperiment();
        }
    }

    private static bool AnyTriggerPressed()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.Controller, devices);
        foreach (var device in devices)
        {
            if (device.TryGetFeatureValue(CommonUsages.triggerButton, out bool pressed) && pressed)
                return true;
        }
        return false;
    }

    // MODIFIED: This entire method was restructured.
    private void HandleRunningState()
    {
        // ====================================================================
        // EYE & WINK STATE CALCULATION (ALWAYS RUNS)
        // This logic now runs on every frame to ensure our state is always current.
        // ====================================================================

        bool isLeftEyeRawOpen = eyetracking.eyeData.left.eye_openness > eyeOpennessThreshold;
        bool isRightEyeRawOpen = eyetracking.eyeData.right.eye_openness > eyeOpennessThreshold;

        leftOpenConsecutiveFrames = isLeftEyeRawOpen ? leftOpenConsecutiveFrames + 1 : 0;
        rightOpenConsecutiveFrames = isRightEyeRawOpen ? rightOpenConsecutiveFrames + 1 : 0;
        bothOpenConsecutiveFrames = (isLeftEyeRawOpen && isRightEyeRawOpen) ? bothOpenConsecutiveFrames + 1 : 0;

        bool isLeftEyeStableOpen = leftOpenConsecutiveFrames >= STABILITY_FRAME_THRESHOLD;
        bool isRightEyeStableOpen = rightOpenConsecutiveFrames >= STABILITY_FRAME_THRESHOLD;
        areBothEyesStableOpen = bothOpenConsecutiveFrames >= STABILITY_FRAME_THRESHOLD;

        if (!isLeftEyeStableOpen && isRightEyeStableOpen) { leftWinkFrameCount++; rightWinkFrameCount = 0; }
        else if (isLeftEyeStableOpen && !isRightEyeStableOpen) { rightWinkFrameCount++; leftWinkFrameCount = 0; }
        else { leftWinkFrameCount = 0; rightWinkFrameCount = 0; }

        float winkFrameThreshold = winkCanConfirmThresholdSeconds * (1 / Time.deltaTime);
        bool leftEyeIntentionalClosed = leftWinkFrameCount >= winkFrameThreshold;
        bool rightEyeIntentionalClosed = rightWinkFrameCount >= winkFrameThreshold;
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
        // GUARD CLAUSE
        // If we are in the middle of transitioning, pause all trial logic.
        // ====================================================================
        if (_isTransitioning) return;

        // ====================================================================
        // TRIAL INTERACTION LOGIC (Only runs when not transitioning)
        // ====================================================================
        cursor.transform.LookAt(mainCamera.transform);
        cursor.transform.Rotate(new Vector3(0, 180, 0));

        if (singleEyeIntentionalWink && !singleEyeIntentionalWink_prev)
        {
            cursorPositionAtWink = cursor.transform.position;
        }

        bool justStartedDrag = false;
        bool justReleasedDrag = false;
        bool justSelected = false;

        if (thisObject2Drag != null)
        {
            thisObject2Drag.transform.LookAt(mainCamera.transform);
            thisObject2Drag.transform.Rotate(new Vector3(0, 180, 0));
        }
        if (thisDropZone != null)
        {
            thisDropZone.transform.LookAt(mainCamera.transform);
            thisDropZone.transform.Rotate(new Vector3(0, 180, 0));
        }

        gameStates.targetSize = _targetSequenceSizes[_currentTrialIndex];
        gameStates.targetAmp = _targetSequenceAmplitudes[_currentTrialIndex];

        bool isHeadMoving = new Vector2(headtracking.headVel_Az, headtracking.headVel_Pol).magnitude > 15;

        if (singleEyeIntentionalWink && isHeadMoving)
        {
            if (!GameStateManager.Instance.isInDrag)
            {
                justStartedDrag = true;
                GameStateManager.Instance.SetDrag(true);
            }
        }
        if (areBothEyesStableOpen && singleEyeIntentionalWink_prev)
        {
            // The key change: We check if an object has been picked up during this trial.
            if (hasPickedup)
            {
                // If we picked something up, this action MUST be a drag-release.
                justReleasedDrag = true;
            }
            else
            {
                // If we never picked anything up, this action can only be a "select".
                justSelected = true;
            }

            // No matter what, if we're opening our eyes, the drag state must end.
            if (GameStateManager.Instance.isInDrag)
            {
                GameStateManager.Instance.SetDrag(false);
            }
        }

        singleEyeIntentionalWink_prev = singleEyeIntentionalWink;

        if (justStartedDrag) cursor.transform.position = cursorPositionAtWink;

        float cursor2ObjectAngle = HandleStartDrag();
        float cursor2DropZoneAngle = HandleEndDrag();

        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;

        bool pickupFailed = justStartedDrag && (cursor2ObjectAngle > currentSelectionThreshold);
        if (!hasPickedup) hasPickedup = justStartedDrag && (cursor2ObjectAngle <= currentSelectionThreshold);

        bool dwelledInDropZoneEnough;
        if (thisDropZone != null && thisDropZone.transform.Find("feedbackRing") != null)
        {
            dwelledInDropZoneEnough = _timeInDropZoneDwell > 0.1f;
            thisDropZone.transform.Find("feedbackRing").gameObject.SetActive(dwelledInDropZoneEnough);
        }

        bool dropFailed_offTarget = justReleasedDrag && (cursor2DropZoneAngle > dropZoneThreshold);
        bool dropSuccess = justReleasedDrag && (cursor2DropZoneAngle <= dropZoneThreshold);
        bool selectionFailed_selected = justSelected;
        bool hasTimedOut = Time.time - _timeAtTargetOnset > TrialTimeoutDuration;

        if (pickupFailed || dropFailed_offTarget || selectionFailed_selected || hasTimedOut)
        {
            errorSoundEffect.Play();
            if (pickupFailed) gameStates.failureMode = "offTarget_pickup";
            else if (selectionFailed_selected) gameStates.failureMode = "selected";
            else if (dropFailed_offTarget) gameStates.failureMode = "offTarget_drop";
            else if (hasTimedOut) gameStates.failureMode = "timedOut";
            StartCoroutine(AdvanceToNextTrialCoroutine());
        }
        else if (dropSuccess)
        {
            gameStates.failureMode = "success";
            alertSoundEffect.Play();
            StartCoroutine(AdvanceToNextTrialCoroutine());
        }

        float rollVelocity = ProcessHeadMovement();

        if (hasPickedup && GameStateManager.Instance.isInDrag && thisObject2Drag != null)
        {
            thisObject2Drag.transform.position = cursor.transform.TransformPoint(0, 0, 0.01f);
            thisObject2Drag.transform.LookAt(mainCamera.transform);
            thisObject2Drag.transform.Rotate(new Vector3(0, 180, 0));
        }

        gameStates.eventType = "";
        if (justStartedDrag) gameStates.eventType = "justStartedDrag";
        else if (justReleasedDrag) gameStates.eventType = "justReleasedDrag";
        else if (justSelected) gameStates.eventType = "justSelected";

        UpdateGameState(cursor2ObjectAngle, cursor2DropZoneAngle, rollVelocity);
    }
    #endregion

    #region --- Experiment Setup & Control ---

    // MODIFIED: Now calls the coroutine
    private void StartExperiment()
    {
        referenceTransform.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
        
        InstantiateAllTargets(); // This creates the master pool of targets
        
        _currentTrialIndex = -1; // Start at -1 so the first call to AdvanceToNextTrial sets up trial 0
        _currentBlockIndex = 0;
        _currentState = ExperimentState.Running;
        
        StartCoroutine(AdvanceToNextTrialCoroutine()); // This will now set up the very first trial
        
        Debug.Log("Experiment started.");
    }
    
    // MODIFIED: Replaced with the coroutine version
    private IEnumerator AdvanceToNextTrialCoroutine()
    {
        _isTransitioning = true;
        // Debug.Log("TRIAL ENDED. Waiting for both eyes to be open...");

        // Reset states
        hasDragged = false;
        hasPickedup = false;
        GameStateManager.Instance.SetDrag(false);
        singleEyeIntentionalWink_prev = false;

        if (thisObject2Drag != null && _currentTrialIndex >= 0 && _currentTrialIndex < _object2DragIndices.Count)
        {
            int dragIndex = _object2DragIndices[_currentTrialIndex];
            thisObject2Drag.transform.position = _initialTargetPositions[dragIndex];
            thisObject2Drag.transform.LookAt(referenceTransform.transform);
            thisObject2Drag.transform.Rotate(new Vector3(0, 180, 0));
        }
        
        // Hide targets during wait
        foreach (var target in _instantiatedTargets)
        {
            if (target != null) target.SetActive(false);
        }

        // Wait until the main update loop confirms eyes are stably open
        yield return new WaitUntil(() => areBothEyesStableOpen);

        // Debug.Log("EYES ARE OPEN! Starting next trial now...");

        // Proceed with setting up the next trial
        _currentTrialIndex++;

        if (_currentTrialIndex >= _object2DragIndices.Count)
        {
            EndSession();
            yield break; // Exit the coroutine
        }
        
        if (_currentTrialIndex > 0 && _currentTrialIndex % TargetsPerBlock == 0)
        {
            _currentBlockIndex++;
        }
        
        UpdateTargetActivation();

        _timeAtTargetOnset = Time.time;
        _timeInDropZoneDwell = 0f;

        // Transition is finished, allow main logic to resume
        _isTransitioning = false;
    }

    private void EndSession()
    {
        _currentState = ExperimentState.Completed;
        cursor.SetActive(false);
        DestroyAllTargets();
        Debug.Log("Session Completed :)");
    }
    #endregion

    #region --- Target & Sequence Generation ---

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

            var randomizedBlocks = blockConditions.OrderBy(b => Random.value).ToList();

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
            var randomizedBlocks = blockConditions.OrderBy(b => Random.value).ToList();

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
    
    private float HandleStartDrag()
    {
        if (thisObject2Drag == null) return -1;

        Vector3 cursorDir = cursor.transform.position - mainCamera.transform.position;
        Vector3 targetDir = thisObject2Drag.transform.position - mainCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);

        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;

        if (angle < currentSelectionThreshold) { SetTargetColor(thisObject2Drag, HoverColor); }
        else { SetTargetColor(thisObject2Drag, ActiveColor); }
        return angle;
    }
    
    private float HandleEndDrag()
    {
        if (thisDropZone == null) return -1;
        
        Vector3 cursorDir = cursor.transform.position - mainCamera.transform.position;
        Vector3 targetDir = thisDropZone.transform.position - mainCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);

        if (angle < dropZoneThreshold) { _timeInDropZoneDwell += Time.deltaTime; }
        else
        {
            SetTargetColor(thisDropZone, DropZoneColor);
            _timeInDropZoneDwell = 0;
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

    private void UpdateGameState(float cursor2ObjectAngle, float cursor2DropZoneAngle, float rollVelocity)
    {
        GameStateManager.Instance.frameOfCentering = false;
        gameStates.trialIndex = _currentTrialIndex;
        gameStates.cursorInWorldPosition = cursor.transform.position;
        gameStates.cursorInHmdPosition = mainCamera.transform.InverseTransformPoint(cursor.transform.position);

        if (thisObject2Drag != null)
        {
            gameStates.object2DragInWorldPosition = thisObject2Drag.transform.position;
            gameStates.object2DragInHmdPosition = mainCamera.transform.InverseTransformPoint(thisObject2Drag.transform.position);
        }

        if (thisDropZone != null)
        {
            gameStates.DropzoneInWorldPosition = thisDropZone.transform.position;
            gameStates.DropzoneInHmdPosition = mainCamera.transform.InverseTransformPoint(thisDropZone.transform.position);
        }

        gameStates.cursorDropzoneAngle = cursor2DropZoneAngle;
        gameStates.cursorObjectAngle = cursor2ObjectAngle;
        GameStateManager.Instance.hmdRollVelocity = rollVelocity;
        GameStateManager.Instance.hmdAngularDeltaFromNeutral = GetLocalAngularDeltaVectorFromNeutral(mainCamera.transform.rotation, mainCamera.transform);

        isInDrag_prev = GameStateManager.Instance.isInDrag;
        
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