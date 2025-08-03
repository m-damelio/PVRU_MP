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

    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;

    protected override void Awake()
    {
        base.Awake();
    }

    public override void Spawned()
    {
        // Call parent spawned first
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

        // Handle stealing progress
        if (IsAttachedToGuard && attachedGuard != null && keyCardCollider != null)
        {
            bool isGrabbable = attachedGuard.State == GuardState.Rest;
            if (keyCardCollider.enabled != isGrabbable)
            {
                keyCardCollider.enabled = isGrabbable;
            }
            
        }
    }

    public override void Render()
    {
        // Call parent render first
        base.Render();
        
        // Add our custom visual state updates
        UpdateAttachmentVisualState();
    }

    private void UpdateAttachmentVisualState()
    {
        if (IsAttachedToGuard && guardAttachPoint != null)
        {
            if (Holder == PlayerRef.None)
            {
                transform.position = guardAttachPoint.position;
                transform.rotation = guardAttachPoint.rotation;
            }
        }
    }

    private void UpdateVisualState()
    {
        bool isHeld = Holder != PlayerRef.None;

        if(keyCardCollider != null) 
        {
            if (!IsAttachedToGuard)
            {
                //keyCardCollider.enabled = !isHeld;
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