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
    [SerializeField] private string deviceIP = "192.168.137.42";
    [SerializeField] private float checkInterval = 1.0f;
    [SerializeField] private int maxRetries = 3;
    [SerializeField] private float retryDelay = 1.0f;
    [SerializeField] private bool currentlyGettingSneakState = false;
    private int consecutiveFailures = 0;
    private float lastSuccessfulRequest = 0f;
    public bool enableLogs = true; // Add this missing variable

    [Header("Sneaking Settings")]
    [SerializeField] private float sneakThreshold = 0.8f;

    [Header("Player Settings")]
    [SerializeField] private PlayerType playerType;
    [SerializeField] private PlayerState playerState = PlayerState.Walking;
    [SerializeField] private LayerMask hackerOnlyLayers;
    [SerializeField] private LayerMask sneakerOnlyLayers;
    private LayerMask originalCullingMask;
    [SerializeField] private bool isSneaker;

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
    [Networked] public bool IsInSneakZoneStatus { get; set; } // Enable sending zone status to ESP32
    [SerializeField] private NetworkRig networkRig;
    private ChangeDetector _chanegDetector;

    [Header("Sneaking local")]
    public bool isSneaking = false;
    public float sneakValue = 1.0f;

    [Header("Hack Device")]
    [SerializeField] private HackDevice hackDevice;

    
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
        _chanegDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (!Object.HasInputAuthority) return;
        SetPlayerType();
        UpdateHardwareVisual();
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

        //Get reference of hackdevice
        hackDevice = FindObjectOfType<HackDevice>();

        isSelected = false;


        // Start the coroutine to check sneak state
        if(isSneaker) StartCoroutine(CheckDeviceLoop());
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
        currentlyGettingSneakState = true;
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
                currentlyGettingSneakState = false;
                break; // Success, exit retry loop
            }
        }

        if (result != null && !result.success)
        {
            consecutiveFailures++;
            if (enableLogs)
                Debug.LogWarning($"SneakCube: Failed after {maxRetries} attempts. Consecutive failures: {consecutiveFailures}");
        }
        currentlyGettingSneakState = false;
        
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
    }



    private void Update()
    {
       
        if (isSneaker && !currentlyGettingSneakState) StartCoroutine(GetDeviceSneakStateWithRetry());
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

    void SetPlayerType()
    {
        playerType = isSneaker ? PlayerType.EnhancedSneaking : PlayerType.EnhancedHacking;
        if (Object.HasInputAuthority)
        {
            NetworkedPlayerType = playerType;
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
                HandleMirrorInput(rigInput);
                HandleHackingInput(rigInput);
            }


            UpdatePlayerState();
            UpdateHeldKeyCardReference();
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
        KeyCode pressedKey1 = rigInput.keyPressed1;
        KeyCode pressedKey2 = rigInput.keyPressed2;
        KeyCode pressedKey3 = rigInput.keyPressed3;
        KeyCode pressedKey4 = rigInput.keyPressed4;
        
        if (hackDevice == null) return;

        if (pressedKey1 == KeyCode.W) hackDevice.SelectSlot(0);
        if (pressedKey1 == KeyCode.A) hackDevice.SelectSlot(1);
        if (pressedKey1 == KeyCode.S) hackDevice.SelectSlot(2);
        if (pressedKey1 == KeyCode.D) hackDevice.SelectSlot(3);

        if (pressedKey1 == KeyCode.L) hackDevice.AdjustActiveSlot(+1);
        if (pressedKey1 == KeyCode.K) hackDevice.AdjustActiveSlot(-1);
    
    }

    void HandleSneakingInput(RigInput rigInput)
    {

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
            Debug.Log("Player hat rotation gefunden " + rigInput.yDelta);
            activeMirror.RpcRotateY(rigInput.yDelta);
            //activeMirror.NetworkedYRotation = rigInput.yDelta;
        }

        if (rigInput.zDelta != 0f && activeMirror != null) {
            Debug.Log("Player hat rotation gefunden " + rigInput.zDelta);
            activeMirror.RpcRotateZ(rigInput.zDelta);
            //activeMirror.NetworkedZRotation = rigInput.zSDelta;
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
    private bool _hasInitializedRender = false;

    public override void Render()
    {
        bool shouldUpdateHardwareVisual = false;

        foreach (var changedProperty in _chanegDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(IsInSneakZoneStatus))
            {
                Debug.Log("Sneaking status changed");
                if (NetworkedPlayerType == PlayerType.EnhancedSneaking)
                {
                    StartCoroutine(SendRegionStatusToDevice(IsInSneakZoneStatus));
                }
            }

            if (changedProperty == nameof(NetworkedPlayerType))
            {
                shouldUpdateHardwareVisual = true;
            }
        }

        if (!_hasInitializedRender)
        {
            shouldUpdateHardwareVisual = true;
            _hasInitializedRender = true;
        }

        if (shouldUpdateHardwareVisual)
        {
            UpdateHardwareVisual();
        }
        // Apply sneaking effects
        UpdateSneakingEffects();
    }


    private void UpdateHardwareVisual()
    {
        if (networkRig?.hardwareRig?.hardwareVisual != null)
        {
            bool shouldBeVisible = NetworkedPlayerType != PlayerType.EnhancedSneaking;
            networkRig.hardwareRig.hardwareVisual.SetActive(shouldBeVisible);
        }
    }

    void UpdateSneakingEffects()
    {

    }

    IEnumerator SendRegionStatusToDevice(bool status)
    {
        // Only the player with input authority should talk to their local hardware
        if (!Object.HasInputAuthority) yield break;

        string url = $"http://{deviceIP}/setregion?s={(status ? "1" : "0")}";
        using (UnityWebRequest requestZone = UnityWebRequest.Get(url))
        {
            Debug.Log($"Sending region status to device: {url}");
            yield return requestZone.SendWebRequest();

            if (requestZone.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"VRPlayer: Failed to set region status: {requestZone.error}");
            }
        }
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




