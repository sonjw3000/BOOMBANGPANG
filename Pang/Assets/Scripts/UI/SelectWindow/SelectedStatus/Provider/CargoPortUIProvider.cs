public class CargoPortUIProvider : ShelfBaseUIProviderBase<CargoPort>
{
	public override string Subtitle => currentTarget != null ?
		(currentTarget.IsInbound ? "IBCargo Port" : "OBCargo Port") :
		"Unknown type";

	public float FilledPercent => currentTarget != null ? currentTarget.FilledPercent : 0.0f;
}
