using UnityEngine;
using Fusion;

public class LaserSpawner : NetworkBehaviour
{
    [Header("Prefab Reference")]
    [SerializeField] private NetworkObject laserHeadPrefab; // Drag hier das LaserHead Prefab rein

    [Header("Spawn Settings")]
    [SerializeField] private Vector3 spawnPosition = new Vector3(3.63f, 0.733f, -0.32f);
    [SerializeField] private Vector3 spawnRotation = new Vector3(0, 180, 0);

    private NetworkObject spawnedLaserHead;

    public override void Spawned()
    {
        // Nur der Host/StateAuthority spawnt das LaserHead
        if (Object.HasStateAuthority)
        {
            SpawnLaserHead();
        }
    }

    private void SpawnLaserHead()
    {
        if (laserHeadPrefab != null && spawnedLaserHead == null)
        {
            Quaternion rotation = Quaternion.Euler(spawnRotation);

            // Spawn das LaserHead über das Netzwerk
            spawnedLaserHead = Runner.Spawn(laserHeadPrefab, spawnPosition, rotation);

            Debug.Log($"LaserHead spawned at position: {spawnPosition}");
        }
    }
}