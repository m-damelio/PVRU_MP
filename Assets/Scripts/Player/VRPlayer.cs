using Fusion;
using Fusion.XR.Host.Rig;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Networking;
using UnityEngine.UIElements;
using UnityEngine.XR;
using UnityEngine.XR.Hands;
using static RotateMirror;

public class VRPlayer : NetworkBehaviour
{
    //--------------SNEAKING------------------------


    [Header("Network Settings")]
    [SerializeField] private string deviceIP = "192.168.178.51";
    [SerializeField] private float checkInterval = 1.0f;
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float retryDelay = 1.0f;


    public bool isInSneakZoneStatus = true; // Enable sending zone status to ESP32


    public bool isSneaking = false;
    public float sneakValue = 1.0f;
    private int consecutiveFailures = 0;
    private float lastSuccessfulRequest = 0f;
    public bool enableLogs = true; // Add this missing variable

    //--------------------------------------------


    // Hardware input code
    public int keypressed = 0; // Counter for pressed keys
    public int functionCode = 0;   
    
    // Binary array: First 3 bits for QWER (001-100), Next 3 bits for numbers 1-4 (001-100)
    // Format: QWER_NUM (6 bits total)
    // Q=001, W=010, E=011, R=100
    // 1=001, 2=010, 3=011, 4=100
    public List<int> functionCodeList = new List<int> {     
        001001, // Q+1 (001001) = 9
        001010, // Q+2 (001010) = 10
        001011, // Q+3 (001011) = 11
        001100, // Q+4 (001100) = 12
        010001, // W+1 (010001) = 17
        010010, // W+2 (010010) = 18
        010011, // W+3 (010011) = 19
        010100, // W+4 (010100) = 20
        011001, // E+1 (011001) = 25
        011010, // E+2 (011010) = 26
        011011, // E+3 (011011) = 27
        011100, // E+4 (011100) = 28
        100001, // R+1 (100001) = 33
        100010, // R+2 (100010) = 34
        100011, // R+3 (100011) = 35
        100100, // R+4 (100100) = 36
        200000,   // A standalone 
        300000,   // S standalone  
        400000,   // D standalone 
        500000    // F standalone 
    };


    [Header("Hardware Detection")]
    [SerializeField] private GameObject hardwareIndicator; // Visual indicator for extra hardware
    [SerializeField] private bool testHardware = false;

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

    [Header("Color Selection")]
    [SerializeField] private colorControl activeColorObject;

    [Header("Movement")]
    public CharacterController characterController;
    //[SerializeField] private float moveSpeed = 3f; 
    // Reference to the NetworkRig which handles visuals for hands/body, etc.
    private Vector3 lastHeadsetPosition;
    private bool isFirstFrame = true;

    [Header("Key Card Interaction")]
    [SerializeField] private float pickupRange = 2f;
    [SerializeField] private LayerMask keyCardLayer = -1; //All layers by default
    [SerializeField] private LayerMask interactionLayer = -1;
    [SerializeField] private NetworkedKeyCard heldKeyCard;

    [Header("Networked properties")]
    [Networked] public PlayerType NetworkedPlayerType { get; set; }
    [Networked] public PlayerState NetworkedPlayerState { get; set; }
    [Networked] public float NetworkedSneakValue { get; set; }
    [SerializeField] private NetworkRig networkRig;

