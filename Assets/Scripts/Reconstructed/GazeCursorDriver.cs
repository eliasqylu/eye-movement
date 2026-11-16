using UnityEngine;

/// <summary>
/// Reconstructed gaze-cursor driver (guid b561769a...). Positions the
/// gaze-cursor object from eye azimuth/polar data using I-DT fixation logic.
/// The serialized field surface is restored from the scene so existing
/// references and parameters deserialize correctly; the full fixation-driving
/// behaviour is implemented in P2 alongside the Aligner technique.
/// </summary>
public class GazeCursorDriver : MonoBehaviour
{
    [Header("Object References")]
    public GameObject gazeCursor;

    [Header("Fixation Settings")]
    public float dispersionMultiplier = 1.5f;
    public float minimumFixationDuration = 0.08f;

    /// <summary>Latest eye azimuth/polar coordinates (degrees).</summary>
    public Vector2 eyeAzPol;
}
