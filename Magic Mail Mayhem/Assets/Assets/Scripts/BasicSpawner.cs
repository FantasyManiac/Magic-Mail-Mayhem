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
