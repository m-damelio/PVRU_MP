using Fusion.Sockets;
using Fusion.XR.Host.Grabbing;
using Fusion.XR.Host.SimpleHands;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Management;
using static RotateMirror;


namespace Fusion.XR.Host.Rig
{
    public enum RigPart
    {
        None,
        Headset,
        LeftController,
        RightController,
        Undefined
    }

    // Include all rig parameters in an network input structure
    public struct RigInput : INetworkInput
    {
        public Vector3 playAreaPosition;
        public Quaternion playAreaRotation;
        public Vector3 leftHandPosition;
        public Quaternion leftHandRotation;
        public Vector3 rightHandPosition;
        public Quaternion rightHandRotation;
        public Vector3 headsetPosition;
        public Quaternion headsetRotation;
        public HandCommand leftHandCommand;
        public HandCommand rightHandCommand;
        public GrabInfo leftGrabInfo;
        public GrabInfo rightGrabInfo;

        //input fields not from fusion
        public NetworkButtons customButtons;
        public Vector3 movementDirection;

        //Button constants
        public const byte INTERACTIONBUTTON = 1 << 0;
        public const byte SNEAKTESTBUTTON = 1 << 1;

        public KeyCode keyPressed1;
        public KeyCode keyPressed2;
        public KeyCode keyPressed3;
        public KeyCode keyPressed4;

        //mirror rotation
        public float yDelta;
        public float zDelta;
    }

    /**
     * 
     * Hardware rig gives access to the various rig parts: head, left hand, right hand, and the play area, represented by the hardware rig itself
     *  
     * Can be moved, either instantanesously, or with a camera fade
     * 
     **/

    public class HardwareRig : MonoBehaviour, INetworkRunnerCallbacks
    {
        public HardwareHand leftHand;
        public HardwareHand rightHand;
        public HardwareHeadset headset;
        public NetworkRunner runner;

        public enum RunnerExpectations
        {
            NoRunner, // For offline usages
            PresetRunner,
            DetectRunner // should not be used in multipeer scenario
        }
        public RunnerExpectations runnerExpectations = RunnerExpectations.DetectRunner;

        bool searchingForRunner = false;

        [Header("Input interpolation")]
        public bool useInputInterpolation = false;
        public float interpolationDelay = 0.008f;
        XRHeadsetInputDevice headsetInputDevice;
        XRControllerInputDevice leftHandInputDevice;
        XRControllerInputDevice rightHandInputDevice;


        public float InterpolationDelay => interpolationDelay;

        [Header("Custom Fields")]
        [SerializeField] private InputActionReference interactionAction;
        [SerializeField] private InputActionReference sneakTestAction;
        public Camera playerCamera;
        public bool enableKeyboardInput = true;
        private VRPlayer vrPlayer; //Will be searched dynamically, change if performance is suffering
        private bool hasSearchedForVRPlayer = false;
        private bool _interactionButton = false;
        private bool _sneakTestButton = false;

        private List<KeyCode> keyPressBuffer = new List<KeyCode>();

        [Header("Local Finger Tracking")]
        [SerializeField] private bool enableFingerTracking;
        [SerializeField] private XRHandSubsystem handSubsystem;
        [SerializeField] private GameObject leftHandModel;
        [SerializeField] private GameObject rightHandModel;
        [SerializeField] private Transform leftHandSkeletalRoot;
        [SerializeField] private Transform rightHandSkeletalRoot;
        [SerializeField] private LayerMask fingerInteractionLayers = -1;

        //local fnger tracking objects (not networked)
        private XRHand leftXRHand;
        private XRHand rightXRHand;

        //Interaction compomnents for fongerbased interaction
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor leftFingerInteractor;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor rightFingerInteractor;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor leftPokeInteractor;
        private UnityEngine.XR.Interaction.Toolkit.Interactors.XRPokeInteractor rightPokeInteractor;

        private bool leftHandTracked = false;
        private bool rightHandTracked = false;
        private Animator leftHandAnimator;
        private Animator rightHandAnimator;

