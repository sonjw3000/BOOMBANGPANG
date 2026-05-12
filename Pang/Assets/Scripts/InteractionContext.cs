using Unity.Mathematics;
using UnityEngine;

public class InteractionContext
{
	public enum InteractionMode
	{
		Select,
		Placement,
		ZonePlacement,
	}

	public readonly struct ZonePlacementPreview
	{
		public readonly ZoneType ZoneType;
		public readonly int Floor;
		public readonly int3 Start;
		public readonly int3 End;
		public readonly bool HasStart;

		public ZonePlacementPreview(ZoneType zoneType, int floor, in int3 start, in int3 end, bool hasStart)
		{
			ZoneType = zoneType;
			Floor = floor;
			Start = start;
			End = end;
			HasStart = hasStart;
		}
	}

	private InteractionMode interactionMode = InteractionMode.Select;
	private int3 mousePos;

	// select
	private GameObject selectedObject;
	public GameObject SelectedObject => selectedObject;

	// placement
	private FacingDirection direction = FacingDirection.North;
	private PlaceableDefinition toBePlaced;

	// zone placement
	private ZoneType zoneToBePlaced;
	private bool hasZonePlacementStart;
	private int zonePlacementFloor;
	private int3 zonePlacementStart;

	// placement mouse move event
	public event System.Action<int3> OnMouseChangedOnPlacement;
	public event System.Action<PlaceableDefinition> OnPlacementChanged;

	// select event
	public event System.Action<GameObject> OnItemSelected;
	public event System.Func<int3, GameObject> OnResolveSelectionFallback;

	// zone placement event
	public event System.Action<ZonePlacementPreview> OnZonePlacementPreviewChanged;
	public event System.Action<ZoneType> OnZonePlacementChanged;
	public event System.Action<ZoneType, RectInt, int> OnZonePlacementConfirmed;

	public PlaceableDefinition ToBePlaced => toBePlaced;
	public InteractionMode Mode => interactionMode;
	public FacingDirection Direction => direction;
	private GridService GridService => GameContext.Instance.GridService;

	private void OnSelectionChange(GameObject gridObj)
	{
		selectedObject = gridObj;
		OnItemSelected?.Invoke(gridObj);
	}

	private void RaiseZonePlacementPreview(in int3 currentPos)
	{
		OnZonePlacementPreviewChanged?.Invoke(new ZonePlacementPreview(
			zoneToBePlaced,
			zonePlacementFloor,
			zonePlacementStart,
			currentPos,
			hasZonePlacementStart
		));
	}

	public void EnterPlacementMode(PlaceableDefinition pd)
	{
		interactionMode = InteractionMode.Placement;
		toBePlaced = pd;
		selectedObject = null;
		hasZonePlacementStart = false;

		OnPlacementChanged?.Invoke(toBePlaced);
	}

	public void EnterZonePlacementMode(ZoneType zoneType, int floor)
	{
		interactionMode = InteractionMode.ZonePlacement;
		zoneToBePlaced = zoneType;
		zonePlacementFloor = floor;
		hasZonePlacementStart = false;
		selectedObject = null;

		OnItemSelected?.Invoke(null);
		OnZonePlacementChanged?.Invoke(zoneType);
		RaiseZonePlacementPreview(mousePos);
	}

	public void ExitPlacementMode()
	{
		interactionMode = InteractionMode.Select;
		toBePlaced = null;

		OnPlacementChanged?.Invoke(null);
	}

	public void ExitZonePlacementMode()
	{
		interactionMode = InteractionMode.Select;
		hasZonePlacementStart = false;

		OnZonePlacementChanged?.Invoke(zoneToBePlaced);
		OnZonePlacementPreviewChanged?.Invoke(new ZonePlacementPreview(
			zoneToBePlaced,
			zonePlacementFloor,
			zonePlacementStart,
			mousePos,
			false
		));
	}

	public void ClearSelection()
	{
		OnSelectionChange(null);
	}

	public void OnMouseMove(int3 pos)
	{
		mousePos = pos;

		switch (Mode)
		{
			case InteractionMode.Placement:
				OnMouseChangedOnPlacement?.Invoke(mousePos);
				break;

			case InteractionMode.ZonePlacement:
				RaiseZonePlacementPreview(mousePos);
				break;
		}
	}

	public void OnLeftClick(in int3 pos)
	{
		switch (Mode)
		{
			case InteractionMode.Select:
				var obj = GameContext.Instance.GridService.GetObjectOnGrid(pos);
				if (obj == null && OnResolveSelectionFallback != null)
				{
					foreach (System.Func<int3, GameObject> resolver in OnResolveSelectionFallback.GetInvocationList())
					{
						obj = resolver(pos);
						if (obj != null)
							break;
					}
				}

				OnSelectionChange(obj);
				break;

			case InteractionMode.Placement:
				PlacementContext ctx = new(
					center: mousePos,
					dir: direction,
					def: toBePlaced
				);
				GridService.OnInstall(ctx);
				ExitPlacementMode();
				break;

			case InteractionMode.ZonePlacement:
				if (hasZonePlacementStart == false)
				{
					hasZonePlacementStart = true;
					zonePlacementStart = pos;
					RaiseZonePlacementPreview(pos);
					break;
				}

				int minX = Mathf.Min(zonePlacementStart.x, pos.x);
				int minZ = Mathf.Min(zonePlacementStart.z, pos.z);
				int maxX = Mathf.Max(zonePlacementStart.x, pos.x);
				int maxZ = Mathf.Max(zonePlacementStart.z, pos.z);
				var bound = new RectInt(minX, minZ, (maxX - minX) + 1, (maxZ - minZ) + 1);

				OnZonePlacementConfirmed?.Invoke(zoneToBePlaced, bound, zonePlacementFloor);
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

			case InteractionMode.ZonePlacement:
				ExitZonePlacementMode();
				break;
		}
	}

	public void RotatePlacement()
	{
		if (Mode != InteractionMode.Placement)
			return;

		direction = direction.Rotate90CW();
		OnMouseChangedOnPlacement?.Invoke(mousePos);
	}
}
