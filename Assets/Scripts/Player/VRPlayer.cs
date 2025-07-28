using Fusion;
using Fusion.XR.Host.Rig;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using static RotateMirror;

public class VRPlayer : NetworkBehaviour
{
    [Header("Hardware Detection")]
    [SerializeField] private GameObject hardwareIndicator; // Visual indicator for extra hardware
    
    [Header("Sneaking Settings")]
    [SerializeField] private float sneakThreshold = 0.8f;
    
    [Header("Player Settings")]
    [SerializeField] private PlayerType playerType;
    [SerializeField] private PlayerState playerState = PlayerState.Walking;
    [SerializeField] private LayerMask hackerOnlyLayers;
    [SerializeField] private LayerMask sneakerOnlyLayers;
    private LayerMask originalCullingMask;

    [Header("Mirror Rotation")]
    [SerializeField] private RotateMirror activeMirror;
    [SerializeField] private List<RotateMirror> mirrors;
    private bool isSelected;

    [Header("Movement")]
    public CharacterController characterController;
    //[SerializeField] private float moveSpeed = 3f; 
    // Reference to the NetworkRig which handles visuals for hands/body, etc.
    private NetworkRig networkRig;
    private Vector3 lastHeadsetPosition;
    private bool isFirstFrame = true;

    [Header("Key Card Interaction")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private LayerMask keyCardLayer = -1; //All layers by default
    [SerializeField] private LayerMask interactionLayer = -1;
    
    [Header("Networked properties")]
    [Networked] public PlayerType NetworkedPlayerType { get; set; }
    [Networked] public PlayerState NetworkedPlayerState { get; set; }
    [Networked] public float NetworkedSneakValue { get; set; }
    
    // Quest 3 specific
    [Header("Quest 3 Hand Tracking")]
    [SerializeField] private XRHandSubsystem handSubsystem;
    //[SerializeField] private bool useHandTracking = true;

    [Header("For Testing Purposes")]
    private DoorNetworkedController doorToOpen;
    //private bool hasSearchedForDoor = false;
    [SerializeField] private NetworkedKeyCard heldKeyCard;
    
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
    
    public override void Spawned()
    {
        // Get reference to NetworkRig
        networkRig = GetComponent<NetworkRig>();
        if (networkRig == null)
        {
            Debug.LogError("VRPlayer requires a NetworkRig component!");
        }
        else
        {
            originalCullingMask = networkRig.hardwareRig.playerCamera.cullingMask;
        }
        
        // Set player type based on hardware detection
        DetectPlayerType();
        UpdateCameraLayers();

    }

    void Start()
    {
        //Get reference of all mirrors in the scene
        mirrors = new List<RotateMirror>(FindObjectsOfType<RotateMirror>());
        isSelected = false;

    }


    private void Update()
    {
        //Lokal Input, welcher Mirror ausgewählt wurde zum drehen -- das drehen wird genetworked
        //Farblich hervorheben welchen wir drehen
        if (!Object.HasInputAuthority) return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (isSelected && activeMirror == mirrors[0])
            {
                DeselectActiveMirror();
                isSelected = false;
            }
            else
            {
                if (mirrors.Count > 0 && !isSelected)
                {
                    activeMirror = mirrors[0];
                    Debug.Log($"Spiegel 1 ausgewählt: {activeMirror.gameObject.name}");
                    SetActiveMirror(activeMirror);
                    //var mirrorNetObj = activeMirror.GetComponent<NetworkObject>();
                    isSelected = true;
                }
            }

        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (isSelected && activeMirror == mirrors[1])
            {
                DeselectActiveMirror();
                isSelected = false;
            }
            else
            {
                if (mirrors.Count > 1 && !isSelected)
                {
                    activeMirror = mirrors[1];
                    Debug.Log($"Spiegel 2 ausgewählt: {activeMirror.gameObject.name}");
                    SetActiveMirror(activeMirror);
                    isSelected = true;
                }
            }
        }

    }

    void SetActiveMirror(RotateMirror mirror)
    {
        if (activeMirror != null)
            activeMirror.SetHighlight(false);

        activeMirror = mirror;

        if (activeMirror != null)
            activeMirror.SetHighlight(true);
    }

