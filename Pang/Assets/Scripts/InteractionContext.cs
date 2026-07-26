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
		AreaEdit,
		LinkEdit,
	}

	public enum InteractionMode
	{
		FacilitySelect,
		FacilityPlacement,
		BuildingSelect,
		BuildingPlacement,
		AreaEdit,
		BuildingLinkEdit,
	}

	public readonly struct AreaPlacementPreview
	{
		public readonly AreaType AreaType;
		public readonly int Floor;
		public readonly int3 Start;
		public readonly int3 End;
		public readonly bool HasStart;

		public AreaPlacementPreview(AreaType areaType, int floor, in int3 start, in int3 end, bool hasStart)
		{
			AreaType = areaType;
			Floor = floor;
			Start = start;
			End = end;
			HasStart = hasStart;
		}
	}

	public readonly struct BuildingPlacementPreview
	{
		public readonly int Floor;
		public readonly int3 Center;
		public readonly bool IsActive;

		public BuildingPlacementPreview(int floor, in int3 center, bool isActive)
		{
			Floor = floor;
			Center = center;
			IsActive = isActive;
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

	// area placement
	private AreaType areaToBePlaced;
	private bool hasAreaPlacementStart;
	private int areaPlacementFloor;
	private int3 areaPlacementStart;

	// building placement
	private int buildingPlacementFloor;

	// placement mouse move event
	public event System.Action<int3> OnMouseChangedOnPlacement;
	public event System.Action<PlaceableDefinition> OnPlacementChanged;

	// select event
	public event System.Action<GameObject> OnItemSelected;
	public event System.Func<int3, bool> OnHandlePriorityLeftClick;
	public event System.Func<int3, GameObject> OnResolveSelectionFallback;
	public event System.Func<int3, bool> OnHandleBuildingSelection;
	public event System.Func<int3, bool> OnHandleBuildingLinkSelection;

	// area placement event
	public event System.Action<AreaPlacementPreview> OnAreaPlacementPreviewChanged;
	public event System.Action<AreaType> OnAreaPlacementChanged;
	public event System.Action<AreaType, RectInt, int> OnAreaPlacementConfirmed;
	public event System.Action<BuildingPlacementPreview> OnBuildingPlacementPreviewChanged;
	public event System.Action<int> OnBuildingPlacementChanged;
	public event System.Action<int3, int> OnBuildingPlacementConfirmed;
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
			(InteractionDomain.Facility, InteractionAction.AreaEdit) => InteractionMode.AreaEdit,
			(InteractionDomain.Building, InteractionAction.LinkEdit) => InteractionMode.BuildingLinkEdit,
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

			case InteractionMode.AreaEdit:
				ExitAreaPlacementMode();
				break;

			case InteractionMode.BuildingLinkEdit:
				ExitBuildingLinkMode();
				break;
		}
	}

	private void RaiseAreaPlacementPreview(in int3 currentPos)
	{
		OnAreaPlacementPreviewChanged?.Invoke(new AreaPlacementPreview(
			areaToBePlaced,
			areaPlacementFloor,
			areaPlacementStart,
			currentPos,
			hasAreaPlacementStart
		));
	}

	private void RaiseBuildingPlacementPreview(in int3 currentPos)
	{
		OnBuildingPlacementPreviewChanged?.Invoke(new BuildingPlacementPreview(
			buildingPlacementFloor,
			currentPos,
			Mode == InteractionMode.BuildingPlacement
		));
	}

	public void EnterPlacementMode(PlaceableDefinition pd)
	{
		if (pd == null)
			return;

		if (pd.RequiresResearch &&
			(GameContext.HasInstance == false ||
				GameContext.Instance.ResearchService?.IsResearched(pd.RequiredResearchUid) != true))
		{
			string message = $"{pd.displayName} requires research: {pd.RequiredResearchUid}.";
			Debug.LogWarning($"[InteractionContext] {message}");
			if (GameContext.HasInstance)
				GameContext.Instance.HudEventManager?.Publish(HudEventType.Warning, message);
			return;
		}

		CancelActivePlacementMode();
		SetMode(InteractionDomain.Facility, InteractionAction.Install);
		toBePlaced = pd;
		selectedObject = null;
		hasAreaPlacementStart = false;

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

	public void ToggleSelectionDomain()
	{
		if (interactionDomain == InteractionDomain.Building)
		{
			ExitBuildingMode();
			return;
		}

		EnterBuildingSelectMode();
	}

	public void EnterAreaPlacementMode(AreaType areaType, int floor)
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Facility, InteractionAction.AreaEdit);
		toBePlaced = null;
		areaToBePlaced = areaType;
		areaPlacementFloor = floor;
		hasAreaPlacementStart = false;
		selectedObject = null;

		OnItemSelected?.Invoke(null);
		OnAreaPlacementChanged?.Invoke(areaType);
		RaiseAreaPlacementPreview(mousePos);
	}

	public void EnterBuildingPlacementMode(int floor)
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Building, InteractionAction.Install);
		toBePlaced = null;
		buildingPlacementFloor = floor;
		hasAreaPlacementStart = false;
		selectedObject = null;

		OnItemSelected?.Invoke(null);
		OnBuildingPlacementChanged?.Invoke(floor);
		RaiseBuildingPlacementPreview(mousePos);
	}

	public void EnterBuildingLinkMode()
	{
		CancelActivePlacementMode();
		SetMode(InteractionDomain.Building, InteractionAction.LinkEdit);
		toBePlaced = null;
		hasAreaPlacementStart = false;
		selectedObject = null;

		OnItemSelected?.Invoke(null);
	}

	public void ExitPlacementMode()
	{
		SetMode(InteractionDomain.Facility, InteractionAction.Select);
		toBePlaced = null;
		hasAreaPlacementStart = false;

		OnPlacementChanged?.Invoke(null);
	}

	public void ExitAreaPlacementMode()
	{
		SetMode(InteractionDomain.Facility, InteractionAction.Select);
		hasAreaPlacementStart = false;

		OnAreaPlacementChanged?.Invoke(areaToBePlaced);
		OnAreaPlacementPreviewChanged?.Invoke(new AreaPlacementPreview(
			areaToBePlaced,
			areaPlacementFloor,
			areaPlacementStart,
			mousePos,
			false
		));
	}

	public void ExitBuildingPlacementMode()
	{
		SetMode(InteractionDomain.Building, InteractionAction.Select);

		OnBuildingPlacementChanged?.Invoke(buildingPlacementFloor);
		OnBuildingPlacementPreviewChanged?.Invoke(new BuildingPlacementPreview(
			buildingPlacementFloor,
			mousePos,
			false
		));
	}

	public void ExitBuildingLinkMode()
	{
		SetMode(InteractionDomain.Building, InteractionAction.Select);
		selectedObject = null;
		OnItemSelected?.Invoke(null);
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

			case InteractionMode.AreaEdit:
				RaiseAreaPlacementPreview(mousePos);
				break;

			case InteractionMode.BuildingPlacement:
				RaiseBuildingPlacementPreview(mousePos);
				break;
		}
	}

	public void OnLeftClick(in int3 pos)
	{
		if (OnHandlePriorityLeftClick != null)
		{
			foreach (System.Func<int3, bool> handler in OnHandlePriorityLeftClick.GetInvocationList())
			{
				if (handler(pos))
					return;
			}
		}

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

			case InteractionMode.BuildingLinkEdit:
				bool linkHandled = false;
				if (OnHandleBuildingLinkSelection != null)
				{
					foreach (System.Func<int3, bool> handler in OnHandleBuildingLinkSelection.GetInvocationList())
					{
						linkHandled = handler(pos);
						if (linkHandled)
							break;
					}
				}
				break;

			case InteractionMode.FacilityPlacement:
				PlacementContext ctx = new(
					center: mousePos,
					dir: direction,
					def: toBePlaced
				);
				GridService.OnInstall(ctx);
				break;

			case InteractionMode.AreaEdit:
				if (hasAreaPlacementStart == false)
				{
					hasAreaPlacementStart = true;
					areaPlacementStart = pos;
					RaiseAreaPlacementPreview(pos);
					break;
				}

				int minX = Mathf.Min(areaPlacementStart.x, pos.x);
				int minZ = Mathf.Min(areaPlacementStart.z, pos.z);
				int maxX = Mathf.Max(areaPlacementStart.x, pos.x);
				int maxZ = Mathf.Max(areaPlacementStart.z, pos.z);
				var bound = new RectInt(minX, minZ, (maxX - minX) + 1, (maxZ - minZ) + 1);

				OnAreaPlacementConfirmed?.Invoke(areaToBePlaced, bound, areaPlacementFloor);
				break;

			case InteractionMode.BuildingPlacement:
				OnBuildingPlacementConfirmed?.Invoke(pos, buildingPlacementFloor);
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

			case InteractionMode.AreaEdit:
				ExitAreaPlacementMode();
				break;

			case InteractionMode.BuildingPlacement:
				ExitBuildingPlacementMode();
				break;

			case InteractionMode.BuildingLinkEdit:
				ExitBuildingLinkMode();
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
