using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using j_utils;
using ViveSR.anipal.Eye;
using UnityEngine.SceneManagement;


public class saveData_Fitts_Drag : MonoBehaviour
{
    public string saveFolder;
    private string logPath;
    public KeyCode loggingKey = KeyCode.Return;
    private static long startTime;
    static readonly string[] ColumnNames = {
        //timestamp
        "frameSequence",
        "systemTimestamp(ms)",
        "unityTimestamp(s)",
        // "deviceTimestamp(ms)",                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                                            

        //event
        "frameOfCentering",
        "trialIndex",
        // "cdGain",
        // "isFailure",
        // "isFixation",
        // "isVorTwd",
        "eventType",

        //Combined eye
        // "combinedEyeLocalOrigin_X",
        // "combinedEyeLocalOrigin_Y",
        // "combinedEyeLocalOrigin_Z",
        // "combinedEyeLocalForward_X",
        // "combinedEyeLocalForward_Y",
        // "combinedEyeLocalForward_Z",
        // "isCombinedEyeGazeRayValid",
        "readVerboseDataSuccess",
        //filtered combined eye forward
        "combinedEyeLocalForward_filtered_X",
        "combinedEyeLocalForward_filtered_Y",
        "combinedEyeLocalForward_filtered_Z",

        //Left eye
        "leftEyeLocalOrigin_X",
        "leftEyeLocalOrigin_Y",
        "leftEyeLocalOrigin_Z",
        "leftEyeLocalOrigin_verbose_X",
        "leftEyeLocalOrigin_verbose_Y",
        "leftEyeLocalOrigin_verbose_Z",

        "leftEyeLocalForward_X",
        "leftEyeLocalForward_Y",
        "leftEyeLocalForward_Z",

        "leftEyeLocalForward_verbose_X",
        "leftEyeLocalForward_verbose_Y",
        "leftEyeLocalForward_verbose_Z",

        "leftEyePupilDiameter",
        "leftEyePupilPositionInSensorArea_X",
        "leftEyePupilPositionInSensorArea_Y",
        // "leftEyePupilPosition_X",
        // "leftEyePupilPosition_Y",
        "leftEyeOpenness",
        // "leftEyeOpennessReadSuccess",
        // "leftEyeFrown",
        // "leftEyeSqueeze",
        // "leftEyeWide",
        "isLeftEyeGazeRayValid",

        //Right eye
        "rightEyeLocalOrigin_X",
        "rightEyeLocalOrigin_Y",
        "rightEyeLocalOrigin_Z",
        "rightEyeLocalOrigin_verbose_X",
        "rightEyeLocalOrigin_verbose_Y",
        "rightEyeLocalOrigin_verbose_Z",
        "rightEyeLocalForward_X",
        "rightEyeLocalForward_Y",
        "rightEyeLocalForward_Z",

        "rightEyeLocalForward_verbose_X",
        "rightEyeLocalForward_verbose_Y",
        "rightEyeLocalForward_verbose_Z",

        "rightEyePupilDiameter",
        "rightEyePupilPositionInSensorArea_X",
        "rightEyePupilPositionInSensorArea_Y",
        // "rightEyePupilPosition_X",
        // "rightEyePupilPosition_Y",
        "rightEyeOpenness",
        // "rightEyeOpennessReadSuccess",
        // "rightEyeFrown",
        // "rightEyeSqueeze",
        // "rightEyeWide",
        "isRightEyeGazeRayValid",

        //eyeVel
        "eyeVel_Az",
        "eyeVel_Pol",

        // //HMD
        "hmdWorldOrigin_X",
        "hmdWorldOrigin_Y",
        "hmdWorldOrigin_Z",
        "hmdWorldForward_X",
        "hmdWorldForward_Y",
        "hmdWorldForward_Z",

        "hmdEulers_X",
        "hmdEulers_Y",
        "hmdEulers_Z",

        // //HMD quaternion
        "hmdWorldQuaternion_W",
        "hmdWorldQuaternion_X",
        "hmdWorldQuaternion_Y",
        "hmdWorldQuaternion_Z",

        // //HMD localToWorldMatrix (Matrix4X4)
        "hmdL2W_m00",
        "hmdL2W_m01",
        "hmdL2W_m02",
        "hmdL2W_m03",

        "hmdL2W_m10",
        "hmdL2W_m11",
        "hmdL2W_m12",
        "hmdL2W_m13",

        "hmdL2W_m20",
        "hmdL2W_m21",
        "hmdL2W_m22",
        "hmdL2W_m23",

        "hmdL2W_m30",
        "hmdL2W_m31",
        "hmdL2W_m32",
        "hmdL2W_m33",

        // //head velocity
        "headVel_Az",
        "headVel_Pol",
        "headAccel_Az",
        "headAccel_Pol",
        "headJolt_Az",
        "headJolt_Pol",
        "headVel_ang_X",
        "headVel_ang_Y",
        "headVel_ang_Z",
        "headAcc_ang_X",
        "headAcc_ang_Y",
        "headAcc_ang_Z",
        "headVel_pos_X",
        "headVel_pos_Y",
        "headVel_pos_Z",
        "headAcc_pos_X",
        "headAcc_pos_Y",
        "headAcc_pos_Z",

        //roll velocity
        "rollDirection",
        "hmdRollVelocity",
        "hmdAngularDeltaVector_x",
        "hmdAngularDeltaVector_y",
        "hmdAngularDeltaVector_z",
        "hmdAngularDeltaVectorFromNeutral_x",
        "hmdAngularDeltaVectorFromNeutral_y",
        "hmdAngularDeltaVectorFromNeutral_z",


        // //cursor world position
        "cursorInWorldPosition_X",
        "cursorInWorldPosition_Y",
        "cursorInWorldPosition_Z",
        "cursorInHeadPosition_X",
        "cursorInHeadPosition_Y",
        "cursorInHeadPosition_Z",
        "artificialGazeTargetOffsetDeg",

        // //stimulus position
        "stimInWorldPosition_X",
        "stimInWorldPosition_Y",
        "stimInWorldPosition_Z",
        "stimInHeadPosition_X",
        "stimInHeadPosition_Y",
        "stimInHeadPosition_Z",
        "stimType",

        "gazeCursorRingInWorldPosition_X",
        "gazeCursorRingInWorldPosition_Y",
        "gazeCursorRingInWorldPosition_Z",

        "gazeCursorRingInHeadPosition_X",
        "gazeCursorRingInHeadPosition_Y",
        "gazeCursorRingInHeadPosition_Z",

        "object2DragInWorldPosition_X",
        "object2DragInWorldPosition_Y",
        "object2DragInWorldPosition_Z",
        "object2DragInHmdPosition_X",
        "object2DragInHmdPosition_Y",
        "object2DragInHmdPosition_Z",
        "DropzoneInWorldPosition_X",
        "DropzoneInWorldPosition_Y",
        "DropzoneInWorldPosition_Z",
        "DropzoneInHmdPosition_X",
        "DropzoneInHmdPosition_Y",
        "DropzoneInHmdPosition_Z",

        // //
        "cursorTargetAngle",
        "gazeTargetAngle",
        "cursorDropzoneAngle",
        "cursorObjectAngle",


        "trackPadClicked",
        "inputState",

        "targetAmp",
        "targetSize",
        "failureMode",

        "isInDrag"
       

    };

