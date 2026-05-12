using Unity.VisualScripting;

[System.Serializable]
public class WorkerArchetype
{
	public WorkerNameDefinition WorkerNameDefinition = new();
	public WorkerVisualDefinition WorkerVisualDefinition;
	public WorkerAbilityDefinition AbilityDefinition = new();
	public WorkerBaseStatDefinition WorkerBaseStat = new();

	public void SetupWorker(AIWorker worker)
	{
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.CargoHandling))		worker.AddComponent<CargoHandlingAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.CarryBox))			worker.AddComponent<CarryBoxAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.Labeling))			worker.AddComponent<LabelingAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.Packing))				worker.AddComponent<PackageAbility>();
		if (AbilityDefinition.abilities.HasFlag(WorkerAbility.PickingStoring))		worker.AddComponent<PickStoreAbility>();
	}

	public void Duplicate(WorkerArchetype other)
	{
		other.WorkerNameDefinition = WorkerNameDefinition;
		other.WorkerVisualDefinition = WorkerVisualDefinition;
		other.AbilityDefinition = AbilityDefinition;
		other.WorkerBaseStat = WorkerBaseStat;
	}
}


