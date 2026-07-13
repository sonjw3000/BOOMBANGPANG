public sealed partial class LicenseService
{
	public LicenseServiceSaveData CaptureState()
	{
		LicenseServiceSaveData data = new();
		foreach (AcquiredLicenseState state in acquiredLicenses)
		{
			if (state?.Definition == null)
				continue;

			data.AcquiredLicenses.Add(new LicenseRuntimeSaveData
			{
				LicenseId = state.Definition.LicenseId,
				Grade = state.Grade,
				ComplianceState = state.ComplianceState,
			});
		}

		return data;
	}

	public void RestoreState(LicenseServiceSaveData data)
	{
		ResetRuntimeState();
		if (data?.AcquiredLicenses == null)
			return;

		foreach (LicenseRuntimeSaveData savedLicense in data.AcquiredLicenses)
		{
			if (savedLicense == null ||
				TryGetDefinition(savedLicense.LicenseId, out LicenseDefinition definition) == false ||
				definition.HasGrade(savedLicense.Grade) == false)
			{
				continue;
			}

			SetAcquiredState(definition, savedLicense.Grade, savedLicense.ComplianceState);
		}

		OnLicensesChanged?.Invoke();
	}

	public void ResetRuntimeState()
	{
		acquiredLicenses.Clear();
		acquiredLicensesById.Clear();
		nonCompliantLicenses.Clear();
		OnLicensesChanged?.Invoke();
	}
}
