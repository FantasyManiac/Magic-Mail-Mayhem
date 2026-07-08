using UnityEngine;
using Fusion;
using System.Collections.Generic;

public class PlayerInventory : NetworkBehaviour
{
    private List<NetworkObject> items = new();
    public void AddItem(NetworkObject item)
    {
        if (item == null)
            return;
        
        items.Add(item);
    }

    public void RemoveItem(NetworkObject item)
    {
        if (item == null)
            return;
        
        items.Remove(item);
    }

    public int GetItemCount()
    {
        return items.Count;
    }
}
