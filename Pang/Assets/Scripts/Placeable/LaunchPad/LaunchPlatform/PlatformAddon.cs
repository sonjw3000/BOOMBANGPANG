using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


public abstract class PlatformAddon
	: MonoBehaviour
	//, IGridPlaceable
	//, IGridPlacementEffect
	//, IInteractionPoint
{

	protected LaunchStation station;


	private int3 gridPosition;
	protected List<int3> interactionPoints = new();

	public int3 GridPosition => gridPosition;
	public IReadOnlyList<int3> InteractionPoints => interactionPoints;



	public void SetPadBase(LaunchStation station) => this.station = station;

	//public void OnPositionSet(in int3 position)
	//{
	//	gridPosition = position;
	//}

	//public void OnRemoved()
	//{

	//}

	//public void OnDestroyedBy(in DestroyContext ctx)
	//{

	//}

}
