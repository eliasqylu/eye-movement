using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Simulated eye tracking source for headsets without eye tracking (Quest 3).
///
/// Gaze: the head forward ray in the HMD local frame plus small synthetic
/// Gaussian jitter, so gaze behaves like head pointing with measurement noise.
/// Openness: each eye is closed by holding its dedicated input (controller
/// primary button in-headset, arrow keys in the editor), which lets the full
/// per-eye Winker clutch logic be exercised end to end.
///
/// When real eye tracking arrives, only this source is replaced; consumers and
/// the interaction state machine stay unchanged.
/// </summary>
public class SimulatedEyeProvider : IEyeTrackerProvider
{
    // --- Gaze synthesis ------------------------------------------------
    /// <summary>Std deviation of the synthetic gaze noise (degrees).</summary>
    private const float JitterStdDeg = 0.3f;

    /// <summary>Approx. horizontal eye offset from head centre (metres).</summary>
    private const float EyeOffsetX = 0.032f;

    // --- Simulated closure state --------------------------------------
    private bool _leftClosed;
    private bool _rightClosed;
    private bool _leftValid;
    private bool _rightValid;

    private readonly System.Random _rng = new System.Random(1234);
    private readonly List<InputDevice> _devices = new List<InputDevice>();

    public string Name => "SimulatedEyeProvider";
    public bool IsTracking => true;

    public void Tick()
    {
        bool leftButton = ReadLeftClosure();
        bool rightButton = ReadRightClosure();

        // Openness follows the held input directly; the 0.3 threshold and the
        // 0.25 s confirmation logic live in the interaction scripts.
        _leftClosed = leftButton;
        _rightClosed = rightButton;
        _leftValid = true;
        _rightValid = true;
    }

    public bool TryGetGazeRay(EyeSide side, out Vector3 origin, out Vector3 direction)
    {
        float sign = side == EyeSide.Left ? -1f : 1f;
        origin = new Vector3(sign * EyeOffsetX, 0f, 0f);

        // Head forward in local space + independent per-axis Gaussian jitter.
        float jx = NextGaussian() * JitterStdDeg * Mathf.Deg2Rad;
        float jy = NextGaussian() * JitterStdDeg * Mathf.Deg2Rad;
        direction = new Vector3(jx, jy, 1f).normalized;
        return true;
    }

    public float GetEyeOpenness(EyeSide side)
    {
        bool closed = side == EyeSide.Left ? _leftClosed : _rightClosed;
        return closed ? 0f : 1f;
    }

    public bool IsEyeValid(EyeSide side)
    {
        return side == EyeSide.Left ? _leftValid : _rightValid;
    }

    // ------------------------------------------------------------------
    // Input: controller primary buttons (headset) + arrow keys (editor)
    // ------------------------------------------------------------------
    private bool ReadLeftClosure()
    {
        if (Input.GetKey(KeyCode.LeftArrow)) return true;
        return ReadControllerButton(
            InputDeviceCharacteristics.Left | InputDeviceCharacteristics.Controller,
            CommonUsages.primaryButton);
    }

    private bool ReadRightClosure()
    {
        if (Input.GetKey(KeyCode.RightArrow)) return true;
        return ReadControllerButton(
            InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller,
            CommonUsages.primaryButton);
    }

    private bool ReadControllerButton(InputDeviceCharacteristics characteristics, InputFeatureUsage<bool> usage)
    {
        InputDevices.GetDevices(_devices);
        foreach (var device in _devices)
        {
            if (device.characteristics.HasFlag(characteristics))
            {
                if (device.TryGetFeatureValue(usage, out bool pressed) && pressed)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // ------------------------------------------------------------------
    // Standard normal sample (Box-Muller)
    // ------------------------------------------------------------------
    private float _spare;
    private bool _hasSpare;

    private float NextGaussian()
    {
        if (_hasSpare)
        {
            _hasSpare = false;
            return _spare;
        }

        float u1, u2;
        do
        {
            u1 = (float)_rng.NextDouble();
        } while (u1 <= Mathf.Epsilon);
        u2 = (float)_rng.NextDouble();

        float mag = Mathf.Sqrt(-2f * Mathf.Log(u1));
        float angle = 2f * Mathf.PI * u2;
        _spare = mag * Mathf.Sin(angle);
        _hasSpare = true;
        return mag * Mathf.Cos(angle);
    }
}
