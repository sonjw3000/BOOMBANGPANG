using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public abstract class WearableFacilityUIProvider<TFacility> : UIProvider<TFacility>, ISelectionInspectorProvider
	where TFacility : Component, IWearableFacility
{
	protected abstract string FacilityTypeLabel { get; }

	public override string Name => currentTarget != null ? currentTarget.name : $"Unknown {FacilityTypeLabel}";
	public override string Subtitle => FacilityTypeLabel;
	public override Sprite Icon => null;

	private string WearDisplay => currentTarget != null ? $"{currentTarget.Wear * 100.0f:0.0}%" : "0.0%";
	private string WearEfficiencyDisplay => currentTarget != null ? $"{currentTarget.WearEfficiency * 100.0f:0.0}%" : "100.0%";
	private string HealthDisplay => currentTarget != null ? $"{currentTarget.Health:0.0}/{currentTarget.MaxHealth:0.0}" : "0.0/0.0";
	private string PositionDisplay => currentTarget != null ? currentTarget.GridPosition.ToString() : "(0,0,0)";

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Wear", WearDisplay));
		infoBlocks.Add(new KeyValueBlock("Wear Efficiency", WearEfficiencyDisplay));
		infoBlocks.Add(new KeyValueBlock("Health", HealthDisplay));
		infoBlocks.Add(new KeyValueBlock("Position", PositionDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 4)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(WearDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(WearEfficiencyDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(HealthDisplay);
		(infoBlocks[3] as KeyValueBlock)?.UpdateValue(PositionDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddOverview("Wear", () => WearDisplay);
		model.AddOverview("Wear Efficiency", () => WearEfficiencyDisplay);
		model.AddOverview("Health", () => HealthDisplay);
		model.AddOverview("Grid Position", () => PositionDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}
}

public sealed class OxygenSupplyUnitUIProvider : WearableFacilityUIProvider<OxygenSupplyUnit>
{
	protected override string FacilityTypeLabel => "Oxygen Supply Unit";
}

public sealed class RefrigerationUnitUIProvider : WearableFacilityUIProvider<RefrigerationUnit>
{
	protected override string FacilityTypeLabel => "Refrigeration Unit";
}
