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

    [Header("Player")]
    [SerializeField] private NetworkPrefabRef playerPrefab;

    [Header("UI")]
    [SerializeField] private GameObject startMenu;
    [SerializeField] private GameObject loadingMenu;

    private NetworkRunner lobbyRunner;
    private NetworkRunner gameRunner;

    private readonly Dictionary<PlayerRef, NetworkObject> spawnedPlayers = new();

    private List<SessionInfo> availableSessions = new();

    private bool lobbyReady;

    private async void Start()
    {
        startMenu.SetActive(false);
        loadingMenu.SetActive(true);

        lobbyRunner = new GameObject("LobbyRunner").AddComponent<NetworkRunner>();
        lobbyRunner.name = "LobbyRunner";

        lobbyRunner.AddCallbacks(this);

        var result = await lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);

        if (!result.Ok)
        {
            Debug.LogError("Failed to join lobby.");
            return;
        }

        lobbyReady = true;

        loadingMenu.SetActive(false);
        startMenu.SetActive(true);

        Debug.Log("Lobby joined.");
    }

    public async void HostGame()
    {
        if (!lobbyReady)
            return;

        string roomName = $"Room_{Guid.NewGuid()}";

        await StartGame(GameMode.Host, roomName);
    }

    public async void JoinGame()
    {
        if (!lobbyReady)
            return;

        SessionInfo room = availableSessions
            .Where(x => x.IsOpen && x.PlayerCount < 2)
            .OrderByDescending(x => x.PlayerCount)
            .FirstOrDefault();

        if (room == null)
        {
            Debug.Log("No available room.");
            return;
        }

        await StartGame(GameMode.Client, room.Name);
    }

    private async Task StartGame(GameMode mode, string sessionName)
    {
        startMenu.SetActive(false);
        loadingMenu.SetActive(true);

        if (lobbyRunner != null)
        {
            await lobbyRunner.Shutdown();
            Destroy(lobbyRunner.gameObject);
            lobbyRunner = null;
        }

        if (gameRunner != null)
        {
            await gameRunner.Shutdown();
            Destroy(gameRunner.gameObject);
        }

        GameObject runnerObject = new GameObject("GameRunner");

        gameRunner = runnerObject.AddComponent<NetworkRunner>();

        gameRunner.ProvideInput = true;

        gameRunner.AddCallbacks(this);

        LocalInput input = GetComponent<LocalInput>();

        if (input != null)
        {
            gameRunner.AddCallbacks(input);
        }

        SceneRef scene = SceneRef.FromIndex(
            SceneManager.GetActiveScene().buildIndex);

        StartGameResult result =
            await gameRunner.StartGame(
                new StartGameArgs
                {
                    GameMode = mode,
                    SessionName = sessionName,
                    PlayerCount = 2,
                    Scene = scene,
                    SceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>()
                });

        if (!result.Ok)
        {
            Debug.LogError(result.ShutdownReason);

            loadingMenu.SetActive(false);
            startMenu.SetActive(true);

            return;
        }

        loadingMenu.SetActive(false);
    }

    public void ReturnToLobby()
    {
        if (startMenu != null)
            startMenu.SetActive(true);

        if (loadingMenu != null)
            loadingMenu.SetActive(false);
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        if (!runner.IsServer)
            return;

        Vector3 spawnPosition =
            new Vector3(
                (player.RawEncoded % 2) * 4f,
                1f,
                0f);

        NetworkObject playerObject =
            runner.Spawn(
                playerPrefab,
                spawnPosition,
                Quaternion.identity,
                player);

        spawnedPlayers[player] = playerObject;

        if (runner.ActivePlayers.Count() >= 2)
        {
            runner.SessionInfo.IsOpen = false;
            GameManager.Instance.StartMatch();
            Debug.Log("Room closed.");
        }
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (spawnedPlayers.TryGetValue(player, out NetworkObject obj))
        {
            runner.Despawn(obj);
            spawnedPlayers.Remove(player);
        }

        if (runner.IsServer &&
            runner.ActivePlayers.Count() < 2)
        {
            runner.SessionInfo.IsOpen = true;
        }
    }

    public void OnSessionListUpdated(
        NetworkRunner runner,
        List<SessionInfo> sessionList)
    {
        availableSessions = sessionList;

        Debug.Log($"Rooms: {sessionList.Count}");

        foreach (SessionInfo session in sessionList)
        {
            Debug.Log(
                $"{session.Name} | " +
                $"{session.PlayerCount}/{session.MaxPlayers} | " +
                $"Open: {session.IsOpen}");
        }
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Shutdown: {shutdownReason}");

        if (runner == gameRunner)
        {
            spawnedPlayers.Clear();

            Destroy(gameRunner.gameObject);
            gameRunner = null;

            ReturnToLobby();
        }
    }

    public void OnDisconnectedFromServer(
        NetworkRunner runner,
        NetDisconnectReason reason)
    {
        Debug.Log($"Disconnected: {reason}");

        if (runner == gameRunner)
        {
            spawnedPlayers.Clear();

            Destroy(gameRunner.gameObject);
            gameRunner = null;

            ReturnToLobby();
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("Connected to server.");
    }

    public void OnConnectFailed(
        NetworkRunner runner,
        NetAddress remoteAddress,
        NetConnectFailedReason reason)
    {
        Debug.LogError($"Connection failed: {reason}");

        ReturnToLobby();
    }

    public void OnConnectRequest(
        NetworkRunner runner,
        NetworkRunnerCallbackArgs.ConnectRequest request,
        byte[] token)
    {
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        // Input is handled by the LocalInput component.
    }

    public void OnInputMissing(
        NetworkRunner runner,
        PlayerRef player,
        NetworkInput input)
    {
    }

    public void OnSceneLoadStart(NetworkRunner runner)
    {
        Debug.Log("Scene loading...");
    }

    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("Scene loaded.");
    }

    public void OnObjectEnterAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnObjectExitAOI(
        NetworkRunner runner,
        NetworkObject obj,
        PlayerRef player)
    {
    }

    public void OnReliableDataReceived(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        ReadOnlySpan<byte> data)
    {
    }

    public void OnReliableDataProgress(
        NetworkRunner runner,
        PlayerRef player,
        ReliableKey key,
        float progress)
    {
    }

    public void OnUserSimulationMessage(
        NetworkRunner runner,
        SimulationMessagePtr message)
    {
    }

    public void OnCustomAuthenticationResponse(
        NetworkRunner runner,
        Dictionary<string, object> data)
    {
    }

    public void OnHostMigration(
        NetworkRunner runner,
        HostMigrationToken hostMigrationToken)
    {
        Debug.Log("Host migration is not implemented.");
    }
}


    // private NetworkRunner _gameRunner;
    // private NetworkRunner _lobbyRunner;

    // public GameObject lobbyObject = new GameObject("LobbyRunner");


    // private List<SessionInfo> _availableSessions = new();

    // [SerializeField]
    // private NetworkPrefabRef _playerPrefab;

    // public GameObject _startMenu;
    // public GameObject _loadingMenu;

    // private readonly Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new();

    // // private static bool IsPressed(Key key)
    // // {
    // //     var keyboard = Keyboard.current;
    // //     return keyboard != null && keyboard[key].isPressed;
    // // }

    // private async void Start()
    // {
    //     _startMenu.SetActive(false);
    //     _loadingMenu.SetActive(true);

    //     _lobbyRunner = lobbyObject.AddComponent<NetworkRunner>();
    //     _lobbyRunner.AddCallbacks(this);

    //     var result = await _lobbyRunner.JoinSessionLobby(SessionLobby.ClientServer);

    //     Debug.Log($"Lobby joined: {result.Ok}");

    //     _startMenu.SetActive(true);
    //     _loadingMenu.SetActive(false);
    // }

    // private async Task StartGame(GameMode mode, string sessionName)
    // {
    //     _startMenu.SetActive(false);
    //     _loadingMenu.SetActive(true);

    //     if (_lobbyRunner != null)
    //     {
    //         await _lobbyRunner.Shutdown();
    //         Destroy(_lobbyRunner);
    //         _lobbyRunner = null;
    //     }

    //     _gameRunner = gameObject.AddComponent<NetworkRunner>();

    //     _gameRunner.ProvideInput = true;
    //     _gameRunner.AddCallbacks(this);

    //     var scene =
    //         SceneRef.FromIndex(SceneManager.GetActiveScene().buildIndex);

    //     var result =
    //         await _gameRunner.StartGame(new StartGameArgs
    //         {
    //             GameMode = mode,
    //             SessionName = sessionName,
    //             PlayerCount = 2,
    //             Scene = scene,
    //             SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
    //         });

    //     if (!result.Ok)
    //     {
    //         Debug.LogError(result.ShutdownReason);
    //         return;
    //     }

    //     _loadingMenu.SetActive(false);
    // }

    // public async void HostGame()
    // {
    //     await StartGame(GameMode.Host, $"Room_{Guid.NewGuid()}");
    // }

    // public async void JoinGame()
    // {
    //     SessionInfo room = null;

    //     foreach (var s in _availableSessions)
    //     {
    //         if (s.IsOpen &&
    //             s.PlayerCount < 2)
    //         {
    //             room = s;
    //             break;
    //         }
    //     }

    //     if (room == null)
    //     {
    //         Debug.Log("No available room.");
    //         return;
    //     }

    //     await StartGame(GameMode.Client, room.Name);
    // }

    // // private void OnGUI()
    // // {
    // //     if (_gameRunner != null)
    // //         return;

    // //     if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
    // //         HostGame();

    // //     if (GUI.Button(new Rect(0, 50, 200, 40), "Join"))
    // //         JoinGame();
    // // }

    // public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    // {
    //     if (!runner.IsServer)
    //         return;

    //     Vector3 spawnPosition =
    //         new Vector3((player.RawEncoded % runner.Config.Simulation.PlayerCount) * 3, 1, 0);

    //     NetworkObject obj = runner.Spawn(
    //         _playerPrefab,
    //         spawnPosition,
    //         Quaternion.identity,
    //         player);

    //     _spawnedCharacters.Add(player, obj);

    //     if (runner.ActivePlayers.Count() >= 2)
    //     {
    //         runner.SessionInfo.IsOpen = false;
    //         Debug.Log("Room closed.");
    //     }
    // }

    // public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    // {
    //     if (_spawnedCharacters.TryGetValue(player, out NetworkObject obj))
    //     {
    //         runner.Despawn(obj);
    //         _spawnedCharacters.Remove(player);
    //     }
    // }

    // public void OnInput(NetworkRunner runner, NetworkInput input)
    // {
    //     // var data = new NetworkInputData();

    //     // if (IsPressed(Key.W))
    //     //     data.Direction += Vector3.forward;

    //     // if (IsPressed(Key.S))
    //     //     data.Direction += Vector3.back;

    //     // if (IsPressed(Key.A))
    //     //     data.Direction += Vector3.left;

    //     // if (IsPressed(Key.D))
    //     //     data.Direction += Vector3.right;

    //     NetworkInputData data = new();

    //     if (Keyboard.current.wKey.isPressed)
    //         data.Move.y += 1;

    //     if (Keyboard.current.sKey.isPressed)
    //         data.Move.y -= 1;

    //     if (Keyboard.current.aKey.isPressed)
    //         data.Move.x -= 1;

    //     if (Keyboard.current.dKey.isPressed)
    //         data.Move.x += 1;

    //     // data.Sprint = Keyboard.current.leftShiftKey.isPressed;

    //     // Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());

    //     // Plane plane = new Plane(Vector3.up, Vector3.zero);

    //     // if (plane.Raycast(ray, out float distance))
    //     // {
    //     //     data.AimPoint = ray.GetPoint(distance);
    //     // }

    //     data.LookDelta = Mouse.current.delta.ReadValue();

    //     data.Sprint = Keyboard.current.leftShiftKey.isPressed;

    //     input.Set(data);
    // }

    // public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    // public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    // {
    //     Debug.Log(shutdownReason);
    // }

    // public void OnConnectedToServer(NetworkRunner runner) { }

    // public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    // {
    //     Debug.Log(reason);
    // }

    // public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    // public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    // public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    // public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) {
        
    //     _availableSessions = sessionList;

    //     Debug.Log($"Available rooms: {sessionList.Count}");

    //     foreach (var s in sessionList)
    //     {
    //         Debug.Log($"{s.Name} ({s.PlayerCount}/{s.MaxPlayers})");
    //     }
    // }

    // public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

    // public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    // public void OnSceneLoadStart(NetworkRunner runner) { }

    // public void OnSceneLoadDone(NetworkRunner runner) { }

    // public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    // public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    // public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

    // public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }

// }