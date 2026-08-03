using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

public enum HumanIncidentResponseType
{
	None,
	WorkMistake,
	MinorInjury,
	AbortTask,
}

public sealed class HumanIncidentPayload
{
	public readonly HumanIncidentType Type;
	public readonly HumanIncidentResponseType ResponseType;
	public readonly HumanIncidentCause Cause;
	public readonly float RiskScore;
	public readonly float Chance;
	public readonly float ExposureGain;
	public readonly float HealthDamage;

	public HumanIncidentPayload(
		HumanIncidentType type,
		HumanIncidentResponseType responseType,
		HumanIncidentCause cause,
		float riskScore,
		float chance,
		float exposureGain,
		float healthDamage)
	{
		Type = type;
		ResponseType = responseType;
		Cause = cause;
		RiskScore = riskScore;
		Chance = chance;
		ExposureGain = exposureGain;
		HealthDamage = healthDamage;
	}
}

public sealed class HumanIncidentService : MonoBehaviour
{
	private const uint DefaultGlobalIncidentSeed = 0x8D12E5A3u;
	private const uint SeveritySalt = 0x68E31DA4u;
	private const uint HealthDamageSalt = 0xB5297A4Du;
	private const uint ItemDamageChanceSalt = 0x1B56C4E9u;
	private const uint ItemDamageAmountSalt = 0xC2B2AE35u;
	private const uint ItemDamageQuantitySalt = 0x27D4EB2Fu;
	private const uint ItemDamageStackSalt = 0x165667B1u;

	[FormerlySerializedAs("baseChance")]
	[SerializeField] private HumanIncidentDefinition definition;
	[SerializeField] private uint globalIncidentSeed = DefaultGlobalIncidentSeed;
	private uint runtimeGlobalIncidentSeed;

	public uint GlobalIncidentSeed => runtimeGlobalIncidentSeed != 0
		? runtimeGlobalIncidentSeed
		: GetConfiguredGlobalSeed();

	private void Awake() => ResetRuntimeState();

	public HumanIncidentSaveData CaptureState()
		=> new() { GlobalSeed = GlobalIncidentSeed };

	public void RestoreState(HumanIncidentSaveData data)
	{
		runtimeGlobalIncidentSeed = data != null && data.GlobalSeed != 0
			? data.GlobalSeed
			: GetConfiguredGlobalSeed();
	}

	public void ResetRuntimeState()
	{
		uint entropy = unchecked((uint)System.Guid.NewGuid().GetHashCode());
		runtimeGlobalIncidentSeed = Mix(GetConfiguredGlobalSeed(), entropy);
		if (runtimeGlobalIncidentSeed == 0)
			runtimeGlobalIncidentSeed = GetConfiguredGlobalSeed();
	}

	private uint GetConfiguredGlobalSeed()
		=> globalIncidentSeed == 0 ? DefaultGlobalIncidentSeed : globalIncidentSeed;

	public void InitializeWorker(HumanWorker worker)
	{
		worker?.EnsureIncidentState(GlobalIncidentSeed);
	}

	public void InitializeNewWorker(HumanWorker worker)
	{
		worker?.ResetIncidentState(GlobalIncidentSeed);
	}

	public float GetExposureRecoveryPerSecond()
		=> definition != null ? definition.RecoveryExposurePerSecond : 0.0f;

	public float GetMaximumUnsafeExposure()
		=> definition != null ? definition.MaximumUnsafeExposure : 0.0f;

	public float CalculateActionFatigue(HumanWorker worker, float baseFatigue, in HumanWorkHandlingResult handling)
	{
		if (worker == null)
			return 0.0f;

		float multiplier = 1.0f;
		if (definition != null && handling.HasHandling)
		{
			float loadRatio = handling.HandlingWeightKg / worker.SafeHandlingWeightKg;
			multiplier = definition.EvaluateHandlingFatigueMultiplier(loadRatio);
		}

		return Mathf.Max(0.0f, baseFatigue) * Mathf.Max(0.0f, multiplier);
	}

