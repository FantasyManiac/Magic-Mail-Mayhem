using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private RawImage iconPrefab;

    private readonly List<RawImage> icons = new();

    public void AddIcon(Texture texture)
    {
        // Debug.Log("Entered to add");
        RawImage icon = Instantiate(iconPrefab, transform);

        icon.texture = texture;

        icons.Add(icon);
    }

    public void RemoveLastIcon()
    {
        if (icons.Count == 0)
            return;

        Destroy(icons[^1].gameObject);

        icons.RemoveAt(icons.Count - 1);
    }
}