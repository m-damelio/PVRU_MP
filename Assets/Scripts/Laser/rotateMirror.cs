using UnityEngine;
using System.Collections;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Oculus.Interaction;


public class RotateMirror : NetworkBehaviour
{
    [Header("Mirror Settings")]
    public Transform controlRotation; 
    private float rotate = 5.0f;
    private bool isSelected;

    [Header("Laser Reference")]
    public LaserBean laser;

    [Networked] public Quaternion NetworkedRotation {get;set;}
    [Networked] public float NetworkedYRotation { get; set; }
    [Networked] public float NetworkedZRotation { get; set; }

    // For Laser-Updates
    private Quaternion lastNetworkedRot;

    //struct für Input Struktur für die Rotation
    public struct MirrorInput : INetworkInput
    {
        public float yDelta;
        public float zDelta;
    }

    // For Laser-Updates
    private Quaternion lastRot;

    public override void Spawned()
    {
        if(Object.HasStateAuthority)
        {
            NetworkedYRotation = controlRotation.rotation.eulerAngles.y;
            NetworkedZRotation = controlRotation.rotation.eulerAngles.z;
            NetworkedRotation = controlRotation.rotation;
        }
        lastNetworkedRot = NetworkedRotation;
    }

    public override void FixedUpdateNetwork()
    {

        // Input-Handling
        //HandleInput();

        // Check if networked rotation has changed (for all clients)
        if (NetworkedRotation != lastNetworkedRot)
        {
            // Apply the networked rotation to the transform
            controlRotation.rotation = NetworkedRotation;

            // Trigger laser update when rotation changes
            TriggerLaserUpdate();

            lastNetworkedRot = NetworkedRotation;
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

    private void OnTriggerEnter(Collider other)
    {
        if(other.tag != "laser" && !isSelected)
        {
            SetHighlight(true);
        }
        else if (other.tag != "laser" && isSelected)
        {
            SetHighlight(false);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRotateY(float angle)
    {
        if (Object.HasStateAuthority)
        {
            Debug.Log("RPCY " + angle);
            NetworkedYRotation += angle;
            UpdateRotation();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRotateZ(float angle)
    {
        if (Object.HasStateAuthority)
        {
            Debug.Log("RPCZ " + angle);
            NetworkedZRotation += angle;
            UpdateRotation();
        }
            
    }

    private void UpdateRotation()
    {
        controlRotation.rotation = Quaternion.Euler(0, NetworkedYRotation, NetworkedZRotation);
        NetworkedRotation = controlRotation.rotation;
        TriggerLaserUpdate();
    }

    public void SetHighlight(bool state)
    {
        Debug.Log(state);
        var color = new Color(1.0f, 0, 1.0f, 1.0f);
        GetComponentInChildren<Renderer>().material.color = state ? color : Color.white;
    }


}