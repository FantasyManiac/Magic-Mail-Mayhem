using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Match")]
    [SerializeField] private float matchDuration = 300f; // 5 minutes

    [Header("Pumpkins")]
    [SerializeField] private NetworkPrefabRef itemPrefab;
    [SerializeField] private Transform[] itemSpawnPoints;
    private bool[] isItemSpawnPointFree;
    [SerializeField] private int startingItems = 5;

    [Networked]
    public TickTimer MatchTimer { get; set; }

    [Networked]
    public NetworkBool MatchEnded { get; set; }

    private readonly List<NetworkObject> spawnedItems = new();

    private void Awake()
    {
        Instance = this;
    }

    public override void Spawned()
    {
        Debug.Log($"GameManager Spawned. StateAuthority={Object.HasStateAuthority}");

        if (!Object.HasStateAuthority)
            return;

        isItemSpawnPointFree = new bool[itemSpawnPoints.Length];

        for (int i = 0; i < isItemSpawnPointFree.Length; i++)
            isItemSpawnPointFree[i] = true;
    }

    public void StartMatch()
    {
        if (!Object.HasStateAuthority)
            return;

        MatchTimer = TickTimer.CreateFromSeconds(Runner, matchDuration);

        Debug.Log("Calling SpawnStartingItems()");

        SpawnStartingItems();

        Debug.Log("Match Started!");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (MatchEnded)
            return;

        if (MatchTimer.Expired(Runner))
        {
            EndMatch();
        }
    }

    private void SpawnStartingItems()
    {
        for (int i = 0; i < startingItems; i++)
        {
            SpawnRandomItem();
        }
    }

    public void SpawnRandomItem()
    {
        if (!Object.HasStateAuthority)
            return;

        if (itemSpawnPoints.Length == 0)
            return;
        
        Debug.Log($"SpawnRandomItem()  Authority={Object.HasStateAuthority}");

        int i = 0;
        Transform point;
        
        do
        {
            i = Random.Range(0, itemSpawnPoints.Length);
            point = itemSpawnPoints[i];
        } while (!isItemSpawnPointFree[i]);

        NetworkObject item =
            Runner.Spawn(
                itemPrefab,
                point.position,
                Quaternion.identity);
        
        isItemSpawnPointFree[i] = false;
        item.GetComponent<Item>().CurrentPosition = i;

        spawnedItems.Add(item);
    }

    public void ItemDelivered(NetworkObject item)
    {
        if (!Object.HasStateAuthority)
            return;

        if (item != null)
        {
            spawnedItems.Remove(item);
            Runner.Despawn(item);
            isItemSpawnPointFree[item.GetComponent<Item>().CurrentPosition] = true;
        }

        SpawnRandomItem();
    }

    public void ItemPicked(NetworkObject item)
    {
        if (!Object.HasStateAuthority)
            return;

        if (item != null)
        {
            spawnedItems.Remove(item);
            Runner.Despawn(item);
        }
    }

    private void EndMatch()
    {
        MatchEnded = true;

        Player[] players = FindObjectsByType<Player>(
            FindObjectsSortMode.None);

        Player winner = null;
        int highestScore = int.MinValue;

        foreach (Player player in players)
        {
            if (player.Score > highestScore)
            {
                highestScore = player.Score;
                winner = player;
            }
        }

        if (winner != null)
        {
            Debug.Log($"Winner: {winner.Object.InputAuthority}");
            Debug.Log($"Score: {winner.Score}");
        }
        else
        {
            Debug.Log("Draw");
        }
    }

    public float GetRemainingTime()
    {
        if (!MatchTimer.IsRunning)
            return 0f;

        return Mathf.Max(
            0f,
            MatchTimer.RemainingTime(Runner) ?? 0f);
    }
}