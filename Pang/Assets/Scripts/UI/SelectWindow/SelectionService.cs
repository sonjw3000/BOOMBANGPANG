using UnityEngine;

public sealed class SelectionService
{

	public bool TryBuildModel(GameObject obj, out SelectionModel model)
	{
		//if (obj.TryGetComponent<>(out var placeable))
		{
			model = null;
			return true;
		}

		model = null;
		return false;
	}

}