    Camera xrCamera;
    int frame;
    string postFix;
    // Start is called before the first frame update
    void Start()
    {
        xrCamera = Camera.main;
        frame = 0;

        logPath = "D:/nodButton_code/data/" + saveFolder + "/";
        string sceneName = SceneManager.GetActiveScene().name;
        string taskName = "unknown";
        GameObject nodObject = GameObject.Find("Main_nod");
        if (nodObject != null && nodObject.activeSelf)
        {
            taskName = "nod";
        }
        else
        {
            GameObject alignObject = GameObject.Find("Main_align");
            if (alignObject != null && alignObject.activeSelf)
            {
                taskName = "align";
            }
            else
            {
                GameObject wink = GameObject.Find("Main_wink");
                if (wink != null && wink.activeSelf)
                {
                    taskName = "wink";
                }
            }
        }
        postFix = taskName + "_" + sceneName;

        if (gameStates.loggingOnStart)
        {
            Directory.CreateDirectory(logPath);

            DateTime now = DateTime.Now;
            string fileName = string.Format("dataLog_{0}_{1}-{2:00}-{3:00}-{4:00}-{5:00}", gameStates.trialIndex, now.Year, now.Month, now.Day, now.Hour, now.Minute);

            string path = logPath + fileName + "_" + postFix + ".csv";

            csv_utils.StartLogging(ColumnNames, path);
        }

    }

