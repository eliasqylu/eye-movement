using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.VisualScripting.FullSerializer.Internal;
using UnityEditor.PackageManager;
using UnityEngine;

public class gameStates : MonoBehaviour
{
    public static bool loggingOnStart = false;
    public static bool frameOfCentering = false;
    public static int trialIndex = 0;
    public static string eventType = "";
    public static bool isEyeFixation = false;
    public static bool isHeadFixation = false;
    public static Vector3 combinedEyeLocalForward = new Vector3(Single.NaN, Single.NaN, Single.NaN);

    public static Vector3  cursorInWorldPosition = new Vector3 (Single.NaN, Single.NaN, Single.NaN);
    public static Vector3  cursorInHmdPosition = new Vector3 (Single.NaN, Single.NaN, Single.NaN);
    public static Vector3 targetInWorldPosition =   new Vector3 (Single.NaN, Single.NaN, Single.NaN);
    public static Vector3 targetInHmdPosition =   new Vector3 (Single.NaN, Single.NaN, Single.NaN);
    public static float cursorInTargetDir = -1f; 
    public static float artificialGazeTargetOffsetDeg =-1f;
    public static string targetType = "";

    public static float gazeTargetAngle = -1f;
    public static float cursorTargetAngle = -1f;

    public static bool isOnset;
    public static bool canRefine;
    public static bool isRefining;
    public static bool canConfirm=false;
    public static bool canConfirm_prev=false;
    public static bool confirmed =false;
    public static bool resetNod = false;
    public static bool freezeCursor = false;
    public static bool trackPadClicked = false;

    public static int prediction = 0;

    public static float targetAmp = 0;
    public static float targetSize = 0;
    public static string failureMode = "";

    public static Vector3 object2DragInWorldPosition = new Vector3(Single.NaN, Single.NaN, Single.NaN);
    public static Vector3 object2DragInHmdPosition = new Vector3(Single.NaN, Single.NaN, Single.NaN);
    public static Vector3 DropzoneInWorldPosition = new Vector3(Single.NaN, Single.NaN, Single.NaN);
    public static Vector3 DropzoneInHmdPosition = new Vector3(Single.NaN, Single.NaN, Single.NaN);

    public static float cursorDropzoneAngle = Single.NaN;
    public static float cursorObjectAngle = Single.NaN;

    public static Vector3 gazeCursorRingInWorldPos = new Vector3(Single.NaN, Single.NaN, Single.NaN);
    public static Vector3 gazeCursorRingInHmdPos = new Vector3(Single.NaN, Single.NaN, Single.NaN);



    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
