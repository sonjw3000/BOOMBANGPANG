using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public class BoxPoolService : FacilityService<BoxPool>
{
	[SerializeField] private BoxBase palletPrefab;
	[SerializeField] private BoxBase boxPrefab;
	[SerializeField] private CargoCapsule capsulePrefab;

	[SerializeField] private float toteCapacity = 150.0f;

	private readonly List<BoxBase> boxes = new();
	private readonly Dictionary<uint, BoxBase> boxesByBoxId = new();
	private readonly Dictionary<BoxType, Stack<BoxBase>> pool = new();
	private uint nextBoxId = 1;

	private GameObject poolContainer;

	private void Awake()
	{
		poolContainer = new GameObject("BoxPool_Inactive");
		poolContainer.transform.SetParent(transform);
		poolContainer.SetActive(false);

		foreach (BoxType type in Enum.GetValues(typeof(BoxType)))
		{
			if (type == BoxType.None || type == BoxType.Any)
				continue;

			pool[type] = new Stack<BoxBase>();
		}
	}

	public IReadOnlyList<BoxBase> Boxes => boxes;
	public IReadOnlyList<BoxPool> RegisteredBoxPools => CollectRegisteredBoxPools();

	public float ToteCapacity => toteCapacity;
	public uint NextBoxId => nextBoxId;

	public void RegisterBox(BoxBase box)
	{
		if (box == null)
			return;

		if (box.BoxId <= 0)
			box.SetBoxId(nextBoxId++);
		else if (nextBoxId <= box.BoxId)
			nextBoxId = box.BoxId + 1;

		boxesByBoxId[box.BoxId] = box;

		if (boxes.Contains(box) == false)
			boxes.Add(box);
	}

	public void UnregisterBox(BoxBase box)
	{
		if (box != null &&
			box.BoxId > 0 &&
			boxesByBoxId.TryGetValue(box.BoxId, out var registeredBox) &&
			registeredBox == box)
		{
			boxesByBoxId.Remove(box.BoxId);
		}

		boxes.Remove(box);
	}

	public uint GetOrCreateBoxId(BoxBase box)
	{
		if (box == null)
			return 0;

		RegisterBox(box);
		return box.BoxId;
	}

	public void SetNextBoxId(uint nextId)
	{
		nextBoxId = nextId == 0 ? 1u : nextId;
	}

	public bool TryGetBoxById(uint boxId, out BoxBase box)
	{
		return boxesByBoxId.TryGetValue(boxId, out box);
	}

	public BoxPool GetClosestAvailableTarget(in int3 pos, InteractionKind interactionKind)
	{
		FacilityDistanceResolver distanceResolver = (BoxPool candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GridService,
				out _,
				out score);

		Predicate<BoxPool> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);
		return TryFindClosestFacility(pos, distanceResolver, out BoxPool target, predicate)
			? target
			: null;
	}

	public BoxPool GetClosestAvailableTarget(uint buildingId, in int3 pos, InteractionKind interactionKind)
	{
		if (buildingId == 0)
			return GetClosestAvailableTarget(pos, interactionKind);

		FacilityDistanceResolver distanceResolver = (BoxPool candidate, in int3 origin, out int score) =>
			InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				candidate,
				interactionKind,
				origin,
				GridService,
				out _,
				out score);

		Predicate<BoxPool> predicate = candidate => candidate.IsInteractionAvailable(interactionKind);
		return TryFindClosestFacility(buildingId, pos, distanceResolver, out BoxPool target, predicate)
			? target
			: null;
	}

	public void ReturnToPool(BoxBase box)
	{
		if (box == null)
			return;

		box.ResetContainer();
		box.gameObject.SetActive(false);
		box.transform.SetParent(poolContainer.transform);

		if (pool.ContainsKey(box.Type) == false)
			pool[box.Type] = new Stack<BoxBase>();

		pool[box.Type].Push(box);
	}

	public BoxBase TakeBox(BoxType type, Transform parent = null)
	{
		BoxBase box = null;

		if (pool.TryGetValue(type, out var stack) && stack.Count > 0)
		{
			box = stack.Pop();
			box.gameObject.SetActive(true);
		}
		else
		{
			BoxBase prefab = ResolvePrefab(type);
			if (prefab == null)
			{
				Debug.LogError($"[BoxPoolService] Missing prefab for box type '{type}'.");
				return null;
			}

			box = Instantiate(prefab, parent != null ? parent : transform);
			RegisterBox(box);
		}

		if (parent != null)
			box.transform.SetParent(parent, false);

		if (box is ToteBox tote)
			tote.UpdateToteCapacity(toteCapacity);

		box.ResetContainer();
		return box;
	}

	public void GiveNewBox(BoxPool boxPool, BoxType type)
	{
		BoxBase box = TakeBox(type, boxPool.transform);
		if (box == null)
			return;

		boxPool.PutBox(box);
	}

	public BoxBase CreateBoxForRestore(BoxType type, uint boxId)
	{
		BoxBase box = TakeBox(type, transform);
		if (box == null)
			return null;

		box.SetBoxId(boxId);
		RegisterBox(box);
		return box;
	}

	public BoxRegistrySaveData CaptureSaveData(Func<OrderLine, int> registerOrderLine)
	{
		BoxRegistrySaveData data = new()
		{
			NextBoxId = nextBoxId,
		};

		foreach (var box in boxes)
		{
			if (box == null)
				continue;

			RegisterBox(box);
			data.Boxes.Add(box.CaptureState(registerOrderLine));
		}

		foreach (var stack in pool.Values)
		{
			foreach (var box in stack)
			{
				if (box == null)
					continue;

				data.InactivePoolBoxIds.Add(GetOrCreateBoxId(box));
			}
		}

		return data;
	}

	public void RestoreSaveData(BoxRegistrySaveData data, Dictionary<uint, BoxBase> restoredBoxes, Dictionary<int, OrderLine> restoredOrderLines)
	{
		if (data == null)
		{
			nextBoxId = 1;
			return;
		}

		foreach (var boxData in data.Boxes)
		{
			BoxBase box = CreateBoxForRestore(boxData.BoxType, boxData.BoxId);
			box.RestoreState(boxData, restoredOrderLines);
			restoredBoxes[boxData.BoxId] = box;
		}

		foreach (uint boxId in data.InactivePoolBoxIds)
		{
			if (restoredBoxes.TryGetValue(boxId, out var box))
				ReturnToPool(box);
		}

		nextBoxId = data.NextBoxId > nextBoxId ? data.NextBoxId : nextBoxId;
	}

	public void DestroyAllBoxes()
	{
		foreach (var box in new List<BoxBase>(boxes))
		{
			if (box != null)
				Destroy(box.gameObject);
		}

		boxes.Clear();
		boxesByBoxId.Clear();
		foreach (var stack in pool.Values)
			stack.Clear();
	}

	public void ResetRuntimeState()
	{
		nextBoxId = 1;
		boxesByBoxId.Clear();
		foreach (var stack in pool.Values)
			stack.Clear();
	}

	private IReadOnlyList<BoxPool> CollectRegisteredBoxPools()
	{
		List<BoxPool> result = new();
		IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
		for (int i = 0; i < buildingIds.Count; ++i)
		{
			if (TryGetBuildingFacilities(buildingIds[i], out IReadOnlyList<BoxPool> facilities) == false)
				continue;

			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
			{
				BoxPool poolFacility = facilities[facilityIndex];
				if (poolFacility != null)
					result.Add(poolFacility);
			}
		}

		return result;
	}

	private BoxBase ResolvePrefab(BoxType type)
	{
		return type switch
		{
			BoxType.Cargo => palletPrefab,
			BoxType.Capsule => capsulePrefab,
			_ => boxPrefab,
		};
	}
}
