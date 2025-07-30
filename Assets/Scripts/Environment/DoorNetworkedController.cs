using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections;

public enum DoorType
{
    Elevator,
    Prison
}

public class DoorNetworkedController : NetworkBehaviour, ILevelResettable
{
    private Animator _animator;
    private AnimatorStateSync _animatorSync;
    [SerializeField] private Collider doorCollider;

    [Networked] public bool IsOpen { get; set; }
    [Networked] public bool HasInitialized { get; set; }

    public AudioSource doorAudioSource;

    [Header("Door Type (for Audio)")]
    [SerializeField] private DoorType doorType;

    [Header("Initial state")]
    [SerializeField] private bool startOpen = false;



    private ChangeDetector _changeDetector;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animatorSync = GetComponent<AnimatorStateSync>();
        if (doorCollider == null) doorCollider = GetComponent<Collider>();
    }

    //Doors didnt appear open when spawned with startOPen = true there added spawned and other methods
    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (Object.HasStateAuthority)
        {
            IsOpen = startOpen;
            HasInitialized = false;
        }

        StartCoroutine(InitializeAfterFrame());
    }

    private IEnumerator InitializeAfterFrame()
    {
        yield return null;

        if (Object.HasStateAuthority && !HasInitialized)
        {
            if (startOpen)
            {
                RPC_SetInitialOpenState();
            }
            HasInitialized = true;
        }
    }

    public override void Render()
    {
        foreach (var changedProperty in _changeDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(IsOpen))
            {
                OnDoorsStateChanged();
            }
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SetInitialOpenState()
    {
        int openStateHash = Animator.StringToHash("Base Layer.OpenDoor");
        _animator.Play(openStateHash, 0, 1.0f); //Jump to end of animation

        if (doorCollider != null) doorCollider.enabled = false;

        Debug.Log("Door: Set to initial open state");
    }

    //Interface ILevelResettable implementation
    public void SetInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsOpen = startOpen;
            if (doorCollider != null) doorCollider.enabled = !startOpen;
        }
    }

    public void ResetToInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsOpen = startOpen;

            if (startOpen)
            {
                RPC_SetInitialOpenState();
            }
            else
            {
                RequestClose();
            }
        }
    }

    public void RequestOpen()
    {

        if (IsOpen)
        {
            Debug.Log("Door: Already open, ignoring request");
            return;
        }
        RPC_SetDoorState(true);
    }

    public void RequestClose()
    {
        if (!IsOpen)
        {
            Debug.Log("Door: Already closed, ignoring request");
            return;
        }

        RPC_SetDoorState(false);
    }

    public void RequestToggle()
    {
        Debug.Log("Door: Request to toggle state received");
        if (!IsOpen) RPC_SetDoorState(true);
        else RPC_SetDoorState(false);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetDoorState(bool shouldOpen)
    {
        Debug.Log($"Door: RPC_SetDoorState received: {shouldOpen}");
        if (IsOpen != shouldOpen)
        {
            IsOpen = shouldOpen;
        }
    }

    private void OnDoorsStateChanged()
    {
        Debug.Log($"Door: State changed to {(IsOpen ? "Open" : "Closed")} via change detector");
        if (IsOpen)
        {
            PlayOpenAnimation();
        }
        else
        {
            PlayCloseAnimation();
        }
    }

    private void PlayOpenAnimation()
    {
        Debug.Log("Door: Playing open animation");
        _animatorSync.NetworkTrigger("OpenDoor");

        if (NetworkedSoundManager.Instance != null)
        {
            switch (doorType)
            {
                case DoorType.Elevator:
                    NetworkedSoundManager.Instance.PlayEnvironmentSound("Elevator_Door_Opening", transform.position);
                    break;
                case DoorType.Prison:
                    NetworkedSoundManager.Instance.PlayEnvironmentSound("Prison_Door_Opening", transform.position);
                    break;
            }
        }

        StartCoroutine(UpdateColliderAfterAnimation());
    }

    private void PlayCloseAnimation()
    {
        Debug.Log("Door: Playing close animation");
        _animatorSync.NetworkTrigger("CloseDoor");

        if (NetworkedSoundManager.Instance != null)
        {
            switch (doorType)
            {
                case DoorType.Elevator:
                    NetworkedSoundManager.Instance.PlayEnvironmentSound("Elevator_Door_Closing", transform.position);
                    break;
                case DoorType.Prison:
                    NetworkedSoundManager.Instance.PlayEnvironmentSound("Prison_Door_Closing", transform.position);
                    break;
            }
        }


        StartCoroutine(UpdateColliderAfterAnimation());
    }

    private IEnumerator UpdateColliderAfterAnimation()
    {
        Debug.Log("Door: Starting collider removal coroutine");

        // Wait one frame to ensure the animation state has updated
        yield return null;

        //Grabs current state info and assumes openDoor animation to be in layer 0
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        float duration = state.length / 1f;

        yield return new WaitForSeconds(duration);

        if (doorCollider != null)
        {
            doorCollider.enabled = !IsOpen;
            Debug.Log("Door: Collider switched");
        }
        else
        {
            Debug.LogWarning("Door: No collider to switch");
        }
    }

    [ContextMenu("Test Open Door")]
    public void TestOpenDoor()
    {
        RequestOpen();
    }

    [ContextMenu("Test Close Door")]
    public void TestCloseDoor()
    {
        RequestClose();
    }
}


