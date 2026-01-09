using Unity.Mathematics;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;

public class InteractionContext
{
	private OrbitCamera mainCamera;

	public enum InteractionMode
	{
		Select,
		Placement,
	}
	private InteractionMode interactionMode = InteractionMode.Select;
	private int3 mousePos;
	
	private IGridPlaceable selectedObject;
	
	private FacingDirection direction = FacingDirection.North;
	private PlaceableDefinition toBePlaced;

	//private List<int3>
	private List<int3> possibleCells = new();
	private List<int3> blockedCells = new();


	public InteractionMode Mode => interactionMode;



	private GridService GridService => GameContext.Instance.GridService;

	private void OnSelectionChange(IGridPlaceable gridObj)
	{
		// 기존 ui hide

		selectedObject = gridObj;

		Camera.main.GetComponent<OrbitCamera>().LockObject(selectedObject);

		// selectedObj가 null이 아니라면
		// 새로운 ui 생성
	}

	public void EnterPlacementMode(PlaceableDefinition pd)
	{
		interactionMode = InteractionMode.Placement;
		toBePlaced = pd;
	}
	public void ExitPlacementMode()
	{
		interactionMode = InteractionMode.Select;
		toBePlaced = null;
	}


	public void OnMouseMove(in int3 pos)
	{
		mousePos = pos;

		possibleCells.Clear();
		blockedCells.Clear();
		PlacementContext ctx = new(
			center: mousePos,
			dir: direction,
			def: toBePlaced
		);
		GridService.OnCheckInstallable(ctx, possibleCells, blockedCells);
	}

	public void OnLeftClick(in int3 pos)
	{
		switch (Mode)
		{
			case InteractionMode.Select:
				var obj = GameContext.Instance.GridService.GetObjectOnGrid(pos);
				OnSelectionChange(obj);
				break;
	
			case InteractionMode.Placement:
				// install
				PlacementContext ctx = new(
					center: mousePos,
					dir: direction,
					def: toBePlaced
				);
				GridService.OnInstall(ctx);
				ExitPlacementMode();
				// exit mode
				break;
		}

	}

	public void OnRightClick(in int3 pos)
	{
		switch (Mode)
		{
			case InteractionMode.Select:
				
				break;

			case InteractionMode.Placement:
				ExitPlacementMode();
				break;
		}
	}

}

