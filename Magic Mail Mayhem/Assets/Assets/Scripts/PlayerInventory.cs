using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System;

public class PlayerInventory : NetworkBehaviour
{
    // public event Action InventoryChanged;
    private readonly List<NetworkObject> items = new();
    private readonly List<string> itemCodes = new();
    private InventoryUI inventoryUI;

    public override void Spawned() {
        if (!Object.HasInputAuthority)
        return;

        inventoryUI = FindFirstObjectByType<InventoryUI>();
    }

    public void AddItem(NetworkObject item)
    {
        if (item == null)
            return;

        Item itemData = item.GetComponent<Item>();
        string code = itemData != null && !string.IsNullOrEmpty(itemData.itemCode) ? itemData.itemCode : item.name;

        int existingIndex = items.IndexOf(item);
        if (existingIndex >= 0)
        {
            itemCodes[existingIndex] = code;
        }
        else
        {
            items.Add(item);
            itemCodes.Add(code);
            inventoryUI.AddIcon(itemData.icon);
        }

        // InventoryChanged?.Invoke();

        // Debug.Log($"Inventory count = {items.Count}, added={code}");
    }

    public void RemoveItem(string itemCode)
    {
        if (string.IsNullOrEmpty(itemCode))
            return;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (itemCodes.Count > i && string.Equals(itemCodes[i], itemCode, System.StringComparison.OrdinalIgnoreCase))
            {
                items.RemoveAt(i);
                itemCodes.RemoveAt(i);

                inventoryUI.RemoveLastIcon();
                // InventoryChanged?.Invoke();
                return;
            }
        }
    }

    public int GetItemCount()
    {
        return items.Count;
    }

    public bool Contains(string itemCode)
    {
        if (string.IsNullOrEmpty(itemCode))
            return false;

        for (int i = 0; i < itemCodes.Count; i++)
        {
            if (string.Equals(itemCodes[i], itemCode, System.StringComparison.OrdinalIgnoreCase))
            {
                Debug.Log($"Inventory contents: {GetInventoryDebugText()}");
                return true;
            }
        }

        Debug.Log($"Inventory contents: {GetInventoryDebugText()}");
        return false;
    }

    private string GetInventoryDebugText()
    {
        return string.Join(", ", itemCodes);
    }

    public IReadOnlyList<string> GetItemCodes()
    {
        return itemCodes;
    }
}
