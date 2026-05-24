using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public abstract class GridPlaceableManager<T> : MonoBehaviour
	where T :
	class,
	IGridPlaceable,
	IInteractionPoint
{
	[SerializeField] protected List<T> items = new();

	protected virtual void OnRegister(T item) { }
	protected virtual void OnUnregister(T item) { }

	public IReadOnlyList<T> PlaceableTargets => items;

	public void Register(T item)
	{
		OnRegister(item);
		items.Add(item);
	}

	public void Unregister(T item)
	{
		OnUnregister(item);
		items.Remove(item);
	}

	public T GetClosestAvailableTarget(in int3 pos, InteractionKind interactionKind)
	{
		T target = null;
		int posPowMin = int.MaxValue;

		for (int i = 0; i < items.Count; ++i)
		{
			if (items[i].IsInteractionAvailable(interactionKind) == false)
				continue;

			if (InteractionPointSelector.TryGetClosestSameRegionInteractionPoint(
				items[i],
				interactionKind,
				pos,
				GameContext.Instance.GridService,
				out _,
				out int sum) == false)
				continue;

			if (posPowMin > sum)
			{
				posPowMin = sum;
				target = items[i];
			}
		}

		return target;
	}
}