    // Update is called once per frame
    void Update()
    {
        //toggle logging
        if (Input.GetKeyDown(loggingKey)) {
            if (csv_utils.isLogging) {
                csv_utils.StopLogging();
            } else {

                Directory.CreateDirectory(logPath);

                DateTime now = DateTime.Now;
                string fileName = string.Format("dataLog_{0}_{1}-{2:00}-{3:00}-{4:00}-{5:00}", gameStates.trialIndex, now.Year, now.Month, now.Day, now.Hour, now.Minute);

                //string path = logPath + fileName + ".csv";
                // string path = logPath + fileName + ".csv";
                string path = logPath + fileName + "_" + postFix + ".csv";

                csv_utils.StartLogging(ColumnNames, path);
            }
        }

        string[] logData = new string[ColumnNames.Length];

        VerboseData eyeData = eyetracking.eyeData;
        Vector3 leftEyeOrigin_mm = eyeData.left.gaze_origin_mm;
        Vector3 leftEyeDirection_normalized = eyeData.left.gaze_direction_normalized;
        float leftEyePupilDiameter_mm = eyeData.left.pupil_diameter_mm;
        float leftEyeOpenness = eyeData.left.eye_openness;
        Vector2 leftEyePupilPositionInSensorArea = eyeData.left.pupil_position_in_sensor_area;

        Vector3 rightEyeOrigin_mm = eyeData.right.gaze_origin_mm;
        Vector3 rightEyeDirection_normalized = eyeData.right.gaze_direction_normalized;
        float rightEyePupilDiameter_mm = eyeData.right.pupil_diameter_mm;
        float rightEyeOpenness = eyeData.right.eye_openness;
        Vector2 rightEyePupilPositionInSensorArea = eyeData.right.pupil_position_in_sensor_area;

        Matrix4x4 cameraTransformLocalToWorldMatrix = xrCamera.transform.transform.localToWorldMatrix;
        //Debug.LogError ("frameSequence: " + eyeData.frame_sequence.ToString ());
        //timestamp
        logData[Array.IndexOf(ColumnNames, "frameSequence")] = frame.ToString();
        logData[Array.IndexOf(ColumnNames, "systemTimestamp(ms)")] = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - startTime).ToString();
        logData[Array.IndexOf(ColumnNames, "unityTimestamp(s)")] = Time.time.ToString();
        // logData[Array.IndexOf (ColumnNames, "deviceTimestamp(ms)")] = (eyeData.timestamp - eyeTrackerTimestamp0).ToString (); //5000000. ToString ();
        logData[Array.IndexOf(ColumnNames, "frameOfCentering")] = GameStateManager.Instance.frameOfCentering.ToString();
        logData[Array.IndexOf(ColumnNames, "trialIndex")] = gameStates.trialIndex.ToString();

        //is this event (onset, vor etc.) flagges as a failure, e.g. due to participant unable to perform trial
        // logData[Array.IndexOf (ColumnNames, "isFailure")] = gameStates.isFailure.ToString();
        // //log alpha, i.e. cdGain  
        // logData[Array.IndexOf (ColumnNames, "cdGain")] = gameStates.cdGain.ToString ("F3");
        //eventType, '', '0','1','2'  = None, targetOnset, vorTwd, vorAway, respectively
        logData[Array.IndexOf(ColumnNames, "eventType")] = gameStates.eventType.ToString();
        // logData[Array.IndexOf (ColumnNames, "isFixation")] = gameStates.isFixation.ToString ();
        // logData[Array.IndexOf (ColumnNames, "isVorTwd")] = gameStates.isVorTwd.ToString ();

        //Combined eye
        // logData[Array.IndexOf (ColumnNames, "combinedEyeLocalOrigin_X")] = combinedEyeLocalOrigin.x.ToString ();
        // logData[Array.IndexOf (ColumnNames, "combinedEyeLocalOrigin_Y")] = combinedEyeLocalOrigin.y.ToString ();
        // logData[Array.IndexOf (ColumnNames, "combinedEyeLocalOrigin_Z")] = combinedEyeLocalOrigin.z.ToString ();
        logData[Array.IndexOf(ColumnNames, "combinedEyeLocalForward_filtered_X")] = eyetracking.combinedEyeLocalForward_processed.x.ToString();
        logData[Array.IndexOf(ColumnNames, "combinedEyeLocalForward_filtered_Y")] = eyetracking.combinedEyeLocalForward_processed.y.ToString();
        logData[Array.IndexOf(ColumnNames, "combinedEyeLocalForward_filtered_Z")] = eyetracking.combinedEyeLocalForward_processed.z.ToString();
        // logData[Array.IndexOf (ColumnNames, "isCombinedEyeGazeRayValid")] = isCombinedEyeGazeRayValid.ToString ();
        logData[Array.IndexOf(ColumnNames, "readVerboseDataSuccess")] = eyetracking.readVerboseDataSuccess.ToString();

