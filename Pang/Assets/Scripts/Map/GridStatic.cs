using System;
using System.Collections.Generic;
using Unity.Mathematics;

public class GridStatic
{
	public static IGridPlaceable GetClosestPlaceable(in int3 pos, IReadOnlyCollection<IGridPlaceable> list, Predicate<IGridPlaceable> pred = null)
	{
		IGridPlaceable closest = null;
		float closestDistSq = float.MaxValue;

		foreach (var placeable in list)
		{
			if (pred(placeable) == false) continue;

			float distSq = math.distancesq(pos, placeable.GridPosition);
			if (distSq < closestDistSq)
			{
				closestDistSq = distSq;
				closest = placeable;
			}
		}

		return closest;
	}

}

