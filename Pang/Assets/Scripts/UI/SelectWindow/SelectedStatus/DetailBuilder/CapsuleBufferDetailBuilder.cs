using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CapsuleBufferDetailBuilder : ShelfBaseDetailContent<CapsuleBuffer>
{
	private TextMeshProUGUI stateValue;
	private TextMeshProUGUI capsuleValue;
	private TextMeshProUGUI inboundAccessValue;
	private TextMeshProUGUI outboundAccessValue;
	private Button emptyStateButton;
	private Button inboundOnlyStateButton;
	private Button outboundOnlyStateButton;
	private Button purchaseEmptyCapsuleButton;
	private Button sellEmptyCapsuleButton;

	private static CapsuleBufferService CapsuleBufferService => GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;
	private static BoxManager BoxMgr => GameContext.HasInstance ? GameContext.Instance.BoxMgr : null;

	protected override void RefreshExtraInfo(IShelfBaseUIProvider shelfProvider)
	{
		base.RefreshExtraInfo(shelfProvider);

		if (provider is not CapsuleBufferUIProvider capsuleBufferProvider || capsuleBufferProvider.Target == null)
			return;

		stateValue ??= AddInfoLine("State");
		capsuleValue ??= AddInfoLine("Capsule");
		inboundAccessValue ??= AddInfoLine("Inbound Access");
		outboundAccessValue ??= AddInfoLine("Outbound Access");

		stateValue.text = capsuleBufferProvider.StateDisplay;
		capsuleValue.text = capsuleBufferProvider.CapsuleDisplay;
		inboundAccessValue.text = capsuleBufferProvider.InboundAccessDisplay;
		outboundAccessValue.text = capsuleBufferProvider.OutboundAccessDisplay;
	}

	protected override void BuildActionTab()
	{
		base.BuildActionTab();

		if (provider is not CapsuleBufferUIProvider capsuleBufferProvider || capsuleBufferProvider.Target == null)
			return;

		emptyStateButton = AddStateButton("Set Empty", capsuleBufferProvider, CapsuleDockState.Empty);
		inboundOnlyStateButton = AddStateButton("Set IB", capsuleBufferProvider, CapsuleDockState.IB);
		outboundOnlyStateButton = AddStateButton("Set OB Standby", capsuleBufferProvider, CapsuleDockState.OBStandby);
		purchaseEmptyCapsuleButton = AddActionButton("Purchase Empty Capsule", () => PurchaseEmptyCapsule(capsuleBufferProvider));
		sellEmptyCapsuleButton = AddActionButton("Sell Empty Capsule", () => SellEmptyCapsule(capsuleBufferProvider));
		UpdateActionButtons();
	}

	protected override void UpdateData()
	{
		base.UpdateData();
		UpdateActionButtons();
	}

	private Button AddStateButton(string label, CapsuleBufferUIProvider capsuleBufferProvider, CapsuleDockState targetState)
	{
		return AddActionButton(label, () =>
		{
			if (capsuleBufferProvider.Target == null)
				return;

			if (CapsuleBufferService != null)
			{
				CapsuleBufferService.SetDockState(capsuleBufferProvider.Target, targetState);
				return;
			}

			capsuleBufferProvider.Target.SetDockState(targetState);
		});
	}

	private void UpdateActionButtons()
	{
		if (provider is not CapsuleBufferUIProvider capsuleBufferProvider || capsuleBufferProvider.Target == null)
			return;

		CapsuleDockState currentState = capsuleBufferProvider.Target.DockState;
		if (emptyStateButton != null)
			emptyStateButton.interactable = currentState != CapsuleDockState.Empty;
		if (inboundOnlyStateButton != null)
			inboundOnlyStateButton.interactable = currentState != CapsuleDockState.IB;
		if (outboundOnlyStateButton != null)
			outboundOnlyStateButton.interactable = currentState != CapsuleDockState.OBStandby;
		if (purchaseEmptyCapsuleButton != null)
			purchaseEmptyCapsuleButton.interactable = CanPurchaseEmptyCapsule(capsuleBufferProvider.Target);
		if (sellEmptyCapsuleButton != null)
			sellEmptyCapsuleButton.interactable = CanSellEmptyCapsule(capsuleBufferProvider.Target);
	}

	private static bool CanPurchaseEmptyCapsule(CapsuleBuffer target)
	{
		return target != null &&
			target.DockState == CapsuleDockState.Empty &&
			target.HasCapsule == false &&
			BoxMgr != null;
	}

	private static bool CanSellEmptyCapsule(CapsuleBuffer target)
	{
		return target != null &&
			target.DockState == CapsuleDockState.Empty &&
			target.DockedCapsule != null &&
			target.DockedCapsule.LogisticsState == CapsuleLogisticsState.Empty &&
			target.IsCapsuleEmpty() &&
			BoxMgr != null;
	}

	private static void PurchaseEmptyCapsule(CapsuleBufferUIProvider capsuleBufferProvider)
	{
		CapsuleBuffer target = capsuleBufferProvider?.Target;
		if (CanPurchaseEmptyCapsule(target) == false)
			return;

		if (BoxMgr.GetNewBox(BoxType.Capsule, out BoxBase box) == false)
			return;

		if (box is not CargoCapsule capsule)
		{
			BoxMgr.DisableBox(box);
			return;
		}

		capsule.SetLogisticsState(CapsuleLogisticsState.Empty);
		if (target.TryDockCapsule(capsule) == false)
			BoxMgr.DisableBox(capsule);
	}

	private static void SellEmptyCapsule(CapsuleBufferUIProvider capsuleBufferProvider)
	{
		CapsuleBuffer target = capsuleBufferProvider?.Target;
		if (CanSellEmptyCapsule(target) == false)
			return;

		if (target.TryUndockCapsule(out CargoCapsule capsule) == false)
			return;

		BoxMgr.DisableBox(capsule);
	}
}
