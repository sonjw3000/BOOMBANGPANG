using System;
using System.Collections.Generic;
using UnityEngine;
using ToteElement = System.ValueTuple<ItemData, int>;

// 지금은 이걸 그대로 사용하지만
// 나중엔 이걸 따로 등록하고 걔를 가리키는 형식으로다가 하자
[System.Serializable]
public class ItemData
{
	[SerializeField] private string name;
	[SerializeField] private uint itemID;
	[SerializeField] private float size;
	// 혹시 모를 render를 위한 프리팹
	[SerializeField] private GameObject itemPrefab;

	public string Name => name;
	public uint ItemID => itemID;
	public float Size => size;
	public GameObject ItemPrefab => itemPrefab;
}

//public class Pallet
//{
//	public List<ItemData> Items { get; private set; }

//	public Pallet(List<ItemData> data) { Items = data; }
//}

//public class TruckManifest
//{
//	public List<Pallet> Pallets { get; private set; } = new List<Pallet>();

//	public TruckManifest(List<Pallet> data) { Pallets = data; }
//}

public class ToteBox
{
	private float capacity = 10.0f;
	private float size = 0.0f;
	private Stack<ToteElement> items = new Stack<ToteElement>();

	public float Capacity => capacity;
	public float Size  => size;
	public Stack<ToteElement> Items  => items;
	public ToteBox(float boxCapacity = 10.0f) => capacity = boxCapacity;

	public bool CanAddItem(ItemData item, int quantity)
	{
		return (Size + item.Size * quantity) <= Capacity;
	}

	public bool AddItem(ItemData item, int quantity)
	{
		if (!CanAddItem(item, quantity))
			return false;
		items.Push((item, quantity));
		size += item.Size * quantity;
		return true;
	}

	public bool RemoveItem(out ToteElement element)
	{
		if (items.Count == 0)
		{
			element = default;
			return false;
		}
		element = items.Pop();
		size -= element.Item1.Size * element.Item2;
		return true;
	}

	public bool PeekItem(out ToteElement element)
	{
		if (items.Count == 0)
		{
			element = default;
			return false;
		}
		element = items.Peek();
		return true;
	}
}
