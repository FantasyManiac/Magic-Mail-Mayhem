using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Match")]
    [SerializeField] private float matchDuration = 300f; // 5 minutes

    [Header("Pumpkins")]
    [SerializeField] private List<NetworkPrefabRef> itemPrefabs;
    [SerializeField] private List<NetworkPrefabRef> npcPrefabs;
    private int itemCount;
    private int NPCCount;
    [SerializeField] private Transform[] itemSpawnPoints;
    [SerializeField] private Transform[] NPCSpawnPoints;
    private bool[] isItemSpawnPointFree;
    private bool[] isNPCSpawnPointFree;
    [SerializeField] private int startingItems = 5;

    [Networked]
    public TickTimer MatchTimer { get; set; }

    [Networked]
    public NetworkBool MatchEnded { get; set; }

    private readonly List<NetworkObject> spawnedItems = new();
    private readonly List<NetworkObject> spawnedNPCs = new();
    // public GameObject inGameUI;

    private void Awake()
    {
        Instance = this;
        itemCount = itemPrefabs.Count - 1;
        NPCCount = npcPrefabs.Count - 1;
    }

    public override void Spawned()
    {
        Debug.Log($"GameManager Spawned. StateAuthority={Object.HasStateAuthority}");

        if (!Object.HasStateAuthority)
            return;

        isItemSpawnPointFree = new bool[itemSpawnPoints.Length];
        isNPCSpawnPointFree = new bool[NPCSpawnPoints.Length];

        for (int i = 0; i < isItemSpawnPointFree.Length; i++)
            isItemSpawnPointFree[i] = true;

        for (int i = 0; i < isNPCSpawnPointFree.Length; i++)
            isNPCSpawnPointFree[i] = true;
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

        if (NPCSpawnPoints.Length == 0)
            return;
        
        Debug.Log($"SpawnRandomItem()  Authority={Object.HasStateAuthority}");

        int i = 0;
        Transform point;
        
        do
        {
            i = Random.Range(0, itemSpawnPoints.Length);
            point = itemSpawnPoints[i];
        } while (!isItemSpawnPointFree[i]);

        int j = 0;
        Transform point2;

        do
        {
            j = Random.Range(0, NPCSpawnPoints.Length);
            point2 = NPCSpawnPoints[j];
        } while (!isNPCSpawnPointFree[j]);

        NetworkObject item =
            Runner.Spawn(
                itemPrefabs[itemCount],
                point.position,
                Quaternion.identity);

        NetworkObject npc =
            Runner.Spawn(
                npcPrefabs[NPCCount],
                point2.position,
                Quaternion.identity);
        
        itemCount -= 1;
        NPCCount -= 1;

        if (itemCount < 0)
            itemCount = itemPrefabs.Count - 1;
        
        if (NPCCount < 0)
            NPCCount = npcPrefabs.Count - 1;

        isItemSpawnPointFree[i] = false;
        isNPCSpawnPointFree[j] = false;
        item.GetComponent<Item>().CurrentPosition = i;
        npc.GetComponent<NPCDelivery>().needs = item.GetComponent<Item>().itemCode;

        spawnedItems.Add(item);
        spawnedNPCs.Add(npc);
    }

    public void ItemDelivered(NetworkObject npc)
    {
        if (!Object.HasStateAuthority)
            return;
        
        if (npc != null)
        {
            spawnedNPCs.Remove(npc);
            Runner.Despawn(npc);
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