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

	// select
	private IGridPlaceable selectedObject;

	// placement
	private FacingDirection direction = FacingDirection.North;
	private PlaceableDefinition toBePlaced;

	// placement mouse move event
	public event System.Action<int3> OnMouseChangedOnPlacement;
	public event System.Action<PlaceableDefinition> OnPlacementChanged;

	public PlaceableDefinition ToBePlaced => toBePlaced;
	public InteractionMode Mode => interactionMode;
	public FacingDirection Direction => direction;
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

		OnPlacementChanged?.Invoke(toBePlaced);
	}
	public void ExitPlacementMode()
	{
		interactionMode = InteractionMode.Select;
		toBePlaced = null;
	}


	public void OnMouseMove(int3 pos)
	{
		if (Mode == InteractionMode.Select)
			return;

		// 일단은 placement에 대해서만
		mousePos = pos;

		OnMouseChangedOnPlacement?.Invoke(mousePos);
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

