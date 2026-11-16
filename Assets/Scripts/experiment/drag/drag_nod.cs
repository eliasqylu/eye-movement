using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using j_utils; // Assuming this contains eyeHead_utils and other dependencies

public class drag_nod : MonoBehaviour
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
    private const float BaseTargetAngle = 5.0f;    // The visual angle (in deg) of the base prefab
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
    [SerializeField] private NodStateMachine nodController;
    private bool isInDrag_prev;

    private static readonly Color DefaultColor = new Color(1, 1, 1, 0.1f);
    private static readonly Color HoverColor = Color.red;
    private static readonly Color ActiveColor = Color.white;
    private static readonly Color DropZoneColor = Color.blue;

    bool hasDragged;
    bool hasPickedup;

    #region --- Unity Lifecycle & State ---

    void Start()
    {
        fittsAmplitudes = fittsAmplitudes.Select(a => a * 0.5f).ToArray();
        cursor.SetActive(true);
        if (mainCamera == null) mainCamera = Camera.main;
        if (nodController == null) nodController = gameObject.GetComponent<NodStateMachine>();
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
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartExperiment();
        }
    }

    private void HandleRunningState()
    {
        GameStateManager.Instance.overwriteValue = -1;
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
        
        // This nod logic is identical to your original script.
        if (!GameStateManager.Instance.canConfirm && GameStateManager.Instance.prediction == 2)
        {
            GameStateManager.Instance.SetCanConfirm(true);
        }
        if (GameStateManager.Instance.canConfirm)
        {
            if (GameStateManager.Instance.resetNod)
            {
                GameStateManager.Instance.SetCanConfirm(false);
                GameStateManager.Instance.SetResetNod(false);
                nodController.ResetToIdle();
                return;
            }
        }

        bool justStartedDrag = false;
        if (!hasDragged && GameStateManager.Instance.isInDrag)
        {
            hasDragged = true;
            justStartedDrag = true;
        }
        bool justSelected = GameStateManager.Instance.canConfirm && GameStateManager.Instance.confirmed && !hasDragged;
        bool justReleasedDrag = GameStateManager.Instance.confirmed && hasDragged;

        if (justSelected)
        {
            nodController.ResetToIdle();
            GameStateManager.Instance.SetCanConfirm(false);
        }
        else if (justReleasedDrag)
        {
            nodController.ResetToIdle();
            GameStateManager.Instance.SetCanConfirm(false);
            GameStateManager.Instance.SetDrag(false);
        }

        float cursor2ObjectAngle = HandleStartDrag();
        float cursor2DropZoneAngle = HandleEndDrag();
        
        float currentSelectionThreshold = _targetSequenceSizes[_currentTrialIndex] / 2.0f;

        bool pickupFailed = justStartedDrag && (cursor2ObjectAngle > currentSelectionThreshold); // Note: Changed to '>' for failed pickup
        if (!hasPickedup) hasPickedup = justStartedDrag && (cursor2ObjectAngle <= currentSelectionThreshold);

        bool dwelledInDropZoneEnough;
        if(thisDropZone != null && thisDropZone.transform.Find("feedbackRing") != null)
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
            AdvanceToNextTrial();
        }
        else if (dropSuccess)
        {
            gameStates.failureMode = "success";
            alertSoundEffect.Play();
            AdvanceToNextTrial();
        }
        
        float rollVelocity = ProcessHeadMovement();

        if (hasPickedup && GameStateManager.Instance.isInDrag && thisObject2Drag!=null)
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

    private void StartExperiment()
    {
        referenceTransform.transform.SetPositionAndRotation(mainCamera.transform.position, mainCamera.transform.rotation);
        
        InstantiateAllTargets(); // This creates the master pool of targets
        
        _currentTrialIndex = -1; // Start at -1 so the first call to AdvanceToNextTrial sets up trial 0
        _currentBlockIndex = 0;
        _currentState = ExperimentState.Running;
        
        AdvanceToNextTrial(); // This will now set up the very first trial
        
        Debug.Log("Experiment started.");
    }
    
    private void AdvanceToNextTrial()
    {
        hasDragged = false;
        hasPickedup = false;
        GameStateManager.Instance.overwriteValue = 0;
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
        
        // Add bounds checking before updating target activation
        int dragIndex_new = _object2DragIndices[_currentTrialIndex];
        int dropIndex_new = _dropzoneIndices[_currentTrialIndex];
        
        if (dragIndex_new >= _instantiatedTargets.Count || dropIndex_new >= _instantiatedTargets.Count)
        {
            Debug.LogError($"Index out of bounds! dragIndex: {dragIndex_new}, dropIndex: {dropIndex_new}, total targets: {_instantiatedTargets.Count}");
            EndSession();
            return;
        }
        
        // This is the key: update the active targets for the new trial
        UpdateTargetActivation();

        _timeAtTargetOnset = Time.time;
        _timeInDropZoneDwell = 0f;
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

                // FIXED: Calculate correct block offset
                // Each condition creates TargetsPerBlock targets
                blockOffset += TargetsPerBlock;
            }
        }
        
        // Debug output to verify indices
        Debug.Log($"Generated {_object2DragIndices.Count} trials");
        Debug.Log($"Max drag index: {_object2DragIndices.Max()}");
        Debug.Log($"Max drop index: {_dropzoneIndices.Max()}");
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

        if (angle < currentSelectionThreshold)
        {
            SetTargetColor(thisObject2Drag, HoverColor);
        }
        else
        {
            SetTargetColor(thisObject2Drag, ActiveColor);
        }
        return angle;
    }
    
    private float HandleEndDrag()
    {
        if (thisDropZone == null) return -1;
        
        Vector3 cursorDir = cursor.transform.position - mainCamera.transform.position;
        Vector3 targetDir = thisDropZone.transform.position - mainCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);

        if (angle < dropZoneThreshold)
        {
            _timeInDropZoneDwell += Time.deltaTime;
        }
        else
        {
            // Reset color to the standard drop zone color, not active color
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
        if (!GameStateManager.Instance.canConfirm && GameStateManager.Instance.canConfirm_prev)
        {
            nodController.ResetToIdle();
        }
        
        GameStateManager.Instance.UpdateAllPreviousStates();
    }
    
    #endregion
    
    #region --- Utility & Helper Functions ---

    // This overload is no longer needed with the new index-based system, but kept for compatibility if used elsewhere.
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
        // Simply destroy all objects in the master list
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