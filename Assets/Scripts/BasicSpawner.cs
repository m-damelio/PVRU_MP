using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;


public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    //[SerializeField] private InputActionReference _moveAction;
    private NetworkRunner _runner;
    private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

    //Store local WASD/stick input.
    private float _moveX;
    private float _moveY;

    private bool _mouseButton0;

    private float joinedCount;

    async void StartGame(GameMode mode)
    {
        //Creates fusion runner and lets it know that user input will be provided
        _runner = gameObject.AddComponent<NetworkRunner>();
        _runner.ProvideInput = true;

        //Create NetworkSceneInfo from current scene
        var scene = SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);
        var sceneInfo = new NetworkSceneInfo();
        if(scene.IsValid) {
            sceneInfo.AddSceneRef(scene, LoadSceneMode.Additive);
        }

        //Start r joing (depends on gamemode) a session with a specific name.
        await _runner.StartGame(new StartGameArgs() 
        {
            GameMode = mode,
            SessionName = "TestRoom",
            Scene = scene,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        });
        joinedCount = 0f;
    }
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { 
        if(runner.IsServer)
        {
            //Create unique posiiton for the player 
            Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1 , 0);
            NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
            networkPlayerObject.gameObject.name = networkPlayerObject.gameObject.name + joinedCount;
            joinedCount++;
            _spawnedCharacters.Add(player, networkPlayerObject);            
        }
    }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) {
        if(_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
        {
            runner.Despawn(networkObject);
            _spawnedCharacters.Remove(player);
        }
     }
    public void OnInput(NetworkRunner runner, NetworkInput input) {
        var data = new NetworkInputData();

        data.direction = new Vector3(_moveX, 0f, _moveY);

        data.buttons.Set(NetworkInputData.MOUSEBUTTON0, _mouseButton0);
        _mouseButton0 = false;

        input.Set(data);
     }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player){ }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data){ }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress){ }

    public void OnMove(InputValue value)
    {
        Vector2 delta = value.Get<Vector2>();
        _moveX = delta.x;
        _moveY = delta.y;
        Debug.Log("X=" + _moveX + "; Y=" + _moveY);
    }
    public void OnFire(InputValue value)
    {
        _mouseButton0 = true;
    }
    private void OnGUI()
    {
        if(_runner == null)
        {
            if(GUI.Button(new Rect(0, 0,200,40), "Host")) StartGame(GameMode.Host);
            if(GUI.Button(new Rect(0,40,200,40), "Join")) StartGame(GameMode.Client);
        }
    }
}
