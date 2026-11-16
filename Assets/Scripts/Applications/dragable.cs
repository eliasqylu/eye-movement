using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class dragable : MonoBehaviour
{
    GameObject cursor;

    public bool inDrag;
    bool inDrag_prev;
    Vector3 dragPoint_center_offset = new Vector3();
    Transform parentTransform;

    public bool dragRecentered = true;
    // Start is called before the first frame update
    void Start()
    {
        parentTransform = transform.parent;

        GameObject nodObject = GameObject.Find("nod");
        if (nodObject != null && nodObject.activeSelf)
        {
            cursor = GameObject.Find("cursor");
        }
        else
        {
            GameObject directAroundObject = GameObject.Find("directAround");
            if (directAroundObject != null && directAroundObject.activeSelf)
            {
                cursor = GameObject.Find("headCursor");
            }
            else
            {
                GameObject wink = GameObject.Find("wink");
                if (wink != null && wink.activeSelf)
                {
                    cursor = GameObject.Find("headCursor");
                }
            }
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (cursor == null) return;


        if (inDrag && !inDrag_prev)
        {
            dragPoint_center_offset = parentTransform.position - cursor.transform.position;
        }

        if (inDrag)
        {
            if (dragRecentered) parentTransform.position = cursor.transform.TransformPoint(new Vector3(0,0,0.01f));
            else parentTransform.position = cursor.transform.position + dragPoint_center_offset;
            CenterAllChildren();
        }

        inDrag_prev = inDrag;
    }

    void CenterAllChildren()
    {
        // foreach (Transform child in this.transform)
        // {
        //     child.LookAt(Camera.main.transform);
        //     child.Rotate(Vector3.up, 180f, Space.Self);
        // }
        
        parentTransform.LookAt(Camera.main.transform);
        parentTransform.Rotate(Vector3.up, 180f, Space.Self);
    }
}
