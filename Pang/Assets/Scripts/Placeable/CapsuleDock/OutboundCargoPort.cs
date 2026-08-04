public sealed class OutboundCargoPort : CargoPort
{
	public override CapsuleDockState DockState => CapsuleDockState.OB;
	public override string PortRoleLabel => "Outbound Cargo Port";

	protected override bool SupportsCargoRoute(CargoRouteKind routeKind)
	{
		return routeKind == CargoRouteKind.Standard || routeKind == CargoRouteKind.Waste;
	}
}
