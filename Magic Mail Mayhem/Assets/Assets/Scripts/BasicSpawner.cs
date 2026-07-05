using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner _gameRunner;
    private NetworkRunner _lobbyRunner;

    public GameObject lobbyObject = new GameObject("LobbyRunner");


    private List<SessionInfo> _availableSessions = new();

    [SerializeField]
    private NetworkPrefabRef _playerPrefab;

    public GameObject _startMenu;
    public GameObject _loadingMenu;

    private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();

    private static bool IsPressed(Key key)
    {
        var keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].isPressed;
    }

    private async void Start()
    {
        _startMenu.SetActive(false);
        _loadingMenu.SetActive(true);

        _lobbyRunner = lobbyObject.AddComponent<NetworkRunner>();
        _lobbyRunner.AddCallbacks(this);

        var result = await _lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);

        Debug.Log($"Lobby joined: {result.Ok}");

        _startMenu.SetActive(true);
        _loadingMenu.SetActive(false);
    }

    private async Task StartGame(GameMode mode, string sessionName)
    {
        _startMenu.SetActive(false);
        _loadingMenu.SetActive(true);

        if (_lobbyRunner != null)
        {
            await _lobbyRunner.Shutdown();
            Destroy(_lobbyRunner);
            _lobbyRunner = null;
        }

        _gameRunner = gameObject.AddComponent<NetworkRunner>();

        _gameRunner.ProvideInput = true;
        _gameRunner.AddCallbacks(this);

        var scene =
            SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

        var result =
            await _gameRunner.StartGame(new StartGameArgs
            {
                GameMode = mode,
                SessionName = sessionName,
                PlayerCount = 2,
                Scene = scene,
                SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
            });

        if (!result.Ok)
        {
            Debug.LogError(result.ShutdownReason);
            return;
        }

        _loadingMenu.SetActive(false);
    }

    public async void HostGame()
    {
        await StartGame(GameMode.Host, $"Room_{Guid.NewGuid()}");
    }

    public async void JoinGame()
    {
        SessionInfo room = null;

        foreach (var s in _availableSessions)
        {
            if (s.IsOpen &&
                s.PlayerCount < 2)
            {
                room = s;
                break;
            }
        }

        if (room == null)
        {
            Debug.Log("No available room.");
            return;
        }

        await StartGame(GameMode.Client, room.Name);
    }

    // private void OnGUI()
    // {
    //     if (_gameRunner != null)
    //         return;

    //     if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
    //         HostGame();

    //     if (GUI.Button(new Rect(0, 50, 200, 40), "Join"))
    //         JoinGame();
    // }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        Vector3 spawnPosition =
            new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);

        NetworkObject obj = runner.Spawn(
            _playerPrefab,
            spawnPosition,
            Quaternion.identity,
            player);

        _spawnedCharacters.Add(player, obj);

        if (runner.ActivePlayers.Count() >= 2)
        {
            runner.SessionInfo.IsOpen = false;
            Debug.Log("Room closed.");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedCharacters.TryGetValue(player, out NetworkObject obj))
        {
            runner.Despawn(obj);
            _spawnedCharacters.Remove(player);
        }
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new NetworkInputData();

        if (IsPressed(Key.W))
            data.Direction += Vector3.forward;

        if (IsPressed(Key.S))
            data.Direction += Vector3.back;

        if (IsPressed(Key.A))
            data.Direction += Vector3.left;

        if (IsPressed(Key.D))
            data.Direction += Vector3.right;

        input.Set(data);
    }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log(shutdownReason);
    }

    public void OnConnectedToServer(NetworkRunner runner) { }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log(reason);
    }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {
        
        _availableSessions = sessionList;

        Debug.Log($"Available rooms: {sessionList.Count}");

        foreach (var s in sessionList)
        {
            Debug.Log($"{s.Name} ({s.PlayerCount}/{s.MaxPlayers})");
        }
    }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

}