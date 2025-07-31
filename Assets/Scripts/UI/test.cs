using Fusion.XR.Host.Locomotion;
using Fusion.XR.Host.Rig;
using UnityEngine;

public class DualRigRayLogger : MonoBehaviour
{
    [Header("Rigs")]
    public HardwareRig vrRig;       
    public HardwareRig desktopRig;   

    [Header("Einstellungen")]
    public bool useVRRig = true;     
    public float raycastDistance = 20f;

    private RigLocomotion activeLocomotion;

    private void Start()
    {

        SetActiveRig(useVRRig ? vrRig : desktopRig);
    }

    private void Update()
    {

        SetActiveRig(useVRRig ? vrRig : desktopRig);


        if (activeLocomotion != null && activeLocomotion.teleportBeamers != null)
        {
            foreach (var beamer in activeLocomotion.teleportBeamers)
            {
                if (beamer.isRayEnabled)
                {
                    if (beamer.BeamCast(out RaycastHit hit))
                        Debug.Log($"[{activeLocomotion.name}] {beamer.name} trifft: {hit.collider.gameObject.name} @ {hit.point}");
                    else
                        Debug.Log($"[{activeLocomotion.name}] {beamer.name} aktiv, aber kein Treffer.");
                }
            }
        }
    }


    public void SetActiveRig(HardwareRig rig)
    {
        if (rig == null) 
        {
            activeLocomotion = null;
            return;
        }
        activeLocomotion = rig.GetComponentInChildren<RigLocomotion>();
    }
}
