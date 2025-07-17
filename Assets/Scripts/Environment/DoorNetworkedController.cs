using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections;

public class DoorNetworkedController : NetworkBehaviour
{
    private Animator _animator;
    private AnimatorStateSync _animatorSync;
    [SerializeField] private Collider doorCollider;

    [Networked] public bool IsOpen {get; set;}

    void Awake()
    {
        _animator = GetComponent<Animator>();
        _animatorSync = GetComponent<AnimatorStateSync>();
        if (doorCollider == null) doorCollider = GetComponent<Collider>();
    }

    public void RequestOpen()
    {
        
        if (IsOpen)
        {
            Debug.Log("Door: Already open, ignoring request");
            return;
        }
        RPC_RequestOpenDoor();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestOpenDoor()
    {
        Debug.Log("Door: RPC_RequestOpenDoor received by state authority");
        if(!IsOpen)
        {
            IsOpen = true;
            RPC_OpenDoor();
        }   
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestCloseDoor()
    {
        Debug.Log("Door: RPC_RequestCloseDoor received by state authority");
        if(IsOpen)
        {
            IsOpen = false;
            RPC_CloseDoor();
        }
    }

    //Called on all peers when the door should open
    //Triggers animation and removes collider afterwards so passage is possible
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenDoor()
    {
        Debug.Log($"Door: RPC_OpenDoor called!");
        _animatorSync.NetworkTrigger("OpenDoor");
        StartCoroutine(SwitchColliderActiveStateAfterAnimation());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_CloseDoor()
    {
        Debug.Log($"Door: RPC_CloseDoor called!");
        _animatorSync.NetworkTrigger("CloseDoor");
        StartCoroutine(SwitchColliderActiveStateAfterAnimation());
    }

    private IEnumerator SwitchColliderActiveStateAfterAnimation()
    {
        Debug.Log("Door: Starting collider removal coroutine");
        
        // Wait one frame to ensure the animation state has updated
        yield return null;
        
        //Grabs current state info and assumes openDoor animation to be in layer 0
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"Door: Current animation state: {state.fullPathHash}, Length: {state.length}");
        
        float duration = state.length / 1f;
        Debug.Log($"Door: Animation duration: {duration} seconds");

        yield return new WaitForSeconds(duration);

        Debug.Log("Door: Switch collider state");
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
        RPC_RequestOpenDoor();
    }

    [ContextMenu("Test Close Door")]
    public void TestCloseDoor()
    {
        RPC_RequestCloseDoor();
    }
}


