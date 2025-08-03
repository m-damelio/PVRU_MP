using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GuardVisionDetection : MonoBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 6f;
    [SerializeField] private float visionAngle = 45f;
    [SerializeField] private LayerMask playerLayer = 1 << 8;
    [SerializeField] private LayerMask obstacleLayer = 1 << 0;
    [SerializeField] private float timeToAlert = 2.5f;
    [SerializeField] private float scanFrequency = 0.1f;
    [SerializeField] private Transform eyeHeight;

    [Header("Raycast Settings")]
    [SerializeField] private int raysPerFrame = 5; //Spreads raycast over multiple frames
    [SerializeField] private float playerHeightOffset = 1.7f; //VR Player head height
    [SerializeField] private float[] additionalHeights = {0.5f, 1.0f}; // VR Player head height if crouching 

    [Header("Vision Visualization Settings")]
    [SerializeField] private bool useRadarSweep = true;
    [SerializeField] private Color normalVisionColor = new Color(0.8f, 0.8f, 0.2f, 0.3f); //Yellow
    [SerializeField] private Color warningVisionColor = new Color(1f, 0.5f, 0f, 0.6f); //orange
    [SerializeField] private Color alertVisionColor = new Color(0.8f, 0.2f, 0.2f, 0.5f); //Red
    [SerializeField] private LineRenderer radarSweepLine;
    [SerializeField] private float sweepSpeed = 30f; //degrees per second
    [SerializeField] private int sweepResolution = 20; //points in sweep line
    [SerializeField] private float groundLevel = 0f; //ground level
    private Gradient gradientNormalColor;
    private Gradient gradientWarningColor;
    private Gradient gradientAlertColor;


    private GuardNetworkedController _guardController;
    private Dictionary<GameObject, float> _playersInVision = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, Vector3> _lastKnownPositions = new Dictionary<GameObject, Vector3>();

    private int _currentRayIndex = 0;
    private float _lastScanTime = 0f;
    private Collider[] _playerColliders;

    private float _currentSweepAngle = 0f;
    private bool _sweepingRight = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _guardController = GetComponent<GuardNetworkedController>();
        _playerColliders = new Collider[10];

        if(radarSweepLine == null)
        {
            useRadarSweep = false;
        }

        ConfigureRadarSweep();
    }

    // Update is called once per frame
    void Update()
    {

        if (!_guardController.IsSpawnedAndValid) return;

        if (_guardController.IsAlert || Time.time - _lastScanTime >= scanFrequency)
        {
            ScanForPlayers();
            _lastScanTime = Time.time;
        }

        UpdateDetectionAndTriggerAlert();
        UpdateRadarSweep();
    }

    private void ScanForPlayers()
    {
        //Sphere check to find nearby players
        int playerCount = Physics.OverlapSphereNonAlloc(transform.position, visionRange, _playerColliders, playerLayer);

        //Processes a few players per frame to spread load
        int playersToCheck = Mathf.Min(raysPerFrame, playerCount);

        for (int i = 0; i < playersToCheck; i++)
        {
            int playerIndex = (_currentRayIndex + i) %playerCount;
            if(_playerColliders[playerIndex] == null) continue;

            CheckPlayerVisibility(_playerColliders[playerIndex]);
        }

        _currentRayIndex = (_currentRayIndex + playersToCheck) % Mathf.Max(1, playerCount);

        //Clean up null references
        CleanupDetectedPlayers();
    }

    private void CheckPlayerVisibility(Collider playerCollider)
    {
        GameObject player = playerCollider.gameObject;
        Vector3 playerPosition = playerCollider.transform.position;

        //check if player is in vision cone angle
        Vector3 directionToPlayer = (playerPosition - transform.position).normalized;
        float angle = Vector3.Angle(transform.forward, directionToPlayer);

        if(angle > visionAngle * 0.5f)
        {
            //Player outside of "vision cone"
            RemovePlayerFromDetection(player);
            return;
        }

        //Check multiple heights for VRPlayer
        bool playerVisible = false;

        Vector3 headPosition = playerPosition + Vector3.up * playerHeightOffset;
        if(HasLineOfSight(headPosition))
        {
            playerVisible = true;
        }
        else
        {
            foreach (float height in additionalHeights)
            {
                Vector3 checkPosition = playerPosition + Vector3.up * height;
                if(HasLineOfSight(checkPosition))
                {
                    playerVisible = true;
                    break;
                }
            }
        }

        if(playerVisible)
        {
            if(!_playersInVision.ContainsKey(player))
            {
                _playersInVision[player] = 0f;
            }
            _lastKnownPositions[player] = playerPosition;
        }
        else
        {
            RemovePlayerFromDetection(player);
        }

    }

    private bool HasLineOfSight(Vector3 targetPosition)
    {
        Vector3 rayOrigin = transform.position + Vector3.up * eyeHeight.position.y;
        Vector3 rayDirection = (targetPosition - rayOrigin).normalized;
        float distance = Vector3.Distance(rayOrigin, targetPosition);

        return !Physics.SphereCast(rayOrigin, 0.1f, rayDirection, out RaycastHit hit, distance, obstacleLayer);
    }

    private void UpdateDetectionAndTriggerAlert()
    {
        var playersToAlert = new List<GameObject>();
        var playersToUpdate = new List<GameObject>(_playersInVision.Keys); //Copy to avoid modification when iterating through dictionary

        foreach (var player in playersToUpdate)
        {   
            if(player == null)
            {
                playersToAlert.Add(player);
                continue;
            }

            float timeDetected = _playersInVision[player];
            _playersInVision[player] = timeDetected + Time.deltaTime;

            //Trigger alert when detection time is reached
            if (timeDetected >= timeToAlert)
            {
                if (_guardController.Object.HasStateAuthority)
                {
                    _guardController.HandlePlayerDetected();
                }
                break; 
            }
        }

        //Remove players who triggered guard/alerts
        foreach(var player in playersToAlert)
        {
            RemovePlayerFromDetection(player);
        }
    }

    private IEnumerator RestartLevelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        FindObjectOfType<LevelManager>()?.RestartCurrentLevel();
    }

    private void RemovePlayerFromDetection(GameObject player)
    {
        _playersInVision.Remove(player);
        _lastKnownPositions.Remove(player);
    }

    private void CleanupDetectedPlayers()
    {
        var playersToRemove = new List<GameObject>();
        foreach (var player in _playersInVision.Keys)
        {
            if(player == null)
            {
                playersToRemove.Add(player);
            }
        }

        foreach( var player in playersToRemove)
        {
            RemovePlayerFromDetection(player);
        }
    }

    private void ShowDetectionAlert(GameObject player)
    {
        Debug.Log($"Guard spotted player: {player.name}");
        //TODO add ui effect for players when they trigger a guard

         GameOverOverlayController overlay = FindObjectOfType<GameOverOverlayController>();
        if (overlay != null)
        {
            overlay.ShowGameOver();
        }
    }

    private void ConfigureRadarSweep()
    {
        if(radarSweepLine == null) return;

        //Set colors as in particle system
        gradientNormalColor = new Gradient();
        gradientNormalColor.SetKeys(
            new GradientColorKey[] { new GradientColorKey(normalVisionColor, 0f), new GradientColorKey(normalVisionColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(normalVisionColor.a, 0f), new GradientAlphaKey(normalVisionColor.a * 0.5f, 1f) }
        );

        gradientAlertColor = new Gradient();
        gradientAlertColor.SetKeys(
            new GradientColorKey[] { new GradientColorKey(alertVisionColor, 0f), new GradientColorKey(alertVisionColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(alertVisionColor.a, 0f), new GradientAlphaKey(alertVisionColor.a * 0.5f, 1f) }
        );

        gradientWarningColor = new Gradient();
        gradientWarningColor.SetKeys(
            new GradientColorKey[] { new GradientColorKey(warningVisionColor, 0f), new GradientColorKey(warningVisionColor, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(warningVisionColor.a, 0f), new GradientAlphaKey(warningVisionColor.a * 0.5f, 1f) }
        );

        radarSweepLine.colorGradient = gradientNormalColor;
        radarSweepLine.startWidth = 0.05f;
        radarSweepLine.endWidth = 0.2f;
        radarSweepLine.positionCount = sweepResolution;
        radarSweepLine.useWorldSpace = false;

        //Init sweep angle and visibility
        _currentSweepAngle = -visionAngle * 0.5f;
        radarSweepLine.enabled = useRadarSweep;
    }

    private void UpdateRadarSweep()
    {
        if(radarSweepLine == null || !useRadarSweep) return;

        float angleStep = sweepSpeed * Time.deltaTime;

        if(_sweepingRight)
        {
            _currentSweepAngle += angleStep;
            if(_currentSweepAngle >= visionAngle * 0.5f)
            {
                _currentSweepAngle = visionAngle *0.5f;
                _sweepingRight = false;
            }
        }
        else
        {
            _currentSweepAngle -= angleStep;
            if(_currentSweepAngle <= -visionAngle * 0.5f)
            {
                _currentSweepAngle = -visionAngle * 0.5f;
                _sweepingRight = true;
            }
        }

        Gradient currentGradient = gradientNormalColor;
        if(_guardController.IsAlert)
        {
            currentGradient = gradientAlertColor;
        }
        else if(_playersInVision.Count > 0)
        {
            currentGradient = gradientWarningColor;
        }
        radarSweepLine.colorGradient = currentGradient;

        GenerateSweepLine();
    }

    private void GenerateSweepLine()
    {
        Vector3[] points = new Vector3[sweepResolution];
        Vector3 eyePosition = Vector3.zero;
        Vector3 wordlEyePosition = transform.position + Vector3.up * eyeHeight.position.y;

        Vector3 sweepDirection = Quaternion.Euler(0, _currentSweepAngle, 0) * Vector3.forward;
        
        bool hitObstacle = false;
        int actualPointCount = sweepResolution;
        float groundOffset = groundLevel - eyeHeight.position.y;

        //check if obstacle along sweep line
        Vector3 fullSweepTarget = transform.position + sweepDirection * visionRange + Vector3.up * groundOffset;
        Vector3 sweepRayDirection = (fullSweepTarget-wordlEyePosition).normalized;
        float maxSweepDistance = Vector3.Distance(wordlEyePosition, fullSweepTarget);

        float obstacleDistance = visionRange;
        if(Physics.SphereCast(wordlEyePosition, 0.1f, sweepRayDirection, out RaycastHit hit, maxSweepDistance, obstacleLayer))
        {
            hitObstacle = true;
            obstacleDistance = hit.distance;
        }

        for(int i = 0; i < sweepResolution; i++)
        {
            float t = (float) i / (sweepResolution -1);

            //Distance along sweep
            float distance = t * visionRange;

            if(hitObstacle && distance > obstacleDistance)
            {
                actualPointCount = i;
                break;
            }

            //Height interpolation from eye level to ground
            float height = Mathf.Lerp(0f, groundOffset, t);

            Vector3 position = sweepDirection * distance + Vector3.up * height;

            //Adds noise except at the end 
            if(!hitObstacle || distance < obstacleDistance * 0.9f)
            {
                position += Random.insideUnitSphere * 0.1f *t;
            }

            points[i] = position;

        }

        //Update line renderer with actual hit count
        radarSweepLine.positionCount = actualPointCount;

        if(actualPointCount > 0)
        {
            Vector3[] finalPoints = new Vector3[actualPointCount];
            for(int i = 0; i < actualPointCount; i++)
            {
                finalPoints[i] = points[i];
            }
            radarSweepLine.SetPositions(finalPoints);
        }
        

    }

    public void ToggleRadarSweep(bool enabled)
    {
        useRadarSweep = enabled;
    }

    private void OnDrawGizmos()
    {
        if(!Application.isPlaying) return;

        //Draw vision cone
        Gizmos.color = Color.yellow;
        Vector3 leftBoundary = Quaternion.Euler(0, -visionAngle * 0.5f, 0) * transform.forward * visionRange;
        Vector3 rightBoundary = Quaternion.Euler(0, visionAngle * 0.5f, 0) * transform.forward * visionRange;

        Gizmos.DrawLine(transform.position, transform.position + leftBoundary);
        Gizmos.DrawLine(transform.position, transform.position + rightBoundary);

        //Draw detection rays
        Gizmos.color = Color.red;
        foreach (var kvp in _playersInVision)
        {
            if(kvp.Key != null && _lastKnownPositions.ContainsKey(kvp.Key))
            {
                Gizmos.DrawLine(transform.position, _lastKnownPositions[kvp.Key]);
                Gizmos.DrawWireSphere(_lastKnownPositions[kvp.Key], 0.3f);
            }
        }

        //Draw vision range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, visionRange);
    }
}
