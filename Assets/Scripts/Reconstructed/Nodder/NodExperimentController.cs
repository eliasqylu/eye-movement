using UnityEngine;

/// <summary>
/// Reconstructed Nodder experiment controller (guid 9069a700...). Field
/// surface restored from the scene so serialized references deserialize; full
/// behaviour is implemented in P3 with the Nodder technique.
/// </summary>
public class NodExperimentController : MonoBehaviour
{
    [Header("Object References")]
    public GameObject cursor;
    public GazeSwitch headGazeGestureDetector;
    public NodDetector nodDetector;
    public headtracking headtrackingScript;

    [Header("Options")]
    public bool detectNod = true;
    public bool allowInputOverwrite;
}
