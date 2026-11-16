using System;
using System.Collections;
using System.Collections.Generic;
using Unity;
using UnityEngine;
using ViveSR;

public class startCalibration : MonoBehaviour {
    //if to start calibration at the start of the scene
    public bool doCalibrationAtStart = false;
    // key to manually restart calibration
    public KeyCode calibrationKey = KeyCode.C;

    // Start is called before the first frame update
    void Start () {
        //start calibration
        if (doCalibrationAtStart) {
            ViveSR.anipal.Eye.SRanipal_Eye_API.LaunchEyeCalibration (IntPtr.Zero);
        }
    }

    // Update is called once per frame
    void Update () {
        //manual restart of calibration
        if (Input.GetKey (calibrationKey)) {
            ViveSR.anipal.Eye.SRanipal_Eye_API.LaunchEyeCalibration (IntPtr.Zero);
        }
    }
}

