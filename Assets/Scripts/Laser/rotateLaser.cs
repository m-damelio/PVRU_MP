using UnityEngine;
using Fusion;

public class RotateLaser : NetworkBehaviour
{
    [Header("Rotation Settings")]
    public float rotationSpeed = 0.5f;
    public float maxVerticalAngle = 60f;
    public float minVerticalAngle = -60f;

    [Networked] public float NetworkedYaw { get; set; }
    [Networked] public float NetworkedPitch { get; set; }
    [Networked] public float NetworkedRoll { get; set; }

    private float currentYaw;
    private float currentPitch;
    private float currentRoll;

    // Input state tracking
    private bool aPressed = false;
    private bool dPressed = false;
    private bool wPressed = false;
    private bool sPressed = false;

    public override void Spawned()
    {
        Vector3 currentEuler = transform.rotation.eulerAngles;
        currentYaw = currentEuler.y;
        currentPitch = currentEuler.x;
        currentRoll = currentEuler.z;

        if (HasStateAuthority)
        {
            NetworkedYaw = currentYaw;
            NetworkedPitch = currentPitch;
            NetworkedRoll = currentRoll;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        bool rotationChanged = false;

        // A/D - Horizontal rotation (Y-axis)
        if (Input.GetKeyDown(KeyCode.A) && !aPressed)
        {
            NetworkedYaw += rotationSpeed;
            rotationChanged = true;
            aPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.A))
        {
            aPressed = false;
        }

        if (Input.GetKeyDown(KeyCode.D) && !dPressed)
        {
            NetworkedYaw -= rotationSpeed;
            rotationChanged = true;
            dPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.D))
        {
            dPressed = false;
        }

        // W/S - Roll rotation (Z-axis)
        if (Input.GetKeyDown(KeyCode.W) && !wPressed)
        {
            NetworkedRoll -= rotationSpeed;
            rotationChanged = true;
            wPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.W))
        {
            wPressed = false;
        }

        if (Input.GetKeyDown(KeyCode.S) && !sPressed)
        {
            NetworkedRoll += rotationSpeed;
            rotationChanged = true;
            sPressed = true;
        }
        if (Input.GetKeyUp(KeyCode.S))
        {
            sPressed = false;
        }

        // Laser-Update triggern wenn sich die Rotation geändert hat
        if (rotationChanged)
        {
            TriggerLaserUpdate();
        }
    }

    public override void Render()
    {
        // Smooth interpolation für alle Clients
        currentYaw = Mathf.LerpAngle(currentYaw, NetworkedYaw, Time.deltaTime * 10f);
        currentPitch = Mathf.LerpAngle(currentPitch, NetworkedPitch, Time.deltaTime * 10f);
        currentRoll = Mathf.LerpAngle(currentRoll, NetworkedRoll, Time.deltaTime * 10f);

        transform.rotation = Quaternion.Euler(currentPitch, currentYaw, currentRoll);
    }

    private void TriggerLaserUpdate()
    {
        // Finde LaserBean auf diesem GameObject oder Parent
        LaserBean laser = GetComponent<LaserBean>();
        if (laser == null)
        {
            laser = GetComponentInParent<LaserBean>();
        }

        if (laser != null)
        {
            laser.RpcForceUpdate();
        }

    }
}