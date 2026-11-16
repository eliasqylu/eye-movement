using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using j_utils;
public class ToggleSearchResult : MonoBehaviour
{
    public GameObject display;
    Camera xrCamera;
    // Start is called before the first frame update
    void Start()
    {
        display.SetActive(false);
        xrCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.D))
        {
            Vector3 displayInHmd = eyeHead_utils.ficks2vec(0, 10);
            display.transform.position = xrCamera.transform.TransformPoint(displayInHmd*0.8f);
            display.transform.LookAt(xrCamera.transform);
            display.transform.Rotate(new Vector3(0, 180, 0));
            display.SetActive(!display.activeSelf);
        }
    }
}