	public HumanIncidentPayload TryCreateIncident(
		HumanWorker worker,
		WorkActionType action,
		in HumanWorkHandlingResult handling)
	{
		if (worker == null || definition == null)
			return null;

		InitializeWorker(worker);

		float loadRatio = handling.HasHandling
			? handling.HandlingWeightKg / worker.SafeHandlingWeightKg
			: 0.0f;
		float overworkExposure = definition.GetOverworkExposure(worker.Fatigue);
		float overloadExposure = definition.GetOverloadExposure(loadRatio);
		bool unqualifiedHazard = handling.IsDangerous && worker.HasAbility(WorkerAbility.HazardHandling) == false;
		float qualificationExposure = unqualifiedHazard ? definition.UnqualifiedHazardExposure : 0.0f;
		float requestedExposure = overworkExposure + overloadExposure + qualificationExposure;
		float exposureGain = worker.AddUnsafeExposure(requestedExposure, definition.MaximumUnsafeExposure);
		float riskScore = worker.Fatigue + worker.UnsafeExposure;
		float baseChance = Mathf.Clamp01(definition.EvaluateIncidentChance(riskScore));
		float chance = baseChance >= 1.0f
			? 1.0f
			: Mathf.Clamp01(baseChance * Mathf.Max(0.0f, worker.GetIncidentMitigationMultiplier()));

		float incidentRoll = worker.IncidentRoll;
		if (chance <= 0.0f || incidentRoll >= chance)
			return null;

		HumanIncidentCause cause = ResolvePrimaryCause(
			worker.Fatigue,
			overworkExposure,
			overloadExposure,
			qualificationExposure);
		uint eventSeed = worker.BeginNextIncidentCycle();
		HumanIncidentType incidentType = RollIncidentType(riskScore, eventSeed);
		float healthRoll = GetDomainRoll(eventSeed, HealthDamageSalt);
		float healthDamage = incidentType == HumanIncidentType.WorkMistake
			? 0.0f
			: definition.GetHealthDamage(incidentType, healthRoll);
		if (incidentType == HumanIncidentType.MinorInjury && healthDamage >= worker.Health)
		{
			incidentType = HumanIncidentType.Collapse;
			healthDamage = definition.GetHealthDamage(incidentType, healthRoll);
		}
		if (incidentType != HumanIncidentType.WorkMistake)
		{
			float maximumSafeDamage = Mathf.Max(0.0f, worker.Health - 1.0f);
			healthDamage = worker.ApplyDamage(Mathf.Min(healthDamage, maximumSafeDamage));
		}

		HumanIncidentResponseType responseType = incidentType switch
		{
			HumanIncidentType.Collapse => HumanIncidentResponseType.AbortTask,
			HumanIncidentType.MinorInjury => HumanIncidentResponseType.MinorInjury,
			_ => HumanIncidentResponseType.WorkMistake,
		};

		switch (incidentType)
		{
			case HumanIncidentType.WorkMistake:
				worker.ReduceUnsafeExposure(0.5f);
				TryApplyMistakeDamage(worker, in handling, eventSeed);
				break;

			case HumanIncidentType.MinorInjury:
				worker.ReduceUnsafeExposure(0.25f);
				break;

			case HumanIncidentType.Collapse:
				worker.ReduceUnsafeExposure(0.0f);
				break;
		}

		HumanIncidentPayload result = new(
			incidentType,
			responseType,
			cause,
			riskScore,
			chance,
			exposureGain,
			healthDamage);
		worker.SetPendingIncident(result);
		PublishHudEvent(worker, action, result);
		Debug.Log(
			$"[HumanIncident] worker={worker.Name}, type={incidentType}, cause={cause}, " +
			$"risk={riskScore:0.##}, chance={chance:P1}, roll={incidentRoll:0.###}, " +
			$"action={action}, incidents={worker.IncidentCount}");
		return result;
	}

