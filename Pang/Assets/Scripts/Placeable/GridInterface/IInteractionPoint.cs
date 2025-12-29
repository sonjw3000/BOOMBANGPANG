using System.Collections.Generic;
using Unity.Mathematics;

public interface IInteractionPoint
{
	public IReadOnlyList<int3> InteractionPoints { get; }

}