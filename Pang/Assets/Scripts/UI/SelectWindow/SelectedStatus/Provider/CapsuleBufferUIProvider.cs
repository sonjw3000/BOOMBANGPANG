using UnityEngine;

public sealed class CapsuleBufferUIProvider : UIProvider<CapsuleBuffer>, IShelfBaseUIProvider
{
	Component IShelfBaseUIProvider.Target => currentTarget;

	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Capsule Buffer";
	public override string Subtitle => "Capsule Buffer";
	public override Sprite Icon => null;

	public string StateDisplay => currentTarget != null ? GetStateLabel(currentTarget.BufferState) : "Unknown";
	public string CapsuleDisplay => currentTarget?.DockedCapsule != null
		? $"Capsule #{currentTarget.DockedCapsule.BoxId}"
		: "Empty";
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
	};

	public override void BuildInfoBlocks()
	{
		infoBlocks.Clear();
		infoBlocks.Add(new KeyValueBlock("State", StateDisplay));
		infoBlocks.Add(new KeyValueBlock("Capsule", CapsuleDisplay));
		infoBlocks.Add(new KeyValueBlock("Filled", FilledPercentDisplay));
	}

	public override void OnUpdate()
	{
		if (infoBlocks.Count < 3)
			return;

		(infoBlocks[0] as KeyValueBlock)?.UpdateValue(StateDisplay);
		(infoBlocks[1] as KeyValueBlock)?.UpdateValue(CapsuleDisplay);
		(infoBlocks[2] as KeyValueBlock)?.UpdateValue(FilledPercentDisplay);
	}

	private static string GetStateLabel(CapsuleBufferState state)
	{
		return state switch
		{
			CapsuleBufferState.IBOnly => "Inbound Only",
			CapsuleBufferState.OBOnly => "Outbound Only",
			CapsuleBufferState.Empty => "Empty",
			_ => "Unknown",
		};
	}
}