        private Dictionary<XRHandJointID, Transform> leftJointCache;
        private Dictionary<XRHandJointID, Transform> rightJointCache;

        public async Task<NetworkRunner> FindRunner()
        {
            while (searchingForRunner) await Task.Delay(10);
            searchingForRunner = true;
            if (runner == null && runnerExpectations != RunnerExpectations.NoRunner)
            {
                if (runnerExpectations == RunnerExpectations.PresetRunner || NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
                {
                    Debug.LogWarning("Runner has to be set in the inspector to forward the input");
                }
                else
                {
                    // Try to detect the runner
                    runner = FindObjectOfType<NetworkRunner>(true);
                    var searchStart = Time.time;
                    while (searchingForRunner && runner == null)
                    {
                        if (NetworkRunner.Instances.Count > 0)
                        {
                            runner = NetworkRunner.Instances[0];
                        }
                        if (runner == null)
                        {
                            await System.Threading.Tasks.Task.Delay(10);
                        }
                    }
                }
            }
            searchingForRunner = false;
            return runner;
        }

        protected virtual void Awake()
        {
            if (leftHand) leftHandInputDevice = leftHand.GetComponentInChildren<XRControllerInputDevice>();
            if (rightHand) rightHandInputDevice = rightHand.GetComponentInChildren<XRControllerInputDevice>();
            if (headset) headsetInputDevice = headset.GetComponentInChildren<XRHeadsetInputDevice>();

            if (leftHandInputDevice == null || rightHandInputDevice == null || headsetInputDevice == null)
            {
                useInputInterpolation = false;
            }

            if (interactionAction != null) interactionAction.action.Enable();
            if (sneakTestAction != null) sneakTestAction.action.Enable();

            if (leftHandModel != null) leftHandAnimator = leftHandModel.GetComponentInChildren<Animator>();
            if (rightHandModel != null) rightHandAnimator = rightHandModel.GetComponentInChildren<Animator>();
            if (enableFingerTracking)
            {
                InitializeHandTracking();
            }
        }

        protected virtual async void Start()
        {
            await FindRunner();
            if (runner)
            {
                runner.AddCallbacks(this);
            }
        }

        private void Update()
        {
            if (enableFingerTracking)
            {
                UpdateFingerTrackingVisibility();
            }

            if(enableKeyboardInput)
            {
                UpdateKeyboardInput();
            }
        }

        //Only tracks one keyboard key per frame.
        private void UpdateKeyboardInput()
        {
            foreach (KeyCode key in System.Enum.GetValues(typeof(KeyCode)))
            {
                if(Input.GetKeyDown(key))
                {
                    keyPressBuffer.Add(key);
                    //Debug.Log($"Key pressed: {key}");

                    if(keyPressBuffer.Count > 4)
                    {
                        keyPressBuffer.RemoveAt(0);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            if (searchingForRunner) Debug.LogError("Cancel searching for runner in HardwareRig");
            searchingForRunner = false;
            if (runner) runner.RemoveCallbacks(this);
            if (interactionAction != null) interactionAction.action.Disable();
            if (sneakTestAction != null) sneakTestAction.action.Disable();
            if (handSubsystem != null)
            {
                handSubsystem.trackingAcquired -= OnHandTrackingAcquired;
                handSubsystem.trackingLost -= OnHandTrackingLost;
                handSubsystem.updatedHands -= OnHandsUpdated;
            }
        }

        private VRPlayer GetVRPlayer()
        {
            if(!hasSearchedForVRPlayer && runner != null && runner.LocalPlayer != null)
            {
                hasSearchedForVRPlayer = true;

                var networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
                foreach (var netObj in networkObjects)
                {
                    if(netObj.HasInputAuthority && netObj.InputAuthority == runner.LocalPlayer)
                    {
                        vrPlayer = netObj.GetComponent<VRPlayer>();
                        if(vrPlayer != null)
                        {
                            Debug.Log($"Found VRPlayer for local player: {runner.LocalPlayer}");
                            break;
                        }
                    }
                }

                if(vrPlayer == null)
                {
                    Debug.LogWarning($"Could not find VRPlayer for local player: {runner.LocalPlayer}");
                }
            }

            return vrPlayer;
        }


        #region Locomotion
        // Update the hardware rig rotation. This will trigger a Riginput network update
        public virtual void Rotate(float angle)
        {
            transform.RotateAround(headset.transform.position, transform.up, angle);
        }

        // Update the hardware rig position. This will trigger a Riginput network update
        public virtual void Teleport(Vector3 position)
        {
            Vector3 headsetOffet = headset.transform.position - transform.position;
            headsetOffet.y = 0;
            transform.position = position - headsetOffet;
        }

        // Teleport the rig with a fader
        public virtual IEnumerator FadedTeleport(Vector3 position)
        {
            if (headset.fader) yield return headset.fader.FadeIn();
            Teleport(position);
            if (headset.fader) yield return headset.fader.WaitBlinkDuration();
            if (headset.fader) yield return headset.fader.FadeOut();
        }

        // Rotate the rig with a fader
        public virtual IEnumerator FadedRotate(float angle)
        {
            if (headset.fader) yield return headset.fader.FadeIn();
            Rotate(angle);
            if (headset.fader) yield return headset.fader.WaitBlinkDuration();
            if (headset.fader) yield return headset.fader.FadeOut();
        }
        #endregion

        #region INetworkRunnerCallbacks

        // Prepare the input, that will be read by NetworkRig in the FixedUpdateNetwork
        public void OnInput(NetworkRunner runner, NetworkInput input) {
            RigInput rigInput = new RigInput();
            rigInput.playAreaPosition = transform.position;
            rigInput.playAreaRotation = transform.rotation;
            if (useInputInterpolation)
            {
                var leftHandInterpolationPose = leftHandInputDevice.InterpolatedPose(InterpolationDelay);
                var rightHandInterpolationPose = rightHandInputDevice.InterpolatedPose(InterpolationDelay);
                var headsetInterpolationPose = headsetInputDevice.InterpolatedPose(InterpolationDelay);
                rigInput.leftHandPosition = leftHandInterpolationPose.position;
                rigInput.leftHandRotation = leftHandInterpolationPose.rotation;
                rigInput.rightHandPosition = rightHandInterpolationPose.position;
                rigInput.rightHandRotation = rightHandInterpolationPose.rotation;
                rigInput.headsetPosition = headsetInterpolationPose.position;
                rigInput.headsetRotation = headsetInterpolationPose.rotation;
            } else
            {
                rigInput.leftHandPosition = transform.InverseTransformPoint(leftHand.transform.position);
                rigInput.leftHandRotation = Quaternion.Inverse(transform.rotation) * leftHand.transform.rotation;
                rigInput.rightHandPosition = transform.InverseTransformPoint(rightHand.transform.position);
                rigInput.rightHandRotation = Quaternion.Inverse(transform.rotation) * rightHand.transform.rotation;
                rigInput.headsetPosition = transform.InverseTransformPoint(headset.transform.position);
                rigInput.headsetRotation = Quaternion.Inverse(transform.rotation) * headset.transform.rotation;
            }

            rigInput.leftHandCommand = leftHand.handCommand;
            rigInput.rightHandCommand = rightHand.handCommand;

            rigInput.leftGrabInfo = leftHand.grabber.GrabInfo;
            rigInput.rightGrabInfo = rightHand.grabber.GrabInfo;

            GatherCustomInput(ref rigInput);

            input.Set(rigInput);
        }

        private void GatherCustomInput(ref RigInput rigInput)
        {
            var currentVRPlayer = GetVRPlayer();

            if (enableKeyboardInput)
            {
                rigInput.keyPressed1 = keyPressBuffer.Count > 0 ? keyPressBuffer[0] : KeyCode.None;
                rigInput.keyPressed2 = keyPressBuffer.Count > 1 ? keyPressBuffer[1] : KeyCode.None;
                rigInput.keyPressed3 = keyPressBuffer.Count > 2 ? keyPressBuffer[2] : KeyCode.None;
                rigInput.keyPressed4 = keyPressBuffer.Count > 3 ? keyPressBuffer[3] : KeyCode.None;

                keyPressBuffer.Clear();

                //See what keys are sent here
                if(rigInput.keyPressed1 != KeyCode.None) Debug.Log($"Sending keyboard input over network: {rigInput.keyPressed1}, {rigInput.keyPressed2}, {rigInput.keyPressed3}, {rigInput.keyPressed4}");


                //For testing i will use this instead of unitys actions since we will use the keyboard presses anyway.
                if (rigInput.keyPressed1 == KeyCode.Alpha1)
                {
                    if (currentVRPlayer != null && currentVRPlayer.NetworkedPlayerType == VRPlayer.PlayerType.EnhancedSneaking)
                    {
                        rigInput.customButtons.Set(RigInput.SNEAKTESTBUTTON, true);
                    }

                }
                else if (rigInput.keyPressed1 == KeyCode.Alpha2)
                {
                    rigInput.customButtons.Set(RigInput.INTERACTIONBUTTON, true);
                }
                else if (rigInput.keyPressed1 == KeyCode.LeftArrow)
                {
                    rigInput.yDelta = -2f;
                }
                else if (rigInput.keyPressed1 == KeyCode.RightArrow)
                {
                    rigInput.yDelta = 2f;
                }
                else if (rigInput.keyPressed1 == KeyCode.UpArrow)
                {
                    rigInput.zDelta = -2f;
                }
                else if (rigInput.keyPressed1 == KeyCode.DownArrow)
                {
                    rigInput.zDelta = 2f;

                }
            }
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) 
        {
            if(player == runner.LocalPlayer)
            {
                hasSearchedForVRPlayer = false;
                vrPlayer = null;
            }
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) 
        {
            if(player == runner.LocalPlayer)
            {
                hasSearchedForVRPlayer = false;
                vrPlayer = null;
            }
        }
        #endregion

        #region Local Finger Tracking 
        void InitializeHandTracking()
        {
            if (handSubsystem == null)
            {
                var xrManager = XRGeneralSettings.Instance?.Manager;
                if (xrManager?.activeLoader != null)
                {
                    handSubsystem = xrManager.activeLoader.GetLoadedSubsystem<XRHandSubsystem>();
                }
            }

            if (handSubsystem != null)
            {
                handSubsystem.trackingAcquired += OnHandTrackingAcquired;
                handSubsystem.trackingLost += OnHandTrackingLost;
                handSubsystem.updatedHands += OnHandsUpdated;
                Debug.Log("HardwareRig: Hand tracking subsystem initialized");
            }
            else
            {
                Debug.LogWarning("HardwareRig: Hand tracking subsystem not available");
                enableFingerTracking = false;
            }
        }

        string GetJointNameFromID(XRHandJointID jointID, bool isLeft)
        {
            string prefix = isLeft ? "L_" : "R_";
            string suffix = "";
            switch (jointID)
            {
                // Thumb
                case XRHandJointID.ThumbMetacarpal: suffix = "ThumbMetacarpal"; break;
                case XRHandJointID.ThumbProximal: suffix = "ThumbProximal"; break;
                case XRHandJointID.ThumbDistal: suffix = "ThumbDistal"; break;
                case XRHandJointID.ThumbTip: suffix = "ThumbTip"; break;

                // Index
                case XRHandJointID.IndexProximal: suffix = "IndexProximal"; break;
                case XRHandJointID.IndexIntermediate: suffix = "IndexIntermediate"; break;
                case XRHandJointID.IndexDistal: suffix = "IndexDistal"; break;
                case XRHandJointID.IndexTip: suffix = "IndexTip"; break;

                // Middle
                case XRHandJointID.MiddleMetacarpal: suffix = "MiddleMetacarpal"; break;
                case XRHandJointID.MiddleProximal: suffix = "MiddleProximal"; break;
                case XRHandJointID.MiddleIntermediate: suffix = "MiddleIntermediate"; break;
                case XRHandJointID.MiddleDistal: suffix = "MiddleDistal"; break;
                case XRHandJointID.MiddleTip: suffix = "MiddleTip"; break;

                // Ring
                case XRHandJointID.RingMetacarpal: suffix = "RingMetacarpal"; break;
                case XRHandJointID.RingIntermediate: suffix = "RingIntermediate"; break;
                case XRHandJointID.RingProximal: suffix = "RingProximal"; break;
                case XRHandJointID.RingDistal: suffix = "RingDistal"; break;
                case XRHandJointID.RingTip: suffix = "RingTip"; break;

                // Pinky
                case XRHandJointID.LittleMetacarpal: suffix = "LittleMetacarpal"; break;
                case XRHandJointID.LittleProximal: suffix = "LittleProximal"; break;
                case XRHandJointID.LittleIntermediate: suffix = "LittleIntermediate"; break;
                case XRHandJointID.LittleDistal: suffix = "LittleDistal"; break;
                case XRHandJointID.LittleTip: suffix = "LittleTip"; break;

                case XRHandJointID.Wrist: suffix = "Wrist"; break;
                case XRHandJointID.Palm: suffix = "Palm"; break;
                default: return null;
            }

            return prefix + suffix;
        }

        void BuildJointCache(GameObject handModel, bool isLeft)
        {
            var cache = new Dictionary<XRHandJointID, Transform>();
            Transform skeletonRoot = leftHandSkeletalRoot;
            if (isLeft == false) skeletonRoot = rightHandSkeletalRoot;

            if (skeletonRoot == null)
            {
                Debug.LogError("Could not find skeleton root transform in hand model");
                return;
            }

            Transform[] allJoints = skeletonRoot.GetComponentsInChildren<Transform>();

            for (int i = 0; i < XRHandJointID.EndMarker.ToIndex(); i++)
            {
                XRHandJointID jointID = (XRHandJointID)i;
                string jointName = GetJointNameFromID(jointID, isLeft);
                if (jointName == null) continue;

                foreach (Transform jointTransform in allJoints)
                {
                    if (jointTransform.name == jointName)
                    {
                        cache[jointID] = jointTransform;
                        break;
                    }
                }
            }

            if (isLeft) leftJointCache = cache;
            else rightJointCache = cache;
        }

        void OnHandTrackingAcquired(XRHand hand)
        {
            Debug.Log("Hand tracking acquired, building joint cache");
            bool isLeft = false;
            if (hand.handedness == Handedness.Left) isLeft = true;
            if (leftHandModel != null) BuildJointCache(leftHandModel, isLeft);
            if (rightHandModel != null) BuildJointCache(rightHandModel, isLeft);
        }

        void OnHandTrackingLost(XRHand hand)
        {
            if (hand.handedness == Handedness.Left) leftHandTracked = false;
            else rightHandTracked = false;
            Debug.Log("Hand tracking lost for: " + (hand.handedness == Handedness.Left ? "Left Hand" : "Right Hand"));
        }

        void OnHandsUpdated(XRHandSubsystem subsystem, XRHandSubsystem.UpdateSuccessFlags updateSuccessFlags, XRHandSubsystem.UpdateType updateType)
        {
            if (!enableFingerTracking) return;

            // Update hand tracking state
            leftHandTracked = (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.LeftHandRootPose) != 0;
            rightHandTracked = (updateSuccessFlags & XRHandSubsystem.UpdateSuccessFlags.RightHandRootPose) != 0;


            //Disable old input source when tracking is active
            if (leftHandInputDevice != null)
            {
                leftHandInputDevice.shouldSynchDevicePosition = !leftHandTracked;
            }
            if (rightHandInputDevice != null)
            {
                rightHandInputDevice.shouldSynchDevicePosition = !rightHandTracked;
            }
            // Get hand data
            if (leftHandTracked && leftHandModel != null)
            {
                leftXRHand = subsystem.leftHand;
                UpdateHandJoints(leftXRHand, leftHandModel);
                UpdateHandInteractions(leftXRHand, true);
            }

            if (rightHandTracked && rightHandModel != null)
            {
                rightXRHand = subsystem.rightHand;
                UpdateHandJoints(rightXRHand, rightHandModel);
            }
        }

        void UpdateHandJoints(XRHand hand, GameObject handObject)
        {
            var cache = hand.handedness == Handedness.Left ? leftJointCache : rightJointCache;
            if (cache == null || !hand.isTracked) return;

            HardwareHand hardwareHand = (hand.handedness == Handedness.Left) ? leftHand : rightHand;

            // Update root pose of the entire hand model
            if (hand.GetJoint(XRHandJointID.Wrist).TryGetPose(out Pose rootPose))
            {
                handObject.transform.SetPositionAndRotation(rootPose.position, rootPose.rotation);
                hardwareHand.transform.SetPositionAndRotation(rootPose.position, rootPose.rotation);
            }

            // Update individual joints
            foreach(var pair in cache)
            {
                XRHandJointID jointID = pair.Key;
                Transform jointTransform = pair.Value;

                if (hand.GetJoint(jointID).TryGetPose(out Pose jointPose))
                {
                    jointTransform.SetPositionAndRotation(jointPose.position, jointPose.rotation);
    
                    //Vector3 worldPos = jointPose.position;
                    //Quaternion worldRot = jointPose.rotation;

                    //jointTransform.localPosition = handObject.transform.InverseTransformPoint(worldPos);
                    //jointTransform.localRotation = Quaternion.Inverse(handObject.transform.rotation) * worldRot;
                }
            }
        }

        void UpdateHandInteractions(XRHand hand, bool isLeft)
        {
            // Detect pinch gesture for interactions
            if (DetectPinchGesture(hand))
            {
                OnPinchDetected(isLeft);
            }

            if (DetectCuttingGesture(hand))
            {
                OnCutDetected(isLeft);
            }

            // Update interactor positions
                var interactor = isLeft ? leftFingerInteractor : rightFingerInteractor;
            var pokeInteractor = isLeft ? leftPokeInteractor : rightPokeInteractor;

            if (interactor != null && hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose))
            {
                interactor.transform.position = indexPose.position;
                interactor.transform.rotation = indexPose.rotation;
                
                if (pokeInteractor != null)
                {
                    pokeInteractor.transform.position = indexPose.position;
                    pokeInteractor.transform.rotation = indexPose.rotation;
                }
            }
        }

        bool DetectPinchGesture(XRHand hand)
        {
            // Simple pinch detection based on distance between thumb and index finger
            if (hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose) &&
                hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose))
            {
                float distance = Vector3.Distance(thumbPose.position, indexPose.position);
                return distance < 0.03f; // 3cm threshold
            }
            return false;
        }

        void OnPinchDetected(bool isLeft)
        {
            string handName = isLeft ? "Left" : "Right";
            Debug.Log($"{handName} hand pinch detected!");
            
            // Trigger interaction events here
            // You can add custom interaction logic based on pinch gestures
        }

        bool DetectCuttingGesture(XRHand hand)
        {
            if (hand.GetJoint(XRHandJointID.IndexTip).TryGetPose(out Pose indexPose) &&
                hand.GetJoint(XRHandJointID.MiddleTip).TryGetPose(out Pose middlePose) &&
                hand.GetJoint(XRHandJointID.RingIntermediate).TryGetPose(out Pose ringPose) &&
                hand.GetJoint(XRHandJointID.ThumbTip).TryGetPose(out Pose thumbPose) &&
                hand.GetJoint(XRHandJointID.Palm).TryGetPose(out Pose palmPose))
            {
                //See if Middle and index are not near palm
                float distancePalmIndex = Vector3.Distance(palmPose.position, indexPose.position);
                if (distancePalmIndex < 0.03f) return false;

                float distancePalmMiddle = Vector3.Distance(palmPose.position, middlePose.position);
                if (distancePalmMiddle < 0.03f) return false;

                float distanceThumbRing = Vector3.Distance(thumbPose.position, ringPose.position);
                if (distanceThumbRing < 0.03f)
                {
                    float distanceMiddleIndex = Vector3.Distance(middlePose.position, indexPose.position);
                    return distanceMiddleIndex < 0.03f;
                }
            }
            return false;
        }

        void OnCutDetected(bool isLeft)
        {
            string handName = isLeft ? "Left" : "Right";
            Debug.Log($"{handName} hand cut detected!");
        }

        void UpdateFingerTrackingVisibility()
        {
            //Determine when to show finger tracking and when controller-animated hands
            bool showLeftFingers = ShouldShowFingerTracking(true);
            bool showRightFingers = ShouldShowFingerTracking(false);

            if (leftHandAnimator != null && leftHandAnimator.enabled != !showLeftFingers)
            {
                leftHandAnimator.enabled = !showLeftFingers;
            }

            if (rightHandAnimator != null && rightHandAnimator.enabled != !showRightFingers)
            {
                rightHandAnimator.enabled = !showRightFingers;
            }

            UpdateOSFHandVisibility(!showLeftFingers, !showRightFingers);
        }

        bool ShouldShowFingerTracking(bool isLeft)
        {
            bool handTracked = isLeft ? leftHandTracked : rightHandTracked;
            bool controllerActive = IsControllerActive(isLeft);

            //Show finger tracking when hands are tracked, controlers are not actively being used, PLayer is in an interaction heavy context
            return handTracked  && enableFingerTracking;
        }

        bool IsControllerActive(bool isLeft)
        {
            var handCommand = isLeft ? leftHand.handCommand : rightHand.handCommand;
            
            // Consider controller "active" if significant input is detected
            return handCommand.triggerCommand > 0.1f || 
                   handCommand.gripCommand > 0.1f || 
                   handCommand.thumbTouchedCommand > 0.5f ||
                   handCommand.indexTouchedCommand > 0.5f;
        }

        void UpdateOSFHandVisibility(bool showLeftOSF, bool showRightOSF)
        {
            // Get references to the OSF hand representations
            var networkRig = GetComponent<VRPlayer>()?.GetComponent<NetworkRig>();
            if (networkRig == null) return;

            var leftOSF = networkRig.leftHand.GetComponent<OSFHandRepresentation>();
            var rightOSF = networkRig.rightHand.GetComponent<OSFHandRepresentation>();

            // Fade OSF hands when finger tracking is active
            if (leftOSF != null)
            {
                leftOSF.DisplayMesh(showLeftOSF);
            }

            if (rightOSF != null)
            {
                rightOSF.DisplayMesh(showRightOSF);
            }
        }

        // Public methods to check hand tracking state
        public bool IsLeftHandTracked() => leftHandTracked;
        public bool IsRightHandTracked() => rightHandTracked;
        public bool IsFingerTrackingEnabled() => enableFingerTracking && handSubsystem != null && handSubsystem.running;

        // Get finger positions for gameplay logic
        public Vector3 GetFingerTipPosition(bool isLeft, XRHandJointID jointID = XRHandJointID.IndexTip)
        {
            var hand = isLeft ? leftXRHand : rightXRHand;
            if (hand.isTracked && hand.GetJoint(jointID).TryGetPose(out Pose pose))
            {
                return pose.position;
            }
            return Vector3.zero;
        }


        #endregion

        #region INetworkRunnerCallbacks (unused)


        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
        public void OnConnectedToServer(NetworkRunner runner) { }
        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
        public void OnSceneLoadDone(NetworkRunner runner) { }
        public void OnSceneLoadStart(NetworkRunner runner) { }
        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
        #endregion
    }
}
