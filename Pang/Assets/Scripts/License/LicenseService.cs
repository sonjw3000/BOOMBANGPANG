using System;
using System.Collections.Generic;
using UnityEngine;

public enum LicenseComplianceState
{
	Compliant = 0,
	NonCompliant = 1,
}

public sealed class AcquiredLicenseState
{
	public LicenseDefinition Definition { get; }
	public LicenseGrade Grade { get; internal set; }
	public LicenseComplianceState ComplianceState { get; internal set; }
	public bool IsCompliant => ComplianceState == LicenseComplianceState.Compliant;

	internal AcquiredLicenseState(
		LicenseDefinition definition,
		LicenseGrade grade,
		LicenseComplianceState complianceState)
	{
		Definition = definition;
		Grade = grade;
		ComplianceState = complianceState;
	}
}

public sealed partial class LicenseService : MonoBehaviour
{
	[SerializeField] private List<LicenseCatalog> catalogs = new();

	private readonly List<LicenseDefinition> definitions = new();
	private readonly Dictionary<string, LicenseDefinition> definitionsById = new(StringComparer.Ordinal);
	private readonly List<AcquiredLicenseState> acquiredLicenses = new();
	private readonly Dictionary<string, AcquiredLicenseState> acquiredLicensesById = new(StringComparer.Ordinal);
	private readonly List<AcquiredLicenseState> nonCompliantLicenses = new();
	private bool definitionsLoaded;

	public event Action OnLicensesChanged;

	public IReadOnlyList<LicenseDefinition> Definitions
	{
		get
		{
			EnsureDefinitionsLoaded();
			return definitions;
		}
	}
	public IReadOnlyList<AcquiredLicenseState> AcquiredLicenses => acquiredLicenses;
	public IReadOnlyList<AcquiredLicenseState> NonCompliantLicenses => nonCompliantLicenses;

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

	public LicenseEvaluationResult Evaluate(LicenseDefinition definition, LicenseGrade grade)
	{
		return LicenseConditionEvaluator.Evaluate(definition, grade, CompanyStateSnapshot.Capture());
	}

	public bool TryAcquireLicense(
		LicenseDefinition definition,
		LicenseGrade grade,
		out LicenseEvaluationResult evaluation)
	{
		evaluation = null;
		if (ContainsDefinition(definition) == false || definition.HasGrade(grade) == false)
			return false;

		LicenseGrade currentGrade = TryGetAcquiredState(definition.LicenseId, out AcquiredLicenseState current)
			? current.Grade
			: LicenseGrade.None;
		if (LicenseGradeUtility.IsUpgrade(currentGrade, grade) == false)
			return false;

		evaluation = Evaluate(definition, grade);
		if (evaluation.IsSatisfied == false)
			return false;

		SetAcquiredState(definition, grade, LicenseComplianceState.Compliant);
		OnLicensesChanged?.Invoke();
		return true;
	}

	public bool TryReturnLicense(string licenseId)
	{
		if (TryGetAcquiredState(licenseId, out AcquiredLicenseState state) == false)
			return false;

		acquiredLicensesById.Remove(licenseId);
		acquiredLicenses.Remove(state);
		nonCompliantLicenses.Remove(state);
		OnLicensesChanged?.Invoke();
		return true;
	}

	public bool TryGetAcquiredState(string licenseId, out AcquiredLicenseState state)
	{
		if (string.IsNullOrWhiteSpace(licenseId))
		{
			state = null;
			return false;
		}

		return acquiredLicensesById.TryGetValue(licenseId, out state);
	}

	public bool TryGetAcquiredGrade(string licenseId, out LicenseGrade grade)
	{
		if (TryGetAcquiredState(licenseId, out AcquiredLicenseState state))
		{
			grade = state.Grade;
			return true;
		}

		grade = LicenseGrade.None;
		return false;
	}

	public bool MeetsRequirement(string licenseId, LicenseGrade minimumGrade)
	{
		return TryGetAcquiredGrade(licenseId, out LicenseGrade acquiredGrade) &&
			LicenseGradeUtility.MeetsRequirement(acquiredGrade, minimumGrade);
	}

	public void ReevaluateAcquiredLicenses()
	{
		if (acquiredLicenses.Count == 0)
			return;

		CompanyStateSnapshot snapshot = CompanyStateSnapshot.Capture();
		bool changed = false;
		foreach (AcquiredLicenseState state in acquiredLicenses)
		{
			LicenseEvaluationResult evaluation = LicenseConditionEvaluator.Evaluate(
				state.Definition,
				state.Grade,
				snapshot);
			LicenseComplianceState nextState = evaluation.IsSatisfied
				? LicenseComplianceState.Compliant
				: LicenseComplianceState.NonCompliant;
			if (state.ComplianceState == nextState)
				continue;

			state.ComplianceState = nextState;
			UpdateNonCompliantRegistration(state);
			changed = true;
		}

		if (changed)
			OnLicensesChanged?.Invoke();
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

	private void SetAcquiredState(
		LicenseDefinition definition,
		LicenseGrade grade,
		LicenseComplianceState complianceState)
	{
		if (acquiredLicensesById.TryGetValue(definition.LicenseId, out AcquiredLicenseState state))
		{
			state.Grade = grade;
			state.ComplianceState = complianceState;
			UpdateNonCompliantRegistration(state);
			return;
		}

		state = new AcquiredLicenseState(definition, grade, complianceState);
		acquiredLicensesById.Add(definition.LicenseId, state);
		acquiredLicenses.Add(state);
		acquiredLicenses.Sort((left, right) =>
			StringComparer.Ordinal.Compare(left.Definition.LicenseId, right.Definition.LicenseId));
		UpdateNonCompliantRegistration(state);
	}

	private void UpdateNonCompliantRegistration(AcquiredLicenseState state)
	{
		if (state.ComplianceState == LicenseComplianceState.NonCompliant)
		{
			if (nonCompliantLicenses.Contains(state) == false)
				nonCompliantLicenses.Add(state);
			return;
		}

		nonCompliantLicenses.Remove(state);
	}
}
