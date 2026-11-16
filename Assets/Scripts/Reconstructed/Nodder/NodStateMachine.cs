using UnityEngine;

/// <summary>
/// Reconstructed Nodder state machine (source missing from the released
/// project). The full state graph (idle -> nod click / down-hold grab ->
/// confirm drop) is implemented in P3; only the surface required for the
/// project to compile and reset is provided here.
/// </summary>
public class NodStateMachine : MonoBehaviour
{
    /// <summary>Reset the state machine back to its idle state.</summary>
    public void ResetToIdle()
    {
        // P3: reset all internal nod states and timers.
    }
}
