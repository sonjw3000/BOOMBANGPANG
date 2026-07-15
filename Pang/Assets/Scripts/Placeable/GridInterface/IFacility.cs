public enum FacilityInvalidationReason
{
	UserRemoval,
	Destroyed,
}

public enum FacilityRemovalFailureReason
{
	None,
	NotRegistered,
	NotPlaced,
	AlreadyInvalidating,
	ContainsItems,
	ContainsBox,
	ContainsCapsule,
	HasActiveTask,
	HasReservation,
	WorkerIsUsing,
	InTransit,
	GridRemovalFailed,
}

public readonly struct FacilityRemovalFailure
{
	public readonly FacilityRemovalFailureReason Reason;
	public readonly string Message;

	public bool HasFailure => Reason != FacilityRemovalFailureReason.None;

	public FacilityRemovalFailure(FacilityRemovalFailureReason reason, string message)
	{
		Reason = reason;
		Message = message;
	}

	public static FacilityRemovalFailure None => default;
}

public readonly struct FacilityInvalidationContext
{
	public readonly FacilityInvalidationReason Reason;
	public readonly DestroyContext DestroyContext;

	public bool IsDestroyed => Reason == FacilityInvalidationReason.Destroyed;

	private FacilityInvalidationContext(FacilityInvalidationReason reason, DestroyContext destroyContext)
	{
		Reason = reason;
		DestroyContext = destroyContext;
	}

	public static FacilityInvalidationContext UserRemoval()
	{
		return new FacilityInvalidationContext(FacilityInvalidationReason.UserRemoval, null);
	}

	public static FacilityInvalidationContext Destroyed(DestroyContext destroyContext)
	{
		return new FacilityInvalidationContext(FacilityInvalidationReason.Destroyed, destroyContext);
	}
}

public interface IFacilityUserRemovalGuard
{
	bool CanUserRemove(out FacilityRemovalFailure failure);
}

public interface IFacility : IGridPlaceable, IHealth
{
	public uint FacilityRulePresetId { get; }
	public int PowerConsumption { get; }

	public void SetFacilityRulePresetId(uint presetId);
}
