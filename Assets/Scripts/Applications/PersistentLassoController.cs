using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PersistentLassoController : MonoBehaviour
{
    [Header("Controls")]
    [Tooltip("Enable to paint on the surface.")]
    public bool lasso = false;
    [Tooltip("Set to true once to clear the canvas.")]
    public bool reset = false;

    [Header("Setup")]
    [Tooltip("The 3D GameObject that acts as the VR cursor.")]
    Transform cursor;

    [Tooltip("The material using the BrushShader.shader")]
    public Material brushMaterial;

    [Tooltip("Resolution of the drawing canvas.")]
    public int textureResolution = 1024;
    
    private RenderTexture canvas;
    private Camera mainCamera;
    private Renderer quadRenderer;
    public LayerMask paintableLayer;

    void Start()
    {
        mainCamera = Camera.main;
        quadRenderer = GetComponent<Renderer>();

        GameObject nodObject = GameObject.Find("nod");
        if (nodObject != null && nodObject.activeSelf)
        {
            cursor = GameObject.Find("cursor").transform;
        }
        else
        {
            GameObject directAroundObject = GameObject.Find("directAround");
            if (directAroundObject != null && directAroundObject.activeSelf)
            {
                cursor = GameObject.Find("headCursor").transform;
            }
            else
            {
                GameObject wink = GameObject.Find("wink");
                if (wink != null && wink.activeSelf)
                {
                    cursor = GameObject.Find("headCursor").transform;
                }
            }
        }

        // 1. Create the persistent RenderTexture (our canvas)
        canvas = new RenderTexture(textureResolution, textureResolution, 0);
        
        // 2. Apply the canvas to the quad's material so we can see it
        quadRenderer.material.shader = Shader.Find("Custom/DisplayShader");
        quadRenderer.material.mainTexture = canvas;

        // 3. Clear the canvas to be fully transparent
        ClearCanvas();
    }

    void Update()
    {
        // Check for the reset flag
        if (reset)
        {
            ClearCanvas();
            reset = false; // Reset the flag automatically
        }

        // Make sure cursor and material are assigned before proceeding
        if (cursor == null || brushMaterial == null) return;
        
        
        if (lasso)
        {
            // Create ray from camera to cursor
            Vector3 direction = (cursor.position - mainCamera.transform.position).normalized;
            Ray ray = new Ray(mainCamera.transform.position, direction);

            if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, paintableLayer))
            {
                // Check if we hit the correct object
                if (hit.collider == GetComponent<Collider>())
                {
                    // Paint at the hit location
                    PaintAt(hit.textureCoord);
                }
            }
        }
    }

    void PaintAt(Vector2 uv)
    {
        // Set the brush properties in the brush material
        brushMaterial.SetVector("_CenterUV", uv);

        // Create a temporary render texture
        RenderTexture temp = RenderTexture.GetTemporary(canvas.width, canvas.height, 0);

        // This is the core "paint" operation. It takes the existing canvas,
        // processes it with the brush shader (which adds our new circle),
        // and draws the result into the temporary texture.
        Graphics.Blit(canvas, temp, brushMaterial);

        // Finally, copy the updated temporary texture back to our main canvas.
        Graphics.Blit(temp, canvas);

        // Release the temporary texture
        RenderTexture.ReleaseTemporary(temp);
    }

    void ClearCanvas()
    {
        RenderTexture.active = canvas;
        GL.Clear(true, true, Color.clear); // Clear to transparent black
        RenderTexture.active = null;
    }

    // Clean up the RenderTexture when the object is destroyed
    void OnDestroy()
    {
        if (canvas != null)
        {
            canvas.Release();
        }
    }
}