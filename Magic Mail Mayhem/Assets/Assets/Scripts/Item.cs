using UnityEngine;
using Fusion;

public class Item : NetworkBehaviour
{
    // public int currentPosition;
    [Networked]
    public int CurrentPosition { get; set; }

    public string itemCode;

    public Texture icon;

    public int ItemID; 

    public bool isSpawned = false;
}
