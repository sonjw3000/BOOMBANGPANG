using System.Collections.Generic;
using UnityEngine;

public partial class LaunchStation
{
	public void InitializeForSaveLoad()
	{
		// Facility registration is now owned by GridService -> FacilityManager.
	}

	public LaunchStationSaveData CaptureState()
	{
		LaunchStationSaveData data = new();
		if (TryGetAddon<CargoStorageAddon>(out var cargoStorage))
		{
			foreach (var cargo in cargoStorage.CargosToLaunch)
			{
				if (cargo != null)
				{
					data.CargoQueueBoxes.Add(new BoxReferenceSaveData
					{
						BoxType = cargo.Type,
						BoxId = cargo.BoxId,
					});
				}
			}
		}

		if (TryGetAddon<LaunchPadAddon>(out var launchPad))
		{
			data.ReadyToLaunch = launchPad.IsReady;
			if (launchPad.CargoToLaunch != null)
			{
				data.LoadedCargoBox = new BoxReferenceSaveData
				{
					BoxType = launchPad.CargoToLaunch.Type,
					BoxId = launchPad.CargoToLaunch.BoxId,
				};
			}
		}

		return data;
	}

	public void RestoreState(LaunchStationSaveData data)
	{
		if (data == null)
			return;

		if (TryGetAddon<CargoStorageAddon>(out var cargoStorage))
		{
			List<BoxBase> cargos = new();
			foreach (var cargoRef in data.CargoQueueBoxes)
			{
				if (cargoRef != null && GameContext.Instance.BoxMgr.TryGetBox(cargoRef.BoxType, cargoRef.BoxId, out var cargo))
					cargos.Add(cargo);
			}

			cargoStorage.RestoreState(cargos);
		}

		if (TryGetAddon<LaunchPadAddon>(out var launchPad))
		{
			BoxBase loadedCargo = null;
			if (data.LoadedCargoBox != null)
				GameContext.Instance.BoxMgr.TryGetBox(data.LoadedCargoBox.BoxType, data.LoadedCargoBox.BoxId, out loadedCargo);
			launchPad.RestoreState(loadedCargo, data.ReadyToLaunch);
		}
	}
}
