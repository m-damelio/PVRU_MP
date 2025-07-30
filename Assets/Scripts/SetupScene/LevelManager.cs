using UnityEngine;
using Fusion;
using System.Collections;
using System.Collections.Generic;

public class LevelManager : NetworkBehaviour, IPlayerJoined, IPlayerLeft
{
    [Header("Level Management")]
    [SerializeField] private List<GameObject> levelPrefabs = new List<GameObject>();
    [SerializeField] private Transform elevatorInteriorEast;
    [SerializeField] private Transform elevatorInteriorWest;
    [SerializeField] private float elevatorDoorCloseDelay = 2f;
    [SerializeField] private float levelTransitionDelay = 2f;

    [Header("Level Progress")]
    [Networked] public int CurrentLevelIndex {get; set;} 
    [Networked] public bool IsLevelComplete {get; set;} 
    [Networked] public bool ArePlayersInElevator {get; set;}
    [Networked] public bool IsTransitioning {get; set;}
    [Networked] public bool IsWestElevatorCurrentGoal {get; set;}

    [Header("Player Tracking")]
    [SerializeField] private LayerMask playerLayer;
    [SerializeField] private float elevatorCheckRadius = 2f;

    public System.Action<int> OnLevelChanged;
    public System.Action OnLevelCompleted;
    public System.Action OnElevatorDoorsOpened;
    public System.Action OnElevatorDoorsClsoed;
    public System.Action OnTransitionStarted;
    public System.Action OnTransitionCompleted;

    private DoorNetworkedController eastElevator;
    private DoorNetworkedController westElevator;
    private AudioSource elevatorAudioSource;
    private Transform _currentElevatorTransform;
    [SerializeField] private DoorNetworkedController _currentElevator;
    private Dictionary<PlayerRef, bool> playersInElevator = new Dictionary<PlayerRef, bool>();
    [SerializeField] private List<PlayerRef> connectedPlayers = new List<PlayerRef>();

    public override void Spawned()
    {
        //Initial parameters (I.e first level should bre from right to left elevator)
        eastElevator = elevatorInteriorEast.GetComponent<DoorNetworkedController>();
        westElevator = elevatorInteriorWest.GetComponent<DoorNetworkedController>();
        elevatorAudioSource = eastElevator.doorAudioSource;
        _currentElevatorTransform = elevatorInteriorWest;
        _currentElevator = westElevator;
        IsWestElevatorCurrentGoal = true;

        if (Object.HasStateAuthority)
        {
            ActivateLevel(0);
        }
    }

    public void PlayerJoined(PlayerRef player)
    {
        if (Object.HasStateAuthority)
        {
            connectedPlayers.Add(player);
            playersInElevator[player] = false;
            Debug.Log($"Added player: {player}");
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

    public override void FixedUpdateNetwork()
    {
        if(!Object.HasStateAuthority) return;
        
        //Check if players are in elevator
        CheckPlayersInElevator();

        //handles level completion and elevator logic
        if (IsLevelComplete && !IsTransitioning)
        {
            if (ArePlayersInElevator && CanProgressToNextLevel())
            {
                StartCoroutine(TransitionToNextLevel());
            }
        }
    }

    private void CheckPlayersInElevator()
    {
        //No need to check if level isn't complete
        if(!IsLevelComplete) return;

        bool allPlayersInElevator = true;
        foreach (var player in connectedPlayers)
        {
            var playerObj = Runner.GetPlayerObject(player);
            if(playerObj != null)
            {
                
                float distance = Vector3.Distance(playerObj.transform.position, _currentElevatorTransform.position);
                bool isInElevator = distance <= elevatorCheckRadius;
                Debug.Log($"Player distance to elevator: {distance}, therefor in elevator:{isInElevator}");
                playersInElevator[player] = isInElevator;

                if(!isInElevator)
                {
                    allPlayersInElevator = false;
                }
            }  
            else
            {
                allPlayersInElevator = false;
            } 
        }

        ArePlayersInElevator = allPlayersInElevator && connectedPlayers.Count > 0;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_CompleteLevel()
    {
        if(!IsLevelComplete)
        {
            IsLevelComplete = true;
            OnLevelCompleted?.Invoke();

            //Open doornetworkedcontrollers door
            if(_currentElevator != null)
            {
                _currentElevator.RequestOpen();
            }

            OnElevatorDoorsOpened?.Invoke();
        }
    }

    private bool CanProgressToNextLevel()
    {
        return CurrentLevelIndex < levelPrefabs.Count - 1;
    }

    private IEnumerator TransitionToNextLevel()
    {
        IsTransitioning = true;
        OnTransitionStarted?.Invoke();

        //Short time for them to be ready
        yield return new WaitForSeconds(elevatorDoorCloseDelay);
        
        //close doors
        if(_currentElevator != null)
        {
            _currentElevator.RequestClose();
        }

        //Play elevator music/animation
        OnElevatorDoorsClsoed?.Invoke();
        //Wait until done
        yield return new WaitForSeconds(levelTransitionDelay);

        //Switch next level
        int nextLevelIndex = CurrentLevelIndex +1;
        if(nextLevelIndex < levelPrefabs.Count)
        {
            ActivateLevel(nextLevelIndex);
        }

        IsTransitioning = false;
        IsLevelComplete = false;
        OnTransitionCompleted?.Invoke();
    }

    private void ActivateLevel(int levelIndex)
    {
        if(levelIndex < 0 || levelIndex >= levelPrefabs.Count) return;

        //Deactivate current level
        if(CurrentLevelIndex < levelPrefabs.Count)
        {
            levelPrefabs[CurrentLevelIndex].SetActive(false);
        }

        //Activate new level
        CurrentLevelIndex = levelIndex;
        levelPrefabs[CurrentLevelIndex].SetActive(true);

        //Switch elevator goal
        if(IsWestElevatorCurrentGoal)
        {
            eastElevator.RequestClose();
            _currentElevator = eastElevator;
            _currentElevatorTransform = elevatorInteriorEast;
            IsWestElevatorCurrentGoal = false;
        }
        else
        {
            westElevator.RequestClose();
            _currentElevator = westElevator;
            _currentElevatorTransform = elevatorInteriorWest;
            IsWestElevatorCurrentGoal = true;
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
            if(playerObj != null && spawnPoints.Length > spawnIndex)
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
            RPC_RestartCurrentLevel();
        }
        else
        {
            RPC_RequestRestartCurrentLevel();
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRestartCurrentLevel()
    {
        RPC_RestartCurrentLevel();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_RestartCurrentLevel()
    {
        IsLevelComplete = false;
        IsTransitioning = false;

        //Close elevator doors
        if(_currentElevator != null)
        {
            _currentElevator.RequestClose();
        }

        //Reset player pos
        ResetPlayerPositions();

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
