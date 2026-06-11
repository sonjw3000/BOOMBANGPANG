using Unity.Mathematics;
using UnityEngine;

public class InteractionContext
{
	public enum InteractionDomain
	{
		Facility,
		Building,
	}

	public enum InteractionAction
	{
		Select,
		Install,
		ZoneEdit,
	}

	public enum InteractionMode
	{
		FacilitySelect,
		FacilityPlacement,
		BuildingSelect,
		BuildingPlacement,
		BuildingZoneEdit,
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

	public readonly struct BuildingPlacementPreview
	{
		public readonly int Floor;
		public readonly int3 Start;
		public readonly int3 End;
		public readonly bool HasStart;

		public BuildingPlacementPreview(int floor, in int3 start, in int3 end, bool hasStart)
		{
			Floor = floor;
			Start = start;
			End = end;
			HasStart = hasStart;
		}
	}

	private InteractionDomain interactionDomain = InteractionDomain.Facility;
	private InteractionAction interactionAction = InteractionAction.Select;
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

	// building placement
	private bool hasBuildingPlacementStart;
	private int buildingPlacementFloor;
	private int3 buildingPlacementStart;

	// placement mouse move event
	public event System.Action<int3> OnMouseChangedOnPlacement;
	public event System.Action<PlaceableDefinition> OnPlacementChanged;

	// select event
	public event System.Action<GameObject> OnItemSelected;
	public event System.Func<int3, GameObject> OnResolveSelectionFallback;
	public event System.Func<int3, bool> OnHandleBuildingSelection;

	// zone placement event
	public event System.Action<ZonePlacementPreview> OnZonePlacementPreviewChanged;
	public event System.Action<ZoneType> OnZonePlacementChanged;
	public event System.Action<ZoneType, RectInt, int> OnZonePlacementConfirmed;
	public event System.Action<BuildingPlacementPreview> OnBuildingPlacementPreviewChanged;
	public event System.Action<int> OnBuildingPlacementChanged;
	public event System.Action<RectInt, int> OnBuildingPlacementConfirmed;
	public event System.Action<InteractionDomain, InteractionAction> OnModeChanged;

	public PlaceableDefinition ToBePlaced => toBePlaced;
	public InteractionMode Mode => ResolveMode(interactionDomain, interactionAction);
	public InteractionDomain Domain => interactionDomain;
	public InteractionAction Action => interactionAction;
	public FacingDirection Direction => direction;
	private GridService GridService => GameContext.Instance.GridService;

	private static InteractionMode ResolveMode(InteractionDomain domain, InteractionAction action)
	{
		return (domain, action) switch
		{
			(InteractionDomain.Facility, InteractionAction.Select) => InteractionMode.FacilitySelect,
			(InteractionDomain.Facility, InteractionAction.Install) => InteractionMode.FacilityPlacement,
			(InteractionDomain.Building, InteractionAction.Select) => InteractionMode.BuildingSelect,
			(InteractionDomain.Building, InteractionAction.Install) => InteractionMode.BuildingPlacement,
			(InteractionDomain.Building, InteractionAction.ZoneEdit) => InteractionMode.BuildingZoneEdit,
			_ => InteractionMode.FacilitySelect,
		};
	}

	private void SetMode(InteractionDomain domain, InteractionAction action)
	{
		if (interactionDomain == domain && interactionAction == action)
			return;

		interactionDomain = domain;
		interactionAction = action;
		OnModeChanged?.Invoke(interactionDomain, interactionAction);
	}

	private void OnSelectionChange(GameObject gridObj)
	{
		selectedObject = gridObj;
		OnItemSelected?.Invoke(gridObj);
	}

	private void CancelActivePlacementMode()
	{
		switch (Mode)
		{
			case InteractionMode.FacilityPlacement:
				ExitPlacementMode();
				break;

			case InteractionMode.BuildingPlacement:
				ExitBuildingPlacementMode();
				break;

			case InteractionMode.BuildingZoneEdit:
				ExitZonePlacementMode();
				break;
		}
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

	private void RaiseBuildingPlacementPreview(in int3 currentPos)
	{
		OnBuildingPlacementPreviewChanged?.Invoke(new BuildingPlacementPreview(
			buildingPlacementFloor,
			buildingPlacementStart,
			currentPos,
			hasBuildingPlacementStart
		));
	}

	public void EnterPlacementMode(PlaceableDefinition pd)
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Facility, InteractionAction.Install);
		toBePlaced = pd;
		selectedObject = null;
		hasZonePlacementStart = false;
		hasBuildingPlacementStart = false;

		OnItemSelected?.Invoke(null);
		OnPlacementChanged?.Invoke(toBePlaced);
	}

