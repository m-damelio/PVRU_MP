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


    [Header("Additional Networked Properties")]
    [Networked] public bool IsAttachedToGuard {get;set;}
    [Networked] public bool IsStealable { get; set;  }

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    private Material _originalMaterial;
    private ChangeDetector _changeDetector;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Spawned()
    {
        // Call parent spawned first
        _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        base.Spawned();

        // Initialize guard attachment
        if (Object.HasStateAuthority)
        {
            IsAttachedToGuard = isAttachedToGuard;

            if (keyCardCollider != null)
            {
                keyCardCollider.enabled = false; //Initially not grabbable
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (IsAttachedToGuard && attachedGuard != null && keyCardCollider != null)
        {
            IsStealable = attachedGuard.State == GuardState.Rest;
            Debug.Log($"StealableKeyCard: Card is stealable {IsStealable}");
        }
    }


    public override void Render()
    {
        foreach (var changedProperty in _changeDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(IsStealable))
            {
                if (keyCardCollider != null) keyCardCollider.enabled = IsStealable;
                if (IsStealable)
                {
                    Debug.Log("Client: Card is now stealable");
                }
                else
                {
                    Debug.Log("Client: card is no longer stealable");
                }
            }
        }
    }
    protected override void OnGrab(NetworkGrabber grabber)
    {
        if (Object.HasStateAuthority)
        {
            if (IsAttachedToGuard)
            {
                IsAttachedToGuard = false;

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
        }
    }
}