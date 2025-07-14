using UnityEngine;
using System.Collections;
using Fusion;

public class RotateMirror : NetworkBehaviour
{
    [Header("Mirror Settings")]
    public Transform controlRotation; 
    private float rotate = 5.0f;

    [Networked] public Quaternion NetworkedRotation { get; set; }

    // F�r Laser-Updates
    private Quaternion lastRot;

    public override void Spawned()
    {
        Debug.Log("Hier");
        if (HasStateAuthority)
        {
            Debug.Log("HierimIf");
            NetworkedRotation = controlRotation.rotation;
        }

        lastRot = controlRotation.rotation;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Input-Handling
        HandleInput();

        // Pr�fen auf �nderungen und Laser-Update triggern
        if (controlRotation.rotation != lastRot)
        {
            TriggerLaserUpdate();
            lastRot = controlRotation.rotation;
        }
    }

    private void HandleInput()
    {
        // Links/Rechts � drehen des Root-Objekts um Y
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Rpc_RotateMirror(Vector3.up, -rotate);
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            Rpc_RotateMirror(Vector3.up, rotate);
            //controlRotation.Rotate(Vector3.up * rotate, Space.World);
            //NetworkedRotation = controlRotation.rotation;
        }

        // Hoch/Runter � kippen des Kindobjekts um lokale X
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            Rpc_RotateMirror(new Vector3(0,1,0), -rotate);
            //transform.Rotate(Vector3.forward * -rotate, Space.World);
            //NetworkedRotation = controlRotation.rotation;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            Rpc_RotateMirror(new Vector3(0,1,0), rotate);
            //transform.Rotate(Vector3.forward * rotate, Space.World);
            //NetworkedRotation = controlRotation.rotation;
        }
    }

    /*
    public override void Render()
    {
        // Alle Clients synchronisieren ihre Rotation mit den networked values
        if (controlRotation.rotation != NetworkedParentRotation)
        {
            controlRotation.rotation = NetworkedParentRotation;
        }

        if (controlRotation.rotation != NetworkedChildRotation)
        {
            controlRotation.rotation = NetworkedChildRotation;
        }
    }
    */
    private void TriggerLaserUpdate()
    {
        // Finde alle LaserBean Objekte und aktualisiere sie
        LaserBean[] lasers = FindObjectsOfType<LaserBean>();
        foreach (LaserBean laser in lasers)
        {
            if (laser != null)
            {
                laser.RpcForceUpdate();
            }
        }
    }

    // Alternative: Spezifische Laser-Referenz setzen
    [Header("Laser Reference (Optional)")]
    public LaserBean specificLaser;

    private void TriggerSpecificLaserUpdate()
    {
        if (specificLaser != null)
        {
            specificLaser.RpcForceUpdate();
        }
        else
        {
            TriggerLaserUpdate();
        }
    }

    // RPC f�r externe Trigger (falls ben�tigt)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RotateMirror(Vector3 axis, float angle)
    {
        controlRotation.Rotate(axis * angle, Space.Self);
        
        NetworkedRotation = controlRotation.rotation;

        TriggerLaserUpdate();
    }


}