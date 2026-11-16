using UnityEngine;

/// <summary>
/// Reconstructed nod-button feedback component (guid 3577d776...). Field
/// surface restored from the scene; behaviour implemented in P3.
/// </summary>
public class NodButtonFeedback : MonoBehaviour
{
    [Header("Object References")]
    public Renderer cursorRenderer;
    public GameObject dragProgressBar;

    [Header("Angular Limits")]
    public float minDownAngle;
    public float minUpAngle_ratio = 0.33f;
    public float maxHeadDeviationAngle = 7f;
    public float maxEyeDeviationAngle = 7f;

    [Header("Options")]
    public bool showButtonFeedback = true;
}
