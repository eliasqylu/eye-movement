using UnityEngine;

/// <summary>
/// Reconstructed shared state manager (the original class was missing from the
/// released Zenodo project). The member surface is reconstructed from all 200+
/// call sites across the experiment / application / data-collection scripts.
///
/// The singleton auto-instantiates a hidden GameObject, so no scene object is
/// required. State setters are kept explicit to match the original API, and
/// <see cref="UpdateAllPreviousStates"/> is invoked at the end of every frame
/// by the experiment controllers to snapshot the "*_prev" fields.
/// </summary>
public class GameStateManager : MonoBehaviour
{
    private static GameStateManager _instance;

    public static GameStateManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindObjectOfType<GameStateManager>();
                if (_instance == null)
                {
                    var go = new GameObject(nameof(GameStateManager));
                    _instance = go.AddComponent<GameStateManager>();
                }
            }
            return _instance;
        }
    }

    // ------------------------------------------------------------------
    // Centering / frame bookkeeping
    // ------------------------------------------------------------------
    [Header("Centering")]
    public bool frameOfCentering;

    // ------------------------------------------------------------------
    // Interaction state (drag / confirm / nod reset)
    // ------------------------------------------------------------------
    [Header("Interaction State")]
    public bool isInDrag;
    public bool isInDrag_prev;

    public bool canConfirm;
    public bool canConfirm_prev;

    /// <summary>One-frame confirmation pulse; auto-cleared at end of frame.</summary>
    public bool confirmed;

    /// <summary>Set when a nod must be cancelled/reset by the state machine.</summary>
    public bool resetNod;

    // ------------------------------------------------------------------
    // Prediction / controller mode
    // 0 = default, 1 = head mode, 2 = gesture/confirm mode
    // ------------------------------------------------------------------
    [Header("Prediction / Controller")]
    public int prediction;
    public int controllerState;

    /// <summary>External input override (-1 none, 0/2 forced modes).</summary>
    public int overwriteValue;

    // ------------------------------------------------------------------
    // Head kinematics
    // ------------------------------------------------------------------
    [Header("Head Kinematics")]
    public int rollDirection;
    public float hmdRollVelocity;
    public Vector3 hmdAngularDeltaVector;
    public Vector3 hmdAngularDeltaFromNeutral;
    public Quaternion CursorOffsetQuaternion = Quaternion.identity;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // ------------------------------------------------------------------
    // Explicit state setters (match original API)
    // ------------------------------------------------------------------
    public void SetDrag(bool value)
    {
        isInDrag = value;
    }

    public void SetCanConfirm(bool value)
    {
        canConfirm = value;
    }

    public void SetConfirmed(bool value)
    {
        confirmed = value;
    }

    public void SetResetNod(bool value)
    {
        resetNod = value;
    }

    /// <summary>
    /// Snapshots the current interaction flags into their previous-frame
    /// counterparts and clears the one-frame confirmation pulse. Called once
    /// at the end of every frame by the experiment controllers.
    /// </summary>
    public void UpdateAllPreviousStates()
    {
        isInDrag_prev = isInDrag;
        canConfirm_prev = canConfirm;
        confirmed = false;
    }
}
