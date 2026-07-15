using System.Collections.Generic;
using UnityEngine;

public partial class BoxManager
{
	public BoxRegistrySaveData CaptureSaveData(System.Func<OrderLine, int> registerOrderLine)
	{
		BoxRegistrySaveData data = new();

		foreach (var nextBoxId in nextBoxIdByType)
		{
			data.NextBoxIds.Add(new BoxIdCounterSaveData
			{
				BoxType = nextBoxId.Key,
				NextBoxId = nextBoxId.Value,
			});
		}

		foreach (var box in activeBoxes)
		{
			if (box == null)
				continue;

			data.Boxes.Add(box.CaptureState(registerOrderLine));
		}

		return data;
	}

	public void RestoreSaveData(BoxRegistrySaveData data, Dictionary<int, OrderLine> restoredOrderLines)
	{
		ResetRuntimeState();
		if (data == null)
			return;

		foreach (var nextBoxId in data.NextBoxIds)
		{
			if (nextBoxId == null || nextBoxId.BoxType == BoxType.None || nextBoxId.BoxType == BoxType.Any)
				continue;

			nextBoxIdByType[nextBoxId.BoxType] = nextBoxId.NextBoxId == 0 ? 1u : nextBoxId.NextBoxId;
		}

		foreach (var boxData in data.Boxes)
		{
			if (boxPools.TryGetValue(boxData.BoxType, out var pool) == false)
			{
				Debug.LogWarning($"[BoxManager] Missing pool for restore box type {boxData.BoxType}.");
				continue;
			}

			GameObject boxObject = pool.Get();
			if (boxObject == null || boxObject.TryGetComponent<BoxBase>(out var box) == false)
			{
				Debug.LogWarning($"[BoxManager] Failed to restore box of type {boxData.BoxType}.");
				if (boxObject != null)
					pool.Release(boxObject);
				continue;
			}

			box.ResetContainer();
			if (box is ToteBox tote)
				tote.UpdateToteCapacity(toteCapacity);

			box.SetBoxId(boxData.BoxId);
			box.MarkValid();
			box.transform.SetParent(boxPoolRoot.transform, false);

			activeBoxes.Add(box);
			if (boxesByBoxId.TryGetValue(box.Type, out var boxDict) == false)
			{
				boxDict = new Dictionary<uint, BoxBase>();
				boxesByBoxId[box.Type] = boxDict;
			}

			boxDict[box.BoxId] = box;
			box.RestoreState(boxData, restoredOrderLines);
		}
	}

	public void ResetRuntimeState()
	{
		activeBoxes.Clear();

		foreach (var boxType in boxPrefabs.Keys)
		{
			if (boxType == BoxType.None || boxType == BoxType.Any)
				continue;

			if (boxesByBoxId.ContainsKey(boxType) == false)
				boxesByBoxId[boxType] = new Dictionary<uint, BoxBase>();
			else
				boxesByBoxId[boxType].Clear();

			nextBoxIdByType[boxType] = 1;
		}
	}

	public void DestroyAllBoxes()
	{
		List<BoxBase> boxesToDisable = new(activeBoxes);
		for (int i = boxesToDisable.Count - 1; i >= 0; --i)
		{
			BoxBase box = boxesToDisable[i];
			if (box != null)
				DisableBox(box);
		}

		activeBoxes.Clear();
		foreach (var boxType in boxPrefabs.Keys)
		{
			if (boxesByBoxId.ContainsKey(boxType))
				boxesByBoxId[boxType].Clear();
		}
	}
}
