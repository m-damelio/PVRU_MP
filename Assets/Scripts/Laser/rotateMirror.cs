using UnityEngine;
using System.Collections;
using Fusion;

public class RotateMirror : NetworkBehaviour
{
    [Header("Mirror Settings")]
    public Transform controlRotation; 
    private float rotate = 5.0f;

    [Header("Laser Reference")]
    public LaserBean laser;

    [Networked] public Quaternion NetworkedRotation {get;set;}
    [Networked] public float NetworkedYRotation { get; set; }
    [Networked] public float NetworkedZRotation { get; set; }


    // For Laser-Updates
    private Quaternion lastRot;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            NetworkedRotation = controlRotation.rotation;
            NetworkedYRotation = controlRotation.rotation.y;
            NetworkedZRotation = controlRotation.rotation.z;
        }

        lastRot = controlRotation.rotation;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Input-Handling
        HandleInput();

        // Prüfen auf Änderungen und Laser-Update triggern
        if (controlRotation.rotation != lastRot)
        {
            Debug.Log("Rotation change");
            TriggerLaserUpdate();
            lastRot = controlRotation.rotation;
        }
        else
        {
            //Clients folgen der NetworkedRotation
            controlRotation.rotation = NetworkedRotation;
        }
    }

    private void TriggerLaserUpdate()
    {
        if (laser != null)
        {
            Debug.Log("Trigger update mirror");
            laser.RpcForceUpdate();
        }
        
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            Rpc_RotateY(-rotate);
        }
        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            Rpc_RotateY(rotate);
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            Rpc_RotateZ(-rotate);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            Rpc_RotateZ(rotate);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RotateY(float angle)
    {
        NetworkedYRotation += angle;
        UpdateRotation();
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void Rpc_RotateZ(float angle)
    {
        NetworkedZRotation += angle;
        UpdateRotation();
    }

    private void UpdateRotation()
    {
        controlRotation.rotation = Quaternion.Euler(0, NetworkedYRotation, NetworkedZRotation);
        NetworkedRotation = controlRotation.rotation;
        TriggerLaserUpdate();
    }


}