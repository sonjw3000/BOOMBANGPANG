using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class ThermalTransferService
{
	private readonly HashSet<IThermalItemContainer> facilityContainers = new();
	private readonly HashSet<IThermalItemContainer> processedContainers = new();
	private readonly List<IThermalItemContainer> facilityContainerScratch = new();

	private FacilityManager facilityManager;
	private BoxManager boxManager;
	private GridService gridService;
	private TemperatureService temperatureService;
	private bool eventsBound;

	public int RegisteredFacilityContainerCount => facilityContainers.Count;
	public int LastProcessedContainerCount { get; private set; }
	public int LastProcessedStackCount { get; private set; }

	public void Bind(
		FacilityManager targetFacilityManager,
		BoxManager targetBoxManager,
		GridService targetGridService,
		TemperatureService targetTemperatureService)
	{
		if (facilityManager == targetFacilityManager &&
			boxManager == targetBoxManager &&
			gridService == targetGridService &&
			temperatureService == targetTemperatureService &&
			eventsBound)
		{
			return;
		}

		Unbind();
		facilityManager = targetFacilityManager;
		boxManager = targetBoxManager;
		gridService = targetGridService;
		temperatureService = targetTemperatureService;

		if (facilityManager != null)
		{
			facilityManager.SubscribeFacilityRegister<IFacility>(
				HandleFacilityRegistered,
				HandleFacilityUnregistered);
			eventsBound = true;
		}

		RebuildRuntimeState();
	}

	public void Unbind()
	{
		if (eventsBound && facilityManager != null)
		{
			facilityManager.UnsubscribeFacilityRegister<IFacility>(
				HandleFacilityRegistered,
				HandleFacilityUnregistered);
		}

		eventsBound = false;
		facilityManager = null;
		boxManager = null;
		gridService = null;
		temperatureService = null;
		ResetRuntimeState();
	}

	public void ResetRuntimeState()
	{
		facilityContainers.Clear();
		processedContainers.Clear();
		facilityContainerScratch.Clear();
		LastProcessedContainerCount = 0;
		LastProcessedStackCount = 0;
	}

	public void RebuildRuntimeState()
	{
		facilityContainers.Clear();
		if (facilityManager == null)
			return;

		IReadOnlyList<uint> buildingIds = facilityManager.GetBuildingIds();
		for (int buildingIndex = 0; buildingIndex < buildingIds.Count; ++buildingIndex)
		{
			IReadOnlyList<IFacility> facilities =
				facilityManager.GetFacilities<IFacility>(buildingIds[buildingIndex]);
			for (int facilityIndex = 0; facilityIndex < facilities.Count; ++facilityIndex)
				RegisterFacilityContainer(facilities[facilityIndex]);
		}
	}

	public void ProcessSimulationTick(in SimulationTickContext context)
	{
		LastProcessedContainerCount = 0;
		LastProcessedStackCount = 0;
		if (gridService == null || gridService.IsReady == false || context.ElapsedWeeks <= 0.0f)
			return;

		processedContainers.Clear();
		facilityContainerScratch.Clear();
		foreach (IThermalItemContainer container in facilityContainers)
			facilityContainerScratch.Add(container);

		for (int i = 0; i < facilityContainerScratch.Count; ++i)
			ProcessContainerOnce(facilityContainerScratch[i], in context);

		IReadOnlyList<BoxBase> boxes = boxManager?.ActiveBoxes;
		if (boxes == null)
			return;

		for (int i = 0; i < boxes.Count; ++i)
		{
			BoxBase box = boxes[i];
			if (box == null || box.IsValid == false)
				continue;

			ProcessContainerOnce(box, in context);
		}
	}

	private void ProcessContainerOnce(
		IThermalItemContainer container,
		in SimulationTickContext context)
	{
		if (IsContainerAlive(container) == false ||
			processedContainers.Add(container) == false ||
			container.TryGetThermalEnvironmentPosition(out int3 position) == false)
		{
			return;
		}

		GridCell cell = gridService.GetCell(position);
		if (cell == null)
			return;

		float containerTemperature = ThermalUtility.ApproachTemperature(
			container.CurrentTemperatureCelsius,
			cell.TemperatureCelsius,
			container.ThermalResponsePerWeek,
			context.ElapsedWeeks);
		container.SetCurrentTemperatureCelsius(containerTemperature);
		++LastProcessedContainerCount;

		IReadOnlyList<ItemStack> stacks = container.Stacks;
		if (stacks == null)
			return;

		float itemResponsePerWeek = temperatureService != null
			? temperatureService.DefaultItemThermalResponsePerWeek
			: 0.0f;
		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			float itemTemperature = ThermalUtility.ApproachTemperature(
				stack.CurrentTemperatureCelsius,
				containerTemperature,
				itemResponsePerWeek,
				context.ElapsedWeeks);
			stack.SetCurrentTemperatureCelsius(itemTemperature);
			++LastProcessedStackCount;
		}
	}

	private void HandleFacilityRegistered(uint buildingId, IFacility facility)
	{
		RegisterFacilityContainer(facility);
	}

	private void HandleFacilityUnregistered(uint buildingId, IFacility facility)
	{
		if (facility is IThermalItemContainer container)
			facilityContainers.Remove(container);
	}

	private void RegisterFacilityContainer(IFacility facility)
	{
		if (facility is IThermalItemContainer container && IsContainerAlive(container))
			facilityContainers.Add(container);
	}

	private static bool IsContainerAlive(IThermalItemContainer container)
	{
		if (container == null)
			return false;

		return container is not UnityEngine.Object unityObject || unityObject != null;
	}
}
