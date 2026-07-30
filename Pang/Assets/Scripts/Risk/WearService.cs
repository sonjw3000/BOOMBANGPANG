using System.Collections.Generic;
using UnityEngine;

public sealed class WearService : MonoBehaviour
{
	[SerializeField, Min(1.0f)] private float outdoorWearMultiplier = 2.0f;

	private readonly HashSet<IWearable> registeredTargets = new();
	private readonly Dictionary<IWearable, float> pendingOperationWeeks = new();
	private readonly List<IWearable> targetScratch = new();
	private bool eventsBound;

	private FacilityManager FacilityManager => GameContext.HasInstance ? GameContext.Instance.FacilityMgr : null;
	private WorkerManager WorkerManager => GameContext.HasInstance ? GameContext.Instance.WorkerMgr : null;
	private BuildingManager BuildingManager => GameContext.HasInstance ? GameContext.Instance.BuildingMgr : null;
	private BuildingAddonService BuildingAddonService => GameContext.HasInstance ? GameContext.Instance.BuildingAddonSvc : null;
	private GridService GridService => GameContext.HasInstance ? GameContext.Instance.GridService : null;

	private void OnEnable()
	{
		BindEvents();
		RebuildRegisteredTargets();
	}

	private void Start()
	{
		BindEvents();
		RebuildRegisteredTargets();
	}

	private void OnDisable() => UnbindEvents();

	public void ReportOperation(IWearable target, float elapsedWeeks, float loadRatio = 1.0f)
	{
		if (target == null || elapsedWeeks <= 0.0f || loadRatio <= 0.0f)
			return;

		float operationWeeks = elapsedWeeks * Mathf.Clamp01(loadRatio);
		if (pendingOperationWeeks.TryGetValue(target, out float pending))
			pendingOperationWeeks[target] = pending + operationWeeks;
		else
			pendingOperationWeeks[target] = operationWeeks;
	}

	public void ProcessQuarterWeekTick()
	{
		targetScratch.Clear();
		foreach (IWearable target in registeredTargets)
			targetScratch.Add(target);

		float quarterWeek = GameTime.SimulationTickWeeks * GameTime.QuarterWeekSimulationTickInterval;
		for (int i = 0; i < targetScratch.Count; ++i)
		{
			IWearable target = targetScratch[i];
			if (target is Object unityObject && unityObject == null)
			{
				Unregister(target);
				continue;
			}

			pendingOperationWeeks.TryGetValue(target, out float operationWeeks);
			float operatingRatio = quarterWeek > 0.0f
				? Mathf.Clamp01(operationWeeks / quarterWeek)
				: 0.0f;
			float wear = target.PassiveWearPerQuarterWeek +
				target.OperatingWearPerQuarterWeek * operatingRatio;
			wear *= CalculateEnvironmentMultiplier(target);
			target.ApplyWear(wear);
		}

		pendingOperationWeeks.Clear();
	}

	public void ResetRuntimeState()
	{
		registeredTargets.Clear();
		pendingOperationWeeks.Clear();
		targetScratch.Clear();
	}

	public void RebuildRuntimeState()
	{
		pendingOperationWeeks.Clear();
		RebuildRegisteredTargets();
	}

	private void BindEvents()
	{
		if (eventsBound || FacilityManager == null || WorkerManager == null || BuildingAddonService == null)
			return;

		FacilityManager.SubscribeFacilityRegister<IWearableFacility>(HandleFacilityRegistered, HandleFacilityUnregistered);
		WorkerManager.OnWorkersChanged += HandleWorkersChanged;
		BuildingAddonService.OnAddonInstalled += HandleAddonInstalled;
		BuildingAddonService.OnAddonRemoved += HandleAddonRemoved;
		eventsBound = true;
	}

	private void UnbindEvents()
	{
		if (eventsBound == false)
			return;

		if (FacilityManager != null)
			FacilityManager.UnsubscribeFacilityRegister<IWearableFacility>(HandleFacilityRegistered, HandleFacilityUnregistered);
		if (WorkerManager != null)
			WorkerManager.OnWorkersChanged -= HandleWorkersChanged;
		if (BuildingAddonService != null)
		{
			BuildingAddonService.OnAddonInstalled -= HandleAddonInstalled;
			BuildingAddonService.OnAddonRemoved -= HandleAddonRemoved;
		}
		eventsBound = false;
	}

	private void RebuildRegisteredTargets()
	{
		registeredTargets.Clear();

		if (FacilityManager != null)
		{
			IReadOnlyList<uint> buildingIds = FacilityManager.GetBuildingIds();
			for (int i = 0; i < buildingIds.Count; ++i)
			{
				IReadOnlyList<IWearableFacility> facilities =
					FacilityManager.GetFacilities<IWearableFacility>(buildingIds[i]);
				for (int j = 0; j < facilities.Count; ++j)
					Register(facilities[j]);
			}
		}

		if (BuildingManager != null)
		{
			IReadOnlyList<Building> buildings = BuildingManager.RegisteredBuildings;
			for (int buildingIndex = 0; buildingIndex < buildings.Count; ++buildingIndex)
			{
				Building building = buildings[buildingIndex];
				if (building == null)
					continue;

				IReadOnlyList<BuildingAddon> addons = building.InstalledAddons;
				for (int addonIndex = 0; addonIndex < addons.Count; ++addonIndex)
					Register(addons[addonIndex]);
			}
		}

		if (WorkerManager == null)
			return;

		IReadOnlyList<AIWorker> workers = WorkerManager.Workers;
		for (int i = 0; i < workers.Count; ++i)
		{
			if (workers[i] is RobotWorker robot)
				Register(robot);
		}
	}

	private void HandleFacilityRegistered(uint buildingId, IFacility facility)
	{
		if (facility is IWearableFacility wearable)
			Register(wearable);
	}

	private void HandleFacilityUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is IWearableFacility wearable)
			Unregister(wearable);
	}

	private void HandleAddonInstalled(Building building, BuildingAddon addon)
	{
		Register(addon);
	}

	private void HandleAddonRemoved(Building building, BuildingAddon addon)
	{
		Unregister(addon);
	}

	private void HandleWorkersChanged()
	{
		targetScratch.Clear();
		foreach (IWearable target in registeredTargets)
		{
			if (target is RobotWorker)
				targetScratch.Add(target);
		}

		for (int i = 0; i < targetScratch.Count; ++i)
			Unregister(targetScratch[i]);

		if (WorkerManager == null)
			return;

		IReadOnlyList<AIWorker> workers = WorkerManager.Workers;
		for (int i = 0; i < workers.Count; ++i)
		{
			if (workers[i] is RobotWorker robot)
				Register(robot);
		}
	}

	private void Register(IWearable target)
	{
		if (target != null)
			registeredTargets.Add(target);
	}

	private void Unregister(IWearable target)
	{
		if (target == null)
			return;

		registeredTargets.Remove(target);
		pendingOperationWeeks.Remove(target);
	}

	private float CalculateEnvironmentMultiplier(IWearable target)
	{
		if (target is not IGridPlaceable placeable || GridService == null)
			return 1.0f;

		GridCell cell = GridService.GetCell(placeable.GridPosition);
		return cell != null && cell.IsIndoor == false
			? outdoorWearMultiplier
			: 1.0f;
	}
}
