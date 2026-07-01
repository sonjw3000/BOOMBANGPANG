using UnityEngine;

public class CargoPortUIProvider : UIProvider<CargoPort>, IShelfBaseUIProvider
{
	Component IShelfBaseUIProvider.Target => currentTarget;

	public override string Name => currentTarget != null ? currentTarget.name : "Unknown Cargo Port";
	public override string Subtitle => currentTarget != null ? currentTarget.PortRoleLabel : "Unknown type";
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

	private static string GetStateLabel(CapsuleDockState state)
	{
		return state switch
		{
			CapsuleDockState.IBStandby => "IB Standby",
			CapsuleDockState.IB => "IB",
			CapsuleDockState.Empty => "Empty",
			CapsuleDockState.OBStandby => "OB Standby",
			CapsuleDockState.OB => "OB",
			_ => "Unknown",
		};
	}
}
