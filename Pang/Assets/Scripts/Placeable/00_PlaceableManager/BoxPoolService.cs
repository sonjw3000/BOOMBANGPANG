using System.Collections.Generic;
using UnityEngine;

public class BoxPoolService : GridPlaceableManager<BoxPool>
{
	[SerializeField] private BoxBase palletPrefab;
	[SerializeField] private BoxBase boxPrefab;

	[SerializeField] private float toteCapacity = 150.0f;

	// 실제 박스들
	private List<BoxBase> boxes = new();

	private Dictionary<BoxType, Stack<BoxBase>> pool = new();

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

	public void RegisterBox(BoxBase box)
	{
		if (!boxes.Contains(box))
			boxes.Add(box);
	}

	public void UnRegisterBox(BoxBase box)
	{
		boxes.Remove(box);
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
		}

		if (box is ToteBox tote)
			tote.UpdateToteCapacity(toteCapacity);

		boxPool.PutBox(box);
	}
}
