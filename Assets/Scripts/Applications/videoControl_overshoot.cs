using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using System.Globalization;
using System.IO;
using System;
using j_utils; // Assuming this namespace contains eyeHead_utils, headtracking, eyetracking
using UnityEngine.UI;
using Unity.Mathematics;

public class videoControl_overshoot : MonoBehaviour
{
    public headtracking  headtrackingScript;
    public GameObject cursor;
    [Header("Video Setup")]
    public VideoPlayer videoPlayer; // Drag your TV_Screen's VideoPlayer component here
    public GameObject playPauseButton; // Drag your PlayPause_Button GameObject here
    public GameObject timeSeekSliderTrack; // Drag your VideoSeek_SliderTrack GameObject here
    public GameObject timeSeekHandle;
    
    int interactableLayer = 1 << 5;
    CursorInteractable currentHoveredObject;
    float sliderTrackLength;
    bool isDraggingSlider;


    [Header("References")]
    private Camera xrCamera;
    public bool freezeCursorForSelecting = false;
    public GameObject gazeCursor; // This GameObject is for eye gaze visualization
    GameObject gazeCursorRing;
    public GameObject xrCam0_proxy;
    Vector3 gazeVector;
    
    bool headInGazeRing_prev;
    Vector3 gazeCursorRingInHeadVec;

    bool lookForDwell;
    bool isHeadFixation_prev;
    float dwelledTime_head = -1f;
    public float dwellTimeThreshold;
    Material gazeCursorRingMaterial;
    bool ringUpdated;
    bool freezeGaze;
    bool dragEnabled;
    float gazeRingSize = 4.5f;
    Vector3 gazeCursorRingScale_orig;
    float gazeHeadAngle;
    float gazeHeadAngle_prev;
    float timeOfHit;
    int collisionPart = 0;
    int collideCode = 0;
    int headToGazePossibleFrames = 0;
// --- Overshoot Detection Variables ---
    private OvershootState _overshootDetectionState = OvershootState.None;
    private float _firstEntryAngle; // Angle of (gazeVector - Vector3.forward) at first entry
    private float _firstEntryTime;
    private float _lastExitTime; // Time when exiting the ring
    private float _currentEntryAngle; // Angle of (gazeVector - Vector3.forward) at second entry
    public float overshootAngleTolerance = 40f; // Tolerance for angle difference (e.g., 10 deg)
    public float maxOvershootTime = 1.0f; // Max time between first entry and second entry for overshoot
    public float minOvershootDuration = 0.05f; // Minimum time between entry/exit to prevent jitter false positives

    /// <summary>
    /// States for tracking overshoot gesture.
    /// </summary>
    private enum OvershootState
    {
        None,           // Not in an overshoot sequence.
        EnteredOnce,    // Entered the ring the first time.
        ExitedOnce,     // Exited the ring after a first entry.
    }

    /// <summary>
    /// Defines the possible interaction states (replacing segments).
    /// </summary>
    public enum InteractionState
    {
        None,       // No interaction active.
        Selecting,  // Single selection mode (dwell).
        Dragging    // Continuous dragging mode (overshoot).
    }

    // Public variable to store the most recently determined interaction state
    public InteractionState currentInteractionState = InteractionState.None;



