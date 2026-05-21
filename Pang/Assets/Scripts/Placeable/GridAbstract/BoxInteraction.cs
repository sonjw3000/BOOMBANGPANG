using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public abstract class BoxInteraction :
	MonoBehaviour,
	IGridPlaceable,
	IGridPlacementEffect,
	IInteractionPoint,
	IBoxHandleable
{
	protected int3 position;
	protected FacingDirection facingDirection;

	protected List<InteractionPoint> interactionPoints = new();
	protected Dictionary<InteractionKind, List<int3>> interactionPointMap = new();

	public IReadOnlyList<InteractionPoint> InteractionPoints => interactionPoints;
	public IReadOnlyDictionary<InteractionKind, List<int3>> InteractionPointMap => interactionPointMap;

	public int3 GridPosition => position;
	public FacingDirection Direction => facingDirection;

	// grid placement effect
	public abstract void OnPositionSet(in int3 pos, FacingDirection direction);
	public abstract void OnDestroyedBy(in DestroyContext ctx);
	public abstract void OnRemoved();

	// box handling
	public abstract bool GetBox(out BoxBase box);
	public abstract bool PutBox(BoxBase box);
	public abstract bool CanGetBox();
	public abstract bool CanPutBox();
	public abstract WorkerStatusTarget BuildingTarget { get; }

	// interaction point
	public void ClearInteractionPoints()
	{
		interactionPoints.Clear();
		interactionPointMap.Clear();
	}

	public void AddInteractionPoint(InteractionKind interactionKind, in int3 point)
	{
		interactionPoints.Add(new(interactionKind, point));

		foreach (InteractionKind value in Enum.GetValues(typeof(InteractionKind)))
		{
			if (value == InteractionKind.None) continue;

			if (interactionKind.HasFlag(value))
			{
				if (!interactionPointMap.ContainsKey(value))
					interactionPointMap[value] = new List<int3>();

				interactionPointMap[value].Add(point);
			}
		}
	}
	public int3 GetClosestInteractionPoint(InteractionKind interactionKind, in int3 from)
	{
		float distance = float.PositiveInfinity;
		int3 closestPoint = default;

		foreach (int3 point in interactionPointMap[interactionKind])
		{
			float d = math.distance(point, from);
			if (distance > d)
			{
				distance = d;
				closestPoint = point;
			}
		}

		if (distance == float.PositiveInfinity)
		{
			Debug.LogError($"No interaction point for {interactionKind} in BoxPool at {position}");
		}

		return closestPoint;
	}

	public bool IsInteractionAvailable(InteractionKind interactionKind)
	{
		if (interactionKind == InteractionKind.Pick)
			return CanGetBox();
		else if(interactionKind == InteractionKind.Put)
			return CanPutBox();

		return false;
	}

}
