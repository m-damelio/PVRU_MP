using Fusion.XR.Host.Grabbing;
using Fusion.XR.Host.Rig;
using NUnit.Framework.Constraints;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Fusion.XR.Host.Locomotion
{
    public struct RayData
    {
        public bool isRayEnabled;
        public Vector3 origin;
        public Vector3 target;
        public Color color;
    }

    /**
     * 
     * Display a line renderer when action input is pressed, and raycast other the selected layer mask to find a destination point
     * 
     **/

    public class RayBeamer : MonoBehaviour
    {
        public HardwareHand hand;

        public bool useRayActionInput = true;
        public InputActionProperty rayAction;
        public Transform origin;
        public LayerMask targetLayerMask = ~0;
        public float maxDistance = 100f;

        [Header("Representation")]
        public LineRenderer lineRenderer;
        public float width = 0.02f;
        public Material lineMaterial;

        public Color hitColor = Color.green;
        public Color noHitColor = Color.red;

        public UnityEvent<Collider, Vector3> onRelease = new UnityEvent<Collider, Vector3>();

        // Define if the beamer ray is active this frame
        public bool isRayEnabled = false;

        [Header("Mirror Actions")]
        public RotateMirror lastMirrorHit = null;
        //private bool isHit;

        [Header("ColorObject Actions")]
        public GameObject lastColorObjectHit = null;

        [Header("UI Actions")]
        public GameObject volumeup;
        public GameObject volumedown;
        public TMP_Text volume_display;
        public int volume;
        public bool isHitVolume;

        private VRPlayer localPlayerCache;

        public enum Status
        {
            NoBeam,
            BeamNoHit,
            BeamHit
        }
        public Status status = Status.NoBeam;

        public RayData ray;
        Vector3 lastHit;
        Collider lastHitCollider = null;

        public virtual void Awake()
        {
            if (lineRenderer != null)
            {
                lineRenderer.material = lineMaterial;
                lineRenderer.numCapVertices = 4;
                lineRenderer.startWidth = width;
                lineRenderer.endWidth = width;
                lineRenderer.useWorldSpace = true;
                lineRenderer.enabled = false;
            }
            
            if (origin == null) origin = transform;
            if (hand == null) hand = GetComponentInParent<HardwareHand>();  
            //isHit = false;
            isHitVolume = false;
        }

        public virtual void Start()
        {
            rayAction.EnableWithDefaultXRBindings(hand.side, new List<string> { "thumbstickClicked", "primaryButton", "secondaryButton" });
            
        }

        public bool BeamCast(out RaycastHit hitInfo, Vector3 origin, Vector3 direction)
        {
            Ray handRay = new Ray(origin, direction);
            return Physics.Raycast(handRay, out hitInfo, maxDistance, targetLayerMask);
        }

        public bool BeamCast(out RaycastHit hitInfo)
        {
            return BeamCast(out hitInfo, ray.origin, origin.forward);
        }

        public void Update() {
        // If useRayActionInput is true, we read the rayAction to determine isRayEnabled for this frame
        if (useRayActionInput && rayAction != null && rayAction.action != null)
        {
            isRayEnabled = rayAction.action.ReadValue<float>() == 1;
        }

        VRPlayer localPlayer = GetLocalVRPlayer();

        ray.isRayEnabled = isRayEnabled;
        
        if (ray.isRayEnabled && localPlayer != null) // Ensure the beamer only works if it has a player owner
            {
                ray.origin = origin.position;
                if (BeamCast(out RaycastHit hit))
                {
                    //for all players
                    //make the beam visible and set the teleport destination
                    lastHitCollider = hit.collider;
                    ray.target = hit.point;
                    ray.color = hitColor;
                    lastHit = hit.point;
                    status = Status.BeamHit;

                    // Only the Hacker can select and interact with mirrors or color objects
                    if (localPlayer.NetworkedPlayerType == VRPlayer.PlayerType.EnhancedHacking)
                    {
                        HandleHackerInteractions(hit, localPlayer);
                    }
                }
                else // The beam hits nothing
                {
                    lastHitCollider = null;
                    ray.target = ray.origin + origin.forward * maxDistance;
                    ray.color = noHitColor;
                    status = Status.BeamNoHit;

                    // If the Hacker was pointing at an object and now isn't, deselect it
                    if (localPlayer.NetworkedPlayerType == VRPlayer.PlayerType.EnhancedHacking)
                    {
                        DeselectAll(localPlayer);
                    }
                }
            }
            else // The beam is off
            {
                // This part runs when the button is released, triggering the teleport for ALL players
                if (status == Status.BeamHit)
                {
                    if (onRelease != null) onRelease.Invoke(lastHitCollider, lastHit);
                }
                status = Status.NoBeam;
                lastHitCollider = null;
            }

        UpdateRay();
    }

    private void HandleHackerInteractions(RaycastHit hit, VRPlayer player)
    {
        // Mirror Interaction Logic
        bool hitMirror = hit.collider.CompareTag("Mirror");
        if (hitMirror)
        {
            var currentMirror = hit.collider.gameObject.transform.GetComponentInParent<RotateMirror>();
            var colorMirror = currentMirror.GetComponent<ColorMirror>();

            if (lastMirrorHit != null && lastMirrorHit != currentMirror)
            {
                DeselectMirror(player);
            }

            if (colorMirror != null && !colorMirror.inactive) {
                currentMirror.SetHighlight(true);
                colorMirror.isSelected = true;
                lastMirrorHit = currentMirror;
                player.SetActiveMirror(currentMirror);
            }
        }
        else
        {
            DeselectMirror(player);
        }

        // Color Object Interaction Logic
        bool hitColorObject = hit.collider.CompareTag("ColorChange");
        if (hitColorObject)
        {
            var currentColorObject = hit.collider.gameObject;
            var colorControl = currentColorObject.transform.GetComponentInParent<colorControl>();

            if (lastColorObjectHit != null && lastColorObjectHit != currentColorObject)
            {
                DeselectColorObject(player);
            }
            
            colorControl.SetSelectionColor(true, currentColorObject);
            lastColorObjectHit = currentColorObject;
            player.SetActiveColorObject(colorControl);
        }
        else
        {
            DeselectColorObject(player);
        }
    }

    private void DeselectMirror(VRPlayer player)
    {
        if (lastMirrorHit != null)
        {
            lastMirrorHit.SetHighlight(false);
            lastMirrorHit.gameObject.GetComponent<ColorMirror>().isSelected = false;
            player.DeselectActiveMirror(lastMirrorHit);
            lastMirrorHit = null;
        }
    }

    private void DeselectColorObject(VRPlayer player)
    {
        if (lastColorObjectHit != null)
        {
            lastColorObjectHit.transform.GetComponentInParent<colorControl>().SetSelectionColor(false, lastColorObjectHit);
            player.DeselectActiveColorObject(lastColorObjectHit.transform.GetComponentInParent<colorControl>());
            lastColorObjectHit = null;
        }
    }

    private void DeselectAll(VRPlayer player)
    {
        DeselectMirror(player);
        DeselectColorObject(player);
    }
    private VRPlayer GetLocalVRPlayer()
    {
        // Return the cached player if we've already found it
        if (localPlayerCache != null) return localPlayerCache;

        // Find the NetworkObject that has input authority for this client
        foreach (var player in FindObjectsByType<VRPlayer>(FindObjectsSortMode.None))
        {
            if (player.Object.HasInputAuthority)
            {
                localPlayerCache = player;
                return player;
            }
        }
        return null;
    }

    public void CancelHit()
    {
        status = Status.NoBeam;
    }

    void UpdateRay() { 
        lineRenderer.enabled = ray.isRayEnabled;
        if (ray.isRayEnabled)
        {
            lineRenderer.SetPositions(new Vector3[] { ray.origin, ray.target });
            lineRenderer.positionCount = 2;
            lineRenderer.startColor = ray.color;
            lineRenderer.endColor = ray.color;
        }
    }
    }
}
