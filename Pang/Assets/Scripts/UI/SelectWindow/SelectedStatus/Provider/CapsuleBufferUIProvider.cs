using UnityEngine;
using UniverseLogistics.UI.Toolkit;

public sealed class CapsuleBufferUIProvider : UIProvider<CapsuleBuffer>, IShelfBaseUIProvider, ISelectionInspectorProvider
{
	Component IShelfBaseUIProvider.Target => currentTarget;

	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Capsule Buffer";
	public override string Subtitle => "Capsule Buffer";
	public override Sprite Icon => null;

	public string StateDisplay => currentTarget != null ? GetStateLabel(currentTarget.DockState) : "Unknown";
	public string CapsuleDisplay => currentTarget?.DockedCapsule != null
		? $"Capsule #{currentTarget.DockedCapsule.BoxId}"
		: "Empty";
	public string DockedBufferDisplay => currentTarget?.DockedCapsule != null
		? currentTarget.DockedCapsule.LogisticsState.ToString()
		: "None";
	public string CapacityDisplay => currentTarget != null ? $"{currentTarget.MaxSize:0.0} units" : "0.0 units";
	public string CurrentSizeDisplay => currentTarget != null ? $"{currentTarget.TotalSize:0.0} units" : "0.0 units";
	public string FilledPercentDisplay => currentTarget != null ? $"{currentTarget.FilledPercent:0.0}%" : "0.0%";
	public string InboundAccessDisplay => currentTarget != null && currentTarget.CanReceiveFromInbound() ? "Open" : "Closed";
	public string OutboundAccessDisplay => currentTarget != null && currentTarget.CanDispatchToOutbound() ? "Open" : "Closed";

	public ItemContainerDisplayInfo GetItemDisplay() => new()
	{
		ContainerName = currentTarget?.DockedCapsule != null
			? $"Docked Capsule #{currentTarget.DockedCapsule.BoxId}"
			: "Docked Capsule",
		HasContainer = currentTarget?.DockedCapsule != null,
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
		model.AddOverview("Inbound", () => InboundAccessDisplay);
		model.AddOverview("Outbound", () => OutboundAccessDisplay);
		model.AddAction("Purchase Capsule", PurchaseEmptyCapsule, CanPurchaseEmptyCapsule);
		model.AddAction("Sell Capsule", SellEmptyCapsule, CanSellEmptyCapsule);
		model.AddAction("Set Empty", () => SetDockState(CapsuleDockState.Empty), () => CanSetDockState(CapsuleDockState.Empty));
		model.AddAction("Set IB", () => SetDockState(CapsuleDockState.IB), () => CanSetDockState(CapsuleDockState.IB));
		model.AddAction("Set OB Standby", () => SetDockState(CapsuleDockState.OBStandby), () => CanSetDockState(CapsuleDockState.OBStandby));
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

	private bool CanSetDockState(CapsuleDockState state) => currentTarget != null && currentTarget.DockState != state;

	private void SetDockState(CapsuleDockState state)
	{
		if (currentTarget == null) return;
		CapsuleBufferService service = GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;
		if (service != null)
			service.SetDockState(currentTarget, state);
		else
			currentTarget.SetDockState(state);
	}

	private bool CanPurchaseEmptyCapsule()
	{
		return currentTarget != null && currentTarget.DockState == CapsuleDockState.Empty &&
			currentTarget.HasCapsule == false && GameContext.HasInstance && GameContext.Instance.BoxMgr != null;
	}

	private bool CanSellEmptyCapsule()
	{
		return currentTarget != null && currentTarget.DockState == CapsuleDockState.Empty &&
			currentTarget.DockedCapsule != null && currentTarget.DockedCapsule.LogisticsState == CapsuleLogisticsState.Empty &&
			currentTarget.IsCapsuleEmpty() && GameContext.HasInstance && GameContext.Instance.BoxMgr != null;
	}

	private void PurchaseEmptyCapsule()
	{
		if (CanPurchaseEmptyCapsule() == false) return;
		BoxManager boxManager = GameContext.Instance.BoxMgr;
		if (boxManager.GetNewBox(BoxType.Capsule, out BoxBase box) == false) return;
		if (box is not CargoCapsule capsule)
		{
			boxManager.DisableBox(box);
			return;
		}
		capsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		if (currentTarget.TryDockCapsule(capsule) == false)
			boxManager.DisableBox(capsule);
	}

	private void SellEmptyCapsule()
	{
		if (CanSellEmptyCapsule() == false || currentTarget.TryUndockCapsule(out CargoCapsule capsule) == false)
			return;
		GameContext.Instance.BoxMgr.DisableBox(capsule);
	}

	private static string GetStateLabel(CapsuleDockState state)
	{
		return state switch
		{
			CapsuleDockState.IB => "IB",
			CapsuleDockState.OBStandby => "OB Standby",
			CapsuleDockState.Empty => "Empty",
			_ => "Unknown",
		};
	}
}
