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
	public void AddInteractionPoint(InteractionKind interactionKind, in int3 point);

	public int3 GetClosestInteractionPoint(InteractionKind interactionKind, in int3 from);

	public bool IsInteractionAvailable(InteractionKind interactionKind);
}
