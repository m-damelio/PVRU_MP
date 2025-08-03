using UnityEngine;
using Fusion;

[RequireComponent(typeof(Collider), typeof(ScanlineController))]
public class SneakZone : NetworkBehaviour, ILevelResettable
{
    [Header("Layer settings")]
    [SerializeField][Layer] private int allowTeleport;
    [SerializeField][Layer] private int disallowTeleport;
    [SerializeField] private LayerMask playerLayer;

    [Header("Networked Proeperties")]
    [Networked] public bool IsSneakZoneActive {get; set;}

    [Header("Initial State")]
    [SerializeField] private bool startActive = true;

    private ScanlineController _scanlineController;
    private Collider _sneakZoneCollider;
    private GameObject _visualGameobject;
    private ChangeDetector _changeDetector;
    private VRPlayer _sneakingPlayerInZone = null;

    void Start()
    {
        _scanlineController = GetComponent<ScanlineController>();
        _sneakZoneCollider = GetComponent<Collider>();
        _visualGameobject = transform.GetChild(0).gameObject;
    }

    public override void Spawned()
    {
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        {
            if (Object.HasStateAuthority)
            {
                _sneakingPlayerInZone = null;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (!IsSneakZoneActive)
        {
            //represents exit logic
            if (_sneakingPlayerInZone != null)
            {
                _sneakingPlayerInZone.IsInSneakZoneStatus = false;
                _sneakingPlayerInZone = null;
            }
            return;
        }

        VRPlayer foundPlayer = null;
        Collider[] hitColliders = Physics.OverlapBox(
            _sneakZoneCollider.bounds.center,
            _sneakZoneCollider.bounds.extents,
            transform.rotation,
            playerLayer,
            QueryTriggerInteraction.UseGlobal
        );

        foreach (var hit in hitColliders)
        {
            var player = hit.GetComponentInParent<VRPlayer>();
            if (player != null && player.NetworkedPlayerType == VRPlayer.PlayerType.EnhancedSneaking)
            {
                foundPlayer = player;
                break; // Found the one sneaking player, no need to check further
            }
        }
        // 2. Check for an "Enter" event
        if (foundPlayer != null && _sneakingPlayerInZone == null)
        {
            Debug.Log("Networked sneakable player entered.");
            _sneakingPlayerInZone = foundPlayer;
            _sneakingPlayerInZone.IsInSneakZoneStatus = true;
        }
        // 3. Check for an "Exit" event
        else if (foundPlayer == null && _sneakingPlayerInZone != null)
        {
            Debug.Log("Networked sneakable player exited.");
            _sneakingPlayerInZone.IsInSneakZoneStatus = false;
            _sneakingPlayerInZone = null;
        }
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
    

    public void SetActive(bool shouldEnable)
    {
        if (Object.HasStateAuthority)
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

        Debug.Log($"SneakZone: Updated to active={IsSneakZoneActive}");
    }

    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // Try to get the collider component if it's not already cached.
        // This is useful for seeing the gizmo outside of Play Mode.
        Collider sneakZoneCollider = _sneakZoneCollider == null ? GetComponent<Collider>() : _sneakZoneCollider;
        if (sneakZoneCollider == null) return;

        // Set the color for the gizmo
        Gizmos.color = Color.blue; // A nice, visible green

        // The OverlapBox query uses the collider's world-space bounds and the transform's rotation.
        // We will replicate those exact parameters for the gizmo.
        Vector3 boxCenter = sneakZoneCollider.bounds.center;
        Vector3 boxSize = sneakZoneCollider.bounds.size;
        Quaternion boxRotation = transform.rotation;

        // To draw a rotated wire cube, we must set the Gizmos.matrix.
        // It's good practice to save and restore the original matrix.
        Matrix4x4 originalMatrix = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(boxCenter, boxRotation, Vector3.one);
        
        // Draw the wire cube with the correct size.
        Gizmos.DrawWireCube(Vector3.zero, boxSize);
        
        // Restore the original matrix
        Gizmos.matrix = originalMatrix;
    }
    #endif

    [ContextMenu("Test switch active")]
    public void TestSwitchActive()
    {
        //State to test
        bool newState = !IsSneakZoneActive;
        //Switch scanline and test sneak zone rpc
        SetActive(newState);
    }
}
