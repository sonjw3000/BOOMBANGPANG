using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LicenseCatalog", menuName = "License/License Catalog")]
public sealed class LicenseCatalog : ScriptableObject
{
	[SerializeField] private List<LicenseDefinition> licenses = new();

	public IReadOnlyList<LicenseDefinition> Licenses => licenses;

	public bool Contains(LicenseDefinition definition)
	{
		return definition != null && licenses != null && licenses.Contains(definition);
	}

	private void OnValidate()
	{
		if (licenses == null)
			return;

		HashSet<LicenseDefinition> registeredDefinitions = new();
		HashSet<string> registeredIds = new(System.StringComparer.Ordinal);

		foreach (LicenseDefinition definition in licenses)
		{
			if (definition == null)
			{
				Debug.LogError($"[LicenseCatalog] Null license definition found in {name}.", this);
				continue;
			}

			if (registeredDefinitions.Add(definition) == false)
				Debug.LogError($"[LicenseCatalog] Duplicate definition {definition.name} found in {name}.", this);

			if (string.IsNullOrWhiteSpace(definition.LicenseId))
			{
				Debug.LogError($"[LicenseCatalog] LicenseId is empty on {definition.name}.", definition);
				continue;
			}

			if (registeredIds.Add(definition.LicenseId) == false)
				Debug.LogError($"[LicenseCatalog] Duplicate LicenseId {definition.LicenseId} found in {name}.", this);
		}
	}
}
