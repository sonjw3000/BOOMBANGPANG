using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class LabelingTask
{
	public LabelingTaskSaveData CaptureState(Func<GameObject, int> getPlaceableId)
	{
		GameObject targetObject = TargetPlaceable is Component component ? component.gameObject : null;
		return new LabelingTaskSaveData
		{
			BuildingId = BuildingId,
			TargetContainerId = targetObject != null && getPlaceableId != null ? getPlaceableId(targetObject) : -1,
			IsTaskEnd = isTaskEnd,
		};
	}
}

public static class LabelingTaskSaveExtensions
{
	public static LabelingTask Restore(this LabelingTaskSaveData data, Dictionary<int, GameObject> placeables)
	{
		if (data == null ||
			placeables == null ||
			placeables.TryGetValue(data.TargetContainerId, out GameObject targetObject) == false ||
			TryGetItemContainer(targetObject, out IItemContainer targetContainer) == false ||
			GameContext.HasInstance == false ||
			GameContext.Instance.BuildingMgr == null ||
			GameContext.Instance.BuildingMgr.TryGetBuilding(data.BuildingId, out Building building) == false ||
			building is not StagingBuilding stagingBuilding)
		{
			return null;
		}

		LabelingTask task = new(stagingBuilding, targetContainer);
		task.RestoreState(data.IsTaskEnd);
		return task;
	}

	private static bool TryGetItemContainer(GameObject targetObject, out IItemContainer targetContainer)
	{
		targetContainer = null;
		if (targetObject == null)
			return false;

		Component[] components = targetObject.GetComponents<Component>();
		for (int i = 0; i < components.Length; ++i)
		{
			if (components[i] is not IItemContainer candidate)
				continue;

			targetContainer = candidate;
			return true;
		}

		return false;
	}
}
