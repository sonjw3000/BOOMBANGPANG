using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Worker/Visual Catalog")]
public class WorkerVisualCatalog : ScriptableObject
{
	[SerializeField] private List<WorkerVisualDefinition> visuals = new();

	public WorkerVisualDefinition FindById(string visualId)
	{
		if (string.IsNullOrWhiteSpace(visualId))
			return null;

		foreach (var visual in visuals)
		{
			if (visual == null)
				continue;

			if (visual.VisualId == visualId)
				return visual;
		}

		return null;
	}
}
