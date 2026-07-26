using System;
using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Contract.ItemContract
{
	[Serializable]
	public sealed class ContractLicenseRequirement
	{
		[SerializeField] private LicenseDefinition license;
		[SerializeField] private LicenseGrade minimumGrade = LicenseGrade.C;

		public LicenseDefinition License => license;
		public string LicenseId => license != null ? license.LicenseId : string.Empty;
		public LicenseGrade MinimumGrade => minimumGrade;
	}

	[CreateAssetMenu(menuName = "Contract/Contract Catalog")]
	public class ContractCatalog : ScriptableObject
	{
		[Header("Catalog Info")]
		[SerializeField] private string displayName = string.Empty;

		[Header("Research Requirement")]
		[SerializeField] private string requiredResearchUid = string.Empty;

		[Header("License Requirements")]
		[Tooltip("Empty means every contract in this catalog is available without a license.")]
		[SerializeField] private ContractLicenseRequirement[] requiredLicenses = Array.Empty<ContractLicenseRequirement>();

		[Header("Contracts")]
		public ContractDefinition[] Contracts;

		public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
		public string RequiredResearchUid => requiredResearchUid;
		public bool RequiresResearch => string.IsNullOrWhiteSpace(requiredResearchUid) == false;
		public IReadOnlyList<ContractLicenseRequirement> RequiredLicenses =>
			requiredLicenses ?? Array.Empty<ContractLicenseRequirement>();
		public bool RequiresLicense => requiredLicenses != null && requiredLicenses.Length > 0;

		private void OnValidate()
		{
			if (requiredLicenses == null || requiredLicenses.Length == 0)
				return;

			HashSet<LicenseDefinition> registeredRequirements = new();
			foreach (ContractLicenseRequirement requirement in requiredLicenses)
			{
				if (requirement == null)
				{
					Debug.LogError($"[ContractCatalog] Null license requirement found in {name}.", this);
					continue;
				}

				if (requirement.License == null)
				{
					Debug.LogError($"[ContractCatalog] License reference is missing in {name}.", this);
					continue;
				}

				if (requirement.MinimumGrade == LicenseGrade.None)
				{
					Debug.LogError(
						$"[ContractCatalog] None cannot be used as a minimum grade for {requirement.License.name} in {name}.",
						this);
					continue;
				}

				if (requirement.License.HasGrade(requirement.MinimumGrade) == false)
				{
					Debug.LogError(
						$"[ContractCatalog] {requirement.License.name} does not define grade {requirement.MinimumGrade} required by {name}.",
						this);
				}

				if (registeredRequirements.Add(requirement.License) == false)
				{
					Debug.LogError(
						$"[ContractCatalog] {requirement.License.name} is required more than once in {name}.",
						this);
				}
			}
		}
	}
}
