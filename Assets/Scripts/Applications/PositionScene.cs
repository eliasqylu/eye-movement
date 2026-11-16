using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PositionScene : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            gameObject.transform.position = Camera.main.transform.TransformPoint(0, 0.37f, 1.1f);
            // gameObject.transform.LookAt(Camera.main.transform);
            // gameObject.transform.Rotate(new Vector3(0, 180, 0));
        }
    }
}
