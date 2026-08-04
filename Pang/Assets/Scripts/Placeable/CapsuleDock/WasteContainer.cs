public sealed class WasteContainer : CargoPort
{
	public override CapsuleDockState DockState => CapsuleDockState.WasteContainer;
	public override string PortRoleLabel => "Waste Container";
	protected override CargoRouteKind SupportedCargoRouteKind => CargoRouteKind.Waste;
}
