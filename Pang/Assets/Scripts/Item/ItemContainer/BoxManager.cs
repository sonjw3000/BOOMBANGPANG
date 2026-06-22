using UnityEngine;
using AYellowpaper.SerializedCollections;
using System.Collections.Generic;

public partial class BoxManager : MonoBehaviour
{
	[SerializeField] private SerializedDictionary<BoxType, GameObject> boxPrefabs = new();

	private Dictionary<BoxType, GameObjectPool> boxPools = new();

	private readonly List<BoxBase> activeBoxes = new();
	private readonly Dictionary<BoxType, Dictionary<uint, BoxBase>> boxesByBoxId = new();
	private readonly Dictionary<BoxType, uint> nextBoxIdByType = new();

	private GameObject boxPoolRoot;

	private float toteCapacity = 80.0f;
	public float ToteCapacity => toteCapacity;

	private void Awake()
	{
		boxPoolRoot = new GameObject("BoxPool");
		boxPoolRoot.transform.SetParent(transform);

		foreach (var kvp in boxPrefabs)
		{
			var boxType = kvp.Key;
			var prefab = kvp.Value;

			if (prefab == null)
			{
				Debug.LogWarning($"Box prefab for BoxType {boxType} is not assigned.");
				continue;
			}

			nextBoxIdByType[boxType] = 1;
			boxesByBoxId[boxType] = new();
			boxPools[boxType] = new GameObjectPool(10, () => Instantiate(prefab, boxPoolRoot.transform));
		}
	}

	public bool GetNewBox(BoxType boxType, out BoxBase box)
	{
		box = null;

		if (!boxPools.TryGetValue(boxType, out var pool))
		{
			Debug.LogWarning($"No pool found for BoxType {boxType}.");
			return false;
		}

		box = pool.Get().GetComponent<BoxBase>();
		if (box == null)
		{
			Debug.LogWarning($"The prefab for BoxType {boxType} does not have a BoxBase component.");
			return false;
		}

		box.ResetContainer();
		if (box is ToteBox tote)
			tote.UpdateToteCapacity(toteCapacity);

		box.SetBoxId(nextBoxIdByType[boxType]++);

		if (activeBoxes.Contains(box) == false)
			activeBoxes.Add(box);

		if (!boxesByBoxId.TryGetValue(box.Type, out var boxDict))
		{
			boxDict = new Dictionary<uint, BoxBase>();
			boxesByBoxId[box.Type] = boxDict;
		}

		boxDict[box.BoxId] = box;
		return true;
	}

	public bool DisableBox(BoxBase box)
	{
		if (box == null)
			return false;

		if (!boxesByBoxId.TryGetValue(box.Type, out var boxDict))
			return false;

		if (!boxDict.ContainsKey(box.BoxId))
			return false;

		boxDict.Remove(box.BoxId);
		activeBoxes.Remove(box);

		if (boxPools.TryGetValue(box.Type, out var pool) == false)
		{
			Debug.LogWarning($"No pool found for BoxType {box.Type}.");
			return false;
		}

		box.ResetContainer();
		box.transform.SetParent(boxPoolRoot.transform, false);
		pool.Release(box.gameObject);
		return true;
	}

	public bool TryGetBox(BoxType boxType, uint boxId, out BoxBase box)
	{
		box = null;
		return boxesByBoxId.TryGetValue(boxType, out var boxDict) && boxDict.TryGetValue(boxId, out box);
	}
}
