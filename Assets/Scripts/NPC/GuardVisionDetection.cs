using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GuardVisionDetection : MonoBehaviour
{
    [Header("Vision Settings")]
    [SerializeField] private float visionRange = 12f;
    [SerializeField] private float visionAngle = 60f;
    [SerializeField] private LayerMask playerLayer = 1 << 8;
    [SerializeField] private LayerMask obstacleLayer = 1 << 0;
    [SerializeField] private float detectionTime = 5f;
    [SerializeField] private float scanFrequency = 0.1f;

    [Header("Raycast Settings")]
    [SerializeField] private int raysPerFrame = 5; //Spreads raycast over multiple frames
    [SerializeField] private float playerHeightOffset = 1.7f; //VR Player head height
    [SerializeField] private float[] additionalHeights = {0.5f, 1.0f}; // VR Player head height if crouching 

    private GuardNetworkedController _guardController;
    private Dictionary<GameObject, float> _detectedPlayers = new Dictionary<GameObject, float>();
    private Dictionary<GameObject, Vector3> _lastKnownPositions = new Dictionary<GameObject, Vector3>();

    private int _currentRayIndex = 0;
    private float _lastScanTime = 0f;
    private Collider[] _playerColliders;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _guardController = GetComponent<GuardNetworkedController>();
        _playerColliders = new Collider[10];
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

        UpdateDetectionTimers();
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
            if(!_detectedPlayers.ContainsKey(player))
            {
                _detectedPlayers[player] = 0f;
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
        Vector3 rayOrigin = transform.position + Vector3.up * 1.6f; //ca. guard eye height 
        Vector3 rayDirection = (targetPosition - rayOrigin).normalized;
        float distance = Vector3.Distance(rayOrigin, targetPosition);

        return !Physics.SphereCast(rayOrigin, 0.1f, rayDirection, out RaycastHit hit, distance, obstacleLayer);
    }

    private void UpdateDetectionTimers()
    {
        var playersToAlert = new List<GameObject>();
        var playersToUpdate = new List<GameObject>(_detectedPlayers.Keys); //Copy to avoid modification when iterating through dictionary

        foreach (var player in playersToUpdate)
        {   
            if(player == null)
            {
                playersToAlert.Add(player);
                continue;
            }

            float timeDetected = _detectedPlayers[player];
            _detectedPlayers[player] = timeDetected + Time.deltaTime;
            
            //Trigger alert when detection time is reached
            if(timeDetected >= detectionTime)
            {
                _guardController.RPC_NotifyPlayerSpotted();
                playersToAlert.Add(player);
                //Show alert effect to player 
                ShowDetectionAlert(player);
            }
        }

        //Remove players who triggered guard/alerts
        foreach(var player in playersToAlert)
        {
            RemovePlayerFromDetection(player);
        }
    }

    private void RemovePlayerFromDetection(GameObject player)
    {
        _detectedPlayers.Remove(player);
        _lastKnownPositions.Remove(player);
    }

    private void CleanupDetectedPlayers()
    {
        var playersToRemove = new List<GameObject>();
        foreach (var player in _detectedPlayers.Keys)
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
        foreach (var kvp in _detectedPlayers)
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
