using Unity.VisualScripting;

[System.Serializable]
public class WorkerArchetype
{
	public WorkerNameDefinition workerNameDefinition;
	public WorkerVisualDefinition workerVisualDefinition;
	public WorkerAbilityDefinition AbilityDefinition;
	public WorkerBaseStatDefinition WorkerBaseStat;

	public WorkerArchetype(WorkerNameDefinition workerName, WorkerVisualDefinition workerVisual, WorkerAbilityDefinition abilityDefinition, WorkerBaseStatDefinition workerBaseStat)
	{
		workerNameDefinition = workerName;
		workerVisualDefinition = workerVisual;
		AbilityDefinition = abilityDefinition;
		WorkerBaseStat = workerBaseStat;
	}

	public void SetupWorker(AIWorker worker)
	{
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.CargoHandling))		worker.AddComponent<CargoHandlingAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.CarryBox))			worker.AddComponent<CarryBoxAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.Labeling))			worker.AddComponent<LabelingAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.Packing))				worker.AddComponent<PackageAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.PickingStoring))		worker.AddComponent<PickStoreAbility>();
	}

}
