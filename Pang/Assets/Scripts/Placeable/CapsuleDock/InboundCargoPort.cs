using System.Collections.Generic;
using System.Linq;

public sealed class InboundCargoPort : CargoPort
{
	public override string PortRoleLabel => "Inbound Cargo Port";

	public override bool CanLinkTo(CargoPort target) => target as OutboundCargoPort != null && LinkedPorts.Contains(target);
}
