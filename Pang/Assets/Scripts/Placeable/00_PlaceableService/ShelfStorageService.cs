using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// 아이템과 선반을 한번에 관리한다
// 아이템ID별로 아이템의 위치를 가진 딕셔너리가 존재함
// 

// 실제 아이템의 저장은 Shelf의 ItemStack이다
// 하지만 이를 ItemID로 편하게 검색하기 위해 ItemLocation을 만들어 참조할 수 있게 하였다
// itemlocation이 데이터를 가지고 있는것처럼 보여도 실제론 itemlocation도 itemstack을 참조중이다.

// itemlocation << 아이템의 위치 정보
// itemstack 실제 아이템의 데이터

[System.Serializable]
public partial class ShelfStorageService : FacilityService<Shelf>, ICollectSupplySource
{
	// shelf, bin 등 아이템 컨테이너 리스트
	[SerializeField] private List<ShelfBase> containers = new();

	// 아이템 ID별 아이템의 위치 리스트
	private readonly Dictionary<uint, List<ShelfBase>> shelvesByItem = new();

	public IReadOnlyList<ShelfBase> Containers => containers;
	public IReadOnlyDictionary<uint, List<ShelfBase>> ShelvesByItem => shelvesByItem;

	private ItemLedger ItemLedger => GameContext.Instance.WMSys.ItemLedger;

	// ---------------------------
	// 이벤트 핸들러
	// ---------------------------
	private void OnItemPresentChanged(ShelfBase shelf, uint itemId, bool present)
	{
		if (present)
			OnItemRegistered(shelf, itemId);
		else
			OnItemUnregistered(shelf, itemId);
	}

	// event
	private void OnItemRegistered(ShelfBase shelf, uint itemId)
	{
		if (shelvesByItem.TryGetValue(itemId, out var list) == false)
		{
			list = new();
			shelvesByItem.Add(itemId, list);
		}

		if (list.Contains(shelf) == false)
			list.Add(shelf);
	}

	private void OnItemUnregistered(ShelfBase shelf, uint itemId)
	{
		if (shelvesByItem.ContainsKey(itemId) == false)
		{
			Debug.LogError("ERROR!! No id here but tried to remove shelf");
			shelvesByItem[itemId] = new();
		}

		shelvesByItem[itemId].Remove(shelf);
		if (shelvesByItem[itemId].Count == 0)
			shelvesByItem.Remove(itemId);
	}

	private void OnQuantityDelta(ShelfBase shelf, uint itemId, int qtyDelta)
	{
		// todo
		// 상황에 따라 itemLedger에 알리지 않아도 될 수 있다
		ItemLedger.OnItemQuantityChanged(itemId, qtyDelta);

	}

	protected override void OnRegisterFacility(uint buildingId, Shelf facility)
	{
		RegisterContainer(facility);
	}

	protected override void OnUnregisterFacility(uint buildingId, Shelf facility)
	{
		UnregisterContainer(facility);
	}

	// ---------------------------
	// 컨테이너 관련
	// ---------------------------
	private void RegisterContainer(ShelfBase container)
	{
		if (container == null)
			return;

		container.OnItemPresentChanged -= OnItemPresentChanged;
		container.OnItemQuantityChanged -= OnQuantityDelta;
		container.OnItemPresentChanged += OnItemPresentChanged;
		container.OnItemQuantityChanged += OnQuantityDelta;

		if (containers.Contains(container) == false)
			containers.Add(container);

		foreach (var item in container.ItemTotals)
		{
			if (item.Value > 0)
				OnItemRegistered(container, item.Key);
		}
	}

	private void UnregisterContainer(ShelfBase container)
	{
		if (container == null)
			return;

		UnsubscribeContainer(container);
		containers.Remove(container);
		RemoveContainerFromIndex(container);
	}

	private void UnsubscribeContainer(ShelfBase container)
	{
		if (container == null)
			return;

		container.OnItemPresentChanged -= OnItemPresentChanged;
		container.OnItemQuantityChanged -= OnQuantityDelta;
	}

	private void RemoveContainerFromIndex(ShelfBase container)
	{
		foreach (var item in container.ItemTotals)
			OnItemUnregistered(container, item.Key);
	}

	// ---------------------------
	// 아이템 관련
	// ---------------------------
	public IEnumerable<ShelfBase> GetSources(uint itemId)
	{
		if (shelvesByItem.TryGetValue(itemId, out var locations) == false)
			yield break;

		for (int i = 0; i < locations.Count; ++i)
		{
			ShelfBase shelf = locations[i];
			if (shelf != null && shelf.GetPickableQuantity(itemId) > 0)
				yield return shelf;
		}
	}

	public IEnumerable<ShelfBase> GetSources(uint buildingId, uint itemId)
	{
		if (buildingId == 0)
		{
			foreach (ShelfBase source in GetSources(itemId))
				yield return source;

			yield break;
		}

		if (shelvesByItem.TryGetValue(itemId, out var locations) == false)
			yield break;

		for (int i = 0; i < locations.Count; ++i)
		{
			ShelfBase shelf = locations[i];
			if (shelf == null || shelf.GetPickableQuantity(itemId) <= 0)
				continue;

			if (TryGetBuildingId(shelf, out uint shelfBuildingId) == false || shelfBuildingId != buildingId)
				continue;

			yield return shelf;
		}
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

	public void TestStoreItem()
	{
		if (Containers.Count <= 0)
		{
			Debug.Log("No Item Container Found");
			return;
		}

		Containers[0].AddItem(111111, 10);
	}


}