	public void EnterBuildingSelectMode()
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Building, InteractionAction.Select);
		selectedObject = null;
		OnItemSelected?.Invoke(null);
	}

	public void EnterZonePlacementMode(ZoneType zoneType, int floor)
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Building, InteractionAction.ZoneEdit);
		toBePlaced = null;
		zoneToBePlaced = zoneType;
		zonePlacementFloor = floor;
		hasZonePlacementStart = false;
		hasBuildingPlacementStart = false;
		selectedObject = null;

		OnItemSelected?.Invoke(null);
		OnZonePlacementChanged?.Invoke(zoneType);
		RaiseZonePlacementPreview(mousePos);
	}

	public void EnterBuildingPlacementMode(int floor)
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Building, InteractionAction.Install);
		toBePlaced = null;
		buildingPlacementFloor = floor;
		hasZonePlacementStart = false;
		hasBuildingPlacementStart = false;
		selectedObject = null;

		OnItemSelected?.Invoke(null);
		OnBuildingPlacementChanged?.Invoke(floor);
		RaiseBuildingPlacementPreview(mousePos);
	}

	public void ExitPlacementMode()
	{
		SetMode(InteractionDomain.Facility, InteractionAction.Select);
		toBePlaced = null;
		hasZonePlacementStart = false;
		hasBuildingPlacementStart = false;

		OnPlacementChanged?.Invoke(null);
	}

	public void ExitZonePlacementMode()
	{
		SetMode(InteractionDomain.Building, InteractionAction.Select);
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

	public void ExitBuildingPlacementMode()
	{
		SetMode(InteractionDomain.Building, InteractionAction.Select);
		hasBuildingPlacementStart = false;

		OnBuildingPlacementChanged?.Invoke(buildingPlacementFloor);
		OnBuildingPlacementPreviewChanged?.Invoke(new BuildingPlacementPreview(
			buildingPlacementFloor,
			buildingPlacementStart,
			mousePos,
			false
		));
	}

	public void ClearSelection()
	{
		OnSelectionChange(null);
	}

	public void SelectObject(GameObject gridObj)
	{
		OnSelectionChange(gridObj);
	}

	public void ExitBuildingMode()
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Facility, InteractionAction.Select);
		selectedObject = null;
		OnItemSelected?.Invoke(null);
	}

	public void OnMouseMove(int3 pos)
	{
		mousePos = pos;

		switch (Mode)
		{
			case InteractionMode.FacilityPlacement:
				OnMouseChangedOnPlacement?.Invoke(mousePos);
				break;

			case InteractionMode.BuildingZoneEdit:
				RaiseZonePlacementPreview(mousePos);
				break;

			case InteractionMode.BuildingPlacement:
				RaiseBuildingPlacementPreview(mousePos);
				break;
		}
	}

	public void OnLeftClick(in int3 pos)
	{
		switch (Mode)
		{
			case InteractionMode.FacilitySelect:
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

			case InteractionMode.BuildingSelect:
				bool handled = false;
				if (OnHandleBuildingSelection != null)
				{
					foreach (System.Func<int3, bool> handler in OnHandleBuildingSelection.GetInvocationList())
					{
						handled = handler(pos);
						if (handled)
							break;
					}
				}

				if (handled == false)
					OnSelectionChange(null);
				break;

			case InteractionMode.FacilityPlacement:
				PlacementContext ctx = new(
					center: mousePos,
					dir: direction,
					def: toBePlaced
				);
				GridService.OnInstall(ctx);
				break;

			case InteractionMode.BuildingZoneEdit:
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

			case InteractionMode.BuildingPlacement:
				if (hasBuildingPlacementStart == false)
				{
					hasBuildingPlacementStart = true;
					buildingPlacementStart = pos;
					RaiseBuildingPlacementPreview(pos);
					break;
				}

				int buildingMinX = Mathf.Min(buildingPlacementStart.x, pos.x);
				int buildingMinZ = Mathf.Min(buildingPlacementStart.z, pos.z);
				int buildingMaxX = Mathf.Max(buildingPlacementStart.x, pos.x);
				int buildingMaxZ = Mathf.Max(buildingPlacementStart.z, pos.z);
				var buildingBound = new RectInt(buildingMinX, buildingMinZ, (buildingMaxX - buildingMinX) + 1, (buildingMaxZ - buildingMinZ) + 1);

				OnBuildingPlacementConfirmed?.Invoke(buildingBound, buildingPlacementFloor);
				break;
		}
	}

	public void OnRightClick(in int3 pos)
	{
		switch (Mode)
		{
			case InteractionMode.FacilitySelect:
			case InteractionMode.BuildingSelect:
				break;

			case InteractionMode.FacilityPlacement:
				ExitPlacementMode();
				break;

			case InteractionMode.BuildingZoneEdit:
				ExitZonePlacementMode();
				break;

			case InteractionMode.BuildingPlacement:
				ExitBuildingPlacementMode();
				break;
		}
	}

	public void RotatePlacement()
	{
		if (Mode != InteractionMode.FacilityPlacement)
			return;

		direction = direction.Rotate90CW();
		OnMouseChangedOnPlacement?.Invoke(mousePos);
	}
}
