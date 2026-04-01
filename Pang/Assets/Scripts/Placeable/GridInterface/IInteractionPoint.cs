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
	//public IReadOnlyList<int3> InteractionPoints { get; }
	//public IReadOnlyList<InteractionPoint> InteractionPoints { get; }
	////public IReadOnlyDictionary<InteractionKind, IReadOnlyList<int3>> InteractionPointMap { get; }
	//public IReadOnlyDictionary<InteractionKind, List<int3>> InteractionPointMap { get; }

	public void AddInteractionPoint(InteractionKind interactionKind, in int3 point);

	public int3 GetClosestInteractionPoint(InteractionKind interactionKind, in int3 from);

}
