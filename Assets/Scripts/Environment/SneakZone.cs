using UnityEngine;
using Fusion;

[RequireComponent(typeof(Collider), typeof(ScanlineController))]
public class SneakZone : NetworkBehaviour
{
    [Header("Layer allowing/disallowing teleport")]
    [SerializeField][Layer] private int allowTeleport;
    [SerializeField][Layer] private int disallowTeleport;

    [Header("Networked Proeperties")]
    [Networked] public bool IsSneakZoneActive {get; set;}

    private ScanlineController _scanlineController;
    private Collider _sneakZoneCollider;
    private GameObject _visualGameobject;

    void Start()
    {
        _scanlineController = GetComponent<ScanlineController>();
        _sneakZoneCollider = GetComponent<Collider>();
        _visualGameobject = transform.GetChild(0).gameObject;
    }

    public override void Spawned()
    {
        IsSneakZoneActive = true;
    
    }

    void OnTriggerEnter(Collider other)
    {
        if (!Object.HasStateAuthority) return;
        if(!IsSneakZoneActive) return;

        var nb = other.GetComponent<NetworkObject>();
        if (nb == null)
        {
            return;
        }

        var player = other.GetComponentInParent<VRPlayer>();
        if (player == null)
        {
            return;
        }
        

        if (player.NetworkedPlayerType == VRPlayer.PlayerType.EnhancedSneaking)
        {
            Debug.Log("Networked sneakable player detected.");
            if (player.NetworkedPlayerState == VRPlayer.PlayerState.Sneaking)
            {
                Debug.Log("Is Sneaking");
            }
            else
            {
                Debug.Log("Is Walking");
            }
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SetActive(bool enabled)
    {
        IsSneakZoneActive = enabled;
        _sneakZoneCollider.enabled = enabled;
        if(_visualGameobject != null)
        {
            if(IsSneakZoneActive)
            {
                _visualGameobject.layer = allowTeleport;
            }
            else
            {
                _visualGameobject.layer = disallowTeleport;
            }
        }
    }
    
    [ContextMenu("Test switch active")]
    public void TestSwitchActive()
    {
        if(_scanlineController == null) return;

        //State to test
        bool newState = !IsSneakZoneActive;

        //Switch scanline and test sneak zone rpc
        RPC_SetActive(newState);
        _scanlineController.RPC_SetActive(newState);
        
    }
}
