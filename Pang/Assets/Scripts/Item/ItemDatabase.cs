using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class ItemDatabase : MonoBehaviour
{
	[SerializeField] private List<ItemCatalog> itemCatalogs = new();
	
	private Dictionary<uint, ItemDefinition> itemIDMap;
	private readonly List<ItemDefinition> orderedItems = new();

	private void BuildDict(Dictionary<uint, ItemDefinition> dict)
	{
		foreach (var catalog in itemCatalogs)
		{
			foreach (var item in catalog.itemDefinitions)
			{
				if (dict.ContainsKey(item.ItemID))
				{
					Debug.LogError($"Duplicate ItemID {item.ItemID} found in {catalog.name}. Each ItemID must be unique.");
				}
				else
				{
					dict[item.ItemID] = item;
				}
			}
		}
	}

	private void OnValidate()
	{
		// 중복 검증
		Dictionary<uint, ItemDefinition> dict = new();
		BuildDict(dict);
	}

	private void Awake()
	{
		BuildDict(itemIDMap = new());
		orderedItems.Clear();
		orderedItems.AddRange(itemIDMap.Values.OrderBy(item => item.ItemID));

		itemCatalogs.Clear();
	}

	public bool TryGetItemBySortedIndex(int index, out ItemDefinition item)
	{
		if (index < 0 || index >= orderedItems.Count)
		{
			item = null;
			return false;
		}

		item = orderedItems[index];
		return item != null;
	}

	public bool GetItemData(uint itemID, out ItemDefinition data)
	{
		return itemIDMap.TryGetValue(itemID, out data);
	}

	public float GetItemSize(uint itemID)
	{
		ItemDefinition data;
		if (GetItemData(itemID, out data))
		{
			return data.Size;
		}
		else
		{
			Debug.LogError($"ItemID {itemID} does not exist in ItemDB.");
			return 0;
		}
	}

	public uint GetRandomItemID()
	{
		uint itemID = itemIDMap.FirstOrDefault().Key;

		int randIdx = Random.Range(0, itemIDMap.Count);

		foreach (var kvp in itemIDMap)
		{
			if (randIdx-- == 0)
			{
				itemID = kvp.Key;
				break;
			}
		}

		return itemID;
	}

}
