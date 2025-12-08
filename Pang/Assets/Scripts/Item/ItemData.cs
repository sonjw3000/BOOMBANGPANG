using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static UnityEditor.Progress;
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

public class ToteBox : IItemContainer
{
	private float capacity = 10.0f;
	private float size = 0.0f;
	private Dictionary<uint, ItemStack> stacks = new();

	protected ItemDatabase itemDB => GameContext.Instance.ItemDB;

	// totebox의 stacks는 많지 않을것으로 예상
	public float Size => size;

	public IReadOnlyDictionary<uint, ItemStack> Stacks => stacks;
	public float Capacity => capacity;
	
	
	public ToteBox(float boxCapacity = 10.0f) => capacity = boxCapacity;

	public bool CanRegister() => true;

	// 장소를 단순 등록
	public void RegisterItem(uint itemId)
	{
		stacks[itemId] = new ItemStack(itemId, capacity);
	}

	public void UnregistereItem(uint itemId)
	{
		stacks.Remove(itemId);
	}

	public int AddItem(uint itemId, int quantity)
	{
		float availableSize = capacity - size;
		float itemSize = itemDB.GetItemSize(itemId);

		// quantity를 줄여야한다
		if (availableSize < itemSize * quantity)
			quantity = Mathf.FloorToInt(availableSize / itemSize);

		int res = stacks[itemId].AddItem(quantity);

		UpdateSize();

		return res;
	}

	public int RemoveItem(uint itemId, int quantity)
	{
		int res = stacks[itemId].RemoveItem(quantity);

		UpdateSize();

		return res;
	}

	private void UpdateSize()
	{
		size = stacks.Values.Sum(s => itemDB.GetItemSize(s.ItemID) * s.Quantity);
	}

}
