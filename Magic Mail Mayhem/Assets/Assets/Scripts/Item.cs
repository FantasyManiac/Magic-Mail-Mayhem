using UnityEngine;
using Fusion;

public class Item : NetworkBehaviour
{
    // public int currentPosition;
    [Networked]
    public int CurrentPosition { get; set; }
}
