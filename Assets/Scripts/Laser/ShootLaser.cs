using UnityEngine;
using Fusion;

public class ShootLaser : NetworkBehaviour
{
    public Material material;
    private GameObject laserObj;
    private LaserBean beam;

    [Networked] public Vector3 NetworkedPosition { get; set; }
    [Networked] public Vector3 NetworkedDirection { get; set; }

    [SerializeField] private Vector3 lastPosition;
    [SerializeField] private Vector3 lastDirection;

    public override void Spawned()
    {
        Debug.Log($"ShootLaser spawned on: {gameObject.name}");
        // Laser-Objekt für alle Clients erstellen
        CreateLaserObject();
    }

    private void CreateLaserObject()
    {
        if (laserObj == null)
        {
            laserObj = new GameObject("Laser Beam");
            laserObj.transform.SetParent(transform, false);
            beam = laserObj.AddComponent<LaserBean>();
            beam.laserMaterial = material;

            Debug.Log("Laser object created successfully");
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        Vector3 currentPosition = transform.position;
        Vector3 currentDirection = transform.right;

        // Nur bei Änderungen aktualisieren
        if (currentPosition != lastPosition || currentDirection != lastDirection)
        {
            NetworkedPosition = currentPosition;
            NetworkedDirection = currentDirection;
            lastPosition = currentPosition;
            lastDirection = currentDirection;
        }
    }

    public override void Render()
    {
        // Sicherstellen, dass das Laser-Objekt existiert
        if (beam == null && laserObj == null)
        {
            CreateLaserObject();
        }
        // Für alle Clients (auch Non-Authority) den Laser aktualisieren
        if (beam != null && (NetworkedPosition != Vector3.zero || NetworkedDirection != Vector3.zero))
        {
            beam.SetupLaser(NetworkedPosition, NetworkedDirection);
        }
    }

    private void OnDestroy()
    {
        if (laserObj != null)
        {
            DestroyImmediate(laserObj);
        }
    }
}