    // Quest 3 specific
    [Header("Quest 3 Hand Tracking")]
    [SerializeField] private XRHandSubsystem handSubsystem;
    //[SerializeField] private bool useHandTracking = true;

    
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
        if (!Object.HasInputAuthority) return;
        DetectPlayerType();
        StartCoroutine(AfterSpawn());

    }

    private IEnumerator AfterSpawn()
    {
        yield return new WaitForSeconds(1);
        networkRig = GetComponent<NetworkRig>();
        if (networkRig == null && networkRig.hardwareRig == null)
        {
            Debug.LogError("VRPlayer requires a NetworkRig component!");
        }
        else
        {
            originalCullingMask = networkRig.hardwareRig.playerCamera.cullingMask;
        }
        UpdateCameraLayers();
    }




    // Helper class to return result from coroutine
    private class RequestResult
    {
        public bool success = false;
        public bool sneakingState = false;
        public string response = "";
        public float requestTime = 0f;
    }

    void Start()
    {
        //Get reference of all mirrors in the scene
        mirrors = new List<RotateMirror>(FindObjectsOfType<RotateMirror>());
        isSelected = false;


        // Start the coroutine to check sneak state
        if(testHardware) StartCoroutine(CheckDeviceLoop());
    }



    //get sneak state
    IEnumerator CheckDeviceLoop()
    {
        int checkCount = 0;
        
        while (true)
        {
            yield return new WaitForSeconds(checkInterval);
            checkCount++;

            yield return StartCoroutine(GetDeviceSneakStateWithRetry());
            
            
            float oldSneakValue = sneakValue;
            sneakValue = isSneaking ? 0.0f : 1.0f;
            
            if (enableLogs && oldSneakValue != sneakValue)
            {
                Debug.Log($"SneakCube: Sneak value updated: {oldSneakValue} -> {sneakValue} [Check #{checkCount}]");
            }
            
            if (consecutiveFailures > 2)
            {
                Debug.Log($"SneakCube: Connection unstable, increasing check interval");
                yield return new WaitForSeconds(1.0f);
            }
        }
    }
    
    IEnumerator GetDeviceSneakStateWithRetry()
    {
        RequestResult result = null;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            if (attempt > 0)
            {
                yield return new WaitForSeconds(retryDelay);
            }
            
            // Create new result object for each attempt
            result = new RequestResult();
            yield return StartCoroutine(GetDeviceSneakState(attempt + 1, result));
            
            if (result.success)
            {
                isSneaking = result.sneakingState;
                consecutiveFailures = 0;
                lastSuccessfulRequest = Time.time;
                break; // Success, exit retry loop
            }
        }
        
        if (result != null && !result.success)
        {
            consecutiveFailures++;
            if (enableLogs)
                Debug.LogWarning($"SneakCube: Failed after {maxRetries} attempts. Consecutive failures: {consecutiveFailures}");
        }
    }

    IEnumerator GetDeviceSneakState(int attemptNumber, RequestResult result)
    {
        string url = $"http://{deviceIP}/sneakstatus";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.timeout = 3;
            request.SetRequestHeader("Connection", "close");
            request.SetRequestHeader("Cache-Control", "no-cache");

            if (enableLogs && attemptNumber == 1)
                Debug.Log($"SneakCube: Requesting {url} (Attempt {attemptNumber})");

            float requestStart = Time.time;
            yield return request.SendWebRequest();
            result.requestTime = Time.time - requestStart;

            if (request.result == UnityWebRequest.Result.Success)
            {
                result.response = request.downloadHandler.text.Trim();
                bool wasSneaking = isSneaking;
                result.sneakingState = result.response == "1" || result.response.ToLower() == "true";
                result.success = true;

                if (enableLogs && (wasSneaking != result.sneakingState || attemptNumber > 1))
                {
                    Debug.Log($"SneakCube: SUCCESS (Attempt {attemptNumber}) - Response '{result.response}' -> Sneaking: {result.sneakingState} (Time: {result.requestTime:F2}s)");
                }
            }
            else
            {
                result.success = false;
                if (enableLogs)
                {
                    Debug.LogWarning($"SneakCube: FAILED (Attempt {attemptNumber}) - {request.error} (Time: {result.requestTime:F2}s)");
                    if (request.responseCode > 0)
                        Debug.LogWarning($"SneakCube: Response Code: {request.responseCode}");
                }
            }
        }

        using (UnityWebRequest requestZone = UnityWebRequest.Get($"http://{deviceIP}/setregion?s={(isInSneakZoneStatus ? "1" : "0")}"))
        {
            yield return requestZone.SendWebRequest();

            if (requestZone.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"SneakCube: Failed to set region status: {requestZone.error}");
            }
        }
    }



    private void Update()
    {
       
        if (testHardware) StartCoroutine(GetDeviceSneakStateWithRetry());
    }


    public void SetActiveMirror(RotateMirror mirror)
    {
        activeMirror = mirror;
    }

    public void DeselectActiveMirror(RotateMirror mirror)
    {
        activeMirror = null;
    }

    public void SetActiveColorObject(colorControl colorObj)
    {
        activeColorObject = colorObj;
    }

    public void DeselectActiveColorObject(colorControl colorObj)
    {
        activeColorObject = null;
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

        if (heldKeyCard != null && heldKeyCard.Holder != Object.InputAuthority)
        {
            Debug.Log("VRPlayer: KEycard was dropped/ejected, clearing reference");
            heldKeyCard = null;
        }
        // Handle sneaking input for enhanced sneaking players
        if (GetInput<RigInput>(out var rigInput))
        {

            HandleMovement(rigInput);
            HandleMirrorInput(rigInput);

            if (rigInput.customButtons.IsSet(RigInput.INTERACTIONBUTTON))
            {
                HandleInteraction();
            }

            if (playerType == PlayerType.EnhancedSneaking)
            {
                HandleSneakingInput(rigInput);
            }

            if (playerType == PlayerType.EnhancedHacking)
            {
                HandleHackingInput(rigInput);
            }


            UpdatePlayerState();
            UpdateHeldKeyCardReference();

            // Quest 3 hand tracking status can be tracked here
            // for gameplay logic (separate from the visual hand representation)
            UpdateHandTrackingStatus();
        }

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
        if (characterController == null) return;

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
        Vector3 actualMovement = transform.position - psoitionBeforeMove;

        if (actualMovement.magnitude < movementToApply.magnitude * 0.9f)
        {
            //If movment sinificantly less than intended realign hardware rig to match charactercontrollers position
            if (networkRig != null && networkRig.hardwareRig != null)
            {
                var hardwareRig = networkRig.hardwareRig;
                Vector3 rigOffset = transform.position - new Vector3(targetHeadsetPosition.x, transform.position.y, targetHeadsetPosition.z);
                hardwareRig.transform.position += rigOffset;
            }
            else
            {
                if (Object.HasInputAuthority) Debug.LogWarning($"HardwareRig not available for local player {Object.InputAuthority}. This is normal in the first few frames after spawning.");
            }
        }
        //Update last headset psoition for next frame
        lastHeadsetPosition = transform.position;
    }

    void HandleHackingInput(RigInput rigInput)
    {
        if (!testHardware) return;
        KeyCode pressedKey1 = rigInput.keyPressed1;
        KeyCode pressedKey2 = rigInput.keyPressed2;
        KeyCode pressedKey3 = rigInput.keyPressed3;
        KeyCode pressedKey4 = rigInput.keyPressed4;

        // Check if any key is pressed
        if (pressedKey1 != KeyCode.None || pressedKey2 != KeyCode.None || pressedKey3 != KeyCode.None || pressedKey4 != KeyCode.None)
        {
            // Add values for each pressed key
            functionCode += GetKeyValue(pressedKey1);
            functionCode += GetKeyValue(pressedKey2);
            functionCode += GetKeyValue(pressedKey3);
            functionCode += GetKeyValue(pressedKey4);

            // Execute function based on the combined function code
            if (keypressed <= 1) 
            {
                if (functionCodeList.Contains(functionCode))
                {
                    Debug.Log($"VRPlayer: Executing function for keys {pressedKey1}, {pressedKey2}, {pressedKey3}, {pressedKey4} with code {functionCode}");
                    HandleSendFunctionCode(functionCode);
                    ExecuteHardwareFunction(functionCode);
                    keypressed = 0; // Reset key pressed count after execution
                }
                else
                {
                    Debug.LogWarning($"VRPlayer: Function code {functionCode} not found for keys {pressedKey1}, {pressedKey2}, {pressedKey3}, {pressedKey4}");
                    keypressed++;
                }
            }
            else
            {
                Debug.LogWarning($"VRPlayer: Invalid function code {functionCode} from keys {pressedKey1}, {pressedKey2}, {pressedKey3}, {pressedKey4}");
                keypressed = 0; // Reset key pressed count
            }
        }
    }

    // send fuction code to a network object with the code
    void HandleSendFunctionCode(int code)
    {
        // This method can be used to send the function code to a networked object
        // For example, you could use RPCs or a custom network message
        Debug.Log($"Sending function code: {code}");
        // Implement your network sending logic here
    }


    void HandleSneakingInput(RigInput rigInput)
    {
        float sneakValue = NetworkedSneakValue;

        if (rigInput.customButtons.IsSet(RigInput.SNEAKTESTBUTTON))
        {
            sneakValue = (sneakValue < sneakThreshold) ? 1.0f : 0.0f;
            Debug.Log($"Sneak Test Button - Setting sneak value to {sneakValue}");
        }
        else
        {
            sneakValue = GetPressurePlateInput();
        }

        NetworkedSneakValue = sneakValue;

        if (sneakValue < sneakThreshold)
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

        if (rigInput.yDelta != 0f && activeMirror != null)
        {
            Debug.Log(rigInput.yDelta);
            activeMirror.RotateY(rigInput.yDelta);
        }

        if (rigInput.zDelta != 0f && activeMirror != null) {
            Debug.Log(rigInput.yDelta);
            activeMirror.RotateZ(rigInput.zDelta);
        }

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
        if (doorOpener != null)
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

        foreach (var collider in colliders)
        {
            var doorOpener = collider.GetComponent<DoorOpener>();
            if (doorOpener != null)
            {
                float distance = Vector3.Distance(playerPosition, collider.transform.position);
                if (distance < closestDistance)
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
        if (heldKeyCard != null && Object.HasInputAuthority)
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


    // Get the value for each keycode to build composite function code using binary system
    private int GetKeyValue(KeyCode key)
    {
        switch (key)
        {
            // Numbers: 1=001, 2=010, 3=011, 4=100 (3-bit binary)
            case KeyCode.Alpha1:
                return 0b001; // 1
            case KeyCode.Alpha2:
                return 0b010; // 2
            case KeyCode.Alpha3:
                return 0b011; // 3
            case KeyCode.Alpha4:
                return 0b100; // 4
            
            // QWER: Q=001, W=010, E=011, R=100 (shifted left 3 bits)
            case KeyCode.Q:
                return 0b001000; // 001 << 3 = 8
            case KeyCode.W:
                return 0b010000; // 010 << 3 = 16
            case KeyCode.E:
                return 0b011000; // 011 << 3 = 24
            case KeyCode.R:
                return 0b100000; // 100 << 3 = 32
            
            // ASDF: Keep as large numbers for standalone actions
            case KeyCode.A:
                return 200000;
            case KeyCode.S:
                return 300000;
            case KeyCode.D:
                return 400000;
            case KeyCode.F:
                return 500000;
            default:
                return 0; // No value for unmapped keys
        }
    }

    // Execute hardware function based on mapped function code
    private void ExecuteHardwareFunction(int functionCode)
    {
        Debug.Log($"Executing hardware function with code: {functionCode}");

        switch (functionCode)
        {
            // Q combinations (001xxx)
            case 001001: // Q+1 
                Debug.Log("Hardware function Q+1");
                break;
            case 001010: // Q+2 
                Debug.Log("Hardware function Q+2");
                break;
            case 001011: // Q+3 
                Debug.Log("Hardware function Q+3");
                break;
            case 001100: // Q+4 
                Debug.Log("Hardware function Q+4");
                break;
                
            // W combinations (010xxx)
            case 010001: // W+1 
                Debug.Log("Hardware function W+1");
                break;
            case 010010: // W+2 
                Debug.Log("Hardware function W+2");
                break;
            case 0b010011: // W+3 
                Debug.Log("Hardware function W+3");
                break;
            case 0b010100: // W+4 
                Debug.Log("Hardware function W+4");
                break;
                
            // E combinations (011xxx)
            case 0b011001: // E+1 
                Debug.Log("Hardware function E+1");
                break;
            case 0b011010: // E+2 
                Debug.Log("Hardware function E+2");
                break;
            case 0b011011: // E+3 
                Debug.Log("Hardware function E+3");
                break;
            case 0b011100: // E+4 
                Debug.Log("Hardware function E+4");
                break;
                
            // R combinations (100xxx)
            case 0b100001: // R+1 
                Debug.Log("Hardware function R+1");
                break;
            case 0b100010: // R+2 
                Debug.Log("Hardware function R+2");
                break;
            case 0b100011: // R+3 
                Debug.Log("Hardware function R+3");
                break;
            case 0b100100: // R+4 
                Debug.Log("Hardware function R+4");
                break;
                
            // ASDF standalone actions
            case 200000: // A
                Debug.Log("Hardware function A - Standalone action");
                break;
            case 300000: // S
                Debug.Log("Hardware function S - Standalone action");
                break;
            case 400000: // D
                Debug.Log("Hardware function D - Standalone action");
                break;
            case 500000: // F
                Debug.Log("Hardware function F - Standalone action");
                break;

            default:
                Debug.Log($"Hardware function with code {functionCode} - Add specific implementation");
                break;
        }

        functionCode = 0; // Reset function code after execution
    }
}




