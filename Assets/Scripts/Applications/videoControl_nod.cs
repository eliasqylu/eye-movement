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
using Unity.VisualScripting;

public class videoControl_nod : MonoBehaviour
{
    public GameObject tvset;
    GameObject DragFrame;
    [Header("Video Setup")]
    VideoPlayer videoPlayer; // Drag your TV_Screen's VideoPlayer component here
    GameObject playPauseButton; // Drag your PlayPause_Button GameObject here
    GameObject timeSeekSliderTrack; // Drag your VideoSeek_SliderTrack GameObject here
    GameObject timeSeekHandle;

    [Header("References")]
    private Camera xrCamera;
    [SerializeField] private GameObject cursor;
    [SerializeField] private GameObject headCursor;
    [SerializeField] private GameObject xrCam0_proxy;
    [SerializeField] private NodStateMachine nodController;

    int interactableLayer = (1 << 5) | (1 << 12);
    CursorInteractable currentHoveredObject;
    float sliderTrackLength;
    bool isDraggingSlider;
    private AudioSource audioSource;
    public GameObject volumnSeekSlider; 
    public GameObject volumnSliderHandle;
    bool isDraggingVolumnSlider = false;
    bool hasDragged;
    GameObject lassoPanel;
    GameObject currentDraggingPanel;



    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
        videoPlayer = tvset.transform.Find("TV").gameObject.GetComponent<VideoPlayer>();
        timeSeekSliderTrack = tvset.transform.Find("ControlPanel/SeekBar/TimeSeekSlider").gameObject;
        timeSeekHandle = tvset.transform.Find("ControlPanel/SeekBar/TimeSeekSlider/TimeSeekHandle").gameObject;
        lassoPanel = tvset.transform.Find("TV/CricleAndSearchDisplay").gameObject;
        sliderTrackLength = timeSeekSliderTrack.transform.localScale.x;// * (1/tvset.transform.localScale.x);
        headCursor.GetComponent<MeshRenderer>().enabled = false;
        DragFrame = tvset.transform.Find("DragFrame").gameObject;
        audioSource = videoPlayer.GetComponent<AudioSource>();
        if (audioSource == null) Debug.LogError("no audio source");
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            tvset.transform.position = xrCamera.transform.TransformPoint(0, 0.5f, 1.05f);
        }
        if (!isDraggingSlider && videoPlayer.isPrepared)
        {
            UpdateVideoSeekHandlePosition();
        }
        if (isDraggingSlider)
        {
            UpdateVideoSeekFromCursor();
            UpdateVideoSeekHandlePosition();
        }
        if (!isDraggingVolumnSlider && audioSource != null)
        {
            UpdateVolumnHandlePosition();
        }
        if (isDraggingVolumnSlider)
        {
            // UpdateVolumnFromCursor();
            UpdateVolumnHandlePosition();
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

        if (!hasDragged && GameStateManager.Instance.isInDrag) hasDragged = true;
        bool justSelected = GameStateManager.Instance.canConfirm && GameStateManager.Instance.confirmed && !hasDragged;
        bool justReleasedDrag = GameStateManager.Instance.confirmed && hasDragged;
        bool justStartedDrag = GameStateManager.Instance.isInDrag && !GameStateManager.Instance.isInDrag_prev;

        Vector3 direction = (cursor.transform.position - xrCamera.transform.position).normalized;
        Ray ray = new Ray(xrCamera.transform.position, direction);
        RaycastHit hit;

        if (justStartedDrag && Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << 11))
        {
            if (hit.collider == lassoPanel.GetComponent<Collider>())
            {
                lassoPanel.GetComponent<PersistentLassoController>().lasso = true;
            }
        }
        else if (justStartedDrag && Physics.Raycast(ray, out hit, Mathf.Infinity, 1 << 12))
        {
            dragable panelToDrag = hit.collider.GetComponent<dragable>();
            if (panelToDrag != null)
            {
                panelToDrag.inDrag = true;
                currentDraggingPanel = hit.collider.gameObject;
            }
        }




        if (justSelected)
        {
            nodController.ResetToIdle();
            GameStateManager.Instance.SetCanConfirm(false);
            PlayBackControl_selected();
        }
        else if (justReleasedDrag)
        {
            Debug.LogWarning("just released drag");
            hasDragged = false;
            nodController.ResetToIdle();
            GameStateManager.Instance.SetCanConfirm(false);
            GameStateManager.Instance.SetDrag(false);
            lassoPanel.GetComponent<PersistentLassoController>().lasso = false;
            if (currentDraggingPanel != null) currentDraggingPanel.GetComponent<dragable>().inDrag = false;
        }

        if (GameStateManager.Instance.isInDrag) //
        {
            PlayBackControl_dragged();
        }
        if (!GameStateManager.Instance.isInDrag)
        {
            isDraggingSlider = false;
        }

        // End of frame updates
        EndOfFrameUpdate(); //
    }


    void EndOfFrameUpdate()
    {
        GameStateManager.Instance.isInDrag_prev = GameStateManager.Instance.isInDrag;
        // If we were confirming but no longer are (e.g., cursor moved off target), reset
        // This check should only trigger if canConfirm was true and now it's not (e.g., target lost)
        if (!GameStateManager.Instance.canConfirm && GameStateManager.Instance.canConfirm_prev) //
        {
            // GameStateManager.Instance.SetCanConfirm(false); // Already set to false, no need to re-set
            nodController.ResetToIdle(); //
            Debug.LogWarning("reset: break confirm eof");
        }

        // Store previous confirmation state
        GameStateManager.Instance.UpdateAllPreviousStates(); //
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
                        if (currentHoveredObject.gameObject.name == "TimeSeekSlider") { isDraggingSlider = false; }
                        else if (currentHoveredObject.gameObject.name == "VolumnSeekSlider") { isDraggingVolumnSlider = false; }
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
                    if (currentHoveredObject.gameObject.name == "TimeSeekSlider") { isDraggingSlider = false; }
                    else if (currentHoveredObject.gameObject.name == "VolumnSeekSlider") { isDraggingVolumnSlider = false; }
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
            // float relativeX = hitPoint.x - (timeSeekSliderTrack.transform.position.x - sliderTrackLength / 2f);
            // float normalizedPosition = Mathf.Clamp01(relativeX / sliderTrackLength);

            Vector3 localHitPoint = timeSeekSliderTrack.transform.InverseTransformPoint(hitPoint);
            float normalizedPosition = localHitPoint.x + 0.5f;

            // Set video time
            videoPlayer.time = normalizedPosition * videoPlayer.length;
        }
    }

    private void PlayBackControl_selected()
    {
        if (currentHoveredObject == null) return;
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
        else if (currentHoveredObject.gameObject.name == "VolumnSeekSlider")
        {
            UpdateVolumnFromCursor();
            if (isDraggingVolumnSlider)
            {
                isDraggingVolumnSlider = false;
            }
        }
    }
    private void PlayBackControl_dragged()
    {
        if (currentHoveredObject == null) return;
        if (currentHoveredObject.gameObject.name == "TimeSeekSlider")
        {
            UpdateVideoSeekFromCursor();
            isDraggingSlider = true;
        }
        else if (currentHoveredObject.gameObject.name == "VolumnSeekSlider")
        {
            isDraggingVolumnSlider = true;
            UpdateVolumnFromCursor();
        }
    }

    private void UpdateVideoSeekHandlePosition()
    {
        if (videoPlayer.length > 0) // Ensure video length is available
        {
            float normalizedTime = (float)(videoPlayer.time / videoPlayer.length);

            float handleLocalX = normalizedTime - 0.5f;
            timeSeekHandle.transform.localPosition = new Vector3(handleLocalX, 0, 0);
        }
    }
    private void UpdateVolumnFromCursor()
    {
        if (audioSource == null) return;

        // Create a plane that aligns with the slider track
        Plane sliderPlane = new Plane(volumnSeekSlider.transform.forward, volumnSeekSlider.transform.position);
        
        // Create a ray from the camera to the cursor
        Ray cursorRay = new Ray(xrCamera.transform.position, cursor.transform.position - xrCamera.transform.position);

        // Check where the ray intersects the plane
        if (sliderPlane.Raycast(cursorRay, out float enter))
        {
            Vector3 hitPoint = cursorRay.GetPoint(enter);

            // Convert the world-space hit point to the local space of the slider track
            Vector3 localHitPoint = volumnSeekSlider.transform.InverseTransformPoint(hitPoint);

            // Normalize the position. A quad's local X-coords are from -0.5 to 0.5.
            // Adding 0.5 maps this range to 0.0 to 1.0.
            float normalizedPosition = Mathf.Clamp01(localHitPoint.x + 0.5f);

            // Set the audio source volume using the normalized position
            audioSource.volume = normalizedPosition;
            Debug.LogWarning("volumn should be:  " + normalizedPosition.ToString());
        }
    }

    /// <summary>
    /// Updates the visual position of the handle to reflect the current volume.
    /// </summary>
    private void UpdateVolumnHandlePosition()
    {
        if (audioSource == null) return;

        // The volume is already a normalized value (0.0 to 1.0)
        float normalizedVolume = audioSource.volume;

        // Convert the 0-1 volume value back to the handle's local X position (-0.5 to 0.5)
        float handleLocalX = normalizedVolume - 0.5f;

        // Apply the new local position to the handle
        volumnSliderHandle.transform.localPosition = new Vector3(handleLocalX, 0, 0);
    }
}