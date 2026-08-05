using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class WorkforceAssignmentModeController : MonoBehaviour
{
	[SerializeField] private GameObject overlayQuadPrefab;
	[SerializeField] private GameObject overlayLabelPrefab;
	[SerializeField] private float markerHeight = 0.04f;
	[SerializeField] private float labelHeight = 0.06f;
	[SerializeField] private float labelScale = 0.28f;
	[SerializeField] private Color availableColor = new(0.18f, 0.72f, 0.9f, 0.32f);
	[SerializeField] private Color hoveredColor = new(0.2f, 0.82f, 0.43f, 0.54f);
	[SerializeField] private Color unavailableColor = new(0.78f, 0.25f, 0.2f, 0.24f);

	private readonly List<GameObject> overlayObjects = new();
	private readonly List<WorkerTask.TaskType> taskTypeBuffer = new();
	private readonly List<WorkerTask.TaskType> preservedTaskTypeBuffer = new();
	private GameObject overlayRoot;
	private MousePicking mousePicking;
	private AIWorker draggedWorker;
	private Building hoveredBuilding;
	private bool isEditing;
	private bool persistentMode;
	[NonSerialized] private InteractionContext boundInteraction;

	private InteractionContext Interaction => boundInteraction ??
		(GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null);
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private BuildingFootprintService FootprintService => GameContext.HasInstance ? GameContext.Instance.BuildingFootprintService : null;
	private WorkerManager WorkerManager => GameContext.HasInstance ? GameContext.Instance.WorkerMgr : null;

	public bool IsEditing => isEditing;
	public bool IsDraggingWorker => draggedWorker != null;
	public bool IsPersistentMode => isEditing && persistentMode;
	public AIWorker DraggedWorker => draggedWorker;
	public Building HoveredBuilding => hoveredBuilding;
	public string StatusText => BuildStatusText();

	public event Action StateChanged;
	public event Action<Building> BuildingSelected;
	public event Action<AIWorker, Building, bool> WorkerDropped;

	public void Configure(GameObject targetOverlayQuadPrefab, GameObject targetOverlayLabelPrefab)
	{
		overlayQuadPrefab = targetOverlayQuadPrefab;
		overlayLabelPrefab = targetOverlayLabelPrefab;
	}

	private void Awake()
	{
		EnsureOverlayRoot();
		mousePicking = FindAnyObjectByType<MousePicking>();
	}

	private void OnEnable()
	{
		EnsureOverlayRoot();
		overlayRoot.SetActive(true);
		mousePicking ??= FindAnyObjectByType<MousePicking>();
		BindInteraction();
		if (isEditing)
			RefreshOverlay();
	}

	private void Start()
	{
		BindInteraction();
	}

	private void OnDisable()
	{
		UnbindInteraction();
		ClearOverlay();
		overlayRoot?.SetActive(false);
	}

	private void BindInteraction()
	{
		InteractionContext interaction = GameContext.HasInstance ? GameContext.Instance.InteractionCtx : null;
		if (interaction == null)
			return;

		if (boundInteraction != null && boundInteraction != interaction)
			UnbindInteraction();

		boundInteraction = interaction;
		boundInteraction.OnHandleWorkforceAssignmentSelection -= HandleBuildingSelection;
		boundInteraction.OnMouseGridPositionChanged -= HandleMouseGridPositionChanged;
		boundInteraction.OnModeChanged -= HandleModeChanged;
		boundInteraction.OnHandleWorkforceAssignmentSelection += HandleBuildingSelection;
		boundInteraction.OnMouseGridPositionChanged += HandleMouseGridPositionChanged;
		boundInteraction.OnModeChanged += HandleModeChanged;
	}

	private void UnbindInteraction()
	{
		if (boundInteraction == null)
			return;

		boundInteraction.OnHandleWorkforceAssignmentSelection -= HandleBuildingSelection;
		boundInteraction.OnMouseGridPositionChanged -= HandleMouseGridPositionChanged;
		boundInteraction.OnModeChanged -= HandleModeChanged;
		boundInteraction = null;
	}

	private void OnDestroy()
	{

		ClearOverlay();
		if (overlayRoot != null)
			Destroy(overlayRoot);
	}

	public bool BeginPersistentMode()
	{
		if (Interaction == null)
			return false;

		isEditing = true;
		persistentMode = true;
		draggedWorker = null;
		hoveredBuilding = null;
		Interaction.EnterWorkforceAssignmentMode();
		RefreshOverlay();
		StateChanged?.Invoke();
		return true;
	}

	public bool BeginWorkerDrag(AIWorker worker)
	{
		if (worker == null || worker.IsOperational == false || Interaction == null)
			return false;

		bool keepPersistentMode = isEditing && persistentMode;
		isEditing = true;
		persistentMode = keepPersistentMode;
		draggedWorker = worker;
		hoveredBuilding = null;
		Interaction.EnterWorkforceAssignmentMode();
		RefreshOverlay();
		StateChanged?.Invoke();
		return true;
	}

	public void CancelWorkerDrag()
	{
		if (draggedWorker == null)
			return;

		draggedWorker = null;
		hoveredBuilding = null;
		if (persistentMode == false)
		{
			EndMode();
			return;
		}

		RefreshOverlay();
		StateChanged?.Invoke();
	}

	public bool TryDropDraggedWorker()
	{
		AIWorker worker = draggedWorker;
		Building building = hoveredBuilding;
		if (worker == null || building == null || CanAssignToBuilding(worker, building) == false)
		{
			CancelWorkerDrag();
			return false;
		}

		BuildPreservedTaskTypes(worker, building, preservedTaskTypeBuffer);
		bool assigned = WorkerManager != null &&
			WorkerManager.TryRequestWorkerAssignment(worker, building.RuntimeBuildingId, preservedTaskTypeBuffer);

		draggedWorker = null;
		hoveredBuilding = building;
		WorkerDropped?.Invoke(worker, building, assigned);
		if (persistentMode == false)
			EndMode();
		else
		{
			RefreshOverlay();
			StateChanged?.Invoke();
		}

		return assigned;
	}

	public bool TryDropDraggedWorkerToOutdoor()
	{
		AIWorker worker = draggedWorker;
		if (worker == null || CanAssignToOutdoor(worker) == false)
		{
			CancelWorkerDrag();
			return false;
		}

		BuildPreservedTaskTypes(worker, null, preservedTaskTypeBuffer);
		bool assigned = WorkerManager != null &&
			WorkerManager.TryRequestWorkerAssignment(worker, 0, preservedTaskTypeBuffer);

		draggedWorker = null;
		hoveredBuilding = null;
		WorkerDropped?.Invoke(worker, null, assigned);
		if (persistentMode == false)
			EndMode();
		else
		{
			RefreshOverlay();
			StateChanged?.Invoke();
		}

		return assigned;
	}

	public bool UpdateDragPointer(Vector2 screenPosition)
	{
		if (draggedWorker == null)
			return false;

		mousePicking ??= FindAnyObjectByType<MousePicking>();
		if (mousePicking == null || mousePicking.TryGetGridPosition(screenPosition, out int3 position) == false)
			return false;

		HandleMouseGridPositionChanged(position);
		return true;
	}

	public void EndMode()
	{
		bool hadState = isEditing || draggedWorker != null || hoveredBuilding != null;
		isEditing = false;
		persistentMode = false;
		draggedWorker = null;
		hoveredBuilding = null;
		ClearOverlay();

		if (Interaction != null && Interaction.Mode == InteractionContext.InteractionMode.WorkforceAssignment)
			Interaction.ExitWorkforceAssignmentMode();

		if (hadState)
			StateChanged?.Invoke();
	}

	public void Refresh()
	{
		if (isEditing)
			RefreshOverlay();
	}

	private void HandleModeChanged(InteractionContext.InteractionDomain _, InteractionContext.InteractionAction __)
	{
		if (Interaction != null && Interaction.Mode == InteractionContext.InteractionMode.WorkforceAssignment)
			return;

		if (isEditing == false && draggedWorker == null && hoveredBuilding == null)
			return;

		isEditing = false;
		persistentMode = false;
		draggedWorker = null;
		hoveredBuilding = null;
		ClearOverlay();
		StateChanged?.Invoke();
	}

	private void HandleMouseGridPositionChanged(int3 position)
	{
		if (isEditing == false || Interaction == null ||
			Interaction.Mode != InteractionContext.InteractionMode.WorkforceAssignment)
		{
			return;
		}

		TryGetBuildingAt(position, out Building building);
		if (hoveredBuilding == building)
			return;

		hoveredBuilding = building;
		RefreshOverlay();
		StateChanged?.Invoke();
	}

	private bool HandleBuildingSelection(int3 position)
	{
		if (isEditing == false || Interaction == null ||
			Interaction.Mode != InteractionContext.InteractionMode.WorkforceAssignment ||
			TryGetBuildingAt(position, out Building building) == false)
		{
			return false;
		}

		hoveredBuilding = building;
		BuildingSelected?.Invoke(building);
		RefreshOverlay();
		StateChanged?.Invoke();
		return true;
	}

	private bool TryGetBuildingAt(int3 position, out Building building)
	{
		building = null;
		GridCell cell = GridService?.GetCell(position);
		return cell != null && cell.BuildingId != 0 && BuildingManager != null &&
			BuildingManager.TryGetBuilding(cell.BuildingId, out building) && building != null;
	}

	private bool CanAssignToBuilding(AIWorker worker, Building building)
	{
		if (worker == null || building == null)
			return false;

		WorkerTaskAssignmentPolicy.GetAssignableTaskTypes(worker, building.Type, taskTypeBuffer);
		for (int i = 0; i < taskTypeBuffer.Count; ++i)
		{
			if (taskTypeBuffer[i] != WorkerTask.TaskType.Undefined)
				return true;
		}

		return false;
	}

	public bool CanAssignToOutdoor(AIWorker worker)
	{
		if (worker == null)
			return false;

		WorkerTaskAssignmentPolicy.GetAssignableTaskTypes(worker, null, taskTypeBuffer);
		for (int i = 0; i < taskTypeBuffer.Count; ++i)
		{
			if (taskTypeBuffer[i] != WorkerTask.TaskType.Undefined)
				return true;
		}

		return false;
	}

	private static void BuildPreservedTaskTypes(
		AIWorker worker,
		Building building,
		List<WorkerTask.TaskType> results)
	{
		results.Clear();
		if (worker == null)
			return;

		BuildingType? buildingType = building != null ? building.Type : null;
		IReadOnlyList<WorkerTask.TaskType> sourceTypes = worker.HasPendingAssignment
			? worker.PendingAssignedTaskTypes
			: worker.AssignedTaskTypes;
		for (int i = 0; i < sourceTypes.Count; ++i)
		{
			WorkerTask.TaskType taskType = sourceTypes[i];
			if (WorkerTaskAssignmentPolicy.CanAssign(worker, buildingType, taskType))
				results.Add(taskType);
		}
	}

	private string BuildStatusText()
	{
		if (isEditing == false)
			return string.Empty;

		if (draggedWorker == null)
			return hoveredBuilding != null
				? $"{hoveredBuilding.DisplayName} staffing selected."
				: "Select a building or drag a worker onto one.";

		if (hoveredBuilding == null)
			return $"Assign {draggedWorker.Name}: move onto a building.";

		return CanAssignToBuilding(draggedWorker, hoveredBuilding)
			? $"Assign {draggedWorker.Name} to {hoveredBuilding.DisplayName}."
			: $"{draggedWorker.Name} has no compatible work in {hoveredBuilding.DisplayName}.";
	}

	private void RefreshOverlay()
	{
		ClearOverlay();
		if (isEditing == false || BuildingManager == null)
			return;

		IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
		for (int i = 0; i < buildings.Count; ++i)
		{
			Building building = buildings[i];
			if (building != null)
				CreateBuildingMarker(building);
		}
	}

	private void CreateBuildingMarker(Building building)
	{
		if (FootprintService == null ||
			FootprintService.TryGetFootprint(building.RuntimeBuildingId, out BuildingFootprintRecord footprint) == false ||
			footprint == null)
		{
			return;
		}

		bool compatible = draggedWorker == null || CanAssignToBuilding(draggedWorker, building);
		Color color = building == hoveredBuilding
			? compatible ? hoveredColor : unavailableColor
			: compatible ? availableColor : unavailableColor;
		RectInt bounds = footprint.Bounds;
		for (int z = bounds.yMin; z < bounds.yMax; ++z)
		{
			for (int x = bounds.xMin; x < bounds.xMax; ++x)
			{
				GridCell cell = GridService?.GetCell(x, footprint.Floor, z);
				if (cell == null || cell.BuildingId != building.RuntimeBuildingId)
					continue;

				GameObject marker = CreateQuadObject();
				if (marker == null)
					return;

				marker.transform.position = new Vector3(x, markerHeight, z);
				marker.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				marker.transform.localScale = Vector3.one;
				MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
				if (renderer != null)
					renderer.material.color = color;
				overlayObjects.Add(marker);
			}
		}

		int currentWorkers = CountWorkers(building.RuntimeBuildingId, planned: false);
		int plannedWorkers = CountWorkers(building.RuntimeBuildingId, planned: true);
		string countText = currentWorkers == plannedWorkers
			? $"Workers {currentWorkers}"
			: $"Workers {currentWorkers} -> {plannedWorkers}";
		GameObject label = CreateLabelObject($"{building.DisplayName}\n{countText}", color);
		if (label == null)
			return;

		label.transform.position = new Vector3(footprint.Center.x, labelHeight, footprint.Center.y);
		label.transform.rotation = Quaternion.Euler(90f, 180f, 0f);
		label.transform.localScale = Vector3.one * labelScale;
		overlayObjects.Add(label);
	}

	private int CountWorkers(uint buildingId, bool planned)
	{
		IReadOnlyList<AIWorker> workers = WorkerManager?.Workers;
		if (workers == null)
			return 0;

		int count = 0;
		for (int i = 0; i < workers.Count; ++i)
		{
			AIWorker worker = workers[i];
			if (worker == null)
				continue;

			uint effectiveBuildingId = planned && worker.HasPendingAssignment
				? worker.PendingPrimaryBuildingId
				: worker.PrimaryBuildingId;
			if (effectiveBuildingId == buildingId)
				++count;
		}

		return count;
	}

	private void EnsureOverlayRoot()
	{
		if (overlayRoot != null)
			return;

		overlayRoot = new GameObject("WorkforceAssignmentOverlayRoot");
		Transform parent = GameContext.HasInstance ? GameContext.Instance.transform : transform;
		overlayRoot.transform.SetParent(parent, false);
		overlayRoot.hideFlags = HideFlags.HideInHierarchy;
	}

	private GameObject CreateQuadObject()
	{
		EnsureOverlayRoot();
		if (overlayQuadPrefab == null)
			return null;

		GameObject marker = Instantiate(overlayQuadPrefab, overlayRoot.transform);
		marker.name = "WorkforceAssignmentMarker";
		return marker;
	}

	private GameObject CreateLabelObject(string text, Color color)
	{
		EnsureOverlayRoot();
		if (overlayLabelPrefab == null)
			return null;

		GameObject label = Instantiate(overlayLabelPrefab, overlayRoot.transform);
		label.name = "WorkforceAssignmentLabel";
		TMPro.TextMeshPro textMesh = label.GetComponent<TMPro.TextMeshPro>();
		if (textMesh != null)
		{
			textMesh.text = text;
			textMesh.color = color;
		}

		return label;
	}

	private void ClearOverlay()
	{
		for (int i = 0; i < overlayObjects.Count; ++i)
		{
			if (overlayObjects[i] != null)
				Destroy(overlayObjects[i]);
		}

		overlayObjects.Clear();
	}
}
