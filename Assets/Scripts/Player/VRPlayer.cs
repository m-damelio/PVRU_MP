using System.Collections.Generic;
using Fusion;
using Fusion.XR.Host.Rig;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using UnityEditor;
using UnityEngine.InputSystem;

public class VRPlayer : NetworkBehaviour
{
    [Header("Hardware Detection")]
    [SerializeField] private GameObject hardwareIndicator; // Visual indicator for extra hardware
    
    [Header("Sneaking Settings")]
    [SerializeField] private float sneakThreshold = 0.8f;
    
    [Header("Player Settings")]
    [SerializeField] private PlayerType playerType;
    [SerializeField] private PlayerState playerState = PlayerState.Walking;

    // Reference to the NetworkRig which handles visuals for hands/body, etc.
    private NetworkRig networkRig;
    
    // Networked properties
    [Networked] public PlayerType NetworkedPlayerType { get; set; }
    [Networked] public PlayerState NetworkedPlayerState { get; set; }
    [Networked] public float NetworkedSneakValue { get; set; }
    
    // Quest 3 specific
    [Header("Quest 3 Hand Tracking")]
    [SerializeField] private XRHandSubsystem handSubsystem;
    [SerializeField] private bool useHandTracking = true;
    
    public enum PlayerType
    {
        EnhancedSneaking, //Player with pressure plate
        EnhancedHacking // Player with midi hardware
    }

    public enum PlayerState
    {
        Walking, //Player is walking
        Sneaking //Player is sneaking
    }
    
    void Start()
    {
        // Get reference to NetworkRig
        networkRig = GetComponent<NetworkRig>();
        if (networkRig == null)
        {
            Debug.LogError("VRPlayer requires a NetworkRig component!");
        }
        
        // Set player type based on hardware detection
        DetectPlayerType();
    }
    
    void DetectPlayerType()
    {
        bool hasPressurePlate = DetectPressurePlate();
        playerType = hasPressurePlate ? PlayerType.EnhancedSneaking : PlayerType.EnhancedHacking;
        
        if (Object.HasInputAuthority)
        {
            NetworkedPlayerType = playerType;
        }
        
        // Update visual indicator
        if (hardwareIndicator != null)
        {
            hardwareIndicator.SetActive(playerType == PlayerType.EnhancedHacking);
        }
    }
    
    bool DetectPressurePlate() 
    {
        //TODO: Implement detecting of hardware
        return true;
    }
    
    public override void FixedUpdateNetwork()
    {
        //update if we have input authority
        if (!Object.HasInputAuthority) return;
        
        // Handle sneaking input for enhanced sneaking players
        if (playerType == PlayerType.EnhancedSneaking)
        {
            HandleSneakingInput();
        }

        UpdatePlayerState();
        
        // Quest 3 hand tracking status can be tracked here
        // for gameplay logic (separate from the visual hand representation)
        UpdateHandTrackingStatus();
    }
    
    void UpdateHandTrackingStatus()
    {
        // Visual hand representation is handled by NetworkRig/NetworkHand
        if (handSubsystem != null && handSubsystem.running)
        {
            bool leftHandTracked = IsHandTracked(Handedness.Left);
            bool rightHandTracked = IsHandTracked(Handedness.Right);
            
            // Use this info for gameplay decisions
        }
    }
    
    bool IsHandTracked(Handedness handedness)
    {
        if (handSubsystem == null) return false;
        return handSubsystem.running;
    }
    
    void HandleSneakingInput()
    {
        float sneakValue = GetPressurePlateInput();
        
        // Update networked sneak value
        NetworkedSneakValue = sneakValue;

        if(sneakValue < sneakThreshold)
        {
            playerState = PlayerState.Sneaking;
        }
        else
        {
            playerState = PlayerState.Walking;
        }
        
        //Debug.Log($"Sneak Value: {sneakValue}, State: {playerState}");
    }

    private void UpdatePlayerState()
    {
        NetworkedPlayerState = playerState;
    }

    float GetPressurePlateInput()
    {
        //To simulate Input of pressure plate
#if UNITY_EDITOR
        if(Keyboard.current != null)
        {
            if(Keyboard.current.digit1Key.wasPressedThisFrame)
            {
                Debug.Log("Key 1 pressed - Sneaking");
                return 0.0f;
            }
            if(Keyboard.current.digit2Key.wasPressedThisFrame)
            {
                Debug.Log("Key 2 pressed - Walking");
                return 1.0f;
            }
        }
        
        // Return the current networked value if no input this frame
        return NetworkedSneakValue;
#else
        //TODO implement getting the value of the pressure plate 
        return 1.0f;   
#endif
    }
    
    public override void Render()
    {
        // Update hardware indicator for all clients
        if (hardwareIndicator != null)
        {
            hardwareIndicator.SetActive(NetworkedPlayerType == PlayerType.EnhancedHacking);
        }
        
        // Apply sneaking effects
        ApplySneakingEffects();
        
        // Debug display for all clients
        if (NetworkedPlayerType == PlayerType.EnhancedSneaking)
        {
            //Debug.Log($"Player {Object.InputAuthority}: SneakValue={NetworkedSneakValue:F2}, State={NetworkedPlayerState}");
        }
    }
    
    void ApplySneakingEffects()
    {
       
    }

    // Helper methods to access NetworkRig data if needed for gameplay
    public Vector3 GetHeadPosition() => networkRig.headset.transform.position;
    public Vector3 GetLeftHandPosition() => networkRig.leftHand.transform.position;
    public Vector3 GetRightHandPosition() => networkRig.rightHand.transform.position;
}