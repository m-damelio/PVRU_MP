using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Fusion;

public class LaserBean : NetworkBehaviour
{

    private LineRenderer laser;
    public Material laserMaterial;

    public bool laserNeedsChange = false;

    [Networked] public Vector3 NetworkedStartPosition { get; set; }
    [Networked] public Vector3 NetworkedDirection { get; set; }
    [Networked] public bool NetworkedIsHittingLoadTarget { get; set; }
    [Networked] public bool NetworkedIsHittingTarget { get; set; }
    [Networked] public NetworkId NetworkedLoadTargetId { get; set; }
    [Networked] public NetworkId NetworkedTargetId { get; set; }

    public chargeBehavior charge;
    public openDoor openD;

    [SerializeField]
    private List<Vector3> laserIndices = new List<Vector3>();

    public GameObject door;
    private bool wasHittingLoadTarget = false;
    private GameObject loadTargetHit;
    private GameObject targetHit;

    private GameObject laserHitInstance;
    private Material hitMaterial;

    private int hitCount;
    private int maxHit = 10;

    // Cached values f�r Performance
    private Vector3 lastStartPos;
    private Vector3 lastDirection;

    void Start()
    {
        Debug.Log($"LaserBean started on: {gameObject.name}");
        SetupLaserVisuals();
    }

    //Set up vom laser und der kleinen Kugel am Ende, das passiert einmal am Anfang
    void SetupLaserVisuals()
    {
        if (laser == null)
        {
            laser = GetComponent<LineRenderer>();
            if (laser == null)
            {
                laser = gameObject.AddComponent<LineRenderer>();
            }

            laser.startWidth = 0.05f;
            laser.endWidth = 0.05f;
            laser.material = laserMaterial;
            laser.startColor = Color.yellow;
            laser.endColor = Color.yellow;
            laser.useWorldSpace = false;
        }

        if (laserHitInstance == null)
        {
            laserHitInstance = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            laserHitInstance.transform.SetParent(this.transform);
            laserHitInstance.transform.localScale = Vector3.one * 0.2f;

            // Entferne den Collider, den Unity automatisch hinzuf�gt
            Destroy(laserHitInstance.GetComponent<Collider>());

            // Erzeuge ein Material mit Emission
            hitMaterial = new Material(Shader.Find("Unlit/Color"));
            hitMaterial.color = Color.yellow;
            hitMaterial.EnableKeyword("_EMISSION");
            laserHitInstance.GetComponent<MeshRenderer>().material = hitMaterial;

            // Deaktiviere initial
            laserHitInstance.SetActive(false);
        }
    }

    /*public void SetMaterial(Material mat)
    {
        laserMaterial = mat;
        if (laser != null)
        {
            laser.material = laserMaterial;
        }
    }*/

