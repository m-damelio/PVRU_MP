using UnityEngine;
using Fusion;
using Fusion.XR.Host.Grabbing;
using System.Collections.Generic;

public class StealableKeyCard : NetworkedKeyCard
{
    [Header("Guard Attachment")]
    [SerializeField] private Transform guardAttachPoint; // Where the keycard attaches to the guard
    [SerializeField] private GuardNetworkedController attachedGuard; // Reference to the guard
    [SerializeField] private bool isAttachedToGuard = true; // Start attached to guard
    [SerializeField] private NetworkObject levelController;
    private NetworkObject originalParentNO;
    private Transform originalParent;
    private bool wasParentedToGuard;



    [Header("Additional Networked Properties")]
    [Networked] public bool IsAttachedToGuard {get;set;}
    [Networked] public bool IsStealable { get; set; }

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Material _originalMaterial;
    private ChangeDetector _changeDetector;
    private Rigidbody _rigidBody;

    protected override void Awake()
    {
        base.Awake();
        _rigidBody = transform.GetComponent<Rigidbody>();
    }

    public override void Spawned()
    {
        // Call parent spawned first
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        base.Spawned();

        originalParent = transform.parent;

        // Initialize guard attachment
        if (Object.HasStateAuthority)
        {
            IsAttachedToGuard = isAttachedToGuard;

            if (keyCardCollider != null)
            {
                keyCardCollider.enabled = false; //Initially not grabbable
            }
            if (attachedGuard != null) originalParentNO = attachedGuard.transform.GetComponent<NetworkObject>();
        }

        UpdateColliderState();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (IsAttachedToGuard && attachedGuard != null && keyCardCollider != null)
        {
            bool newStealableState = attachedGuard.State == GuardState.Rest;
            if (IsStealable != newStealableState)
            {
                Debug.Log($"StealableKeyCard: IsStealable changing from {IsStealable} to new staet {newStealableState}");
                IsStealable = newStealableState;
            }
            
        }
        if (Holder == PlayerRef.None && guardAttachPoint != null && IsAttachedToGuard)
        {
            transform.position = guardAttachPoint.position;
            transform.rotation = guardAttachPoint.rotation;
        }
    }


    public override void Render()
    {
        base.Render();

        foreach (var changedProperty in _changeDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(IsStealable))
            {
                UpdateColliderState();
            }
            if (changedProperty == nameof(IsAttachedToGuard))
            {
                UpdateGravity();
            }
        }
    }
    private void UpdateColliderState()
    {
        if (keyCardCollider != null)
        {
            bool shouldBeEnabled = IsStealable;
            keyCardCollider.enabled = shouldBeEnabled;
            
            Debug.Log($"StealableKeyCard: Collider enabled: {shouldBeEnabled}, IsStealable: {IsStealable}, IsAttached: {IsAttachedToGuard}");
        }
    }

    private void UpdateGravity()
    {
        if (_rigidBody != null) _rigidBody.useGravity = !IsAttachedToGuard;
    }
    protected override void OnGrab(NetworkGrabber grabber)
    {
        if (Object.HasStateAuthority)
        {
            if (IsAttachedToGuard)
            {
                IsAttachedToGuard = false;

                transform.SetParent(levelController.transform);
            }
        }
        base.OnGrab(grabber);
    }

    // For setting up the keycard on the guard
    public void AttachToGuard(GuardNetworkedController guard, Transform attachPoint)
    {
        if (Object.HasStateAuthority)
        {
            attachedGuard = guard;
            guardAttachPoint = attachPoint;
            IsAttachedToGuard = true;
            Holder = PlayerRef.None;
            if(originalParent != null) transform.SetParent(originalParentNO.transform);
        }
    }
}