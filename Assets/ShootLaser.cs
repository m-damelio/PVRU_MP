using UnityEngine;

public class ShootLaser : MonoBehaviour
{
    public Material material;
    private GameObject laserObj;
    private LaserBean beam;

    [SerializeField]
    private Vector3 lastPosition;
    private Vector3 lastDirection;

    private void Start()
    {
        
    }

    void Update()
    {
        // Aktuelle Position des Lasers
        Vector3 currentPosition = transform.position;
        Vector3 currentDirection = transform.right;

        //Laser nur einmal erstellen
        if (laserObj == null)
        {
            laserObj = new GameObject("Laser Beam");
            laserObj.transform.SetParent(transform, false);
            beam = laserObj.AddComponent<LaserBean>();

        }
        
        if(currentPosition != lastPosition || currentDirection != lastDirection)
        {
            beam.Setup(currentPosition, currentDirection, material);
            lastPosition = currentPosition;
            lastDirection = currentDirection;
        }
    }
}

