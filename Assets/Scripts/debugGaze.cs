using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class debugGaze : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        Vector3 gazeDirection = 0.5f * (eyetracking.leftEyeLocalForward + eyetracking.rightEyeLocalForward);
        gazeDirection = eyetracking.previousGazeCursorCentroids[0].normalized;
        transform.position = Camera.main.transform.TransformPoint(gazeDirection);
        transform.LookAt(Camera.main.transform);
        transform.Rotate(0, 180, 0);
    }
}
