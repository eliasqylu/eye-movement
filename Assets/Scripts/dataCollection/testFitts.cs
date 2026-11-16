using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using j_utils; // Assuming this contains eyeHead_utils and other dependencies

public class testFitts : MonoBehaviour
{
    // Enum to manage the state of the experiment for a clearer workflow
    private enum ExperimentState { Idle, Running, Completed }
    private ExperimentState _currentState = ExperimentState.Idle;

    [Header("Object References")]
    [Tooltip("The user-controlled cursor object.")]
    public GameObject cursor;
    [Tooltip("A template/prefab for the target object.")]
    public GameObject targetPrefab;
    [Tooltip("The main XR camera for calculating positions and rotations.")]
    Camera xrCamera;
    [Tooltip("An empty GameObject to store the initial HMD orientation.")]
    public GameObject referenceTransform;
    [Tooltip("Sound effect to play on trial timeout.")]
    public AudioSource errorSoundEffect;

    [Header("Fitts' Law Settings")]
    [Tooltip("The angular amplitudes (in degrees) for each block of targets.")]
    public float[] fittsAmplitudes = { 10.0f, 30.0f };
    [Tooltip("Time in seconds the cursor must remain on a target to select it.")]
    public float dwellThreshold = 0.8f;
    [Tooltip("Time in seconds before a trial automatically fails.")]
    private const float TrialTimeoutDuration = 10.0f;
    private const int TargetsPerBlock = 11;

    [Header("Head Movement Settings")]
    [Tooltip("The threshold for detecting significant head roll velocity.")]
    public float rollVelocityThreshold;

    // Private State Variables
    private List<Vector3> _targetSequenceHmdPositions = new List<Vector3>();
    private List<GameObject> _instantiatedTargets = new List<GameObject>();
    private int _currentTrialIndex = 0;
    private int _currentBlockIndex = 0;

    private float _timeInDwell = 0;
    private float _timeAtTargetOnset;
    
    private float _selectionAngleThreshold;
    private Quaternion _previousHmdRotation;

    private static readonly Color DefaultColor = new Color(1, 1, 1, 0.5f);
    private static readonly Color HoverColor = Color.cyan;
    private static readonly Color ActiveColor = Color.black;

    #region --- Unity Lifecycle Methods ---

    void Start()
    {
        xrCamera = Camera.main;
        SetupExperiment();
    }

    void Update()
    {
        // Use a state machine for clarity
        switch (_currentState)
        {
            case ExperimentState.Idle:
                HandleIdleState();
                break;

            case ExperimentState.Running:
                HandleRunningState();
                break;

            case ExperimentState.Completed:
                // Do nothing if the session is over
                break;
        }
    }

    #endregion

    #region --- State Management ---

