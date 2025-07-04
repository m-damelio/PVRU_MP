using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections;

public class DoorNetworkedController : NetworkBehaviour
{
    private Animator _animator;
    [SerializeField] private Collider doorCollider;

    void Awake()
    {
        _animator = GetComponent<Animator>();
        if (doorCollider == null) doorCollider = GetComponent<Collider>();
    }

    //Called on all peers when the door should open
    //Triggers animation and removes collider afterwards so passage is possible
    [Rpc(RpcSources.InputAuthority, RpcTargets.All)]
    public void RPC_OpenDoor()
    {
        _animator.SetTrigger("OpenDoor");
        StartCoroutine(RemoveColliderAfterAnimation());
    }

    private IEnumerator RemoveColliderAfterAnimation()
    {
        //Grabs current state info and assumes openDoor animation to be in layer 0. Divides by SpeedMultiplier for accuracy if changed in animator controller
        var state = _animator.GetCurrentAnimatorStateInfo(0);
        float duration = state.length / _animator.GetFloat("SpeedMultiplier");

        yield return new WaitForSeconds(duration);

        if (doorCollider != null) doorCollider.enabled = false;
    }

    public void RequestOpen()
    {
        if (Object.HasInputAuthority)
        {
            RPC_OpenDoor();
        }
    }
}
