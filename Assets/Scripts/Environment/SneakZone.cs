using UnityEngine;
using Fusion;

[RequireComponent(typeof(Collider), typeof(ScanlineController))]
public class SneakZone : NetworkBehaviour, ILevelResettable
{
    [Header("Layer allowing/disallowing teleport")]
    [SerializeField][Layer] private int allowTeleport;
    [SerializeField][Layer] private int disallowTeleport;

    [Header("Networked Proeperties")]
    [Networked] public bool IsSneakZoneActive {get; set;}

    [Header("Initial State")]
    [SerializeField] private bool startActive = true;

    private ScanlineController _scanlineController;
    private Collider _sneakZoneCollider;
    private GameObject _visualGameobject;
    private GameObject _disallowTeleportColliderHolder;

    private ChangeDetector _changeDetector;

    void Start()
    {
        _scanlineController = GetComponent<ScanlineController>();
        _sneakZoneCollider = GetComponent<Collider>();
        _visualGameobject = transform.GetChild(0).gameObject;
        _disallowTeleportColliderHolder = transform.GetChild(1).gameObject;
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
    }

    public void SetInitialState()
    {
        if (Object.HasStateAuthority)
        {
            IsSneakZoneActive = startActive;
        }

    }

    public void ResetToInitialState()
    {
        if(Object.HasStateAuthority)
        {
            IsSneakZoneActive = startActive;
        }
    }

    public override void Render()
    {
        foreach (var changedProperty in _changeDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(IsSneakZoneActive)) UpdateSneakZoneVisuals();
        }
    }
    void OnTriggerEnter(Collider other)
    {
        if(Object == null) return;
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

    public void SetActive(bool shouldEnable)
    {
        if(Object.HasStateAuthority)
        {
            IsSneakZoneActive = shouldEnable;
        }
        else
        {
            RPC_RequestSetActive(shouldEnable);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSetActive(bool shouldEnable)
    {
        if(IsSneakZoneActive != shouldEnable)
        {
            IsSneakZoneActive = shouldEnable;
        }
    }
    
    private void UpdateSneakZoneVisuals()
    {
        if(_sneakZoneCollider != null) _sneakZoneCollider.enabled = IsSneakZoneActive;

        if(_scanlineController != null) _scanlineController.RPC_SetActive(IsSneakZoneActive);

        //Don't allow teleport when sneak zone is active
        if(_visualGameobject != null) _visualGameobject.layer = IsSneakZoneActive ? disallowTeleport : allowTeleport;
        if (_disallowTeleportColliderHolder != null) _disallowTeleportColliderHolder.SetActive(IsSneakZoneActive);

        Debug.Log($"SneakZone: Updated to active={IsSneakZoneActive}");
    }

    [ContextMenu("Test switch active")]
    public void TestSwitchActive()
    {
        //State to test
        bool newState = !IsSneakZoneActive;
        //Switch scanline and test sneak zone rpc
        SetActive(newState);
    }
}