    // Diese Methode wird vom ShootLaser Script aufgerufen
    public void SetupLaser(Vector3 startPos, Vector3 dir)
    {
        if (HasStateAuthority)
        {
            NetworkedStartPosition = startPos;
            NetworkedDirection = dir;
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (HasStateAuthority)
        {
            // Nur Authority berechnet die Laser-Physik
            if (laserNeedsChange = true || NetworkedStartPosition != lastStartPos || NetworkedDirection != lastDirection)
            {
                CalculateLaserPath();
                lastStartPos = NetworkedStartPosition;
                lastDirection = NetworkedDirection;
                laserNeedsChange = false;
            }
        }
    }

    public override void Render()
    {
        // Alle Clients rendern basierend auf den networked values
        if (laser == null)
        {
            SetupLaserVisuals();
        }

        // Pr�fe auf �nderungen in den Load Target States
        HandleLoadTargetStateChange();
        HandleTargetStateChange();

        // Aktualisiere den Laser visuell
        if (NetworkedStartPosition != Vector3.zero || NetworkedDirection != Vector3.zero)
        {
            UpdateLaserBeam();
        }
    }

    private void HandleLoadTargetStateChange()
    {
        if (NetworkedIsHittingLoadTarget != wasHittingLoadTarget)
        {
            if (NetworkedIsHittingLoadTarget && !wasHittingLoadTarget)
            {
                Debug.Log("Laser trifft jetzt LoadTarget. starte Fade");
                GameObject loadTarget = FindLoadTargetById(NetworkedLoadTargetId);
                if (loadTarget != null)
                {
                    charge = loadTarget.GetComponent<chargeBehavior>();
                    if (charge != null)
                    {
                        StartCoroutine(charge.ColourFade());
                    }
                }
            }
            else if (!NetworkedIsHittingLoadTarget && wasHittingLoadTarget)
            {
                Debug.Log("Laser trifft LoadTarget nicht mehr. starte DeFade");
                if (charge != null)
                {
                    StartCoroutine(charge.ColourDeFade());
                }
            }
            wasHittingLoadTarget = NetworkedIsHittingLoadTarget;
        }
    }

    private void HandleTargetStateChange()
    {
        if (NetworkedIsHittingTarget)
        {
            GameObject target = FindTargetById(NetworkedTargetId);
            if (target != null)
            {
                openD = target.GetComponent<openDoor>();
                if (openD != null)
                {
                    StartCoroutine(openD.Open());
                }
            }
        }
    }

    private GameObject FindLoadTargetById(NetworkId id)
    {
        if (!id.IsValid) return null;

        var networkObj = Runner.FindObject(id);
        return networkObj?.gameObject;
    }

    private GameObject FindTargetById(NetworkId id)
    {
        if (!id.IsValid) return null;

        var networkObj = Runner.FindObject(id);
        return networkObj?.gameObject;
    }

    //Autoritative Laserberechnung: F�hrt die physikalische Berechnung des Laserpfads aus (inkl.Spiegelung, Treffer, Netzwerk-Update). Wird nur von der Authority ausgef�hrt.
    private void CalculateLaserPath()
    {
        hitCount = 0;
        laserIndices.Clear();

        bool isHittingLoadTarget = false;
        bool isHittingTarget = false;
        NetworkId loadTargetId = default(NetworkId);
        NetworkId targetId = default(NetworkId);

        CastRay(NetworkedStartPosition, NetworkedDirection, true, ref isHittingLoadTarget, ref isHittingTarget, ref loadTargetId, ref targetId);

        // Netzwerk-States aktualisieren
        NetworkedIsHittingLoadTarget = isHittingLoadTarget;
        NetworkedIsHittingTarget = isHittingTarget;
        NetworkedLoadTargetId = loadTargetId;
        NetworkedTargetId = targetId;
    }

    // Lokale Berechnung f�r Visual Updates (alle Clients) zeichnet den Laser strahl
    void UpdateLaserBeam()
    {
        
        hitCount = 0;
        laserIndices.Clear();

        bool tempLoadTarget = false;
        bool tempTarget = false;
        NetworkId tempLoadId = default(NetworkId);
        NetworkId tempTargetId = default(NetworkId);

        CastRay(NetworkedStartPosition, NetworkedDirection, true, ref tempLoadTarget, ref tempTarget, ref tempLoadId, ref tempTargetId);
        UpdateLineRenderer();

        if (laserHitInstance != null && laserIndices.Count > 0)
        {
            laserHitInstance.transform.position = laserIndices[laserIndices.Count - 1];
        }
    }

    void CastRay(Vector3 pos, Vector3 dir, bool isFirstSegment, ref bool isHittingLoadTarget, ref bool isHittingTarget, ref NetworkId loadTargetId, ref NetworkId targetId)
    {
        laserIndices.Add(pos);
        Ray ray = new Ray(pos, dir);

        if (Physics.Raycast(ray, out RaycastHit hit, 30f, ~0))
        {
            laserIndices.Add(hit.point);

            if (laserHitInstance != null)
            {
                laserHitInstance.transform.position = hit.point;
                laserHitInstance.SetActive(true);
            }

            if (hit.collider.CompareTag("Mirror"))
            {
              
                if (hitCount >= maxHit)
                {
                    return;
                }
                else
                {
                    hitCount++;
                    Vector3 reflected = Vector3.Reflect(dir, hit.normal);
                    CastRay(hit.point, reflected, true, ref isHittingLoadTarget, ref isHittingTarget, ref loadTargetId, ref targetId);
                }
            }
            else if (hit.collider.CompareTag("LoadTarget"))
            {
                isHittingLoadTarget = true;
                loadTargetHit = hit.collider.gameObject;

                // Versuche NetworkObject zu finden
                var networkObj = hit.collider.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    loadTargetId = networkObj.Id;
                }

                charge = hit.collider.gameObject.GetComponent<chargeBehavior>();
            }
            else if (hit.collider.CompareTag("Target"))
            {
                Debug.Log("Target hit");
                isHittingTarget = true;
                targetHit = hit.collider.gameObject;

                // Versuche NetworkObject zu finden
                var networkObj = hit.collider.GetComponent<NetworkObject>();
                if (networkObj != null)
                {
                    targetId = networkObj.Id;
                }

                openD = hit.collider.gameObject.GetComponent<openDoor>();

                //Versuche die T�r aufzumachen
                if(door != null) door.GetComponent<DoorNetworkedController>().RequestOpen();
            }
        }
        else
        {
            laserIndices.Add(ray.GetPoint(30));

            if (laserHitInstance != null)
            {
                laserHitInstance.SetActive(false);
            }
        }
    }

    void UpdateLineRenderer()
    {
        if (laser != null && laserIndices.Count > 0)
        {
            laser.positionCount = laserIndices.Count;
            for (int i = 0; i < laserIndices.Count; i++)
            {
                Vector3 localP = transform.InverseTransformPoint(laserIndices[i]);
                laser.SetPosition(i, localP);
            }
        }
    }

    // Diese Methode kann von Spiegeln aufgerufen werden
    public void ForceUpdate()
    {
        if (HasStateAuthority)
        {
            //CalculateLaserPath();
            laserNeedsChange = true;
        }
    }

    // RPC f�r externe Updates (z.B. von Spiegeln)
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RpcForceUpdate()
    {
        ForceUpdate();
    }
}