using System;
using System.Collections.Generic;
using UnityEngine;

public sealed class LicenseService : MonoBehaviour
{
	[SerializeField] private List<LicenseCatalog> catalogs = new();

	private readonly List<LicenseDefinition> definitions = new();
	private readonly Dictionary<string, LicenseDefinition> definitionsById = new(StringComparer.Ordinal);
	private bool definitionsLoaded;

	public IReadOnlyList<LicenseDefinition> Definitions
	{
		get
		{
			EnsureDefinitionsLoaded();
			return definitions;
		}
	}

	private void Awake()
	{
		RebuildDefinitions();
	}

	private void OnValidate()
	{
		ValidateCatalogs();
	}

	public bool TryGetDefinition(string licenseId, out LicenseDefinition definition)
	{
		EnsureDefinitionsLoaded();

		if (string.IsNullOrWhiteSpace(licenseId))
		{
			definition = null;
			return false;
		}

		return definitionsById.TryGetValue(licenseId, out definition);
	}

	public bool ContainsDefinition(LicenseDefinition definition)
	{
		if (definition == null)
			return false;

		return TryGetDefinition(definition.LicenseId, out LicenseDefinition registeredDefinition) &&
			registeredDefinition == definition;
	}

	private void EnsureDefinitionsLoaded()
	{
		if (definitionsLoaded == false)
			RebuildDefinitions();
	}

	private void RebuildDefinitions()
	{
		definitions.Clear();
		definitionsById.Clear();

		if (catalogs != null)
		{
			foreach (LicenseCatalog catalog in catalogs)
				RegisterCatalog(catalog);
		}

		definitionsLoaded = true;
	}

	private void RegisterCatalog(LicenseCatalog catalog)
	{
		if (catalog == null || catalog.Licenses == null)
			return;

		foreach (LicenseDefinition definition in catalog.Licenses)
		{
			if (definition == null || string.IsNullOrWhiteSpace(definition.LicenseId))
				continue;

			if (definitionsById.TryAdd(definition.LicenseId, definition) == false)
			{
				Debug.LogError(
					$"[LicenseService] Duplicate LicenseId {definition.LicenseId} found while registering {catalog.name}.",
					catalog);
				continue;
			}

			definitions.Add(definition);
		}
	}

	private void ValidateCatalogs()
	{
		if (catalogs == null)
			return;

		HashSet<LicenseCatalog> registeredCatalogs = new();
		Dictionary<string, LicenseDefinition> registeredDefinitions = new(StringComparer.Ordinal);

		foreach (LicenseCatalog catalog in catalogs)
		{
			if (catalog == null)
			{
				Debug.LogError("[LicenseService] Null license catalog is registered.", this);
				continue;
			}

			if (registeredCatalogs.Add(catalog) == false)
				Debug.LogError($"[LicenseService] Catalog {catalog.name} is registered more than once.", this);

			if (catalog.Licenses == null)
				continue;

			foreach (LicenseDefinition definition in catalog.Licenses)
			{
				if (definition == null || string.IsNullOrWhiteSpace(definition.LicenseId))
					continue;

				if (registeredDefinitions.TryGetValue(definition.LicenseId, out LicenseDefinition existing))
				{
					Debug.LogError(
						$"[LicenseService] Duplicate LicenseId {definition.LicenseId}: {existing.name}, {definition.name}.",
						this);
					continue;
				}

				registeredDefinitions.Add(definition.LicenseId, definition);
			}
		}
	}
}
