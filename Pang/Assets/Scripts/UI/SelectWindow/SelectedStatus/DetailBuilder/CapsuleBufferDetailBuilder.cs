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

	private static CapsuleBufferService CapsuleBufferService => GameContext.HasInstance ? GameContext.Instance.CapsuleBufferSvc : null;

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

		emptyStateButton = AddStateButton("Set Empty", capsuleBufferProvider, CapsuleBufferState.Empty);
		inboundOnlyStateButton = AddStateButton("Set Inbound Only", capsuleBufferProvider, CapsuleBufferState.IBOnly);
		outboundOnlyStateButton = AddStateButton("Set Outbound Only", capsuleBufferProvider, CapsuleBufferState.OBOnly);
		UpdateActionButtons();
	}

	protected override void UpdateData()
	{
		base.UpdateData();
		UpdateActionButtons();
	}

	private Button AddStateButton(string label, CapsuleBufferUIProvider capsuleBufferProvider, CapsuleBufferState targetState)
	{
		return AddActionButton(label, () =>
		{
			if (capsuleBufferProvider.Target == null)
				return;

			if (CapsuleBufferService != null)
			{
				CapsuleBufferService.SetBufferState(capsuleBufferProvider.Target, targetState);
				return;
			}

			capsuleBufferProvider.Target.SetBufferState(targetState);
		});
	}

	private void UpdateActionButtons()
	{
		if (provider is not CapsuleBufferUIProvider capsuleBufferProvider || capsuleBufferProvider.Target == null)
			return;

		CapsuleBufferState currentState = capsuleBufferProvider.Target.BufferState;
		if (emptyStateButton != null)
			emptyStateButton.interactable = currentState != CapsuleBufferState.Empty;
		if (inboundOnlyStateButton != null)
			inboundOnlyStateButton.interactable = currentState != CapsuleBufferState.IBOnly;
		if (outboundOnlyStateButton != null)
			outboundOnlyStateButton.interactable = currentState != CapsuleBufferState.OBOnly;
	}
}
