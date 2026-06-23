using System.Collections.Generic;
using System.Linq;

public sealed class OutboundCargoPort : CargoPort
{
	public override string PortRoleLabel => "Outbound Cargo Port";
	
	public override bool CanLinkTo(CargoPort target) => target is InboundCargoPort && target != this && LinkedPorts.Contains(target) == false;
}
