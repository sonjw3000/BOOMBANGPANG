public sealed class OutboundCargoPort : CargoPort
{
	public override CapsuleDockState DockState => CapsuleDockState.OB;
	public override string PortRoleLabel => "Outbound Cargo Port";
}