    void Start()
    {
        ConfigureHeadtracking();
        xrCamera = Camera.main;
        sliderTrackLength = timeSeekSliderTrack.transform.localScale.x;

        // Locate the gazeCursorRing child object and get its material
        if (gazeCursor != null)
        {
            Transform ringTransform = gazeCursor.transform.Find("eyeGestureControl"); // Still looking for this name
            if (ringTransform != null)
            {
                gazeCursorRing = ringTransform.gameObject;
                gazeCursorRingScale_orig = gazeCursorRing.transform.localScale;
                gazeCursorRingMaterial = gazeCursorRing.GetComponent<Renderer>().material;
            }
            else
            {
                Debug.LogError("Could not find 'eyeGestureControl' child object under GazeCursor. Ensure the name matches and it uses eyeGestureControl3.shader.", this);
            }
        }
        else
        {
            Debug.LogError("Gaze Cursor GameObject is not assigned in the inspector.", this);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (currentInteractionState == InteractionState.None)
        {
            gazeCursorRingMaterial.SetColor("_Color", Color.white);
        }
        if (!isDraggingSlider && videoPlayer.isPrepared)
        {
            UpdateSeekHandlePosition();
        }
        if (isDraggingSlider)
        {
            UpdateVideoSeekFromCursor();
            UpdateSeekHandlePosition();
        }

        HandleGazeInteraction();
        GameStateManager.Instance.frameOfCentering = false; //

        // Start experiment on space key
        if (Input.GetKeyDown(KeyCode.Space)) //
        {
            xrCam0_proxy.transform.position = xrCamera.transform.position; //
            xrCam0_proxy.transform.rotation = xrCamera.transform.rotation; //
            GameStateManager.Instance.frameOfCentering = true; //
        }

        gazeVector = 0.5f * (eyetracking.leftEyeLocalForward + eyetracking.rightEyeLocalForward);
        if (gazeCursor != null)
        {
            // Preserve the original freezeCursorForSelecting behavior
            if (freezeCursorForSelecting == false || (freezeCursorForSelecting && freezeGaze == false))
            {
                gazeCursor.transform.position = xrCamera.transform.TransformPoint(gazeVector);
            }
            gazeCursor.transform.LookAt(xrCamera.transform);
            gazeCursor.transform.Rotate(0, 180, 0); // Correct for LookAt behavior
        }
        else
        {
            Debug.LogError("Gaze Cursor is null, cannot update its transform. Please assign the gazeCursor GameObject in the Inspector.", this);
            return; // Exit Update if a critical component is missing
        }

        gazeCursorRingInHeadVec = xrCamera.transform.InverseTransformPoint(gazeCursorRing.transform.position);
        bool headInGazeRing = Vector3.Angle(gazeCursorRingInHeadVec, Vector3.forward) < gazeRingSize / 2f;

        // Determine eye and head movement states
        float gaze2gazeRingAng = Vector3.Angle(gazeVector, gazeCursorRingInHeadVec);
        bool eyeMoving = gaze2gazeRingAng > gazeRingSize * 0.75f || new Vector2(eyetracking.eyeInWorldVel_Az, eyetracking.eyeInWorldVel_Pol).magnitude > 160f; // Assuming eyetracking exists
        float headVel = new Vector2(headtracking.headVel_Az, headtracking.headVel_Pol).magnitude;
        bool headMoving = !headtracking.isHeadInFixation; //new Vector2(headtracking.headVel_Az, headtracking.headVel_Pol).magnitude > 2f; // Assuming headtracking exists

        bool eyeHeadCloseEnoughforVisual = Vector3.Angle(gazeVector, Vector3.forward) < 10;
        gazeHeadAngle = Vector3.Angle(gazeVector, Vector3.forward);
        // bool headMovingToGaze = gazeHeadAngle < gazeHeadAngle_prev && headVel > 2 && eyeMoving == false;
        // bool headMovingAwayGaze = gazeHeadAngle > gazeHeadAngle_prev && headMoving && eyeMoving == false;
        // headMovingToGaze = IsHeadMovingTowardsGaze(headVel > 2, eyeMoving, 1, 0.5f);
        int frameThreshold = 5;
        bool headMovingToGaze = IsHeadMovingTowardsGaze2(eyeMoving, headVel > 5, frameThreshold);

        if (headMovingToGaze && eyeHeadCloseEnoughforVisual)
        {
            if (gazeCursorRing != null)
            {
                gazeCursorRing.SetActive(true);
                if (ringUpdated == false)
                {
                    freezeGaze = true; // Preserve original freezeGaze behavior
                }
                ringUpdated = true;
            }
        }
        else if (eyeMoving) // If eye is moving, deactivate the ring and reset its update flag
        {
            if (gazeCursorRing != null)
            {
                gazeCursorRing.SetActive(false);
            }
            ringUpdated = false;
            freezeGaze = false;
            dragEnabled = false;
            currentInteractionState = InteractionState.None;
        }
        
        // --- Overshoot Detection Logic ---
        // Calculate the raw angle for the current frame
        Vector3 vectorGaze2HeadCurrent = gazeVector - Vector3.forward;
        float currentRawAngleGaze2HeadDeg = CalculateAngleFromEast(vectorGaze2HeadCurrent);

        // State machine for overshoot detection
        if (headInGazeRing)
        {
            // Declare these here so they are in scope for all conditions below within this 'if' block.
            float timeSinceFirstEntry = 0f;
            float timeSinceLastExit = 0f;

            if (!headInGazeRing_prev) // Just entered the ring, and moving towards center
            {
                Debug.Log($"Entered ring. OvershootState: {_overshootDetectionState}");
                if (_overshootDetectionState == OvershootState.None)
                {
                    // First entry
                    _overshootDetectionState = OvershootState.EnteredOnce;
                    _firstEntryAngle = currentRawAngleGaze2HeadDeg;
                    _firstEntryTime = Time.time;
                    Debug.Log($"First Entry: Angle={_firstEntryAngle:F2}deg, Time={_firstEntryTime:F2}");
                    // Reset interaction state to Selecting by default upon first entry
                    currentInteractionState = InteractionState.Selecting;
                    dragEnabled = false; // Ensure drag is off for normal selection
                }
                else if (_overshootDetectionState == OvershootState.ExitedOnce)
                {
                    // Second entry - potential overshoot
                    _currentEntryAngle = currentRawAngleGaze2HeadDeg;
                    timeSinceFirstEntry = Time.time - _firstEntryTime;
                    timeSinceLastExit = Time.time - _lastExitTime;

                    // Calculate angular difference, considering a 180-degree flip for opposite sides
                    float angleDifference = Mathf.Abs(WrapAngleDeg(_firstEntryAngle + 180f) - _currentEntryAngle);
                    angleDifference = Mathf.Min(angleDifference, 360f - angleDifference); // Shortest angular distance

                    Debug.Log($"Second Entry: Angle={_currentEntryAngle:F2}deg, FirstAngleFlipped={WrapAngleDeg(_firstEntryAngle + 180f):F2}deg, AngleDiff={angleDifference:F2}deg, TimeSinceFirstEntry={timeSinceFirstEntry:F2}s, TimeSinceLastExit={timeSinceLastExit:F2}s");

                    if (angleDifference <= overshootAngleTolerance &&
                        timeSinceFirstEntry <= maxOvershootTime &&
                        timeSinceLastExit >= minOvershootDuration) // Ensure it wasn't just a brief flicker out/in
                    {
                        Debug.Log("Overshoot DETECTED!");
                        currentInteractionState = InteractionState.Dragging;
                        // For dragging, once overshoot is detected, enable continuous marker placement *after* dwell.
                        // We do NOT set dragEnabled = true here directly. It will be set after dwell below.
                        // Also adjust ring size/scale.
                        gazeRingSize = 13.8f;//9;
                        gazeCursorRing.transform.localScale = gazeCursorRingScale_orig * 2;

                        // Reset overshoot state after successful detection for a new sequence
                        _overshootDetectionState = OvershootState.None;
                    }
                    else
                    {
                        Debug.Log("No overshoot (failed conditions).");
                        // If no overshoot, treat this as a new single entry sequence
                        currentInteractionState = InteractionState.Selecting;
                        dragEnabled = false;
                        _overshootDetectionState = OvershootState.EnteredOnce; // Start new sequence from this entry
                        _firstEntryAngle = currentRawAngleGaze2HeadDeg;
                        _firstEntryTime = Time.time;
                    }
                }
            }
        }
        else // Head is OUT of gaze (outside the ring)
        {
            if (headInGazeRing_prev) // Just exited the ring
            {
                Debug.Log($"Exited ring. OvershootState: {_overshootDetectionState}");
                if (_overshootDetectionState == OvershootState.EnteredOnce)
                {
                    _overshootDetectionState = OvershootState.ExitedOnce;
                    _lastExitTime = Time.time;
                    Debug.Log($"Exited Once: LastExitTime={_lastExitTime:F2}");
                }
                else
                {
                    // Reset all overshoot state if exited without a prior 'EnteredOnce'
                    // This covers cases where 'Dragging' was active and head exited.
                    ResetOvershootState();
                }

                if (_overshootDetectionState != OvershootState.ExitedOnce)
                {
                    gazeCursorRing.SetActive(false);
                    gazeCursorRingMaterial.SetColor("_Color", Color.white);
                }

            }
            else // Still outside the ring (headInGazeRing_prev is also false)
            {
                // If we were in ExitedOnce state but haven't re-entered quickly enough, reset.
                if (_overshootDetectionState == OvershootState.ExitedOnce && (Time.time - _lastExitTime > maxOvershootTime))
                {
                    Debug.Log("Overshoot sequence timed out. Resetting.");
                    ResetOvershootState();
                }
            }
            // General reset for colors/size when fully out
            currentInteractionState = InteractionState.None;
            dragEnabled = false;
            // gazeCursorRingMaterial.SetColor("_Color", Color.white);
            gazeRingSize = 6.9f;//4.5f;
            gazeCursorRing.transform.localScale = gazeCursorRingScale_orig;


        }

        // --- Dwell Logic & Marker Placement ---
        // This is where dwell is tracked and actions are triggered based on interaction state.
        if (headInGazeRing) // Only dwell if head is in gaze and eye is not moving
        {
            if (!headInGazeRing_prev) // Just entered head fixation
            {

                lookForDwell = true;
                // Update ring color based on current interaction state during dwell
                if (currentInteractionState == InteractionState.Selecting)
                {
                    gazeCursorRingMaterial.SetColor("_Color", Color.green); // Green for Selecting
                }
                else if (currentInteractionState == InteractionState.Dragging)
                {
                    gazeCursorRingMaterial.SetColor("_Color", Color.blue); // Blue for Dragging
                    // freezeGaze = false; // Gaze is NOT frozen during dragging (as per user's earlier comment)
                    gazeRingSize = 13.8f; //9;
                    gazeCursorRing.transform.localScale = gazeCursorRingScale_orig * 2;
                }
            }
        }
        else // Not dwelling (e.g., eye moving, head moving away, or not in gaze)
        {
            lookForDwell = false;
            dragEnabled = false;
            dwelledTime_head = -1;
            // dragEnabled is only set to false on full exit or successful overshoot check failure.
            // Color reset is handled in the main 'else' block for !headInGazeRing.
        }

        //count up head fixation dwell time
        if (lookForDwell && isHeadFixation_prev == false && headtracking.isHeadInFixation)
        {
            dwelledTime_head = 0;
        }
        if (lookForDwell && headtracking.isHeadInFixation)
        {
            dwelledTime_head += Time.deltaTime;
        }

        //place one marker/click (only if currently in "Selecting" segment)
        if (currentInteractionState == InteractionState.Selecting && lookForDwell && dwelledTime_head > dwellTimeThreshold)
        {
            PlayBackControl_selected();
            lookForDwell = false;
        }
        else if (currentInteractionState == InteractionState.Dragging && lookForDwell && dwelledTime_head > dwellTimeThreshold)
        {
            dragEnabled = true;
        }

        if (dragEnabled)
        {
            freezeGaze = false;
            PlayBackControl_dragged();
        }
        else
        {
            isDraggingSlider = false;
        }

    
        // Update previous states for the next frame's calculations
        isHeadFixation_prev = headtracking.isHeadInFixation;
        headInGazeRing_prev = headInGazeRing;
        gazeHeadAngle_prev = gazeHeadAngle;

        if (gazeCursorRing.activeSelf && !dragEnabled)
        {
            freezeGaze = true;
        }
        if (currentInteractionState != InteractionState.Dragging)
        {
            isDraggingSlider = false;
        }
    
    }

    private void ConfigureHeadtracking()
    {
        Debug.Log("Configuring Headtracking script...");

        // 1. Change the debugObject_Cursor and cursor GameObject references
        //    to be the same object as headCursor.
        if (headtrackingScript.headCursor != null)
        {
            headtrackingScript.debugObject_Cursor = headtrackingScript.headCursor;
            headtrackingScript.cursor = headtrackingScript.headCursor;
            Debug.Log("debugObject_Cursor and cursor now point to headCursor.");
        }
        else
        {
            Debug.LogWarning("headCursor is not assigned in the Headtracking script. Cannot reassign debugObject_Cursor and cursor.");
        }


        // 2. Toggle off useCentroidForCursor and detectCursorFixation
        headtrackingScript.useCentroidForCursor = false;
        headtrackingScript.detectCursorFixation = false;
        Debug.Log("useCentroidForCursor and detectCursorFixation toggled OFF.");

        // 3. Toggle on useCentroidForHeadFov and detectHeadFovFixation
        headtrackingScript.useCentroidForHeadFov = true;
        headtrackingScript.detectHeadFovFixation = true;
        Debug.Log("useCentroidForHeadFov and detectHeadFovFixation toggled ON.");

        Debug.Log("Headtracking configuration complete.");
    }

    private void ResetOvershootState()
    {
        _overshootDetectionState = OvershootState.None;
        _firstEntryAngle = 0f;
        _firstEntryTime = 0f;
        _lastExitTime = 0f;
        _currentEntryAngle = 0f;
        Debug.Log("Overshoot detection state fully reset.");
    }

    private void UpdateGazeRingDirection(float segment1CenterAngleDeg)
    {
        if (gazeCursorRingMaterial == null)
        {
            Debug.LogError("Gaze Cursor Ring Material is not assigned. Cannot update shader property.", this);
            return;
        }
        // Pass the pre-calculated and offset angle directly to the shader.
        gazeCursorRingMaterial.SetFloat("_Segment1CenterAngleDeg", segment1CenterAngleDeg);
    }

    private float CalculateAngleFromEast(Vector3 vector)
    {
        // Handle zero vector to avoid Mathf.Atan2(0,0) issues.
        // If vector is (0,0,Z), its XY projection is (0,0). We define its angle as 0 for consistency.
        if (vector.x == 0f && vector.y == 0f) return 0f;

        // atan2(y, x) returns angle in radians from -PI to PI relative to +X axis.
        // Convert to degrees, then wrap to 0-360.
        float angleRad = Mathf.Atan2(vector.y, vector.x);
        float angleDeg = angleRad * Mathf.Rad2Deg;
        return WrapAngleDeg(angleDeg);
    }


    private float WrapAngleDeg(float angle)
    {
        return (angle % 360.0f + 360.0f) % 360.0f;
    }

    public bool IsHeadMovingTowardsGaze2(bool isEyeMoving, bool isHeadMoving, int frameThreshold)
    {
        if (isEyeMoving || !isHeadMoving)
        {
            return false;
        }
        if (gazeHeadAngle < gazeHeadAngle_prev)
        {
            headToGazePossibleFrames += 1;
        }
        else
        {
            headToGazePossibleFrames = 0;
        }
        if (headToGazePossibleFrames > frameThreshold)
        {
            return true;
        }
        else return false;
    }


    private void HandleGazeInteraction()
    {
        RaycastHit hit;
        // The gaze vector comes from gazeOrigin.forward, assuming gazeOrigin is correctly oriented
        if (Physics.Raycast(xrCamera.transform.position, cursor.transform.position - xrCamera.transform.position, out hit, Mathf.Infinity, interactableLayer))
        {
            CursorInteractable cursorInteractable = hit.collider.GetComponent<CursorInteractable>();
            if (cursorInteractable != null)
            {
                if (currentHoveredObject != cursorInteractable)
                {
                    if (currentHoveredObject != null)
                    {
                        if (currentHoveredObject.gameObject.name=="TimeSeekSlider"){isDraggingSlider = false;}
                        currentHoveredObject.OnCursorExit();
                    }
                    currentHoveredObject = cursorInteractable;
                    currentHoveredObject.OnCursorEnter();
                }
            }
            else // Hit something on an interactable layer but it doesn't have GazeInteractable script
            {
                if (currentHoveredObject != null)
                {
                    if (currentHoveredObject.gameObject.name=="TimeSeekSlider"){isDraggingSlider = false;}
                    currentHoveredObject.OnCursorExit();
                    currentHoveredObject = null;
                }
            }
        }
        else // No hit
        {
            if (currentHoveredObject != null)
            {
                currentHoveredObject.OnCursorExit();
                currentHoveredObject = null;
            }
        }
    }

    private void TogglePlayPause()
    {
        if (videoPlayer.isPlaying)
        {
            videoPlayer.Pause();
            Debug.Log("Video Paused.");
        }
        else
        {
            videoPlayer.Play();
            Debug.Log("Video Playing.");
        }
    }

    private void UpdateVideoSeekFromCursor()
    {
        
        if (!videoPlayer.isPrepared || videoPlayer.length <= 0) return;

        // Determine where the gaze ray intersects the plane of the slider track
        Plane sliderPlane = new Plane(timeSeekSliderTrack.transform.forward, timeSeekSliderTrack.transform.position);
        Ray cursorRay = new Ray(xrCamera.transform.position, cursor.transform.position - xrCamera.transform.position);

        float enter;
        if (sliderPlane.Raycast(cursorRay, out enter))
        {
            Vector3 hitPoint = cursorRay.GetPoint(enter);

            // Calculate position relative to the slider track's left edge
            float relativeX = hitPoint.x - (timeSeekSliderTrack.transform.position.x - sliderTrackLength / 2f);

            // Normalize this position to a 0-1 range
            float normalizedPosition = Mathf.Clamp01(relativeX / sliderTrackLength);

            // Set video time
            videoPlayer.time = normalizedPosition * videoPlayer.length;
        }
    }

    private void PlayBackControl_selected()
    {
        if (currentHoveredObject.gameObject.name == "PlayPause_button")
        {
            TogglePlayPause();
        }
        else if (currentHoveredObject.gameObject.name == "TimeSeekSlider")
        {
            UpdateVideoSeekFromCursor();
            if (isDraggingSlider)
            {
                isDraggingSlider = false;
            }
        }
    }
    private void PlayBackControl_dragged() {
        if (currentHoveredObject.gameObject.name == "TimeSeekSlider")
        {
            UpdateVideoSeekFromCursor();
            isDraggingSlider = true;
        }
    }
    
    private void UpdateSeekHandlePosition()
    {
        if (videoPlayer.length > 0) // Ensure video length is available
        {
            float normalizedTime = (float)(videoPlayer.time / videoPlayer.length);

            // Calculate handle's local X position relative to the slider track's center
            // The slider track is centered, so X goes from -sliderTrackLength/2 to +sliderTrackLength/2
            float handleX = (normalizedTime - 0.5f) * sliderTrackLength;

            Vector3 newHandlePos = timeSeekSliderTrack.transform.position;
            newHandlePos.x = timeSeekSliderTrack.transform.position.x + handleX;
            newHandlePos.y = timeSeekHandle.transform.position.y; // Keep Y and Z constant
            newHandlePos.z = timeSeekHandle.transform.position.z;

            timeSeekHandle.transform.position = newHandlePos;
        }
    }

}