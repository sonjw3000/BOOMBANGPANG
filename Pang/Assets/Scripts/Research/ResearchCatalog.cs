using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ResearchCatalog", menuName = "Research/Research Catalog")]
public sealed class ResearchCatalog : ScriptableObject
{
	[Serializable]
	private sealed class ResearchTreeJson
	{
		[SerializeField] private int schemaVersion = 1;
		[SerializeField] private List<ResearchDefinition> nodes = new();

		public int SchemaVersion => schemaVersion;
		public List<ResearchDefinition> Nodes => nodes;
	}

	[SerializeField] private TextAsset researchTreeJson;

	private readonly List<ResearchDefinition> definitions = new();
	private readonly Dictionary<string, ResearchDefinition> definitionsById = new(StringComparer.Ordinal);
	private bool indexBuilt;

	public IReadOnlyList<ResearchDefinition> Definitions
	{
		get
		{
			EnsureIndex();
			return definitions;
		}
	}

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
		definitions.Clear();
		definitionsById.Clear();
		indexBuilt = false;

		if (researchTreeJson == null)
		{
			if (logErrors)
				Debug.LogError("[ResearchCatalog] Research tree JSON is missing.", this);
			return false;
		}

		ResearchTreeJson tree;
		try
		{
			tree = JsonUtility.FromJson<ResearchTreeJson>(researchTreeJson.text);
		}
		catch (Exception exception)
		{
			if (logErrors)
				Debug.LogError($"[ResearchCatalog] Failed to parse {researchTreeJson.name}: {exception.Message}", this);
			return false;
		}

		if (tree == null || tree.SchemaVersion < 1 || tree.Nodes == null)
		{
			if (logErrors)
				Debug.LogError($"[ResearchCatalog] {researchTreeJson.name} has an invalid root structure.", this);
			return false;
		}

		definitions.AddRange(tree.Nodes);
		bool valid = true;

		foreach (ResearchDefinition definition in definitions)
		{
			if (definition == null)
				continue;

			if (string.IsNullOrWhiteSpace(definition.Uid))
			{
				valid = false;
				if (logErrors)
					Debug.LogError("[ResearchCatalog] A research node has an empty UID.", this);
				continue;
			}

			if (definition.Cost < 0 || definition.DurationWeeks < 1)
			{
				valid = false;
				if (logErrors)
				{
					Debug.LogError(
						$"[ResearchCatalog] {definition.Uid} has invalid cost or duration values.",
						this);
				}
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
							this);
					}
				}
			}
		}

		indexBuilt = true;
		return valid;
	}
}
