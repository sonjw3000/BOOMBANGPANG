using System.Collections.Generic;
using UnityEngine;

public class BoxPoolService : GridPlaceableManager<BoxPool>
{
	[SerializeField] private BoxBase palletPrefab;
	[SerializeField] private BoxBase boxPrefab;

	[SerializeField] private float toteCapacity = 150.0f;

	// 실제 박스들
	private List<BoxBase> boxes = new();
	private Dictionary<uint, BoxBase> boxesByBoxId = new();

	private Dictionary<BoxType, Stack<BoxBase>> pool = new();
	private uint nextBoxId = 1;

	private GameObject poolContainer;

	private void Awake()
	{
		poolContainer = new GameObject("BoxPool_Inactive");
		poolContainer.transform.SetParent(this.transform);
		poolContainer.SetActive(false);

		foreach (BoxType type in System.Enum.GetValues(typeof(BoxType)))
		{
			if (type == BoxType.None || type == BoxType.Any) continue;
			pool[type] = new Stack<BoxBase>();
		}
	}

	public IReadOnlyList<BoxBase> Boxes => boxes;
	//public IReadOnlyList<BoxPool> BoxPoolZones => boxPoolZones;

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

		if (!boxes.Contains(box))
			boxes.Add(box);
	}

	public void UnRegisterBox(BoxBase box)
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

	public void ReturnToPool(BoxBase box)
	{
		if (box == null) return;

		box.ResetContainer();
		box.gameObject.SetActive(false);
		box.transform.SetParent(poolContainer.transform);

		if (!pool.ContainsKey(box.Type))
		{
			pool[box.Type] = new Stack<BoxBase>();
		}
		
		pool[box.Type].Push(box);
	}

	public void GiveNewBox(BoxPool boxPool, BoxType type)
	{
		BoxBase box = null;

		if (pool.TryGetValue(type, out var stack) && stack.Count > 0)
		{
			box = stack.Pop();
			box.gameObject.SetActive(true);
		}
		else
		{
			box = Instantiate(type == BoxType.Cargo ? palletPrefab : boxPrefab, boxPool.transform).GetComponent<BoxBase>();
			RegisterBox(box);
		}

		if (box is ToteBox tote)
			tote.UpdateToteCapacity(toteCapacity);

		boxPool.PutBox(box);
	}

	public BoxBase CreateBoxForRestore(BoxType type, uint boxId)
	{
		BoxBase box = null;
		if (pool.TryGetValue(type, out var stack) && stack.Count > 0)
		{
			box = stack.Pop();
			box.gameObject.SetActive(true);
		}
		else
		{
			BoxBase prefab = type == BoxType.Cargo ? palletPrefab : boxPrefab;
			box = Instantiate(prefab, transform);
		}

		if (box is ToteBox tote)
			tote.UpdateToteCapacity(toteCapacity);

		box.SetBoxId(boxId);
		RegisterBox(box);
		box.ResetContainer();
		return box;
	}

	public BoxRegistrySaveData CaptureSaveData(System.Func<OrderLine, int> registerOrderLine)
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
}