    /// <summary>
    /// Waits for user input to begin the experiment.
    /// </summary>
    private void HandleIdleState()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            StartExperiment();
        }
    }

    /// <summary>
    /// Manages the active experiment loop each frame.
    /// </summary>
    private void HandleRunningState()
    {
        float cursorTargetAngle = HandleDwellSelection();
        float rollVelocity = ProcessHeadMovement();

        bool isDwelling = _timeInDwell >= dwellThreshold;
        bool hasTimedOut = Time.time - _timeAtTargetOnset > TrialTimeoutDuration;

        if (isDwelling || hasTimedOut)
        {
            if (hasTimedOut) errorSoundEffect.Play();
            AdvanceToNextTrial();
        }

        UpdateGameState(cursorTargetAngle, rollVelocity);
    }

    #endregion

    #region --- Experiment Setup & Control ---

    /// <summary>
    /// Prepares the experiment by generating the target sequence.
    /// </summary>
    private void SetupExperiment()
    {
        // Calculate the selection threshold based on target size
        float targetSize = targetPrefab.transform.localScale.x;
        // This calculation seems specific; kept it as is. Comment explains its likely purpose.
        // It likely converts a fixed world-space size to an angular size threshold.
        _selectionAngleThreshold = 5.0f / 0.0873f * targetSize; 

        // Generate the full sequence of targets for all blocks
        GenerateFittsLawSequence();
    }

    /// <summary>
    /// Kicks off the experiment, instantiating targets and setting initial state.
    /// </summary>
    private void StartExperiment()
    {
        // Set the reference orientation for the entire session
        referenceTransform.transform.SetPositionAndRotation(xrCamera.transform.position, xrCamera.transform.rotation);
        
        DestroyAllTargets();
        InstantiateTargetRing();
        UpdateTargetActivation();

        // Set initial state
        _currentTrialIndex = 0;
        _timeAtTargetOnset = Time.time;
        _currentState = ExperimentState.Running;
        
        // Update visuals for the first target
        SetTargetColor(_currentTrialIndex, ActiveColor);

        Debug.Log("Experiment started.");
    }
    
    /// <summary>
    /// Advances the experiment to the next trial or completes the session.
    /// </summary>
    private void AdvanceToNextTrial()
    {
        // Set previous target back to the default color
        SetTargetColor(_currentTrialIndex, DefaultColor);
        _currentTrialIndex++;

        // Check if the entire session is complete
        if (_currentTrialIndex >= _targetSequenceHmdPositions.Count)
        {
            EndSession();
            return;
        }

        // Check if we need to advance to the next block
        if (_currentTrialIndex > 0 && _currentTrialIndex % TargetsPerBlock == 0)
        {
            _currentBlockIndex++;
            UpdateTargetActivation();
        }

        // Reset timers and update the new target's visuals
        _timeAtTargetOnset = Time.time;
        _timeInDwell = 0f;
        SetTargetColor(_currentTrialIndex, ActiveColor);
    }
    
    /// <summary>
    /// Cleans up and marks the session as completed.
    /// </summary>
    private void EndSession()
    {
        _currentState = ExperimentState.Completed;
        cursor.SetActive(false);
        DestroyAllTargets();
        Debug.Log("Session Completed :)");
    }

    #endregion

    #region --- Target & Sequence Generation ---

    /// <summary>
    /// Generates and shuffles the sequence of targets for all Fitts' Law blocks.
    /// </summary>
    private void GenerateFittsLawSequence()
    {
        _targetSequenceHmdPositions.Clear();
        const float distance = 1.0f; // Keep targets 1m away

        // Randomize the order of the amplitude blocks for counterbalance
        var randomizedAmplitudes = fittsAmplitudes.OrderBy(a => Random.value).ToList();
        Debug.Log($"Block order: {randomizedAmplitudes[0]} deg, then {randomizedAmplitudes[1]} deg.");

        foreach (float amplitude in randomizedAmplitudes)
        {
            // A. Calculate the 11 unique target positions for the current amplitude
            var ringPositions = new List<Vector3>();
            for (int i = 0; i < TargetsPerBlock; i++)
            {
                float angleOnRing = i * (360.0f / TargetsPerBlock);
                float azimuth = amplitude * Mathf.Cos(angleOnRing * Mathf.Deg2Rad);
                float polar = amplitude * Mathf.Sin(angleOnRing * Mathf.Deg2Rad);

                Vector3 targetDirection = eyeHead_utils.ficks2vec(azimuth, polar);
                ringPositions.Add(targetDirection * distance);
            }

            // B. Create the trial sequence by jumping to the opposite target
            int currentIndex = 0;
            int jump = TargetsPerBlock / 2; // e.g., jump of 5 for 11 targets
            for (int i = 0; i < TargetsPerBlock; i++)
            {
                _targetSequenceHmdPositions.Add(ringPositions[currentIndex]);
                currentIndex = (currentIndex + jump) % TargetsPerBlock;
            }
        }
    }
    
    /// <summary>
    /// Instantiates all GameObjects for the target ring based on the generated sequence.
    /// </summary>
    private void InstantiateTargetRing()
    {
        targetPrefab.SetActive(false); // Use the target as a prefab
        for (int i = 0; i < _targetSequenceHmdPositions.Count; i++)
        {
            // Position targets relative to the initial reference orientation
            Vector3 worldPos = referenceTransform.transform.TransformPoint(_targetSequenceHmdPositions[i]);
            GameObject targetInstance = Instantiate(targetPrefab, worldPos, Quaternion.identity);
            
            SetTargetColor(targetInstance, DefaultColor);
            _instantiatedTargets.Add(targetInstance);
        }
    }

    /// <summary>
    /// Activates targets for the current block and ensures they face the camera.
    /// </summary>
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
                // Make targets always face the user
                target.transform.LookAt(xrCamera.transform);
                target.transform.Rotate(0, 180, 0); // Correct for LookAt's forward direction
            }
        }
    }
    
    #endregion

    #region --- Per-Frame Logic & Calculations ---

    /// <summary>
    /// Checks if the cursor is dwelling on the current target and updates visuals.
    /// </summary>
    /// <returns>The angle between the cursor and the target.</returns>
    private float HandleDwellSelection()
    {
        if (_currentTrialIndex >= _instantiatedTargets.Count) return -1;
        
        GameObject currentTarget = _instantiatedTargets[_currentTrialIndex];
        Vector3 cursorDir = cursor.transform.position - xrCamera.transform.position;
        Vector3 targetDir = currentTarget.transform.position - xrCamera.transform.position;
        float angle = Vector3.Angle(cursorDir, targetDir);

        if (angle < _selectionAngleThreshold)
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

    /// <summary>
    /// Calculates head roll velocity and direction.
    /// </summary>
    /// <returns>The calculated roll velocity.</returns>
    private float ProcessHeadMovement()
    {
        Quaternion currentHmdRotation = xrCamera.transform.rotation;
        float rollVelocity = GetRollVelocity(currentHmdRotation, _previousHmdRotation, xrCamera.transform);
        _previousHmdRotation = currentHmdRotation;
        
        // Update external state manager for roll direction
        GameStateManager.Instance.rollDirection = DetectRollDirection(rollVelocity, rollVelocityThreshold);
        return rollVelocity;
    }

    /// <summary>
    /// Updates external game state managers with current trial data.
    /// </summary>
    private void UpdateGameState(float cursorTargetAngle, float rollVelocity)
    {
        // This section centralizes all writes to external state managers
        GameStateManager.Instance.frameOfCentering = false;
        gameStates.trialIndex = _currentTrialIndex;
        gameStates.cursorInWorldPosition = cursor.transform.position;
        gameStates.cursorInHmdPosition = xrCamera.transform.InverseTransformPoint(cursor.transform.position);

        if (_currentTrialIndex < _instantiatedTargets.Count)
        {
            GameObject currentTarget = _instantiatedTargets[_currentTrialIndex];
            gameStates.targetInWorldPosition = currentTarget.transform.position;
            gameStates.targetInHmdPosition = xrCamera.transform.InverseTransformPoint(currentTarget.transform.position);
        }

        gameStates.cursorTargetAngle = cursorTargetAngle;
        GameStateManager.Instance.hmdRollVelocity = rollVelocity;
        GameStateManager.Instance.hmdAngularDeltaFromNeutral = GetLocalAngularDeltaVectorFromNeutral(xrCamera.transform.rotation, xrCamera.transform);
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

    /// <summary>
    /// Safely destroys all instantiated target objects and clears the list.
    /// </summary>
    private void DestroyAllTargets()
    {
        foreach (GameObject target in _instantiatedTargets)
        {
            if (target != null) Destroy(target);
        }
        _instantiatedTargets.Clear();
    }

    // --- Static & Public Utility Functions (Kept from original) ---

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
        Quaternion rotationDelta = currentRotation * referenceTransform.transform.rotation;
        rotationDelta.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);
        return trackedTransform.InverseTransformDirection(rotationAxis) * angleInDegrees;
    }

    public int DetectRollDirection(float currentRollVelocity, float rollVelocityThreshold)
    {
        if (Mathf.Abs(currentRollVelocity) <= rollVelocityThreshold) return 0; // No significant roll
        return currentRollVelocity > 0 ? 1 : -1; // Right : Left
    }
    
    #endregion
}