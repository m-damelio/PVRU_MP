using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections;

public class DoorNetworkedController : NetworkBehaviour
{
    private Animator _animator;
    [SerializeField] private Collider doorCollider;

    [Networked] public bool IsOpen {get; set;}

    void Awake()
    {
        _animator = GetComponent<Animator>();
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

    //Called on all peers when the door should open
    //Triggers animation and removes collider afterwards so passage is possible
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenDoor()
    {
        Debug.Log($"Door: RPC_OpenDoor called!");
        _animator.SetTrigger("OpenDoor");
        StartCoroutine(RemoveColliderAfterAnimation());
    }

    private IEnumerator RemoveColliderAfterAnimation()
    {
        Debug.Log("Door: Starting collider removal coroutine");
        
        // Wait one frame to ensure the animation state has updated
        yield return null;
        
        //Grabs current state info and assumes openDoor animation to be in layer 0
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        Debug.Log($"Door: Current animation state: {state.fullPathHash}, Length: {state.length}");
        
        float speedMultiplier = _animator.GetFloat("SpeedMultiplier");
        if (speedMultiplier == 0)
        {
            speedMultiplier = 1f; // Fallback to prevent division by zero
            Debug.LogWarning("Door: SpeedMultiplier was 0, using fallback value of 1");
        }
        
        float duration = state.length / speedMultiplier;
        Debug.Log($"Door: Animation duration: {duration} seconds (SpeedMultiplier: {speedMultiplier})");

        yield return new WaitForSeconds(duration);

        Debug.Log("Door: Removing collider");
        if (doorCollider != null) 
        {
            doorCollider.enabled = false;
            Debug.Log("Door: Collider disabled");
        }
        else
        {
            Debug.LogWarning("Door: No collider to disable");
        }
    }
    
}


