using System.Collections.Generic;

public partial class CargoStorageAddon
{
	public void RestoreState(IEnumerable<BoxBase> cargos)
	{
		cargosToLaunch.Clear();
		if (cargos == null)
			return;

		foreach (var cargo in cargos)
			TryStoreCargo(cargo);
	}
}
