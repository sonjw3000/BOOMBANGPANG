using System.Collections.Generic;
using Unity.Mathematics;

public class InteractionPoint
{
	readonly public InteractionKind InteractionKind;
	readonly public int3 Point;

	public InteractionPoint(InteractionKind interactionKind, int3 point)
	{
		InteractionKind = interactionKind;
		Point = point;
	}
}

public interface IInteractionPoint
{
	public IReadOnlyList<InteractionPoint> InteractionPoints { get; }

	public void ClearInteractionPoints();

	public void AddInteractionPoint(InteractionKind interactionKind, in int3 point);

	public int3 GetClosestInteractionPoint(InteractionKind interactionKind, in int3 from);

	public bool IsInteractionAvailable(InteractionKind interactionKind);
}

public static class InteractionPointSelector
{
	public static bool TryGetInteractionPointInBuilding(
		IInteractionPoint target,
		InteractionKind interactionKind,
		in int3 from,
		uint buildingId,
		out int3 point,
		out int distance)
	{
		if (buildingId == 0)
			return TryGetInteractionPoint(target, interactionKind, from, out point, out distance);

		point = default;
		distance = int.MaxValue;

		var gridService = GameContext.Instance.GridService;
		if (target == null || gridService == null)
			return false;

		IReadOnlyList<InteractionPoint> interactionPoints = target.InteractionPoints;
		if (interactionPoints == null || interactionPoints.Count <= 0)
			return false;

		bool found = false;
		for (int i = 0; i < interactionPoints.Count; ++i)
		{
			InteractionPoint interactionPoint = interactionPoints[i];
			if ((interactionPoint.InteractionKind & interactionKind) == 0)
				continue;

			int3 candidate = interactionPoint.Point;
			GridCell candidateCell = gridService.GetCell(candidate);
			if (candidateCell == null || candidateCell.BuildingId != buildingId)
				continue;

			int candidateDistance =
				math.abs(from.x - candidate.x) +
				math.abs(from.y - candidate.y) +
				math.abs(from.z - candidate.z);

			if (candidateDistance >= distance)
				continue;

			point = candidate;
			distance = candidateDistance;
			found = true;
		}

		return found;
	}

	public static bool TryGetInteractionPoint(
		IInteractionPoint target,
		InteractionKind interactionKind,
		in int3 from,
		out int3 point,
		out int distance)
	{
		point = default;
		distance = int.MaxValue;

		var gridService = GameContext.Instance.GridService;

		if (target == null || gridService == null)
			return false;

		IReadOnlyList<InteractionPoint> interactionPoints = target.InteractionPoints;
		if (interactionPoints == null || interactionPoints.Count <= 0)
			return false;

		bool found = false;
		for (int i = 0; i < interactionPoints.Count; ++i)
		{
			InteractionPoint interactionPoint = interactionPoints[i];
			if ((interactionPoint.InteractionKind & interactionKind) == 0)
				continue;

			int3 candidate = interactionPoint.Point;
			if (gridService.IsSameRegion(from, candidate) == false)
				continue;

			int candidateDistance =
				math.abs(from.x - candidate.x) +
				math.abs(from.y - candidate.y) +
				math.abs(from.z - candidate.z);

			if (candidateDistance >= distance)
				continue;

			point = candidate;
			distance = candidateDistance;
			found = true;
		}

		return found;
	}
}
