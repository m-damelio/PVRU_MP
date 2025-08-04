using UnityEngine;
using Fusion;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class LevelManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [Header("Level Management")]
    [SerializeField] private List<GameObject> levelPrefabs = new List<GameObject>();
    [SerializeField] private Transform elevatorInteriorPosZ;
    [SerializeField] private Transform elevatorInteriorNegZ;
    [SerializeField] private float elevatorDoorCloseDelay = 2f;
    [SerializeField] private float levelTransitionDelay = 2f;
    [SerializeField] private bool firstGoalIsPosZ = false;

    [Header("Level Progress")]
    [Networked] public int CurrentLevelIndex {get; set;} 
    [Networked] public bool IsLevelComplete {get; set;} 
    [Networked] public bool ArePlayersInElevator {get; set;}
    [Networked] public bool IsTransitioning {get; set;}
    [Networked] public bool IsPosZElevatorCurrentGoal {get; set;}
    private bool isEntranceLevelUsed = true;

    [Header("Player Tracking")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float elevatorCheckRadius = 2f;

    public System.Action<int> OnLevelChanged;
    public System.Action OnLevelCompleted;
    public System.Action OnNewLevelActive;
    public System.Action OnElevatorDoorsOpened;
    public System.Action OnElevatorDoorsClsoed;
    public System.Action OnTransitionStarted;
    public System.Action OnTransitionCompleted;

    private DoorNetworkedController posZElevator;
    private DoorNetworkedController negZElevator;
    private AudioSource elevatorAudioSource;
    private Transform _currentElevatorTransform;
    [SerializeField] private DoorNetworkedController _currentElevator;
    private Dictionary<PlayerRef, bool> playersInElevator = new Dictionary<PlayerRef, bool>();
    [SerializeField] private List<PlayerRef> connectedPlayers = new List<PlayerRef>();

    private ChangeDetector _changeDetector;

    public override void Spawned()
    {
         _changeDetector = GetChangeDetector(ChangeDetector.Source.SimulationState);
        if (!Object.HasStateAuthority) return;

        //Initial parameters (I.e first level should bre from right to left elevator)
        posZElevator = elevatorInteriorPosZ.GetComponent<DoorNetworkedController>();
        negZElevator = elevatorInteriorNegZ.GetComponent<DoorNetworkedController>();
        elevatorAudioSource = posZElevator.doorAudioSource;

        //Switches the bool before the level is set, since the activate level function will switch the currentElevator and stuff like that
        firstGoalIsPosZ = !firstGoalIsPosZ;
        if (firstGoalIsPosZ)
        {
            _currentElevatorTransform = elevatorInteriorPosZ;
            _currentElevator = posZElevator;
        }
        else
        {
            _currentElevatorTransform = elevatorInteriorNegZ;
            _currentElevator = negZElevator;
        }

        CurrentLevelIndex = 0;
        IsPosZElevatorCurrentGoal = firstGoalIsPosZ;
        ActivateLevel(0);

    }

    public void PlayerJoined(PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            connectedPlayers.Add(player);
            playersInElevator[player] = false;
            Debug.Log($"Added player: {player}. Total players: {Runner.ActivePlayers}");

            if (Runner.ActivePlayers.Count() == 2)
            {
                OnTwoPlayersJoined();
            }
        }
    }

    public void PlayerLeft(PlayerRef player)
    {
        if(Object.HasStateAuthority)
        {
            connectedPlayers.Remove(player);
            playersInElevator.Remove(player);
        }
    }

    private void OnTwoPlayersJoined()
    {
        if (isEntranceLevelUsed)
        {
            StartCoroutine(UpdateActiveAndStartFirst());
        }
    }

    private IEnumerator UpdateActiveAndStartFirst()
    {
        yield return new WaitForSeconds(20f);

        UpdateActiveLevelPrefab();
        UpdateCurrentElevatorReference();
        RPC_CompleteLevel();
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        //handles level completion and elevator logic
        if (IsLevelComplete && !IsTransitioning)
        {
            CheckPlayersInElevator();

            if (ArePlayersInElevator && CanProgressToNextLevel())
            {
                Debug.Log("All conditions met for level transition - starting Transition");
                StartCoroutine(TransitionToNextLevel());
            }
        }
    }

    public override void Render()
    {
        foreach (var changedProperty in _changeDetector.DetectChanges(this))
        {
            if (changedProperty == nameof(CurrentLevelIndex))
            {
                UpdateActiveLevelPrefab();
            }
            if (changedProperty == nameof(IsPosZElevatorCurrentGoal))
            {
                UpdateCurrentElevatorReference();
            }
        }
    }

    private void UpdateActiveLevelPrefab()
    {
        // Deactivate all levels
        for (int i = 0; i < levelPrefabs.Count; i++)
        {
            levelPrefabs[i].SetActive(i == CurrentLevelIndex);
        }

        if (levelPrefabs[CurrentLevelIndex].activeSelf)
        {
            var networkObjects = levelPrefabs[CurrentLevelIndex].GetComponentsInChildren<NetworkObject>(true);
            foreach (var netObj in networkObjects)
            {
                if (!netObj.gameObject.activeSelf)
                {
                    netObj.gameObject.SetActive(true);
                }
            }
        }
    }

    private void UpdateCurrentElevatorReference()
    {
        if (IsPosZElevatorCurrentGoal)
        {
            _currentElevator = posZElevator;
            _currentElevatorTransform = elevatorInteriorPosZ;
            Debug.Log("Switched to PosZ elevator as goal");
        }
        else
        {
            _currentElevator = negZElevator;
            _currentElevatorTransform = elevatorInteriorNegZ;
            Debug.Log("Switched to NegZ elevator as goal");
        }
    }

    private void CheckPlayersInElevator()
    {
        Debug.Log("CheckPlayersInElevator");

        bool allPlayersInElevator = true;
        int playersChecked = 0;
        foreach (var player in connectedPlayers)
        {
            var playerObj = Runner.GetPlayerObject(player);
            if (playerObj != null)
            {
                playersChecked++;
                float distance = Vector3.Distance(playerObj.transform.position, _currentElevatorTransform.position);
                bool isInElevator = distance <= elevatorCheckRadius;

                //Debug.Log($"Player distance to elevator: {distance}, therefor in elevator:{isInElevator}");
                //Debug.Log($"Player position: {playerObj.transform.position}, Elevator position: {_currentElevatorTransform.position}");

                playersInElevator[player] = isInElevator;

                if (!isInElevator)
                {
                    allPlayersInElevator = false;
                }
            }
            else
            {
                Debug.LogWarning($"Player {player} object is null");
                allPlayersInElevator = false;
            }
        }

        bool previousState = ArePlayersInElevator;
        ArePlayersInElevator = allPlayersInElevator && connectedPlayers.Count > 0 && playersChecked > 0;

        if (previousState != ArePlayersInElevator)
        {
            Debug.Log($"ArePlayersInElevator changed from {previousState} to {ArePlayersInElevator}");
            Debug.Log($"Connected players: {connectedPlayers.Count}, Players checked: {playersChecked}");
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_CompleteLevel()
    {
        if(!IsLevelComplete)
        {
            Debug.Log("Level completed!");
            IsLevelComplete = true;
            OnLevelCompleted?.Invoke();

            //Open doornetworkedcontrollers door
            if(_currentElevator != null)
            {
                //Debug.Log($"Opening elevator door: {_currentElevator.gameObject.name}");
                _currentElevator.RequestOpen();
            }

            OnElevatorDoorsOpened?.Invoke();
        }
    }

    private bool CanProgressToNextLevel()
    {
        bool canProgress = CurrentLevelIndex < levelPrefabs.Count - 1;
        Debug.Log($"CanProgressToNextLevel: {canProgress} (current: {CurrentLevelIndex}, max: {levelPrefabs.Count - 1})");
        return canProgress;
    }

    private IEnumerator TransitionToNextLevel()
    {
        Debug.Log("Starting level transition");
        IsTransitioning = true;
        OnTransitionStarted?.Invoke();

        //Short time for them to be ready
        yield return new WaitForSeconds(elevatorDoorCloseDelay);

        //close doors
        if (_currentElevator != null)
        {
            _currentElevator.RequestClose();
        }
        DoorNetworkedController oldElevator = _currentElevator;

        //Play elevator music/animation
        OnElevatorDoorsClsoed?.Invoke();

        //Wait until done
        yield return new WaitForSeconds(levelTransitionDelay);

        //Switch next level
        int nextLevelIndex = CurrentLevelIndex + 1;
        if (nextLevelIndex < levelPrefabs.Count)
        {
            ActivateLevel(nextLevelIndex);
            OnNewLevelActive?.Invoke();
        }

        IsTransitioning = false;
        IsLevelComplete = false;
        OnTransitionCompleted?.Invoke();
        if (oldElevator != null)
        {
            oldElevator.RequestOpen();
        }
        Debug.Log("Level transition complete");
    }

    private void ActivateLevel(int levelIndex)
    {
        if(levelIndex < 0 || levelIndex >= levelPrefabs.Count) return;

        Debug.Log($"Activating level {levelIndex}");
        //Deactivate current level
        if (CurrentLevelIndex < levelPrefabs.Count && CurrentLevelIndex != levelIndex)
        {
            levelPrefabs[CurrentLevelIndex].SetActive(false);
        }

        //Activate new level
        CurrentLevelIndex = levelIndex;
        levelPrefabs[CurrentLevelIndex].SetActive(true);

        //Switch elevator goal
        if (IsPosZElevatorCurrentGoal)
        {
            negZElevator.RequestClose();
            _currentElevator = negZElevator;
            _currentElevatorTransform = elevatorInteriorNegZ;
            IsPosZElevatorCurrentGoal = false;
            Debug.Log("Switched to East elevator as goal");
        }
        else
        {
            posZElevator.RequestClose();
            _currentElevator = posZElevator;
            _currentElevatorTransform = elevatorInteriorPosZ;
            IsPosZElevatorCurrentGoal = true;
            Debug.Log("Switched to West elevator as goal");
        }

        OnLevelChanged?.Invoke(CurrentLevelIndex);

        //ResetPlayerPositions(); ? maybe
    }


    private void ResetPlayerPositions()
    {
        //Get spawn points in new level
        var spawnPoints = levelPrefabs[CurrentLevelIndex].GetComponentsInChildren<PlayerSpawnPoint>();

        int spawnIndex = 0;
        foreach (var player in connectedPlayers)
        {
            var playerObj = Runner.GetPlayerObject(player);
            if (playerObj != null && spawnPoints.Length > spawnIndex)
            {
                playerObj.transform.position = spawnPoints[spawnIndex].transform.position;
                playerObj.transform.rotation = spawnPoints[spawnIndex].transform.rotation;
                spawnIndex++;
            }
        }
    }

    //Public method to restart the level (can be called by components that trigger failure)
    public void RestartCurrentLevel()
    {
        if (Object.HasStateAuthority)
        {
            RPC_RestartCurrentLevel(true);
        }
        else
        {
            RPC_RequestRestartCurrentLevel();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRestartCurrentLevel()
    {
        RPC_RestartCurrentLevel(true);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRestartCurrentLevelWithoutPlayerPos()
    {
        RPC_RestartCurrentLevel(false);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_RestartCurrentLevel(bool restartPosition)
    {
        IsLevelComplete = false;
        IsTransitioning = false;

        //Close elevator doors
        if(_currentElevator != null)
        {
            _currentElevator.RequestClose();
        }

        //Reset player pos
        if (restartPosition) ResetPlayerPositions();

        var levelController = levelPrefabs[CurrentLevelIndex].GetComponent<LevelController>();
        if(levelController != null)
        {
            levelController.RestartLevel();
        }
    }

    //Test methods for debugging
    [ContextMenu("Complete Current Level")]
    public void DebugCompleteLevel()
    {
        RPC_CompleteLevel();
    }

    [ContextMenu("Skip to Next Level")]
    public void DebugSkipLevel()
    {
        if(Object.HasStateAuthority) StartCoroutine(TransitionToNextLevel());
    }

    [ContextMenu("Restart current level")]
    public void DebugRestartLevel()
    {
        RestartCurrentLevel();
    }

    private void OnDrawGizmosSelected()
    {
        if (_currentElevatorTransform != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(_currentElevatorTransform.position, elevatorCheckRadius);
        }
    }

}