    void DeselectActiveMirror()
    {
        if (activeMirror != null)
            activeMirror.SetHighlight(false);

        activeMirror = null;
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

    void UpdateCameraLayers()
    {
        //Updates the culling mask for the players when spawned once
        if (networkRig == null) return;
        LayerMask newCullingMask = originalCullingMask;

        switch (NetworkedPlayerType)
        {
            case PlayerType.EnhancedSneaking:
                newCullingMask = originalCullingMask | sneakerOnlyLayers;
                newCullingMask &= ~hackerOnlyLayers; //remove hacker only
                break;
            case PlayerType.EnhancedHacking:
                newCullingMask = originalCullingMask | hackerOnlyLayers;
                newCullingMask &= ~sneakerOnlyLayers; //remove sneaker only 
                break;
        }

        networkRig.hardwareRig.playerCamera.cullingMask = newCullingMask;

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
        
        if(heldKeyCard != null && heldKeyCard.Holder != Object.InputAuthority)
        {
            Debug.Log("VRPlayer: KEycard was dropped/ejected, clearing reference");
            heldKeyCard = null;
        }
        // Handle sneaking input for enhanced sneaking players
        if(GetInput<RigInput>(out var rigInput))
        {

            HandleMovement(rigInput);
            HandleMirrorInput(rigInput);

            if (rigInput.customButtons.IsSet(RigInput.INTERACTIONBUTTON))
            {
                HandleInteraction();
            }

            if(playerType == PlayerType.EnhancedSneaking)
            {
                HandleSneakingInput(rigInput);
            }

            KeyCode pressedKey1 = rigInput.keyPressed1;
            KeyCode pressedKey2 = rigInput.keyPressed2;
            KeyCode pressedKey3 = rigInput.keyPressed3;
            KeyCode pressedKey4 = rigInput.keyPressed4;

            //TODO: Put hardware interactions here, i.e. what they should/call
            if(pressedKey1 != KeyCode.None)
            {
                //Debug.Log($"Key {pressedKey1} arrived at VRPlayer script");
            }
            if(pressedKey2 != KeyCode.None)
            {
                //Debug.Log($"Key {pressedKey2} arrived at VRPlayer script");
            }
            if(pressedKey3 != KeyCode.None)
            {
                //Debug.Log($"Key {pressedKey3} arrived at VRPlayer script");
            }
            if(pressedKey4 != KeyCode.None)
            {
                //Debug.Log($"Key {pressedKey4} arrived at VRPlayer script");
            }

        }
        

        UpdatePlayerState();
        UpdateHeldKeyCardReference();
        
        // Quest 3 hand tracking status can be tracked here
        // for gameplay logic (separate from the visual hand representation)
        UpdateHandTrackingStatus();
    }

    private void UpdateHeldKeyCardReference()
    {
        var rightGrabber = networkRig.rightGrabber;
        if (rightGrabber != null)
        {
            var grabInfo = rightGrabber.GrabInfo;
            if (grabInfo.grabbedObjectId != NetworkBehaviourId.None && Runner.TryFindBehaviour(grabInfo.grabbedObjectId, out NetworkedKeyCard cardInRightHand))
            {
                heldKeyCard = cardInRightHand;
                return;
            }
        }

        var leftGrabber = networkRig.leftGrabber;
        if (leftGrabber != null)
        {
            var grabInfo = leftGrabber.GrabInfo;
            if (grabInfo.grabbedObjectId != NetworkBehaviourId.None && Runner.TryFindBehaviour(grabInfo.grabbedObjectId, out NetworkedKeyCard cardInLeftHand))
            {
                heldKeyCard = cardInLeftHand;
                return;
            }
        }
        
        heldKeyCard = null;
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
            if(networkRig != null && networkRig.hardwareRig != null)
            {
                var hardwareRig = networkRig.hardwareRig;
                Vector3 rigOffset = transform.position - new Vector3(targetHeadsetPosition.x, transform.position.y, targetHeadsetPosition.z);
                hardwareRig.transform.position += rigOffset;
            }
            else
            {
                if(Object.HasInputAuthority) Debug.LogWarning($"HardwareRig not available for local player {Object.InputAuthority}. This is normal in the first few frames after spawning.");
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

    void HandleMirrorInput(RigInput rigInput)
    {

        if (rigInput.yDelta != 0f)
            activeMirror.RotateY(rigInput.yDelta);

        if (rigInput.zDelta != 0f)
            activeMirror.RotateZ(rigInput.zDelta);

    }

    private void HandleInteraction()
    {
        //Handles interacting with a dooropener when keycard is held
        Debug.Log("VRPlayer: HandleInteraction called");

        if (heldKeyCard == null)
        {
            Debug.Log("VRPlayer: Not holding a keycard to interact with");
            return;
        }

        var doorOpener = FindNearbyDoorOpener();
        if(doorOpener != null)
        {
            Debug.Log($"VRPlayer: Inserting key card into door opener: {doorOpener.name}");
            heldKeyCard.InsertInto(doorOpener);
            return;
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

    private DoorOpener FindNearbyDoorOpener()
    {
        Vector3 playerPosition = transform.position;
        Collider[] colliders = Physics.OverlapSphere(playerPosition, pickupRange, interactionLayer);
        DoorOpener closestDoorOpener = null;
        float closestDistance = float.MaxValue;

        foreach(var collider in colliders)
        {
            var doorOpener = collider.GetComponent<DoorOpener>();
            if(doorOpener != null)
            {
                float distance = Vector3.Distance(playerPosition, collider.transform.position);
                if(distance < closestDistance)
                {
                    closestDistance = distance;
                    closestDoorOpener = doorOpener;
                }
            }
        } 
        return closestDoorOpener;
    }
    
    public override void Render()
    {
        // Update hardware indicator for all clients
        if (hardwareIndicator != null)
        {
            hardwareIndicator.SetActive(NetworkedPlayerType == PlayerType.EnhancedHacking);
        }

        //Show Key Card if held
        if(heldKeyCard != null && Object.HasInputAuthority)
        {
            //Debug.Log($"Holding key card: {heldKeyCard.KeyID}");
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
        //Debug.Log($"CharacterController hit: {hit.collider.name} on layer {hit.collider.gameObject.layer}");
    }

    void OnTriggerEnter(Collider other)
    {
        //Debug.Log($"Trigger entered: {other.name} on layer {other.gameObject.layer}");
    }

    void OnCollisionEnter(Collision collision)
    {
        //Debug.Log($"Collision entered: {collision.collider.name} on layer {collision.collider.gameObject.layer}");
    }

    // Helper methods to access NetworkRig data if needed for gameplay
    public Vector3 GetHeadPosition() => networkRig.headset.transform.position;
    public Vector3 GetLeftHandPosition() => networkRig.leftHand.transform.position;
    public Vector3 GetRightHandPosition() => networkRig.rightHand.transform.position;
}