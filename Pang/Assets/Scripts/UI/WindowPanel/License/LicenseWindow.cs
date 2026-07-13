using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
	public sealed class LicenseWindow : MonoBehaviour
	{
		[SerializeField] private UIWindow window;
		[SerializeField] private Transform licenseListRoot;
		[SerializeField] private Transform detailListRoot;
		[SerializeField] private GameObject listItemPrefab;

		[Header("Window MetaData")]
		[SerializeField] private string title = "License Management";
		[SerializeField] private Sprite icon;

		private LicenseDefinition selectedDefinition;
		private LicenseService LicenseService => GameContext.HasInstance ? GameContext.Instance.LicenseService : null;

		private void Awake()
		{
			window ??= GetComponentInChildren<UIWindow>(true);
			if (window != null)
			{
				window.SetTitle(title);
				window.SetIcon(icon);
				window.Close();
			}
		}

		private void OnEnable()
		{
			if (LicenseService != null)
				LicenseService.OnLicensesChanged += HandleLicensesChanged;

			Refresh();
		}

		private void OnDisable()
		{
			if (GameContext.HasInstance && GameContext.Instance.LicenseService != null)
				GameContext.Instance.LicenseService.OnLicensesChanged -= HandleLicensesChanged;
		}

		public void Open()
		{
			gameObject.SetActive(true);
			window?.Open();
			Refresh();
		}

		public void Close()
		{
			window?.Close();
			gameObject.SetActive(false);
		}

		private void Refresh()
		{
			LicenseService service = LicenseService;
			if (service == null || listItemPrefab == null)
				return;

			ClearChildren(licenseListRoot);
			IReadOnlyList<LicenseDefinition> definitions = service.Definitions;
			foreach (LicenseDefinition definition in definitions)
			{
				if (definition == null)
					continue;

				string label = FormatLicenseListLabel(service, definition);
				CreateRow(licenseListRoot, label, true, () => SelectLicense(definition));
			}

			if (selectedDefinition == null && definitions.Count > 0)
				selectedDefinition = definitions[0];

			RefreshSelectedLicense();
		}

		private void SelectLicense(LicenseDefinition definition)
		{
			selectedDefinition = definition;
			RefreshSelectedLicense();
		}

		private void RefreshSelectedLicense()
		{
			ClearChildren(detailListRoot);
			LicenseService service = LicenseService;
			if (service == null || selectedDefinition == null || listItemPrefab == null)
				return;

			bool acquired = service.TryGetAcquiredState(selectedDefinition.LicenseId, out AcquiredLicenseState state);
			string status = acquired
				? $"Active Grade {state.Grade} / {state.ComplianceState}"
				: "Not acquired";
			CreateRow(detailListRoot, status, false, null, acquired && state.IsCompliant == false);

			CompanyStateSnapshot snapshot = CompanyStateSnapshot.Capture();
			foreach (LicenseGradeDefinition gradeDefinition in selectedDefinition.Grades)
			{
				if (gradeDefinition == null)
					continue;

				LicenseEvaluationResult evaluation = LicenseConditionEvaluator.Evaluate(
					selectedDefinition,
					gradeDefinition.Grade,
					snapshot);
				LicenseGrade currentGrade = acquired ? state.Grade : LicenseGrade.None;
				bool canAcquire = LicenseGradeUtility.IsUpgrade(currentGrade, gradeDefinition.Grade) && evaluation.IsSatisfied;
				string gradeLabel = $"Acquire Grade {gradeDefinition.Grade} - {(evaluation.IsSatisfied ? "Ready" : "Requirements not met")}";
				CreateRow(
					detailListRoot,
					gradeLabel,
					canAcquire,
					() => AcquireGrade(gradeDefinition.Grade));

				AddEvaluationRows(evaluation);
			}

			if (acquired)
				CreateRow(detailListRoot, "Return License", true, ReturnSelectedLicense, true);
		}

		private void AddEvaluationRows(LicenseEvaluationResult evaluation)
		{
			foreach (LicenseConditionGroupEvaluation group in evaluation.Groups)
			{
				if (group.Conditions.Count == 0)
				{
					string buildingLabel = group.IsSatisfied
						? $"  [OK] Active building #{group.BuildingId}"
						: "  [X] Active building required";
					CreateRow(detailListRoot, buildingLabel, false, null, group.IsSatisfied == false);

					if (group.IsSatisfied == false && group.Group?.Conditions != null)
					{
						foreach (LicenseCondition requiredCondition in group.Group.Conditions)
						{
							if (requiredCondition == null)
								continue;

							string comparison = FormatComparison(requiredCondition.Comparison);
							string label = $"  [X] {requiredCondition.Metric} {comparison} " +
								$"{requiredCondition.TargetValue:0.##} (Current unavailable)";
							CreateRow(detailListRoot, label, false, null, true);
						}
					}

					continue;
				}

				foreach (LicenseConditionEvaluation condition in group.Conditions)
				{
					string marker = condition.IsSatisfied ? "[OK]" : "[X]";
					string comparison = FormatComparison(condition.Condition.Comparison);
					string label = $"  {marker} {condition.Condition.Metric} {comparison} {condition.Condition.TargetValue:0.##} " +
						$"(Current {condition.ObservedValue:0.##}, Building #{group.BuildingId})";
					CreateRow(detailListRoot, label, false, null, condition.IsSatisfied == false);
				}
			}
		}

		private void AcquireGrade(LicenseGrade grade)
		{
			LicenseService?.TryAcquireLicense(selectedDefinition, grade, out _);
		}

		private void ReturnSelectedLicense()
		{
			if (selectedDefinition != null)
				LicenseService?.TryReturnLicense(selectedDefinition.LicenseId);
		}

		private void HandleLicensesChanged()
		{
			Refresh();
		}

		private GameObject CreateRow(
			Transform root,
			string label,
			bool interactable,
			Action onClick,
			bool warning = false)
		{
			if (root == null || listItemPrefab == null)
				return null;

			GameObject row = Instantiate(listItemPrefab, root);
			Button button = row.GetComponent<Button>();
			TMP_Text text = row.GetComponentInChildren<TMP_Text>(true);
			if (text != null)
			{
				text.text = label;
				text.color = warning ? new Color(1.0f, 0.45f, 0.25f) : Color.white;
			}

			if (button != null)
			{
				button.onClick.RemoveAllListeners();
				button.interactable = interactable;
				if (interactable && onClick != null)
					button.onClick.AddListener(() => onClick());
			}

			return row;
		}

		private static string FormatLicenseListLabel(LicenseService service, LicenseDefinition definition)
		{
			if (service.TryGetAcquiredState(definition.LicenseId, out AcquiredLicenseState state) == false)
				return definition.DisplayName;

			string warning = state.IsCompliant ? string.Empty : " [!]";
			return $"{definition.DisplayName} [{state.Grade}]{warning}";
		}

		private static string FormatComparison(LicenseNumericComparison comparison)
		{
			return comparison switch
			{
				LicenseNumericComparison.Equal => "=",
				LicenseNumericComparison.LessThan => "<",
				LicenseNumericComparison.LessThanOrEqual => "<=",
				LicenseNumericComparison.GreaterThan => ">",
				LicenseNumericComparison.GreaterThanOrEqual => ">=",
				_ => "?",
			};
		}

		private static void ClearChildren(Transform root)
		{
			if (root == null)
				return;

			foreach (Transform child in root)
				Destroy(child.gameObject);
		}
	}
}
