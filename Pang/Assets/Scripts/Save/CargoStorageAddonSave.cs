using System.Collections.Generic;

public partial class CargoStorageAddon
{
	public void RestoreState(IEnumerable<BoxBase> cargos)
	{
		foreach (BoxBase cargo in cargosToLaunch)
		{
			if (cargo != null)
				cargo.OnInvalidated -= HandleStoredCargoInvalidated;
		}

		cargosToLaunch.Clear();
		if (cargos == null)
			return;

		foreach (var cargo in cargos)
			TryStoreCargo(cargo);
	}
}
