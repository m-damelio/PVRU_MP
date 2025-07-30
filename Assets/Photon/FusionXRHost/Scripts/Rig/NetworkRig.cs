using Fusion.XR.Host.Grabbing;
using UnityEngine;


namespace Fusion.XR.Host.Rig
{
    /**
     * 
     * Networked VR user
     * 
     * Handle the synchronisation of the various rig parts: headset, left hand, right hand, and playarea (represented here by the NetworkRig)
     * Use the local HardwareRig rig parts position info when this network rig is associated with the local user 
     * 
     * 
     **/

    [RequireComponent(typeof(NetworkTransform))]
    // We ensure to run after the NetworkTransform or NetworkRigidbody, to be able to override the interpolation target behavior in Render()
    [DefaultExecutionOrder(NetworkRig.EXECUTION_ORDER)]
    public class NetworkRig : NetworkBehaviour
    {
        public const int EXECUTION_ORDER = 100;
        public HardwareRig hardwareRig;
        public NetworkHand leftHand;
        public NetworkHand rightHand;
        public NetworkHeadset headset;
        public NetworkGrabber leftGrabber;
        public NetworkGrabber rightGrabber;

        [HideInInspector]
        public NetworkTransform networkTransform;

        private void Awake()
        {
            networkTransform = GetComponent<NetworkTransform>();
            leftGrabber = leftHand.GetComponent<NetworkGrabber>();
            rightGrabber = rightHand.GetComponent<NetworkGrabber>();
        }

        // As we are in host topology, we use the input authority to track which player is the local user
        public bool IsLocalNetworkRig => Object.HasInputAuthority;

        public override void Spawned()
        {
            base.Spawned();
            if (IsLocalNetworkRig)
            {
                hardwareRig = FindObjectOfType<HardwareRig>();
                if (hardwareRig == null) Debug.LogError("Missing HardwareRig in the scene");
            }
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();

            // update the rig at each network tick
            if (GetInput<RigInput>(out var input))
            {
                var vrPlayer = GetComponent<VRPlayer>();

                if(vrPlayer != null && vrPlayer.characterController != null && Object.HasInputAuthority)
                {
                    //VRPlayer handles movement
                }
                else
                {
                    //For non phyics movement
                    transform.position = input.playAreaPosition;
                    transform.rotation = input.playAreaRotation;
                }

                leftHand.transform.localPosition = input.leftHandPosition;
                leftHand.transform.localRotation = input.leftHandRotation;
                rightHand.transform.localPosition = input.rightHandPosition;
                rightHand.transform.localRotation = input.rightHandRotation;
                headset.transform.localPosition = input.headsetPosition;
                headset.transform.localRotation = input.headsetRotation;

                leftHand.HandCommand = input.leftHandCommand;
                rightHand.HandCommand = input.rightHandCommand;
                leftGrabber.GrabInfo = input.leftGrabInfo;
                rightGrabber.GrabInfo = input.rightGrabInfo;
            }
        }

        /*
        public override void Render()
        {
            base.Render();
            if (IsLocalNetworkRig)
            {
                // For local user, only update visual elements that don't affect physics
                // Don't override the main transform position if using CharacterController
                var vrPlayer = GetComponent<VRPlayer>();
                if (vrPlayer != null && vrPlayer.characterController != null)
                {
                    // Let CharacterController handle the main transform position
                    // Only update hand and headset positions
                    leftHand.transform.position = hardwareRig.leftHand.transform.position;
                    leftHand.transform.rotation = hardwareRig.leftHand.transform.rotation;
                    rightHand.transform.position = hardwareRig.rightHand.transform.position;
                    rightHand.transform.rotation = hardwareRig.rightHand.transform.rotation;
                    headset.transform.position = hardwareRig.headset.transform.position;
                    headset.transform.rotation = hardwareRig.headset.transform.rotation;
                }
                else
                {
                    // Original behavior for non-physics movement
                    transform.position = hardwareRig.transform.position;
                    transform.rotation = hardwareRig.transform.rotation;
                    leftHand.transform.position = hardwareRig.leftHand.transform.position;
                    leftHand.transform.rotation = hardwareRig.leftHand.transform.rotation;
                    rightHand.transform.position = hardwareRig.rightHand.transform.position;
                    rightHand.transform.rotation = hardwareRig.rightHand.transform.rotation;
                    headset.transform.position = hardwareRig.headset.transform.position;
                    headset.transform.rotation = hardwareRig.headset.transform.rotation;
                }
            }
        }
        */
    }
}
