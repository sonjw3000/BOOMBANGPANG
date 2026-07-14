using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResearchCatalog", menuName = "Research/Research Catalog")]
public sealed class ResearchCatalog : ScriptableObject
{
	[SerializeField] private List<ResearchDefinition> definitions = new();

	private readonly Dictionary<string, ResearchDefinition> definitionsById = new(StringComparer.Ordinal);
	private bool indexBuilt;

	public IReadOnlyList<ResearchDefinition> Definitions => definitions;

	private void OnEnable()
	{
		RebuildIndex(false);
	}

	private void OnValidate()
	{
		RebuildIndex(true);
	}

	public bool TryGet(string researchId, out ResearchDefinition definition)
	{
		EnsureIndex();

		if (string.IsNullOrWhiteSpace(researchId))
		{
			definition = null;
			return false;
		}

		return definitionsById.TryGetValue(researchId, out definition);
	}

	public bool ValidateKeys()
	{
		return RebuildIndex(true);
	}

	private void EnsureIndex()
	{
		if (indexBuilt == false)
			RebuildIndex(false);
	}

	private bool RebuildIndex(bool logErrors)
	{
		definitionsById.Clear();
		bool valid = true;

		foreach (ResearchDefinition definition in definitions)
		{
			if (definition == null)
				continue;

			if (string.IsNullOrWhiteSpace(definition.Uid))
			{
				valid = false;
				if (logErrors)
					Debug.LogError($"[ResearchCatalog] {definition.name} has an empty UID.", definition);
				continue;
			}

			if (definitionsById.TryAdd(definition.Uid, definition) == false)
			{
				valid = false;
				if (logErrors)
					Debug.LogError($"[ResearchCatalog] Duplicate research UID: {definition.Uid}", this);
			}
		}

		foreach (ResearchDefinition definition in definitions)
		{
			if (definition == null || string.IsNullOrWhiteSpace(definition.Uid))
				continue;

			foreach (string prerequisiteUid in definition.PrerequisiteUids)
			{
				if (string.IsNullOrWhiteSpace(prerequisiteUid) ||
					definitionsById.ContainsKey(prerequisiteUid) == false)
				{
					valid = false;
					if (logErrors)
					{
						Debug.LogError(
							$"[ResearchCatalog] {definition.Uid} has an unknown prerequisite UID: {prerequisiteUid}",
							definition);
					}
				}
			}
		}

		indexBuilt = true;
		return valid;
	}
}
