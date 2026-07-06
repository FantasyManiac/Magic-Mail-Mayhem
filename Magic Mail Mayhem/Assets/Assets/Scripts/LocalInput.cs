using System;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalInput : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField]
    private float mouseSensitivity = 0.15f;

    private Vector2 accumulatedLookDelta;

    private void Update()
    {
        if (Mouse.current == null)
            return;

        accumulatedLookDelta += Mouse.current.delta.ReadValue();
    }

    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        NetworkInputData data = new();

        if (Keyboard.current != null)
        {
            Vector2 move = Vector2.zero;

            if (Keyboard.current.wKey.isPressed)
                move.y += 1f;

            if (Keyboard.current.sKey.isPressed)
                move.y -= 1f;

            if (Keyboard.current.aKey.isPressed)
                move.x -= 1f;

            if (Keyboard.current.dKey.isPressed)
                move.x += 1f;

            data.Move = move;
            bool sprintHeld = Keyboard.current.shiftKey.isPressed || Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
            data.Sprint = sprintHeld;
        }

        if (Mouse.current != null)
        {
            Vector2 delta = accumulatedLookDelta;
            accumulatedLookDelta = Vector2.zero;
            float multiplier = runner != null ? runner.DeltaTime * 60f : Time.deltaTime * 60f;
            data.LookDelta = delta * mouseSensitivity * multiplier;
        }

        input.Set(data);
    }

    public void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void UnlockCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        LockCursor();
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        UnlockCursor();
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        UnlockCursor();
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) { }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }

    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }

    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }

    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }

    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }

    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }

    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }

    public void OnSceneLoadStart(NetworkRunner runner) { }

    public void OnSceneLoadDone(NetworkRunner runner) { }

    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }

    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ReadOnlySpan<byte> data) { }

    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
}