        //Left eye
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalOrigin_X")] = eyetracking.leftEyeLocalOrigin.x.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalOrigin_Y")] = eyetracking.leftEyeLocalOrigin.y.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalOrigin_Z")] = eyetracking.leftEyeLocalOrigin.z.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalOrigin_verbose_X")] = leftEyeOrigin_mm.x.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalOrigin_verbose_Y")] = leftEyeOrigin_mm.y.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalOrigin_verbose_Z")] = leftEyeOrigin_mm.z.ToString();

        logData[Array.IndexOf(ColumnNames, "leftEyeLocalForward_X")] = eyetracking.leftEyeLocalForward.x.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalForward_Y")] = eyetracking.leftEyeLocalForward.y.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalForward_Z")] = eyetracking.leftEyeLocalForward.z.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalForward_verbose_X")] = leftEyeDirection_normalized.x.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalForward_verbose_Y")] = leftEyeDirection_normalized.y.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyeLocalForward_verbose_Z")] = leftEyeDirection_normalized.z.ToString();

        logData[Array.IndexOf(ColumnNames, "leftEyePupilDiameter")] = leftEyePupilDiameter_mm.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyePupilPositionInSensorArea_X")] = leftEyePupilPositionInSensorArea.x.ToString();
        logData[Array.IndexOf(ColumnNames, "leftEyePupilPositionInSensorArea_Y")] = leftEyePupilPositionInSensorArea.y.ToString();
        // logData[Array.IndexOf (ColumnNames, "leftEyePupilPosition_X")] = leftEyePupilPosition.x.ToString ();
        // logData[Array.IndexOf (ColumnNames, "leftEyePupilPosition_Y")] = leftEyePupilPosition.y.ToString ();
        logData[Array.IndexOf(ColumnNames, "leftEyeOpenness")] = leftEyeOpenness.ToString();
        // logData[Array.IndexOf (ColumnNames, "leftEyeOpennessReadSuccess")] = leftEyeOpennessReadSuccess.ToString ();
        // logData[Array.IndexOf (ColumnNames, "leftEyeFrown")] = leftEyeFrown.ToString ("F5");
        // logData[Array.IndexOf (ColumnNames, "leftEyeSqueeze")] = leftEyeSqueeze.ToString ("F5");
        // logData[Array.IndexOf (ColumnNames, "leftEyeWide")] = leftEyeWide.ToString ("F5");
        logData[Array.IndexOf(ColumnNames, "isLeftEyeGazeRayValid")] = eyetracking.isLeftEyeGazeRayValid.ToString();

        // //pupil size read success
        // logData[Array.IndexOf (ColumnNames, "leftPupilDiameterReadSuccess")] = leftPupilDiameterReadSuccess.ToString ();
        // logData[Array.IndexOf (ColumnNames, "righPupilDiameterReadSuccess")] = rightPupilDiameterReadSuccess.ToString ();
        // // eye expression read sucess
        // logData[Array.IndexOf (ColumnNames, "leftEyeExpressionReadSuccess")] = leftEyeExpressionReadSuccess.ToString ();
        // logData[Array.IndexOf (ColumnNames, "rightEyeExpressionReadSuccess")] = rightEyeExpressionReadSuccess.ToString ();


        //Right eye
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalOrigin_X")] = eyetracking.rightEyeLocalOrigin.x.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalOrigin_Y")] = eyetracking.rightEyeLocalOrigin.y.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalOrigin_Z")] = eyetracking.rightEyeLocalOrigin.z.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalOrigin_verbose_X")] = rightEyeOrigin_mm.x.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalOrigin_verbose_Y")] = rightEyeOrigin_mm.y.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalOrigin_verbose_Z")] = rightEyeOrigin_mm.z.ToString();

        logData[Array.IndexOf(ColumnNames, "rightEyeLocalForward_X")] = eyetracking.rightEyeLocalForward.x.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalForward_Y")] = eyetracking.rightEyeLocalForward.y.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalForward_Z")] = eyetracking.rightEyeLocalForward.z.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalForward_verbose_X")] = rightEyeDirection_normalized.x.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalForward_verbose_Y")] = rightEyeDirection_normalized.y.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyeLocalForward_verbose_Z")] = rightEyeDirection_normalized.z.ToString();

        logData[Array.IndexOf(ColumnNames, "rightEyePupilDiameter")] = rightEyePupilDiameter_mm.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyePupilPositionInSensorArea_X")] = rightEyePupilPositionInSensorArea.x.ToString();
        logData[Array.IndexOf(ColumnNames, "rightEyePupilPositionInSensorArea_Y")] = rightEyePupilPositionInSensorArea.y.ToString();
        // logData[Array.IndexOf (ColumnNames, "rightEyePupilPosition_X")] = rightEyePupilPosition.x.ToString ();
        // logData[Array.IndexOf (ColumnNames, "rightEyePupilPosition_Y")] = rightEyePupilPosition.y.ToString ();
        logData[Array.IndexOf(ColumnNames, "rightEyeOpenness")] = rightEyeOpenness.ToString();
        // logData[Array.IndexOf (ColumnNames, "rightEyeOpennessReadSuccess")] = rightEyeOpennessReadSuccess.ToString ();
        // logData[Array.IndexOf (ColumnNames, "rightEyeFrown")] = rightEyeFrown.ToString ("F5");
        // logData[Array.IndexOf (ColumnNames, "rightEyeSqueeze")] = rightEyeSqueeze.ToString ("F5");
        // logData[Array.IndexOf (ColumnNames, "rightEyeWide")] = rightEyeWide.ToString ("F5");
        logData[Array.IndexOf(ColumnNames, "isRightEyeGazeRayValid")] = eyetracking.isRightEyeGazeRayValid.ToString();

        logData[Array.IndexOf(ColumnNames, "eyeVel_Az")] = eyetracking.eyeVel_Az.ToString();
        logData[Array.IndexOf(ColumnNames, "eyeVel_Pol")] = eyetracking.eyeVel_Pol.ToString();

        // //HMD
        logData[Array.IndexOf(ColumnNames, "hmdWorldOrigin_X")] = xrCamera.transform.position.x.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldOrigin_Y")] = xrCamera.transform.position.y.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldOrigin_Z")] = xrCamera.transform.position.z.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldForward_X")] = xrCamera.transform.forward.x.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldForward_Y")] = xrCamera.transform.forward.y.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldForward_Z")] = xrCamera.transform.forward.z.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdEulers_X")] = xrCamera.transform.eulerAngles.x.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdEulers_Y")] = xrCamera.transform.eulerAngles.y.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdEulers_Z")] = xrCamera.transform.eulerAngles.z.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdWorldQuaternion_W")] = xrCamera.transform.rotation.w.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldQuaternion_X")] = xrCamera.transform.rotation.x.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldQuaternion_Y")] = xrCamera.transform.rotation.y.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdWorldQuaternion_Z")] = xrCamera.transform.rotation.z.ToString();

        // //HMD localToWorldMatrix(Matrix4X4)
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m00")] = cameraTransformLocalToWorldMatrix.m00.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m01")] = cameraTransformLocalToWorldMatrix.m01.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m02")] = cameraTransformLocalToWorldMatrix.m02.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m03")] = cameraTransformLocalToWorldMatrix.m03.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdL2W_m10")] = cameraTransformLocalToWorldMatrix.m10.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m11")] = cameraTransformLocalToWorldMatrix.m11.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m12")] = cameraTransformLocalToWorldMatrix.m12.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m13")] = cameraTransformLocalToWorldMatrix.m13.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdL2W_m20")] = cameraTransformLocalToWorldMatrix.m20.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m21")] = cameraTransformLocalToWorldMatrix.m21.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m22")] = cameraTransformLocalToWorldMatrix.m22.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m23")] = cameraTransformLocalToWorldMatrix.m23.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdL2W_m30")] = cameraTransformLocalToWorldMatrix.m30.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m31")] = cameraTransformLocalToWorldMatrix.m31.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m32")] = cameraTransformLocalToWorldMatrix.m32.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdL2W_m33")] = cameraTransformLocalToWorldMatrix.m33.ToString();

        logData[Array.IndexOf(ColumnNames, "headVel_Az")] = headtracking.headVel_Az.ToString();
        logData[Array.IndexOf(ColumnNames, "headVel_Pol")] = headtracking.headVel_Pol.ToString();
        logData[Array.IndexOf(ColumnNames, "headAccel_Az")] = headtracking.headAccel_Az.ToString();
        logData[Array.IndexOf(ColumnNames, "headAccel_Pol")] = headtracking.headAccel_Pol.ToString();
        logData[Array.IndexOf(ColumnNames, "headJolt_Az")] = headtracking.headJolt_Az.ToString();
        logData[Array.IndexOf(ColumnNames, "headJolt_Pol")] = headtracking.headJolt_Pol.ToString();

        logData[Array.IndexOf(ColumnNames, "headVel_ang_X")] = headtracking.angularVelocity.x.ToString();
        logData[Array.IndexOf(ColumnNames, "headVel_ang_Y")] = headtracking.angularVelocity.y.ToString();
        logData[Array.IndexOf(ColumnNames, "headVel_ang_Z")] = headtracking.angularVelocity.z.ToString();
        logData[Array.IndexOf(ColumnNames, "headAcc_ang_X")] = headtracking.angularAcceleration.x.ToString();
        logData[Array.IndexOf(ColumnNames, "headAcc_ang_Y")] = headtracking.angularAcceleration.y.ToString();
        logData[Array.IndexOf(ColumnNames, "headAcc_ang_Z")] = headtracking.angularAcceleration.z.ToString();

        logData[Array.IndexOf(ColumnNames, "headVel_pos_X")] = headtracking.positionVelocity.x.ToString();
        logData[Array.IndexOf(ColumnNames, "headVel_pos_Y")] = headtracking.positionVelocity.y.ToString();
        logData[Array.IndexOf(ColumnNames, "headVel_pos_Z")] = headtracking.positionVelocity.z.ToString();
        logData[Array.IndexOf(ColumnNames, "headAcc_pos_X")] = headtracking.positionAcceleration.x.ToString();
        logData[Array.IndexOf(ColumnNames, "headAcc_pos_Y")] = headtracking.positionAcceleration.y.ToString();
        logData[Array.IndexOf(ColumnNames, "headAcc_pos_Z")] = headtracking.positionAcceleration.z.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdAngularDeltaVector_x")] = GameStateManager.Instance.hmdAngularDeltaVector.x.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdAngularDeltaVector_y")] = GameStateManager.Instance.hmdAngularDeltaVector.y.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdAngularDeltaVector_z")] = GameStateManager.Instance.hmdAngularDeltaVector.z.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdAngularDeltaVectorFromNeutral_x")] = GameStateManager.Instance.hmdAngularDeltaFromNeutral.x.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdAngularDeltaVectorFromNeutral_y")] = GameStateManager.Instance.hmdAngularDeltaFromNeutral.y.ToString();
        logData[Array.IndexOf(ColumnNames, "hmdAngularDeltaVectorFromNeutral_z")] = GameStateManager.Instance.hmdAngularDeltaFromNeutral.z.ToString();

        logData[Array.IndexOf(ColumnNames, "hmdRollVelocity")] = GameStateManager.Instance.hmdRollVelocity.ToString();
        logData[Array.IndexOf(ColumnNames, "rollDirection")] = GameStateManager.Instance.rollDirection.ToString();


        logData[Array.IndexOf(ColumnNames, "cursorInWorldPosition_X")] = gameStates.cursorInWorldPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "cursorInWorldPosition_Y")] = gameStates.cursorInWorldPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "cursorInWorldPosition_Z")] = gameStates.cursorInWorldPosition.z.ToString();
        logData[Array.IndexOf(ColumnNames, "cursorInHeadPosition_X")] = gameStates.cursorInHmdPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "cursorInHeadPosition_Y")] = gameStates.cursorInHmdPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "cursorInHeadPosition_Z")] = gameStates.cursorInHmdPosition.z.ToString();

        logData[Array.IndexOf(ColumnNames, "stimInWorldPosition_X")] = gameStates.targetInWorldPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "stimInWorldPosition_Y")] = gameStates.targetInWorldPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "stimInWorldPosition_Z")] = gameStates.targetInWorldPosition.z.ToString();
        logData[Array.IndexOf(ColumnNames, "stimInHeadPosition_X")] = gameStates.targetInHmdPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "stimInHeadPosition_Y")] = gameStates.targetInHmdPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "stimInHeadPosition_Z")] = gameStates.targetInHmdPosition.z.ToString();

        logData[Array.IndexOf(ColumnNames, "artificialGazeTargetOffsetDeg")] = gameStates.artificialGazeTargetOffsetDeg.ToString();
        logData[Array.IndexOf(ColumnNames, "stimType")] = gameStates.targetType;


        logData[Array.IndexOf(ColumnNames, "gazeCursorRingInWorldPosition_X")] = gameStates.gazeCursorRingInWorldPos.x.ToString();
        logData[Array.IndexOf(ColumnNames, "gazeCursorRingInWorldPosition_Y")] = gameStates.gazeCursorRingInWorldPos.y.ToString();
        logData[Array.IndexOf(ColumnNames, "gazeCursorRingInWorldPosition_Z")] = gameStates.gazeCursorRingInWorldPos.z.ToString();
        logData[Array.IndexOf(ColumnNames, "gazeCursorRingInHeadPosition_X")] = gameStates.gazeCursorRingInHmdPos.x.ToString();
        logData[Array.IndexOf(ColumnNames, "gazeCursorRingInHeadPosition_Y")] = gameStates.gazeCursorRingInHmdPos.y.ToString();
        logData[Array.IndexOf(ColumnNames, "gazeCursorRingInHeadPosition_Z")] = gameStates.gazeCursorRingInHmdPos.z.ToString();
         
        logData[Array.IndexOf(ColumnNames, "object2DragInWorldPosition_X")] = gameStates.object2DragInWorldPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "object2DragInWorldPosition_Y")] = gameStates.object2DragInWorldPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "object2DragInWorldPosition_Z")] = gameStates.object2DragInWorldPosition.z.ToString();
        logData[Array.IndexOf(ColumnNames, "object2DragInHmdPosition_X")] = gameStates.object2DragInHmdPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "object2DragInHmdPosition_Y")] = gameStates.object2DragInHmdPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "object2DragInHmdPosition_Z")] = gameStates.object2DragInHmdPosition.z.ToString();
        logData[Array.IndexOf(ColumnNames, "DropzoneInWorldPosition_X")] = gameStates.DropzoneInWorldPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "DropzoneInWorldPosition_Y")] = gameStates.DropzoneInWorldPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "DropzoneInWorldPosition_Z")] = gameStates.DropzoneInWorldPosition.z.ToString();
        logData[Array.IndexOf(ColumnNames, "DropzoneInHmdPosition_X")] = gameStates.DropzoneInHmdPosition.x.ToString();
        logData[Array.IndexOf(ColumnNames, "DropzoneInHmdPosition_Y")] = gameStates.DropzoneInHmdPosition.y.ToString();
        logData[Array.IndexOf(ColumnNames, "DropzoneInHmdPosition_Z")] = gameStates.DropzoneInHmdPosition.z.ToString();

        logData[Array.IndexOf(ColumnNames, "cursorTargetAngle")] = gameStates.cursorTargetAngle.ToString();
        logData[Array.IndexOf (ColumnNames, "gazeTargetAngle")] = gameStates.gazeTargetAngle.ToString();
        logData[Array.IndexOf(ColumnNames, "cursorDropzoneAngle")] = gameStates.cursorDropzoneAngle.ToString();
        logData[Array.IndexOf(ColumnNames, "cursorObjectAngle")] = gameStates.cursorObjectAngle.ToString();

        logData[Array.IndexOf(ColumnNames, "trackPadClicked")] = gameStates.trackPadClicked.ToString();
        logData[Array.IndexOf(ColumnNames, "inputState")] = GameStateManager.Instance.prediction.ToString();

        logData[Array.IndexOf (ColumnNames, "targetAmp")] = gameStates.targetAmp.ToString();
        logData[Array.IndexOf (ColumnNames, "targetSize")] = gameStates.targetSize.ToString();
        logData[Array.IndexOf (ColumnNames, "failureMode")] = gameStates.failureMode.ToString();
        logData[Array.IndexOf(ColumnNames, "isInDrag")] = GameStateManager.Instance.isInDrag.ToString();

        csv_utils.Log(logData);

        frame += 1;
    }

    private void OnDisable() {
        csv_utils.StopLogging();
    }

    void OnApplicationQuit() {
        csv_utils.StopLogging();
    }
}
