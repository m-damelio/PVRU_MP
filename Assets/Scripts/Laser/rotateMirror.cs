using UnityEngine;
using System.Collections;
using Fusion;

public class RotateMirror : NetworkBehaviour
{
    [Header("Mirror Settings")]
    public Transform mirrorChild; // das Kindobjekt mit dem Spiegel
    private float rotate = 5.0f;

    [Networked] public Quaternion NetworkedParentRotation { get; set; }
    [Networked] public Quaternion NetworkedChildRotation { get; set; }

    // Für Input-Handling
    private bool leftPressed = false;
    private bool rightPressed = false;
    private bool upPressed = false;
    private bool downPressed = false;

    // Für Laser-Updates
    private Quaternion lastParentRot;
    private Quaternion lastChildRot;

    public override void Spawned()
    {
        Debug.Log("Hier");
        if (HasStateAuthority)
        {
            Debug.Log("HierimIf");
            NetworkedParentRotation = transform.rotation;
            NetworkedChildRotation = mirrorChild.rotation;
        }

        lastParentRot = transform.rotation;
        lastChildRot = mirrorChild.rotation;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        // Input-Handling
        HandleInput();

        // Prüfen auf Änderungen und Laser-Update triggern
        if (transform.rotation != lastParentRot || mirrorChild.rotation != lastChildRot)
        {
            TriggerLaserUpdate();
            lastParentRot = transform.rotation;
            lastChildRot = mirrorChild.rotation;
        }
    }

    private void HandleInput()
    {
        // Links/Rechts – drehen des Root-Objekts um Y
        if (Input.GetKeyDown(KeyCode.LeftArrow) && !leftPressed)
        {
            transform.Rotate(Vector3.up * -rotate, Space.World);
            NetworkedParentRotation = transform.rotation;
            leftPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.LeftArrow))
        {
            leftPressed = false;
        }

        if (Input.GetKeyDown(KeyCode.RightArrow) && !rightPressed)
        {
            transform.Rotate(Vector3.up * rotate, Space.World);
            NetworkedParentRotation = transform.rotation;
            rightPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.RightArrow))
        {
            rightPressed = false;
        }

        // Hoch/Runter – kippen des Kindobjekts um lokale X
        if (Input.GetKeyDown(KeyCode.UpArrow) && !upPressed)
        {
            mirrorChild.Rotate(Vector3.forward * -rotate, Space.Self);
            NetworkedChildRotation = mirrorChild.rotation;
            upPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.UpArrow))
        {
            upPressed = false;
        }

        if (Input.GetKeyDown(KeyCode.DownArrow) && !downPressed)
        {
            mirrorChild.Rotate(Vector3.forward * rotate, Space.Self);
            NetworkedChildRotation = mirrorChild.rotation;
            downPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.DownArrow))
        {
            downPressed = false;
        }
    }

    public override void Render()
    {
        // Alle Clients synchronisieren ihre Rotation mit den networked values
        if (transform.rotation != NetworkedParentRotation)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, NetworkedParentRotation, Time.deltaTime * 10f);
        }

        if (mirrorChild.rotation != NetworkedChildRotation)
        {
            mirrorChild.rotation = Quaternion.Lerp(mirrorChild.rotation, NetworkedChildRotation, Time.deltaTime * 10f);
        }
    }

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

    // RPC für externe Trigger (falls benötigt)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcRotateMirror(Vector3 axis, float angle, bool isParent)
    {
        if (isParent)
        {
            transform.Rotate(axis * angle, Space.World);
            NetworkedParentRotation = transform.rotation;
        }
        else
        {
            mirrorChild.Rotate(axis * angle, Space.Self);
            NetworkedChildRotation = mirrorChild.rotation;
        }

        TriggerLaserUpdate();
    }


}