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

public class videoControl_line : MonoBehaviour
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
    public bool useLineInteraction = false;
    public GameObject gazeCursor; // This GameObject is for eye gaze visualization
    public GameObject lineVisual;
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

    public enum RingSegment
    {
        None,       // The head cursor did not trigger an entry event, or was outside a segment.
        Selecting,  // The head cursor entered the "Selecting" segment (left side).
        Dragging    // The head cursor entered the "Dragging" segment (right side).
    }
    // Public variable to store the most recently entered ring segment
    public RingSegment lastEnteredRingSegment = RingSegment.None;


    void Start()
    {
        ConfigureHeadtracking();
        xrCamera = Camera.main;
        sliderTrackLength = timeSeekSliderTrack.transform.localScale.x;

        // Locate the gazeCursorRing child object and get its material
        if (gazeCursor != null)
        {
            Transform ringTransform = gazeCursor.transform.Find("eyeGestureControl");
            if (ringTransform != null)
            {
                gazeCursorRing = ringTransform.gameObject;
                gazeCursorRingScale_orig = gazeCursorRing.transform.localScale;
                Renderer ringRenderer = gazeCursorRing.GetComponent<Renderer>();
                if (ringRenderer != null)
                {
                    gazeCursorRingMaterial = ringRenderer.material;
                }
                else
                {
                    Debug.LogError("GazeCursorRing (eyeGestureControl) does not have a Renderer component. Please ensure it's set up correctly.", this);
                }
            }
            else
            {
                Debug.LogError("Could not find 'eyeGestureControl' child object under GazeCursor. Ensure the name matches.", this);
            }
        }
        else
        {
            Debug.LogError("Gaze Cursor GameObject is not assigned in the inspector.", this);
        }
        lineVisual.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
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

        flipLineVisual(lineVisual.transform);
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
        bool eyeMoving = gaze2gazeRingAng > gazeRingSize * 0.5f || new Vector2(eyetracking.eyeInWorldVel_Az, eyetracking.eyeInWorldVel_Pol).magnitude > 160f; // Assuming eyetracking exists
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
                    // Calculate vectorGaze2Head: The vector FROM HMD's Vector3.forward TO the Eye Gaze.
                    Vector3 vectorGaze2Head = gazeVector - Vector3.forward;

                    // Calculate the RAW angle from East for this vector
                    float rawAngleGaze2HeadDeg = CalculateAngleFromEast(vectorGaze2Head);

                    // Set the center of the "Selecting" segment in the shader.
                    // This angle positions the "Selecting" segment to the left of the HMD's forward
                    // in the ring's coordinate system (where the ring's 0-degree is aligned with gazeVector).
                    float segment1CenterAngleDegForShader = WrapAngleDeg(rawAngleGaze2HeadDeg + 270.0f);

                    // Pass this FINAL angle (in degrees) to the shader for ring orientation
                    UpdateGazeRingDirection(segment1CenterAngleDegForShader);
                    freezeGaze = true; // Preserve original freezeGaze behavior


                    AlignLineVisualYWithAngle(gazeCursor.transform, lineVisual.transform, rawAngleGaze2HeadDeg, -1 * vectorGaze2Head, gazeCursorRingScale_orig.x * 0.75f);
                    lineVisual.SetActive(true);

                }
                ringUpdated = true;
            }
        }
        else if (eyeMoving) // If eye is moving, deactivate the ring and reset its update flag
        {
            if (gazeCursorRing != null)
            {
                gazeCursorRing.SetActive(false);
                lineVisual.SetActive(false);
            }
            ringUpdated = false;
            freezeGaze = false; // Preserve original freezeGaze behavior
        }

        if (useLineInteraction)
        {
            collisionPart = DetermineSegmentEnteredByLine(lineVisual.transform);
            if (collisionPart != 0)
            {
                timeOfHit = Time.time;
                collideCode = collisionPart;
            }
            if (Time.time - timeOfHit > 2f)
            {
                collideCode = 0;
            }
            Debug.LogWarning("collideCode " + collideCode.ToString());
        }
        if (headInGazeRing)
        {
            if (headInGazeRing_prev == false)
            {
                lookForDwell = true; // Set flag to begin looking for dwell time

                // Determine which segment the head cursor (HMD's fixed forward) crossed.
                // We will now read the shader's _Segment1CenterAngleDeg property for the exact orientation.
                if (useLineInteraction)
                {
                    if (collideCode == 1)
                    {
                        lastEnteredRingSegment = RingSegment.Selecting;
                        lineVisual.SetActive(false);
                    }
                    else if (collideCode == 2)
                    {
                        lastEnteredRingSegment = RingSegment.Dragging;
                        lineVisual.SetActive(false);
                    }
                }
                else
                {
                    lastEnteredRingSegment = DetermineSegmentEntered();
                }


                // You can now use 'lastEnteredRingSegment' for any immediate actions upon entry:
                Debug.Log($"Head cursor entered the ring via: {lastEnteredRingSegment}");

                // Change segment colors based on which segment is entered
                if (lastEnteredRingSegment == RingSegment.Selecting)
                {
                    gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.green); // Selecting color
                    gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.green);   // Dragging color
                }
                else if (lastEnteredRingSegment == RingSegment.Dragging)
                {
                    gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.blue); // Selecting color (default)
                    gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.blue); // Dragging color
                    freezeGaze = false; // Preserve original freezeGaze behavior
                    gazeRingSize = 13.8f;//9;
                    gazeCursorRing.transform.localScale = gazeCursorRingScale_orig * 2;
                    lineVisual.SetActive(false);
                }
            }
        }
        else // Reset conditions when head is NOT in gaze area (exit)
        {
            lookForDwell = false;
            dragEnabled = false;
            dwelledTime_head = -1;
            lastEnteredRingSegment = RingSegment.None;
            // Always reset segment colors to their defaults when head is out of gaze
            if (useLineInteraction)
            {
                gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.white); // Default white for Selecting
                gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.white);   // Default red for Dragging
            }
            else
            {
                gazeCursorRingMaterial.SetColor("_SegmentColor1", Color.white); // Default white for Selecting
                gazeCursorRingMaterial.SetColor("_SegmentColor2", Color.blue);   // Default red for Dragging
            }
            if (headInGazeRing_prev)
            {
                gazeRingSize = 6.9f;//4.5f;
                gazeCursorRing.transform.localScale = gazeCursorRingScale_orig;
                gazeCursorRing.SetActive(false);
                ringUpdated = false;
            }
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

        Debug.LogWarning("playback!");
        Debug.LogWarning("lookForDwell: " + lookForDwell);
        Debug.LogWarning("dwelledTime_head: " + dwelledTime_head);
        Debug.LogWarning(lastEnteredRingSegment);
        //place one marker/click (only if currently in "Selecting" segment)
        if (lastEnteredRingSegment == RingSegment.Selecting && lookForDwell && dwelledTime_head > dwellTimeThreshold)
        {
            Debug.LogWarning("playback selected!2");
            if (gazeCursorRing.activeSelf)
            {
                PlayBackControl_selected();
            }
            lookForDwell = false;
        }
        else if (lastEnteredRingSegment == RingSegment.Dragging && lookForDwell && dwelledTime_head > dwellTimeThreshold)
        {
            dragEnabled = true;
        }

        if (dragEnabled)
        {
            freezeGaze = false; // Preserve original freezeGaze behavior
            if (gazeCursorRing.activeSelf)
            {
                Debug.LogWarning("playback dragged!");
                PlayBackControl_dragged();
            }
        }
        else
        {
            isDraggingSlider = false;
        }
        isHeadFixation_prev = headtracking.isHeadInFixation;
        headInGazeRing_prev = headInGazeRing;
        gazeHeadAngle_prev = gazeHeadAngle;
        if (lastEnteredRingSegment != RingSegment.Dragging)
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

    private int DetermineSegmentEnteredByLine(Transform lineVisual)
    {

        // Create a Ray from the HMD's position along its forward vector.
        Ray hmdRay = new Ray(xrCamera.transform.position, xrCamera.transform.forward);

        RaycastHit hit; // This will store information about what the raycast hits.

        // Perform the raycast.
        // Physics.Raycast returns true if the ray hits a collider.
        // We can filter by layerMask if specified.
        if (Physics.Raycast(hmdRay, out hit, Mathf.Infinity, 1 << 5))
        {
            // Debug.LogWarning("hit!!)");
            // Check if the collider hit belongs to our specific lineVisual GameObject.
            if (hit.transform == lineVisual)
            {
                // The 'hit.point' is the world-space coordinate where the ray intersected the collider.
                Vector3 intersectionPointWorld = hit.point;

                // Convert this world-space intersection point to the lineVisual's local coordinate space.
                // This is essential because the green/blue distinction is based on local UVs (and thus local X).
                Vector3 localIntersectionPoint = lineVisual.InverseTransformPoint(intersectionPointWorld);

                // Determine which part of the line was hit based on the local X-coordinate.
                // Remember: `i.uv.x < 0.5` corresponds to `local_x < 0` for a standard quad.
                if (localIntersectionPoint.x < 0)
                {
                    return 1;
                }
                else // localIntersectionPoint.x >= 0
                {
                    return 2;
                }
            }
        }
        return 0;
    }


    private RingSegment DetermineSegmentEntered()
    {
        if (gazeCursorRingMaterial == null)
        {
            Debug.LogError("Gaze Cursor Ring Material is not assigned. Cannot determine segment.", this);
            return RingSegment.None;
        }

        // --- 1. Get the Ring's Orientation Angle from the Shader's current state ---
        // PULL from the shader: the _Segment1CenterAngleDeg that was set in the *current* frame's UpdateGazeRingDirection call.
        // This angle defines the center of the "Selecting" segment in the ring's local coordinate system.
        float selectingSegmentCenterAngleFromShaderDeg = gazeCursorRingMaterial.GetFloat("_Segment1CenterAngleDeg");
        // Ensure it's wrapped 0-360, although the C# Update method should pass it wrapped.
        selectingSegmentCenterAngleFromShaderDeg = WrapAngleDeg(selectingSegmentCenterAngleFromShaderDeg);

        // --- 2. Calculate the Head Cursor's Angle Relative to the Ring's Orientation ---
        // This is the original calculation for the head cursor's angle relative to the gaze ring.
        Vector3 gazeRingToHeadVec = xrCamera.transform.InverseTransformPoint(gazeCursorRing.transform.position);
        Vector3 currentVectorGaze2Head = gazeRingToHeadVec.normalized - Vector3.forward;
        float headForwardAngleInRingDeg = CalculateAngleFromEast(-1 * currentVectorGaze2Head);

        // --- 3. Compare and Determine Segment ---
        // Each segment is 180 degrees.
        float halfArcDeg = 90.0f; // Half of 180 degrees

        // Calculate the shortest angular difference between the head cursor's angle
        // and the shader's selecting segment center angle.
        float angleDiffDeg = Mathf.Abs(headForwardAngleInRingDeg - selectingSegmentCenterAngleFromShaderDeg);
        angleDiffDeg = Mathf.Min(angleDiffDeg, 360.0f - angleDiffDeg); // Shortest distance over the circle

        // If the difference is within half of the selecting segment's arc (90 degrees for 180-degree segment)
        if (angleDiffDeg <= halfArcDeg)
        {
            return RingSegment.Selecting;
        }
        else
        {
            return RingSegment.Dragging;
        }
    }

    private float WrapAngleDeg(float angle)
    {
        return (angle % 360.0f + 360.0f) % 360.0f;
    }

    void AlignLineVisualYWithAngle(Transform gazeCursor, Transform lineVisual, float angleFromEast, Vector3 vectorGaze2Head, float radius)
    {
        // Ensure valid transforms
        if (gazeCursor == null || lineVisual == null)
        {
            Debug.LogWarning("AlignLineVisualYWithAngle: GazeCursor or LineVisual Transform is null.");
            return;
        }

        // 1. Convert the angle from degrees to radians for trigonometric functions.
        float angleRad = angleFromEast * Mathf.Deg2Rad;

        // 2. Calculate the target direction in world space based on the angle.
        // This represents the desired world-space direction for lineVisual's local Y-axis.
        Vector3 targetWorldYDirection = new Vector3(Mathf.Cos(angleRad), Mathf.Sin(angleRad), 0);

        // 3. Determine the required world rotation for lineVisual.
        // We want lineVisual's world Y-axis to point towards 'targetWorldYDirection'.
        // We also need to ensure that lineVisual rotates *only* around gazeCursor's local forward axis.
        // This means lineVisual's world Z-axis should remain aligned with gazeCursor's world Z-axis (its local forward).

        // Create a rotation that aligns the lineVisual's world Z-axis with gazeCursor's world Z-axis
        // and its world Y-axis with targetWorldYDirection.
        // Quaternion.LookRotation(forward, upwards) creates a rotation where 'forward' maps to Z and 'upwards' maps to Y.
        // Here, 'forward' is gazeCursor.forward, and 'upwards' is targetWorldYDirection.
        Quaternion desiredWorldRotation = Quaternion.LookRotation(gazeCursor.forward, targetWorldYDirection);

        // 4. Convert the desired world rotation into a local rotation for lineVisual.
        // Since lineVisual is a child of gazeCursor, its world rotation is gazeCursor.rotation * lineVisual.localRotation.
        // So, lineVisual.localRotation = Quaternion.Inverse(gazeCursor.rotation) * desiredWorldRotation.
        lineVisual.localRotation = Quaternion.Inverse(gazeCursor.rotation) * desiredWorldRotation;
        lineVisual.localPosition = vectorGaze2Head.normalized * radius;
    }

    void flipLineVisual(Transform lineVisual)
    {
        Vector3 greenPartDirectionWorld = -lineVisual.right;

        bool needsFlip = (greenPartDirectionWorld.y <= 0 && greenPartDirectionWorld.x >= 0);

        if (needsFlip)
        {
            // Rotating 180 degrees around lineVisual's local Z-axis (which aligns
            // with gazeCursor's local forward axis) will re-orient the local X-axis
            // by 180 degrees, effectively flipping the green part's orientation.
            lineVisual.localRotation *= Quaternion.Euler(0, 0, 180f);
        }
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