	public float GetMistakeCleanupSeconds()
		=> definition != null ? definition.MistakeCleanupSeconds : 0.0f;

	private HumanIncidentType RollIncidentType(float riskScore, uint eventSeed)
	{
		IncidentSeverityBand band = definition.GetSeverityBand(riskScore);
		float totalWeight = Mathf.Max(0.0f, band.MistakeWeight) +
			Mathf.Max(0.0f, band.MinorInjuryWeight) +
			Mathf.Max(0.0f, band.MajorIncidentWeight);
		if (totalWeight <= Mathf.Epsilon)
			return HumanIncidentType.WorkMistake;

		float roll = GetDomainRoll(eventSeed, SeveritySalt) * totalWeight;
		float mistakeUpper = Mathf.Max(0.0f, band.MistakeWeight);
		if (roll < mistakeUpper)
			return HumanIncidentType.WorkMistake;

		float minorUpper = mistakeUpper + Mathf.Max(0.0f, band.MinorInjuryWeight);
		return roll < minorUpper ? HumanIncidentType.MinorInjury : HumanIncidentType.Collapse;
	}

	private void TryApplyMistakeDamage(HumanWorker worker, in HumanWorkHandlingResult handling, uint eventSeed)
	{
		if (handling.Destination == null || handling.Quantity <= 0 || GameContext.HasInstance == false)
			return;

		ItemStack targetStack = FindDamageTarget(handling.Destination, handling.ItemId, eventSeed);
		if (targetStack == null)
			return;

		bool targetIsFragile = GameContext.Instance.ItemDB.GetItemData(
			targetStack.ItemID,
			out ItemDefinition targetDefinition) &&
			(targetDefinition.Tag & ItemTag.Fragile) != 0;
		float damageChance = definition.MistakeItemDamageChance;
		if (targetIsFragile)
			damageChance *= definition.FragileDamageChanceMultiplier;
		if (GetDomainRoll(eventSeed, ItemDamageChanceSalt) >= Mathf.Clamp01(damageChance))
			return;

		int maximumQuantity = Mathf.Min(handling.Quantity, targetStack.Quantity);
		int affectedQuantity = Mathf.Clamp(
			1 + Mathf.FloorToInt(GetDomainRoll(eventSeed, ItemDamageQuantitySalt) * maximumQuantity),
			1,
			maximumQuantity);
		if (handling.Destination.TryRemoveFromStack(targetStack, affectedQuantity, out ItemStack removedStack) == false)
			return;

		float damage = definition.GetMistakeItemDamage(
			GetDomainRoll(eventSeed, ItemDamageAmountSalt),
			targetIsFragile);
		ItemDamageService itemDamageService = GameContext.Instance.ItemDamage;
		if (itemDamageService == null ||
			itemDamageService.TryCreateDamagedStack(
				removedStack,
				affectedQuantity,
				damage,
				ItemDamageCause.Handling,
				out ItemStack damagedStack,
				out ItemDamageChange damageChange) == false)
		{
			RestoreRemovedStack(handling.Destination, removedStack);
			return;
		}

		if (handling.Destination.AddStack(damagedStack) == false)
		{
			damagedStack.Recycle();
			RestoreRemovedStack(handling.Destination, removedStack);
			return;
		}
		if (damagedStack.Quantity <= 0)
			damagedStack.Recycle();

		removedStack.Recycle();
		int3 origin = worker.GridPosition;
		itemDamageService.CommitDamage(in damageChange, in origin, handling.Destination);
		worker.ReportItemDamageIncident();
	}

	private static void RestoreRemovedStack(IItemContainer destination, ItemStack removedStack)
	{
		if (destination == null || removedStack == null)
			return;

		if (destination.AddStack(removedStack) == false)
		{
			Debug.LogError("[HumanIncident] Failed to restore an item stack after mistake damage rollback.");
			return;
		}

		if (removedStack.Quantity <= 0)
			removedStack.Recycle();
	}

