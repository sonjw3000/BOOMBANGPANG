using UnityEngine;

public sealed class WasteBinDock : CapsuleBuffer
{
	[SerializeField] private bool provisionInitialBin = true;

	public override CapsuleDockState DockState => CapsuleDockState.WasteBin;
	protected override CargoRouteKind SupportedCargoRouteKind => CargoRouteKind.Waste;

	private void Start()
	{
		if (provisionInitialBin)
			TryProvisionBin();
	}

	internal void ProvisionReplacementBin()
	{
		TryProvisionBin();
	}

	private void TryProvisionBin()
	{
		if (HasCapsule || GameContext.HasInstance == false)
			return;

		BoxManager boxManager = GameContext.Instance.BoxMgr;
		if (boxManager == null ||
			boxManager.GetNewBox(BoxType.WasteBin, out BoxBase box) == false ||
			box is not WasteBin wasteBin)
		{
			return;
		}

		wasteBin.SetLogisticsState(CapsuleLogisticsState.Empty);
		if (TryDockCapsule(wasteBin) == false)
			boxManager.DisableBox(wasteBin);
	}
}
