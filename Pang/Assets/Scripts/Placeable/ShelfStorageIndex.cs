using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Timeline;

// 아이템과 선반을 한번에 관리한다
// 아이템ID별로 아이템의 위치를 가진 딕셔너리가 존재함
// 

// 실제 아이템의 저장은 ShelfBase의 ItemStack이다
// 하지만 이를 ItemID로 편하게 검색하기 위해 ItemLocation을 만들어 참조할 수 있게 하였다
// itemlocation이 데이터를 가지고 있는것처럼 보여도 실제론 itemlocation도 itemstack을 참조중이다.

// itemlocation << 아이템의 위치 정보
// itemstack 실제 아이템의 데이터

[System.Serializable]
public class ShelfStorageIndex : MonoBehaviour
{
	// shelf, bin 등 아이템 컨테이너 리스트
	[SerializeField] private List<ShelfBase> containers = new();

	// 아이템 ID별 아이템의 위치 리스트
	private readonly Dictionary<uint, List<ShelfBase>> shelvesByItem = new();

	public IReadOnlyList<ShelfBase> Containers => containers;
	public IReadOnlyDictionary<uint, List<ShelfBase>> ShelvesByItem => shelvesByItem;

	private ItemDatabase ItemDB => GameContext.Instance.ItemDB;

	// event
	public void OnItemAdded(ShelfBase shelf, uint itemId)
	{
		if (shelvesByItem.TryGetValue(itemId, out var list) == false)
		{
			list = new();
			shelvesByItem.Add(itemId, list);
		}

		list.Add(shelf);
	}

	public void OnItemRemoved(ShelfBase shelf, uint itemId)
	{
		if (shelvesByItem.TryGetValue(itemId, out var list) == false)
		{
			Debug.LogError("ERROR!! No id here but tried to remove shelf");
			shelvesByItem[itemId] = new();
		}

		shelvesByItem[itemId].Remove(shelf);
	}

	// ---------------------------
	// 컨테이너 관련
	// ---------------------------
	// 컨테이너에 저장될 아이템의 종류를 업데이트한다
	public void OnContainerAdded(ShelfBase container)
	{
		container.OnItemRegistered += OnItemAdded;
		container.OnItemUnregistered += OnItemRemoved;
		containers.Add(container);
	}

	public void OnContainerRemoved(ShelfBase container)
	{
		container.OnItemRegistered -= OnItemAdded;
		container.OnItemUnregistered -= OnItemRemoved;
		containers.Remove(container);
	}

	// ---------------------------
	// 아이템 관련
	// ---------------------------
	public bool GetItemLocations(uint itemID, out List<ShelfBase> locations)
	{
		return shelvesByItem.TryGetValue(itemID, out locations);
	}

	public bool GetClosestItemLocation(uint itemID, int3 from, out ShelfBase shelf)
	{
		shelf = default;
		if (shelvesByItem.ContainsKey(itemID) == false) return false;

		var locations = shelvesByItem[itemID];

		//float minDist = float.MaxValue;
		//float3 floatFrom = (float3)from;
		//foreach (var loc in locations)
		//{
		//	var containerPos = loc.Container.PickingPosition;
		//	float distPow = 
		//	if (dist < minDist)
		//	{
		//		minDist = dist;
		//		location = loc;
		//	}
		//}

		// 임시로 그냥 첫번째 위치 반환
		shelf = locations[0];

		return true;
	}

	public IEnumerable<ShelfBase> QueryPlaceCandidate(uint itemID, int qty)
	{
		bool hasCandidate = false;

		if (ShelvesByItem.TryGetValue(itemID, out var locations))
		{
			for (int i = 0; i < locations.Count; ++i)
			{
				ShelfBase shelf = locations[i];
				if (shelf.CanAccept(itemID, qty))
				{
					hasCandidate = true;
					yield return shelf;
				}
			}
		}
		
		if (hasCandidate == false)
		{
			// todo
			// 최적화 해야함
			for (int i = 0; i < containers.Count; ++i)
			{
				if (containers[i].CanAccept(itemID, qty))
					yield return containers[i];
			}

		}
	}

	//public void TestStoreItem()
	//{
	//	if (Containers.Count < 3)
	//	{
	//		Debug.Log($"ItemContainer is not enough!!, need more than 3! current: {Containers.Count}");
	//		return;
	//	}

	//	AddItemLocation(123333, Containers[0]);
	//	AddItemLocation(123123, Containers[1]);
	//	AddItemLocation(14412, Containers[2]);

	//	ItemDB.InsertOrderedItems(123333);
	//	ItemDB.InsertOrderedItems(123123);
	//	ItemDB.InsertOrderedItems(14412);

	//	Debug.Log("Test Store Item");
	//}

	//public void TestFullStockItems()
	//{
	//	if (Containers.Count <= 0)
	//	{
	//		Debug.Log("No Item Container Found");
	//		return;
	//	}

	//	Containers[0].AddItem(123333, 100);
	//	Containers[1].AddItem(123123, 100);
	//	Containers[2].AddItem(14412, 100);
	//}


}
