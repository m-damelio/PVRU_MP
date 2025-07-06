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

    [Header("Movement")]
    public CharacterController characterController;
    [SerializeField] private float moveSpeed = 3f; 
    // Reference to the NetworkRig which handles visuals for hands/body, etc.
    private NetworkRig networkRig;
    private Vector3 lastHeadsetPosition;
    private bool isFirstFrame = true;
    
    // Networked properties
    [Networked] public PlayerType NetworkedPlayerType { get; set; }
    [Networked] public PlayerState NetworkedPlayerState { get; set; }
    [Networked] public float NetworkedSneakValue { get; set; }
    
    // Quest 3 specific
    [Header("Quest 3 Hand Tracking")]
    [SerializeField] private XRHandSubsystem handSubsystem;
    [SerializeField] private bool useHandTracking = true;

    [Header("For Testing Purposes")]
    private DoorNetworkedController doorToOpen;
    private bool hasSearchedForDoor = false;
    
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

    void Update()
    {
        if (Object.HasInputAuthority && characterController != null)
        {
            Debug.Log($"CharacterController - IsGrounded: {characterController.isGrounded}, " +
                    $"Position: {transform.position}, " +
                    $"Velocity: {characterController.velocity}");
        }
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
    
    private DoorNetworkedController GetDoorToOpen()
    {
        if (!hasSearchedForDoor && Object.HasInputAuthority)
        {
            hasSearchedForDoor = true;
            doorToOpen = FindObjectOfType<DoorNetworkedController>();
            
            if (doorToOpen != null)
            {
                Debug.Log($"Found door for player {Object.InputAuthority}");
            }
            else
            {
                Debug.LogWarning($"Could not find door for player {Object.InputAuthority}");
            }
        }
        
        return doorToOpen;
    }
    
    public override void FixedUpdateNetwork()
    {
        //update if we have input authority
        if (!Object.HasInputAuthority) return;
        
        // Handle sneaking input for enhanced sneaking players
        if(GetInput<RigInput>(out var rigInput))
        {

            HandleMovement(rigInput);

            if(rigInput.customButtons.IsSet(RigInput.INTERACTIONBUTTON))
            {
                HandleInteraction();
            }

            if(playerType == PlayerType.EnhancedSneaking)
            {
                HandleSneakingInput(rigInput);
            }

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

    void HandleMovement(RigInput rigInput)
    {
        if(characterController == null) return;

        Vector3 targetHeadsetPosition = rigInput.headsetPosition;
    
        // Initialize on first frame
        if (isFirstFrame)
        {
            transform.position = new Vector3(targetHeadsetPosition.x, transform.position.y, targetHeadsetPosition.z);
            lastHeadsetPosition = targetHeadsetPosition;
            isFirstFrame = false;
            return;
        }

        // Calculate the movement delta from the last valid position
        Vector3 headsetMovementDelta = targetHeadsetPosition - lastHeadsetPosition;
        Vector3 movementToApply = new Vector3(headsetMovementDelta.x, 0, headsetMovementDelta.z);
        
        // Apply gravity
        if (!characterController.isGrounded)
        {
            movementToApply.y += Physics.gravity.y * Runner.DeltaTime;
        }

        //Save position before applying movement
        Vector3 psoitionBeforeMove = transform.position;
        //Move character
        characterController.Move(movementToApply);
        //Calculate movement after collisions
        Vector3 actualMovement = transform.position-psoitionBeforeMove;

        if(actualMovement.magnitude < movementToApply.magnitude * 0.9f)
        {
            //If movment sinificantly less than intended realign hardware rig to match charactercontrollers position
            var hardwareRig = networkRig.hardwareRig;
            if(hardwareRig != null)
            {
                Vector3 rigOffset = transform.position - new Vector3(targetHeadsetPosition.x, transform.position.y, targetHeadsetPosition.z);
                hardwareRig.transform.position += rigOffset;
            }
        }
        //Update last headset psoition for next frame
        lastHeadsetPosition = transform.position;
    }
    
    void HandleSneakingInput(RigInput rigInput)
    {
        float sneakValue = NetworkedSneakValue;
        
        if(rigInput.customButtons.IsSet(RigInput.SNEAKTESTBUTTON))
        {
            sneakValue = (sneakValue < sneakThreshold) ? 1.0f : 0.0f;
            Debug.Log($"Sneak Test Button - Setting sneak value to {sneakValue}");
        }
        else
        {
            sneakValue = GetPressurePlateInput();
        }

        NetworkedSneakValue = sneakValue;

        if(sneakValue < sneakThreshold)
        {
            playerState = PlayerState.Sneaking;
        }
        else
        {
            playerState = PlayerState.Walking;
        }
    }

    private void HandleInteraction()
    {
        Debug.Log("VRPlayer: HandleInteraction called");

        var door = GetDoorToOpen();
        if (door != null)
        {
            Debug.Log($"VRPlayer: Door found: {door.name}, calling RequestOpen()");
            door.RequestOpen();
        }
        else
        {
            Debug.LogWarning("No door found to interact with!");
        }
    }
    
    private void UpdatePlayerState()
    {
        NetworkedPlayerState = playerState;
    }

    float GetPressurePlateInput()
    {
        //TODO 
        return NetworkedSneakValue;
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

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Debug.Log($"CharacterController hit: {hit.collider.name} on layer {hit.collider.gameObject.layer}");
    }

    void OnTriggerEnter(Collider other)
    {
        Debug.Log($"Trigger entered: {other.name} on layer {other.gameObject.layer}");
    }

    void OnCollisionEnter(Collision collision)
    {
        Debug.Log($"Collision entered: {collision.collider.name} on layer {collision.collider.gameObject.layer}");
    }

    // Helper methods to access NetworkRig data if needed for gameplay
    public Vector3 GetHeadPosition() => networkRig.headset.transform.position;
    public Vector3 GetLeftHandPosition() => networkRig.leftHand.transform.position;
    public Vector3 GetRightHandPosition() => networkRig.rightHand.transform.position;
}