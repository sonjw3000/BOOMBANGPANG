using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class CapsuleBufferUIProvider : UIProvider<CapsuleBuffer>, IShelfBaseUIProvider, ISelectionInspectorProvider
{
	Component IShelfBaseUIProvider.Target => currentTarget;

	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Capsule Buffer";
	public override string Subtitle => "Capsule Buffer";
	public override Sprite Icon => null;

	public string StateDisplay => GetStateLabel(currentTarget);
	public string CapsuleDisplay => currentTarget?.DockedCapsule != null
		? $"Capsule #{currentTarget.DockedCapsule.BoxId}"
		: "Empty";
	public string DockedBufferDisplay => currentTarget?.DockedCapsule != null
		? currentTarget.DockedCapsule.LogisticsState.ToString()
		: "None";
	public string CapacityDisplay => currentTarget != null ? $"{currentTarget.MaxSize:0.0} units" : "0.0 units";
	public string CurrentSizeDisplay => currentTarget != null ? $"{currentTarget.TotalSize:0.0} units" : "0.0 units";
	public string FilledPercentDisplay => currentTarget != null ? $"{currentTarget.FilledPercent:0.0}%" : "0.0%";
	public string OutboundAccessDisplay => currentTarget != null && currentTarget.CanDispatchToOutbound() ? "Open" : "Closed";

	public ItemContainerDisplayInfo GetItemDisplay() => new()
	{
		ContainerName = currentTarget?.DockedCapsule != null
			? $"Docked Capsule #{currentTarget.DockedCapsule.BoxId}"
			: "Docked Capsule",
		HasContainer = currentTarget?.DockedCapsule != null,
		Container = currentTarget?.DockedCapsule,
		Items = ItemContainerDisplayUtility.BuildItemRows(currentTarget?.DockedCapsule),
		ManifestItems = ItemContainerDisplayUtility.BuildManifestRows(currentTarget?.DockedCapsule),
	};

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("State", StateDisplay));
		infoBlocks.Add(new KeyValueBlock("Capsule", CapsuleDisplay));
		infoBlocks.Add(new KeyValueBlock("DockedBuffer", DockedBufferDisplay));
		infoBlocks.Add(new KeyValueBlock("Filled", FilledPercentDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 4)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(StateDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(CapsuleDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(DockedBufferDisplay);
		(infoBlocks[3] as KeyValueBlock)?.UpdateValue(FilledPercentDisplay);
	}

	public void BuildInspectorModel(SelectionInspectorModel model)
	{
		model.Clear();
		model.AddTab("Cargo", GetCargoVersion, BuildCargoPanel);
		model.AddOverview("State", () => StateDisplay);
		model.AddOverview("Capsule", () => CapsuleDisplay);
		model.AddOverview("Logistics", () => DockedBufferDisplay);
		model.AddOverview("Filled", () => FilledPercentDisplay);
		model.AddOverview("Outbound", () => OutboundAccessDisplay);
		model.AddAction("Purchase Capsule", PurchaseEmptyCapsule, CanPurchaseEmptyCapsule);
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

	private bool CanPurchaseEmptyCapsule()
	{
		CapsuleBufferService service = GameContext.HasInstance
			? GameContext.Instance.CapsuleBufferSvc
			: null;
		return service?.CanPurchaseCapsule(currentTarget) == true;
	}

	private void PurchaseEmptyCapsule()
	{
		if (GameContext.HasInstance)
			GameContext.Instance.CapsuleBufferSvc?.TryPurchaseCapsule(currentTarget);
	}

	private static string GetStateLabel(CapsuleBuffer buffer)
	{
		if (buffer == null)
			return "Unknown";
		if (buffer.DockedCapsule == null)
			return "Vacant";
		if (buffer.IsCapsuleEmpty())
			return "Empty";

		return buffer.DockedCapsule.LogisticsState == CapsuleLogisticsState.OB
			? "Outbound"
			: "Loaded";
	}
}
