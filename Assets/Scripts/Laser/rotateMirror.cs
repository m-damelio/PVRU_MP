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
        NetworkedYRotation = gameObject.transform.rotation.eulerAngles.y;
        NetworkedZRotation = gameObject.transform.rotation.eulerAngles.z;
    }

    public override void FixedUpdateNetwork()
    {
       
        // Input-Handling
        //HandleInput();

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

    public void RotateY(float angle)
    {
        NetworkedYRotation += angle;
        UpdateRotation();
    }

    public void RotateZ(float angle)
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

    public void SetHighlight(bool state)
    {
        var color = new Color(255, 0, 255, 255);
        GetComponentInChildren<Renderer>().material.color = state ? color : Color.white;
    }


}