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
	public IReadOnlyList<int3> InteractionPoints { get; }
	//public IReadOnlyDictionary<InteractionKind, IReadOnlyList<int3>> InteractionPointMap { get; }

}
