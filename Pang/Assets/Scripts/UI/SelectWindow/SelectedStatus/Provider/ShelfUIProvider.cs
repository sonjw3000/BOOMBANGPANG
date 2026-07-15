using UniverseLogistics.UI.Toolkit;

public sealed class ShelfUIProvider : ShelfBaseUIProviderBase<ShelfBase>, ISelectionInspectorProvider
{
	public override string Subtitle => "Shelf";

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Inventory", GetInventoryVersion, BuildInventoryPanel);
		model.AddOverview("Capacity", () => CapacityDisplay);
		model.AddOverview("Current Size", () => CurrentSizeDisplay);
		model.AddOverview("Filled", () => FilledPercentDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}

	private int GetInventoryVersion()
	{
		return SelectionDetailContentUtility.GetItemContainerVersion(currentTarget);
	}

	private SelectionDetailPanelModel BuildInventoryPanel()
	{
		return SelectionDetailContentUtility.BuildItemContainerPanel(
			"INVENTORY",
			$"{CurrentSizeDisplay} / {CapacityDisplay}",
			GetItemDisplay());
	}
}
