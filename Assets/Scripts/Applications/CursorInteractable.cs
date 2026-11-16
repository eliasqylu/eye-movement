using UnityEngine;

public class CursorInteractable : MonoBehaviour
{
    [Tooltip("The default material for the object when not hovered.")]
    public Material defaultMaterial;

    private Renderer objectRenderer;
    private bool isHovered = false;

    public Color defaultColor = new Color(0.5f, 0.5f, 0.5f, 1);
    public Color hoverColor = new Color(0.3f, 0.3f, 0.3f, 1);

    float hoverEnlargeRtio = 1.2f;

    void Awake()
    {
        objectRenderer = GetComponent<Renderer>();
        if (objectRenderer == null)
        {
            Debug.LogError("CursorInteractable: No Renderer found on " + gameObject.name, this);
            enabled = false; // Disable script if no renderer
            return;
        }

        // Set the initial material to default
        if (defaultMaterial != null)
        {
            objectRenderer.material = defaultMaterial;
        }
        else
        {
            // If no default material is assigned, use the current material as default
            defaultMaterial = objectRenderer.material;
        }
    }

    // Call this method when the gaze enters the object
     public void OnCursorEnter()
    {
        if (!isHovered)
        {
            isHovered = true;
            // objectRenderer.material.color = hoverColor;
            Vector3 scale = gameObject.transform.localScale;
            if (gameObject.name == "TimeSeekSlider")
            {
                gameObject.transform.localScale = new Vector3(scale.x, scale.y * hoverEnlargeRtio, scale.z);
            }
            else
            {
                gameObject.transform.localScale = new Vector3(scale.x * hoverEnlargeRtio, scale.y * hoverEnlargeRtio, scale.z);
            }
        }
    }

    // Call this method when the gaze exits the object
    public void OnCursorExit()
    {
        if (isHovered)
        {
            isHovered = false;
            // objectRenderer.material.color = defaultColor;
            Vector3 scale = gameObject.transform.localScale;
            if (gameObject.name == "TimeSeekSlider")
            {
                gameObject.transform.localScale = new Vector3(scale.x, scale.y / hoverEnlargeRtio, scale.z);
            }
            else
            {
                gameObject.transform.localScale = new Vector3(scale.x / hoverEnlargeRtio, scale.y / hoverEnlargeRtio, scale.z);
            }
        }
    }
}


    // public void OnCursorEnter()
    // {
    //     if (!isHovered)
    //     {
    //         isHovered = true;
    //         objectRenderer.material.color = Color.black;
    //         if (gameObject.name == "TimeSeekSlider")
    //         {
    //             objectRenderer.material.color = Color.yellow;
    //             BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
    //             Vector3 currentSize = boxCollider.size;
    //             boxCollider.size = new Vector3(currentSize.x, 2, currentSize.z);
    //         }
    //     }
    // }

    // // Call this method when the gaze exits the object
    // public void OnCursorExit()
    // {
    //     if (isHovered)
    //     {
    //         isHovered = false;
    //         objectRenderer.material.color = Color.black;
    //         if (gameObject.name == "TimeSeekSlider")
    //         {
    //             objectRenderer.material.color = Color.white;
    //             BoxCollider boxCollider = gameObject.GetComponent<BoxCollider>();
    //             Vector3 currentSize = boxCollider.size;
    //             boxCollider.size = new Vector3(currentSize.x, 1, currentSize.z);
    //         }
    //     }
    // }