	private static ItemStack FindDamageTarget(IItemContainer container, uint itemId, uint eventSeed)
	{
		int candidateCount = 0;
		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack != null && stack.Quantity > 0 && (itemId == 0 || stack.ItemID == itemId))
				++candidateCount;
		}

		if (candidateCount == 0)
			return null;

		int selected = Mathf.Clamp(
			Mathf.FloorToInt(GetDomainRoll(eventSeed, ItemDamageStackSalt) * candidateCount),
			0,
			candidateCount - 1);
		for (int i = 0; i < container.Stacks.Count; ++i)
		{
			ItemStack stack = container.Stacks[i];
			if (stack == null || stack.Quantity <= 0 || (itemId != 0 && stack.ItemID != itemId))
				continue;
			if (selected-- == 0)
				return stack;
		}

		return null;
	}

	private static HumanIncidentCause ResolvePrimaryCause(
		float fatigue,
		float overworkExposure,
		float overloadExposure,
		float qualificationExposure)
	{
		if (qualificationExposure >= overloadExposure && qualificationExposure >= overworkExposure && qualificationExposure > 0.0f)
			return HumanIncidentCause.UnqualifiedHazard;
		if (overloadExposure >= overworkExposure && overloadExposure > 0.0f)
			return HumanIncidentCause.Overload;
		if (overworkExposure > 0.0f)
			return HumanIncidentCause.Overwork;
		return fatigue > 0.0f ? HumanIncidentCause.Fatigue : HumanIncidentCause.None;
	}

	private static void PublishHudEvent(HumanWorker worker, WorkActionType action, HumanIncidentPayload payload)
	{
		if (payload == null || GameContext.HasInstance == false)
			return;

		string incidentLabel = payload.Type switch
		{
			HumanIncidentType.Collapse => "suffered a major accident",
			HumanIncidentType.MinorInjury => "suffered a minor injury",
			_ => "made a handling mistake",
		};
		GameContext.Instance.HudEventManager?.Publish(
			HudEventType.Warning,
			$"{worker.Name} {incidentLabel} during {FormatEnumLabel(action.ToString())}",
			worker);
	}

	private static string FormatEnumLabel(string raw)
	{
		if (string.IsNullOrWhiteSpace(raw))
			return string.Empty;

		System.Text.StringBuilder builder = new(raw.Length + 8);
		for (int i = 0; i < raw.Length; ++i)
		{
			char current = raw[i];
			if (i > 0 && char.IsUpper(current) && char.IsLower(raw[i - 1]))
				builder.Append(' ');
			builder.Append(current);
		}
		return builder.ToString();
	}

	public static uint BuildWorkerSeed(uint globalSeed, uint workerId)
	{
		uint seed = Mix(globalSeed == 0 ? DefaultGlobalIncidentSeed : globalSeed, workerId + 0x9E3779B9u);
		return seed == 0 ? DefaultGlobalIncidentSeed : seed;
	}

	public static uint NextUInt(ref uint state)
	{
		if (state == 0)
			state = DefaultGlobalIncidentSeed;
		uint value = state;
		value ^= value << 13;
		value ^= value >> 17;
		value ^= value << 5;
		state = value == 0 ? DefaultGlobalIncidentSeed : value;
		return state;
	}

	public static float NextUnitFloat(ref uint state)
		=> (NextUInt(ref state) & 0x00FFFFFFu) / 16777216.0f;

	private static float GetDomainRoll(uint eventSeed, uint salt)
	{
		uint state = Mix(eventSeed, salt);
		return NextUnitFloat(ref state);
	}

	private static uint Mix(uint first, uint second)
	{
		uint value = first ^ second;
		value ^= value >> 16;
		value *= 0x7FEB352Du;
		value ^= value >> 15;
		value *= 0x846CA68Bu;
		value ^= value >> 16;
		return value;
	}
}
