using UnityEngine;

/// <summary>Which physical eye a sample belongs to.</summary>
public enum EyeSide
{
    Left = 0,
    Right = 1
}

/// <summary>
/// Hardware-agnostic eye tracking source. All experiment code consumes eye
/// data through this interface so the underlying data source can be swapped
/// (simulated provider on Quest 3, PICO provider on Pico 4 Pro) without
/// touching the interaction logic.
///
/// All gaze directions are unit vectors expressed in the HMD/headset local
/// coordinate frame; origins are in the same frame (metres).
/// </summary>
public interface IEyeTrackerProvider
{
    /// <summary>Human-readable provider name (logging / debugging).</summary>
    string Name { get; }

    /// <summary>Whether the provider currently delivers valid samples.</summary>
    bool IsTracking { get; }

    /// <summary>Refresh internal state once per frame, before consumers read.</summary>
    void Tick();

    /// <summary>
    /// Gets the local-space gaze ray for one eye.
    /// Returns false when the eye is not tracked.
    /// </summary>
    bool TryGetGazeRay(EyeSide side, out Vector3 origin, out Vector3 direction);

    /// <summary>Per-eye openness in [0,1]; 1 = fully open, 0 = closed.</summary>
    float GetEyeOpenness(EyeSide side);

    /// <summary>Whether the given eye's sample is valid this frame.</summary>
    bool IsEyeValid(EyeSide side);
}
