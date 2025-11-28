using UnityEngine;
using System.Collections.Generic;

// database of all item types in the game
[System.Serializable]
public class ItemDatabase
{
	[SerializeField] private List<ItemData> items = new();
	public IReadOnlyList<ItemData> Items => items;


	public bool GetItemData(uint itemID, out ItemData data)
	{
		data = items.Find(item => item.ItemID == itemID);

		return data != null;
	}

	public uint GetRandomItemID()
	{
		return items[Random.Range(0, items.Count)].ItemID;
	}
}