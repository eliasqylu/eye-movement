using UnityEngine;

/// <summary>
/// Selects and owns the active <see cref="IEyeTrackerProvider"/> for the
/// application. Auto-created at runtime before any scene loads, so no scene
/// wiring is required. The provider factory is the single switch point for
/// swapping data sources (simulation now, PICO later).
/// </summary>
public static class EyeTrackingBootstrap
{
    private static IEyeTrackerProvider _active;

    public static IEyeTrackerProvider Active
    {
        get
        {
            if (_active == null)
            {
                _active = CreateProvider();
            }
            return _active;
        }
    }

    /// <summary>
    /// Factory for the eye data source. Real hardware providers are added here
    /// as they become available; the simulated provider is the default so the
    /// full system runs on a headset without eye tracking.
    /// </summary>
    private static IEyeTrackerProvider CreateProvider()
    {
        return new SimulatedEyeProvider();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        // Force creation up front so the provider exists before other scripts.
        _ = Active;
    }
}

/// <summary>
/// Drives the active provider's per-frame update. Auto-created at runtime and
/// attached to a persistent hidden object.
/// </summary>
public class EyeTrackingProviderDriver : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Create()
    {
        var go = new GameObject(nameof(EyeTrackingProviderDriver));
        DontDestroyOnLoad(go);
        go.AddComponent<EyeTrackingProviderDriver>();
    }

    private void Update()
    {
        EyeTrackingBootstrap.Active.Tick();
    }
}
