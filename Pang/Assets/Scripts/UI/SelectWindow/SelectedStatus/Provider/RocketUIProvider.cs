using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class RocketUIProvider : UIProvider<Rocket>, IShelfBaseUIProvider, ISelectionInspectorProvider
{
	Component IShelfBaseUIProvider.Target => currentTarget;

	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Rocket";
	public override string Subtitle => "Rocket";
	public override Sprite Icon => null;

	public string CapacityDisplay => currentTarget != null ? $"{currentTarget.MaxSize:0.0} units" : "0.0 units";
	public string CurrentSizeDisplay => currentTarget != null ? $"{currentTarget.TotalSize:0.0} units" : "0.0 units";
	public string FilledPercentDisplay => currentTarget != null ? $"{currentTarget.FilledPercent:0.0}%" : "0.0%";
	public string StateDisplay => currentTarget != null ? currentTarget.State.ToString() : "Unknown";

	public ItemContainerDisplayInfo GetItemDisplay() => new()
	{
		ContainerName = "Docked Capsule",
		HasContainer = currentTarget?.DockedCapsule != null,
		Items = ItemContainerDisplayUtility.BuildItemRows(currentTarget?.DockedCapsule),
		ManifestItems = ItemContainerDisplayUtility.BuildManifestRows(currentTarget?.DockedCapsule),
	};

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("Capacity", CapacityDisplay));
		infoBlocks.Add(new KeyValueBlock("Current Size", CurrentSizeDisplay));
		infoBlocks.Add(new KeyValueBlock("Filled", FilledPercentDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 3)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(CapacityDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(CurrentSizeDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(FilledPercentDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Cargo", GetCargoVersion, BuildCargoPanel);
		model.AddOverview("State", () => StateDisplay);
		model.AddOverview("Capacity", () => CapacityDisplay);
		model.AddOverview("Current Size", () => CurrentSizeDisplay);
		model.AddOverview("Filled", () => FilledPercentDisplay);
		model.AddAction("Remove", DeleteObject, isDangerous: true);
	}

	private int GetCargoVersion()
	{
		unchecked
		{
			return SelectionDetailContentUtility.GetItemContainerVersion(currentTarget?.DockedCapsule) * 31 +
				(currentTarget?.DockedCapsule != null ? (int)currentTarget.DockedCapsule.BoxId : 0);
		}
	}

	private SelectionDetailPanelModel BuildCargoPanel()
	{
		return SelectionDetailContentUtility.BuildItemContainerPanel(
			"CARGO",
			$"{CurrentSizeDisplay} / {CapacityDisplay}",
			GetItemDisplay());
	}
}
