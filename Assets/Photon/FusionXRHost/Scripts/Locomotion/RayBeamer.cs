using Fusion.XR.Host.Grabbing;
using Fusion.XR.Host.Rig;
using Meta.XR.ImmersiveDebugger.UserInterface.Generic;
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
        private bool isHit;

        [Header("ColorObject Actions")]
        public GameObject lastColorObjectHit = null;

        [Header("UI Actions")]
        public GameObject volumeup;
        public GameObject volumedown;
        public TMP_Text volume_display;
        public int volume;
        public bool isHitVolume;

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
            isHit = false;
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
            //  Usefull for the mouse teleporter of the desktop mode, which disables the action reading to have its own logic to enable the beamer
            if (useRayActionInput && rayAction != null && rayAction.action != null)
            {
                isRayEnabled = rayAction.action.ReadValue<float>() == 1;
            }

            ray.isRayEnabled = isRayEnabled;
            if (ray.isRayEnabled)
            {
                var player = FindFirstObjectByType<VRPlayer>();
                ray.origin = origin.position;
                if (BeamCast(out RaycastHit hit))
                {
                    Debug.Log(hit.collider.name);
                    lastHitCollider = hit.collider;
                    ray.target = hit.point;
                    ray.color = hitColor;
                    lastHit = hit.point;
                    status = Status.BeamHit;

                    /////////////// Mirror /////////////////
                    if (hit.collider.CompareTag("Mirror"))
                    {
                        var currentMirror = hit.collider.gameObject.transform.GetComponentInParent<RotateMirror>();
                        

                        if (lastMirrorHit != null && lastMirrorHit != currentMirror)
                        {
                            lastMirrorHit.SetHighlight(false); // Alten Spiegel deaktivieren
                            player.DeselectActiveMirror(lastMirrorHit);

                        }
                        currentMirror.SetHighlight(true); // Aktuellen Spiegel aktivieren
                        lastMirrorHit = currentMirror; // Speichern für später
                        player.SetActiveMirror(currentMirror);
                    }
                    else
                    {
                        // Kein Spiegel mehr getroffen
                        if (lastMirrorHit != null)
                        {
                            lastMirrorHit.SetHighlight(false);
                            player.DeselectActiveMirror(lastMirrorHit);
                            lastMirrorHit = null;
                        }
                    }

                    /////////////// ColorObj /////////////////
                    if (hit.collider.CompareTag("ColorChange"))
                    {
                        var currentColorObject = hit.collider.gameObject;
                        var colorControl = currentColorObject.transform.GetComponentInParent<colorControl>();

                        if (lastColorObjectHit != null && lastColorObjectHit != currentColorObject)
                        {
                            lastColorObjectHit.transform.GetComponentInParent<colorControl>().SetSelectionColor(false, lastColorObjectHit); // Altes Objekt deaktivieren
                            player.DeselectActiveColorObject(lastColorObjectHit.transform.GetComponentInParent<colorControl>());
                        }
                        colorControl.SetSelectionColor(true, currentColorObject); // Aktuelles ColorObject einfärben
                        lastColorObjectHit = currentColorObject; // Speichern für später
                        player.SetActiveColorObject(colorControl);
                    }

                    else
                    {
                        // Kein Objekt mehr getroffen
                        if (lastColorObjectHit != null)
                        {
                            lastColorObjectHit.transform.GetComponentInParent<colorControl>().SetSelectionColor(false, lastColorObjectHit);
                            player.DeselectActiveColorObject(lastColorObjectHit.transform.GetComponentInParent<colorControl>());
                            lastColorObjectHit = null;
                        }
                    }

                    if (hit.collider.CompareTag("Buttons"))
                    {
                        Debug.Log("Button hit");
                        
                    }

                    else if (hit.collider.CompareTag("VolumeUP"))
                    {
                        if (!isHitVolume)
                        {
                            Debug.Log("Volume up");
                            volume += 1;
                            volume_display.text = volume.ToString();
                            isHitVolume = true;
                        }
                        
                    }

                    else if (hit.collider.CompareTag("VolumeDOWN"))
                    {
                        if (!isHitVolume)
                        {
                            if (volume >0)
                            {
                                Debug.Log("Volume up");
                                volume -= 1;
                                volume_display.text = volume.ToString();
                                isHitVolume = true;
                            }
                            
                        }


                    }

                    isHitVolume = false;

                }
                

                else
                {
                    lastHitCollider = null;
                    ray.target = ray.origin + origin.forward * maxDistance;
                    ray.color = noHitColor;
                    status = Status.BeamNoHit;

                    // Auch nichts getroffen (Raycast trifft nichts)
                    if (lastMirrorHit != null)
                    {
                        lastMirrorHit.SetHighlight(false);
                        lastMirrorHit = null;
                    }
                }
            }
            else
            {
                if (status == Status.BeamHit)
                {
                    if (onRelease != null) onRelease.Invoke(lastHitCollider, lastHit);
                }
                status = Status.NoBeam;
                lastHitCollider = null;
            }

            UpdateRay();
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
