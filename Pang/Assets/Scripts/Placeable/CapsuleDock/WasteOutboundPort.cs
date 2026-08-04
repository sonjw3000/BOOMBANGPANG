public sealed class WasteOutboundPort : CargoPort
{
	public override CapsuleDockState DockState => CapsuleDockState.WasteOutbound;
	public override string PortRoleLabel => "Waste Outbound Port";
	protected override CargoRouteKind SupportedCargoRouteKind => CargoRouteKind.Waste;
}
