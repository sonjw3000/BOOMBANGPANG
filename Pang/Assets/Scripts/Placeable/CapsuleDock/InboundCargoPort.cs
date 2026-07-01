public sealed class InboundCargoPort : CargoPort
{
	public override CapsuleDockState DockState => CapsuleDockState.IBStandby;
	public override string PortRoleLabel => "Inbound Cargo Port";
}
