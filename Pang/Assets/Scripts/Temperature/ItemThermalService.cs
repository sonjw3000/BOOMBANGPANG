using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class ItemThermalService
{
	private const float ThermalDamageMultiplier = 0.1f;

	private readonly HashSet<IThermalItemContainer> facilityContainers = new();
	private readonly HashSet<IThermalItemContainer> processedContainers = new();
	private readonly List<IThermalItemContainer> facilityContainerScratch = new();

	private FacilityManager facilityManager;
	private BoxManager boxManager;
	private GridService gridService;
	private ItemDatabase itemDatabase;
	private ItemDamageService itemDamageService;
	private bool eventsBound;

	public int RegisteredFacilityContainerCount => facilityContainers.Count;
	public int LastProcessedContainerCount { get; private set; }
	public int LastProcessedStackCount { get; private set; }

	public void Bind(
		FacilityManager targetFacilityManager,
		BoxManager targetBoxManager,
		GridService targetGridService,
		ItemDatabase targetItemDatabase,
		ItemDamageService targetItemDamageService)
	{
		if (facilityManager == targetFacilityManager &&
			boxManager == targetBoxManager &&
			gridService == targetGridService &&
			itemDatabase == targetItemDatabase &&
			itemDamageService == targetItemDamageService &&
			eventsBound)
		{
			return;
		}

		Unbind();
		facilityManager = targetFacilityManager;
		boxManager = targetBoxManager;
		gridService = targetGridService;
		itemDatabase = targetItemDatabase;
		itemDamageService = targetItemDamageService;

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
		itemDatabase = null;
		itemDamageService = null;
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
			ThermalUtility.GetResponsePerWeek(container.ThermalResponse),
			context.ElapsedWeeks);
		container.SetCurrentTemperatureCelsius(containerTemperature);
		++LastProcessedContainerCount;

		IReadOnlyList<ItemStack> stacks = container.Stacks;
		if (stacks == null)
			return;

		for (int i = 0; i < stacks.Count; ++i)
		{
			ItemStack stack = stacks[i];
			if (stack == null || stack.Quantity <= 0)
				continue;

			ItemDefinition definition = ResolveItemDefinition(stack.ItemID);
			float itemResponsePerWeek = definition != null
				? ThermalUtility.GetResponsePerWeek(definition.ThermalResponse)
				: ThermalUtility.NormalResponsePerWeek;
			float itemTemperature = ThermalUtility.ApproachTemperature(
				stack.CurrentTemperatureCelsius,
				containerTemperature,
				itemResponsePerWeek,
				context.ElapsedWeeks);
			stack.SetCurrentTemperatureCelsius(itemTemperature);
			ApplyThermalDamage(stack, definition, in position, container);
			++LastProcessedStackCount;
		}
	}

	private ItemDefinition ResolveItemDefinition(uint itemId)
	{
		return itemDatabase != null &&
			itemDatabase.GetItemData(itemId, out ItemDefinition definition)
				? definition
				: null;
	}

	private void ApplyThermalDamage(
		ItemStack stack,
		ItemDefinition definition,
		in int3 originCell,
		IItemContainer container)
	{
		if (stack == null || definition == null || itemDamageService == null || stack.IsDestroyed)
			return;

		float currentTemperature = stack.CurrentTemperatureCelsius;
		float freezingDifference = definition.FreezingDamageTemperatureCelsius - currentTemperature;
		if (freezingDifference > 0.0f)
		{
			itemDamageService.TryApplyDamage(
				stack,
				freezingDifference * ThermalDamageMultiplier,
				in originCell,
				container,
				ItemDamageCause.Freezing,
				out _);
			return;
		}

		float heatDifference = currentTemperature - definition.HeatDamageTemperatureCelsius;
		if (heatDifference <= 0.0f)
			return;

		itemDamageService.TryApplyDamage(
			stack,
			heatDifference * ThermalDamageMultiplier,
			in originCell,
			container,
			ItemDamageCause.Overheating,
			out _);
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
