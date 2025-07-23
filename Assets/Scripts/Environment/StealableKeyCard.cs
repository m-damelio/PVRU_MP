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
    [SerializeField] private float stealRange = 1.5f; // How close player needs to be to steal
    
    [Header("Stealing Settings")]
    [SerializeField] private bool requireSneaking = true; // Must be sneaking to steal
    [SerializeField] private float stealDuration = 2f; // How long to hold button to steal
    [SerializeField] private AudioClip stealSound;
    [SerializeField] private AudioSource audioSource;

    [Header("Additional Networked Properties")]
    [Networked] public bool IsAttachedToGuard {get;set;}
    [Networked] public float StealProgress {get;set;} // 0-1 progress of stealing
    [Networked] public PlayerRef StealingPlayer {get;set;} // Who is currently stealing

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
        }
    }

    private bool ShouldBeGrabbable(NetworkGrabber grabber)
    {
        return !IsAttachedToGuard;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        // Handle stealing progress
        if (StealingPlayer != PlayerRef.None && IsAttachedToGuard)
        {
            // Check if stealing player is still in range and meets requirements
            if (CanPlayerSteal(StealingPlayer))
            {
                StealProgress += Runner.DeltaTime / stealDuration;

                if (StealProgress >= 1f)
                {
                    // Stealing complete!
                    CompleteSteal();
                }
            }
            else
            {
                // Cancel stealing if requirements no longer met
                CancelSteal();
            }
        }
    }

    public override void Render()
    {
        // Call parent render first
        base.Render();
        
        // Add our custom visual state updates
        UpdateStealingVisualState();
    }

    private void UpdateStealingVisualState()
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
            // Enable collider for stealing even when attached, but disable when held
            keyCardCollider.enabled = !isHeld;
        }
    }

    // Called by VR player when they want to steal
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_StartSteal(PlayerRef player, RpcInfo info = default)
    {
        if(!IsAttachedToGuard) return;
        if(StealingPlayer != PlayerRef.None) return; // Already being stolen
        if(!CanPlayerSteal(player)) return;

        StealingPlayer = player;
        StealProgress = 0f;
        Debug.Log($"StealableKeyCard: Player {player} started stealing keycard");
    }

    // Called when player stops trying to steal (releases button)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_CancelSteal(PlayerRef player, RpcInfo info = default)
    {
        if(StealingPlayer != player) return;
        
        CancelSteal();
    }

    private void CancelSteal()
    {
        Debug.Log($"StealableKeyCard: Stealing cancelled");
        StealingPlayer = PlayerRef.None;
        StealProgress = 0f;
    }

    private void CompleteSteal()
    {
        Debug.Log($"StealableKeyCard: Stealing completed by player {StealingPlayer}");
        
        // Detach from guard
        IsAttachedToGuard = false;
        Holder = StealingPlayer;
        
        // Notify guard (could trigger alert behavior)
        if(attachedGuard != null)
        {
            attachedGuard.RPC_NotifyPlayerSpotted(); // Or create a specific "RPC_KeyCardStolen" method
        }

        // Play steal sound
        if(stealSound != null)
        {
            RPC_PlayStealSound();
        }

        // Reset stealing state
        StealingPlayer = PlayerRef.None;
        StealProgress = 0f;

        RPC_NotifyStolen();
    }

    private bool CanPlayerSteal(PlayerRef player)
    {
        // Find the VR player
        var playerObjects = FindObjectsByType<VRPlayer>(FindObjectsSortMode.None);
        VRPlayer vrPlayer = null;
        
        foreach(var p in playerObjects)
        {
            if(p.Object.InputAuthority == player)
            {
                vrPlayer = p;
                break;
            }
        }

        if(vrPlayer == null) return false;

        if(!attachedGuard.IsAlarmRunning) return false;

        // Check distance
        float distance = Vector3.Distance(vrPlayer.transform.position, transform.position);
        if(distance > stealRange) return false;

        // Check if player needs to be sneaking
        if(requireSneaking && vrPlayer.NetworkedPlayerState != VRPlayer.PlayerState.Sneaking)
        {
            return false;
        }

        return true;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayStealSound()
    {
        if(stealSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(stealSound);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_NotifyStolen()
    {
        Debug.Log($"StealableKeyCard: Keycard stolen - notifying all clients");
    }


    // New methods specific to stealing functionality
    public bool IsStealable()
    {
        return IsAttachedToGuard && Holder == PlayerRef.None;
    }

    public float GetStealProgress()
    {
        return StealProgress;
    }

    // For setting up the keycard on the guard
    public void AttachToGuard(GuardNetworkedController guard, Transform attachPoint)
    {
        if(Object.HasStateAuthority)
        {
            attachedGuard = guard;
            guardAttachPoint = attachPoint;
            IsAttachedToGuard = true;
            Holder = PlayerRef.None;
        }
    }
}