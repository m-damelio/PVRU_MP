using UnityEngine;
using System.Collections.Generic;

public class LaserBean : MonoBehaviour
{
    public Material laserMaterial;
    public Vector3 startPosition;
    public Vector3 direction;

    private LineRenderer laser;
    public chargeBehavior charge;

    [SerializeField]
    private List<Vector3> laserIndices = new List<Vector3>();

    private bool isHittingLoadTarget = false;
    private bool wasHittingLoadTarget = false;
    private GameObject loadTargetHit;

    void Start()
    {
 
    }

    public void Setup(Vector3 startPos, Vector3 direction, Material mat)
    {
        this.startPosition = startPos;
        this.direction = direction;
        this.laserMaterial = mat;

        if (laser == null)
        {
            laser = gameObject.AddComponent<LineRenderer>();
            laser.startWidth = 0.1f;
            laser.endWidth = 0.1f;
            laser.material = laserMaterial;
            laser.startColor = Color.cyan;
            laser.endColor = Color.cyan;
            laser.useWorldSpace = false;
        }

        laserIndices.Clear();
        wasHittingLoadTarget = isHittingLoadTarget; // Merke alten Status
        isHittingLoadTarget = false;
        loadTargetHit = null;

        CastRay(startPosition, direction, true);
        UpdateLaser();

        // Nach der Strahlverfolgung prüfen, ob der Zustand sich geändert hat
        if (isHittingLoadTarget && !wasHittingLoadTarget)
        {
            Debug.Log("Laser trifft jetzt LoadTarget. starte Fade");
            if (charge != null)
            {
                StartCoroutine(charge.ColourFade());
            }
        }
        else if (!isHittingLoadTarget && wasHittingLoadTarget)
        {
            Debug.Log("Laser trifft LoadTarget nicht mehr. starte DeFade");
            if (charge != null)
            {
                StartCoroutine(charge.ColourDeFade());
            }
        }
    }



    void CastRay(Vector3 pos, Vector3 dir, bool isFirstSegment)
    {
        laserIndices.Add(pos);
        Ray ray = new Ray(pos, dir);

        if (Physics.Raycast(ray, out RaycastHit hit, 30f, ~0))
        {
            laserIndices.Add(hit.point);

            if (hit.collider.CompareTag("Mirror"))
            {
                Vector3 reflected = Vector3.Reflect(dir, hit.normal);
                CastRay(hit.point, reflected, true);
            }
            else if (hit.collider.CompareTag("LoadTarget"))
            {
                isHittingLoadTarget = true;
                loadTargetHit = hit.collider.gameObject;
                charge = hit.collider.gameObject.GetComponent<chargeBehavior>();
            }
        }
        else
        {
            laserIndices.Add(ray.GetPoint(30));
        }
    }

    void UpdateLaser()
    {
        laser.positionCount = laserIndices.Count;
        for (int i = 0; i < laserIndices.Count; i++)
        {
            Vector3 localP = transform.InverseTransformPoint(laserIndices[i]);
            laser.SetPosition(i, localP);
        }
    }

}
