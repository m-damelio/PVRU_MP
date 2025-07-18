using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections;

public class DoorNetworkedController : NetworkBehaviour, ILevelResettable
{
    private Animator _animator;
    private AnimatorStateSync _animatorSync;
    [SerializeField] private Collider doorCollider;

    [Networked] public bool IsOpen {get; set;}

    public AudioSource doorAudioSource;

    [Header("Initial state")]
    [SerializeField] private bool startOpen = false;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animatorSync = GetComponent<AnimatorStateSync>();
        if (doorCollider == null) doorCollider = GetComponent<Collider>();
    }

    //Interface ILevelResettable implementation
    public void SetInitialState()
    {
        if(Object.HasStateAuthority)
        {
            IsOpen = startOpen;
            if(doorCollider != null) doorCollider.enabled = !startOpen;
        }
    }

    public void ResetToInitialState()
    {
        if(Object.HasStateAuthority)
        {
            IsOpen = startOpen;

            if(startOpen)
            {
                RequestOpen();
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
        if(!IsOpen)
        {
            Debug.Log("Door: Already closed, ignoring request");
            return;
        }

        RPC_SetDoorState(false);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetDoorState(bool shouldOpen)
    {
        Debug.Log($"Door: RPC_SetDoorState received: {shouldOpen}");
        if(IsOpen != shouldOpen)
        {
            IsOpen = shouldOpen;

            //Notify clients to play animation
            if(shouldOpen)
            {
                RPC_PlayOpenAnimation();
            }
            else
            {
                RPC_PlayCloseAnimation();
            }
        }   
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayOpenAnimation()
    {
        Debug.Log("Door: Playing open animation");
        _animatorSync.NetworkTrigger("OpenDoor");
        StartCoroutine(UpdateColliderAfterAnimation());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlayCloseAnimation()
    {
        Debug.Log("Door: Playing close animation");
        _animatorSync.NetworkTrigger("CloseDoor");
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


