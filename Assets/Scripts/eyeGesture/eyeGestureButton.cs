using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class eyeGestureButton : MonoBehaviour
{
    Camera xrCamera;
    Vector3 gazeDirection;
    int layerMask_nodButton;
    int layerMask_selectionButton;
    public Color hoverColor = new Color(0, 255, 255, 125);
    public Color defaultColor = new Color(0, 255, 255, 125);
    float hitTimeSelectionButton;
    float hitTimeNodButton;
    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
        layerMask_selectionButton = 1 << 8;
        layerMask_nodButton = 1 << 9;
    }

    // Update is called once per frame
    void Update()
    {
        bool isHeadMode = GameStateManager.Instance.prediction == 1;
        gazeDirection = 0.5f * (eyetracking.leftEyeLocalForward + eyetracking.rightEyeLocalForward);

        if (isHeadMode == false)
        {
            return;
        }

        if (Physics.Raycast(xrCamera.transform.position, xrCamera.transform.TransformDirection(gazeDirection), out RaycastHit hit, Mathf.Infinity, layerMask_selectionButton))
        {
            gameObject.GetComponent<Renderer>().material.color = hoverColor;
            if (GameStateManager.Instance.canConfirm==false){
                hitTimeSelectionButton = Time.time;
            }           
            GameStateManager.Instance.SetCanConfirm(true);
        }
        else
        {
            gameObject.GetComponent<Renderer>().material.color = defaultColor;
        }

        if (Physics.Raycast(xrCamera.transform.position, xrCamera.transform.TransformDirection(gazeDirection), out RaycastHit hit1, Mathf.Infinity, layerMask_nodButton))
        {
            hitTimeNodButton = Time.time;
        }

        if (hitTimeNodButton - hitTimeSelectionButton < 0.3f && hitTimeNodButton > hitTimeSelectionButton)
        {
            // GameStateManager.Instance.prediction = 2;
            GameStateManager.Instance.SetConfirmed(true);
        }
    }
}
