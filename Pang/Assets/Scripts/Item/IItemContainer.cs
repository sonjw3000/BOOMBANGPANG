using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

// 아이템 보관함
// 선반, 상자, 기타등등이 이를 사용
public interface IItemContainer
{
	public IReadOnlyList<ItemStack> Stacks { get; }

	public IReadOnlyDictionary<uint, int> ItemTotals { get; }

	public float TotalSize { get; }
	public float MaxSize { get; }
	public float FilledPercent => MaxSize <= 0 ? 0 : (TotalSize / MaxSize) * 100.0f;
	public ItemTag ItemTags { get; }

	public bool CanRegister();

	public int GetQuantity(uint itemId);

	public int GetAcceptableQuantity(uint itemId, int requested);

	public bool CanAcceptStack(ItemStack stack);

	public int AddItem(uint itemId, int quantity);

	public int RemoveItem(uint itemId, int quantity);

	public bool AddStack(ItemStack stack);

	public bool RemoveStack(ItemStack stack);

	public bool TryRemoveFromStack(ItemStack stack, int quantity, out ItemStack removedStack);
}

public interface IThermalItemContainer : IItemContainer
{
	public float CurrentTemperatureCelsius { get; }
	public ThermalResponse ThermalResponse { get; }

	public bool TryGetThermalEnvironmentPosition(out int3 position);

	public void SetCurrentTemperatureCelsius(float temperatureCelsius);
}

public static class ThermalUtility
{
	public const float AbsoluteZeroCelsius = -273.15f;
	public const float SlowResponsePerWeek = 4.0f;
	public const float NormalResponsePerWeek = 16.0f;
	public const float FastResponsePerWeek = 64.0f;

	public static float GetResponsePerWeek(ThermalResponse response)
	{
		return response switch
		{
			ThermalResponse.Slow => SlowResponsePerWeek,
			ThermalResponse.Fast => FastResponsePerWeek,
			_ => NormalResponsePerWeek,
		};
	}

	public static float SanitizeCelsius(float temperatureCelsius)
	{
		if (float.IsNaN(temperatureCelsius) || float.IsInfinity(temperatureCelsius))
			return GridCell.DefaultTemperatureCelsius;

		return Mathf.Max(AbsoluteZeroCelsius, temperatureCelsius);
	}

	public static float ApproachTemperature(
		float currentTemperatureCelsius,
		float targetTemperatureCelsius,
		float responsePerWeek,
		float elapsedWeeks)
	{
		float current = SanitizeCelsius(currentTemperatureCelsius);
		float target = SanitizeCelsius(targetTemperatureCelsius);
		if (responsePerWeek <= 0.0f || elapsedWeeks <= 0.0f)
			return current;

		float blend = 1.0f - Mathf.Exp(-responsePerWeek * elapsedWeeks);
		return SanitizeCelsius(Mathf.Lerp(current, target, Mathf.Clamp01(blend)));
	}
}

public interface IItemPickReservable
{
	public IReadOnlyDictionary<uint, int> ItemToBePicked { get; }

	public event System.Action<IItemContainer, uint, int> OnItemReservedPickChanged;

	public int GetPickableQuantity(uint itemId);

	public int ReservePicking(uint itemId, int quantity);

	public int ReleaseReservedPick(uint itemId, int quantity);

	public int ConsumeReservedPick(uint itemId, int quantity);
}

// 실제 item 저장
[System.Serializable]
public class ItemStack
{
	private const float DefaultConditionMaximum = 1000.0f;
	private static readonly Stack<ItemStack> pool = new();

	private uint itemID;
	private int quantity = 0;
	private float currentFreshness = DefaultConditionMaximum;
	private float currentIntegrity = DefaultConditionMaximum;
	private float currentTemperatureCelsius = GridCell.DefaultTemperatureCelsius;
	private ItemStatus status = ItemStatus.None;
	private ItemQuality quality = ItemQuality.None;
	private PackageOutboundStage outboundStage = PackageOutboundStage.None;

	public uint ItemID => itemID;
	public int Quantity => quantity;
	public float CurrentFreshness => currentFreshness;
	public float CurrentIntegrity => currentIntegrity;
	public float MaximumFreshness => MaxFreshness;
	public float MaximumIntegrity => MaxIntegrity;
	public int FreshnessPercent => CalculatePercent(currentFreshness, MaxFreshness);
	public int DamagePercent => CalculatePercent(MaxIntegrity - currentIntegrity, MaxIntegrity);
	public float DamageRatio => Mathf.Clamp01((MaxIntegrity - currentIntegrity) / MaxIntegrity);
	public bool IsDestroyed => currentIntegrity <= 0.0f;
	public float CurrentTemperatureCelsius => currentTemperatureCelsius;
	public ItemStatus Status => status;
	public ItemQuality Quality => quality;
	public PackageOutboundStage OutboundStage => outboundStage;
	public float Size => GameContext.Instance.ItemDB.GetItemSize(ItemID) * Quantity;
	public bool IsDefaultIdentity =>
		FreshnessPercent == 100 &&
		DamagePercent == 0 &&
		status == ItemStatus.None &&
		quality == ItemQuality.None &&
		outboundStage == PackageOutboundStage.None;

	public ItemStack(
		uint itemID,
		float currentFreshness = float.PositiveInfinity,
		float currentIntegrity = float.PositiveInfinity,
		ItemStatus status = ItemStatus.None,
		PackageOutboundStage outboundStage = PackageOutboundStage.None,
		ItemQuality quality = ItemQuality.None,
		float currentTemperatureCelsius = GridCell.DefaultTemperatureCelsius)
	{
		Initialize(
			itemID,
			currentFreshness,
			currentIntegrity,
			status,
			outboundStage,
			quality,
			currentTemperatureCelsius);
	}

	public static ItemStack Rent(
		uint itemID,
		float currentFreshness = float.PositiveInfinity,
		float currentIntegrity = float.PositiveInfinity,
		ItemStatus status = ItemStatus.None,
		PackageOutboundStage outboundStage = PackageOutboundStage.None,
		ItemQuality quality = ItemQuality.None,
		float currentTemperatureCelsius = GridCell.DefaultTemperatureCelsius)
	{
		if (pool.Count > 0)
		{
			ItemStack stack = pool.Pop();
			stack.Initialize(
				itemID,
				currentFreshness,
				currentIntegrity,
				status,
				outboundStage,
				quality,
				currentTemperatureCelsius);
			return stack;
		}

		return new ItemStack(
			itemID,
			currentFreshness,
			currentIntegrity,
			status,
			outboundStage,
			quality,
			currentTemperatureCelsius);
	}

	public static ItemStack RentDefault(uint itemID)
	{
		return Rent(itemID);
	}

	public bool HasItemID(uint itemID) => this.itemID == itemID;
	public bool HasStatus(ItemStatus target) => status == target;
	public bool HasQuality(ItemQuality target) => target != ItemQuality.None && (quality & target) == target;

	public void SetCurrentFreshness(float value)
	{
		currentFreshness = ClampCondition(value, MaxFreshness);
	}

	public void SetCurrentIntegrity(float value)
	{
		currentIntegrity = ClampCondition(value, MaxIntegrity);
	}

	public float ApplyIntegrityDamage(float amount)
	{
		if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0.0f || currentIntegrity <= 0.0f)
			return 0.0f;

		float previous = currentIntegrity;
		currentIntegrity = Mathf.Max(0.0f, currentIntegrity - amount);
		return previous - currentIntegrity;
	}

	public float ApplyFreshnessLoss(float amount)
	{
		if (float.IsNaN(amount) || float.IsInfinity(amount) || amount <= 0.0f || currentFreshness <= 0.0f)
			return 0.0f;

		float previous = currentFreshness;
		currentFreshness = Mathf.Max(0.0f, currentFreshness - amount);
		return previous - currentFreshness;
	}

	public void SetCurrentTemperatureCelsius(float temperatureCelsius)
	{
		currentTemperatureCelsius = ThermalUtility.SanitizeCelsius(temperatureCelsius);
	}

	public void SetStatus(ItemStatus status)
	{
		this.status = status;
	}

	public void AddQuality(ItemQuality quality)
	{
		this.quality |= quality;
	}

	public void SetOutboundStage(PackageOutboundStage outboundStage)
	{
		this.outboundStage = outboundStage;
	}

	public bool HasMatchingIdentity(ItemStack other)
	{
		if (other == null)
			return false;

		return itemID == other.itemID &&
			FreshnessPercent == other.FreshnessPercent &&
			DamagePercent == other.DamagePercent &&
			status == other.status &&
			quality == other.quality &&
			outboundStage == other.outboundStage;
	}

	public bool CanMergeWith(ItemStack other)
	{
		return other != null && HasMatchingIdentity(other);
	}

	// returns actual added amount
	public int AddItem(int amount)
	{
		if (amount <= 0)
			return 0;

		quantity += amount;

		if (quantity < 0)
			Debug.LogError("Why it's under 0");

		return amount;
	}

	// returns actual removed amount
	// picking task가 아이템을 실제로 훑고 지나갔을 때
	public int RemoveItem(int amount)
	{
		amount = math.min(amount, quantity);

		quantity -= amount;

		if (quantity < 0)
			Debug.LogError("Why it's under 0");

		return amount;
	}

	protected virtual ItemStack CreateEmptyLikeThis()
	{
		return Rent(
			itemID,
			currentFreshness,
			currentIntegrity,
			status,
			outboundStage,
			quality,
			currentTemperatureCelsius);
	}

	public virtual ItemStack CreateTransferStack(int amount)
	{
		if (amount <= 0)
			return null;

		ItemStack stack = CreateEmptyLikeThis();
		stack.AddItem(amount);
		return stack;
	}

	public ItemStack CloneWithQuantity(int quantity)
	{
		return CreateTransferStack(quantity);
	}

	public ItemStack Split(int amount)
	{
		if (amount <= 0)
			return null;

		int removed = RemoveItem(amount);
		return removed > 0 ? CreateTransferStack(removed) : null;
	}

	public bool TryMergeFrom(ItemStack other)
	{
		if (ReferenceEquals(this, other) || CanMergeWith(other) == false)
			return false;

		int originalQuantity = Quantity;
		int incomingQuantity = other.Quantity;
		int mergedQuantity = originalQuantity + incomingQuantity;
		if (mergedQuantity <= 0)
			return false;

		float mergedFreshness = WeightedAverage(
			currentFreshness,
			originalQuantity,
			other.currentFreshness,
			incomingQuantity,
			mergedQuantity);
		float mergedIntegrity = WeightedAverage(
			currentIntegrity,
			originalQuantity,
			other.currentIntegrity,
			incomingQuantity,
			mergedQuantity);
		float mergedTemperature = WeightedAverage(
			currentTemperatureCelsius,
			originalQuantity,
			other.currentTemperatureCelsius,
			incomingQuantity,
			mergedQuantity);

		int added = AddItem(incomingQuantity);
		if (added != incomingQuantity)
			return false;

		SetCurrentFreshness(mergedFreshness);
		SetCurrentIntegrity(mergedIntegrity);
		SetCurrentTemperatureCelsius(mergedTemperature);
		other.RemoveItem(added);
		return true;
	}

	public virtual void Recycle()
	{
		ResetState();

		if (GetType() == typeof(ItemStack))
			pool.Push(this);
	}

	private void Initialize(
		uint itemID,
		float currentFreshness,
		float currentIntegrity,
		ItemStatus status,
		PackageOutboundStage outboundStage,
		ItemQuality quality,
		float currentTemperatureCelsius)
	{
		this.itemID = itemID;
		quantity = 0;
		this.currentFreshness = ClampCondition(currentFreshness, MaxFreshness);
		this.currentIntegrity = ClampCondition(currentIntegrity, MaxIntegrity);
		this.currentTemperatureCelsius = ThermalUtility.SanitizeCelsius(currentTemperatureCelsius);
		this.status = status;
		this.outboundStage = outboundStage;
		this.quality = quality;
	}

	private void ResetState()
	{
		itemID = 0;
		quantity = 0;
		currentFreshness = DefaultConditionMaximum;
		currentIntegrity = DefaultConditionMaximum;
		currentTemperatureCelsius = GridCell.DefaultTemperatureCelsius;
		status = ItemStatus.None;
		quality = ItemQuality.None;
		outboundStage = PackageOutboundStage.None;
	}

	private float MaxFreshness => ResolveItemDefinition()?.MaxFreshness ?? DefaultConditionMaximum;
	private float MaxIntegrity => ResolveItemDefinition()?.MaxIntegrity ?? DefaultConditionMaximum;

	private ItemDefinition ResolveItemDefinition()
	{
		if (GameContext.HasInstance == false ||
			GameContext.Instance.ItemDB == null ||
			GameContext.Instance.ItemDB.GetItemData(itemID, out ItemDefinition definition) == false)
		{
			return null;
		}

		return definition;
	}

	private static int CalculatePercent(float value, float maximum)
	{
		if (maximum <= 0.0f)
			return 0;

		return Mathf.Clamp(
			Mathf.FloorToInt(Mathf.Clamp01(value / maximum) * 100.0f + 0.0001f),
			0,
			100);
	}

	private static float ClampCondition(float value, float maximum)
	{
		if (float.IsNaN(value) || float.IsPositiveInfinity(value))
			return maximum;
		if (float.IsNegativeInfinity(value))
			return 0.0f;

		return Mathf.Clamp(value, 0.0f, maximum);
	}

	private static float WeightedAverage(
		float first,
		int firstQuantity,
		float second,
		int secondQuantity,
		int totalQuantity)
	{
		return (float)(((double)first * firstQuantity + (double)second * secondQuantity) / totalQuantity);
	}
}

public enum ItemStatus
{
	NotDefined = -1,
	None = 0,
	Labeled = 1,
	Packed = 2,
}

[System.Flags]
public enum ItemQuality
{
	None = 0,
	Waste = 1 << 0,
}

public enum PackageOutboundStage
{
	None,
	WaitingForShipping,
	Shipping,
	InDelivery,
	Completed,
}

// 특정 item이 위치한 정보를 간편히 표현한 자료구조
// 여기서 건들면 연동되게 해야함
// task는 해당 자료구조를 참조한다
// 사용의 편의성을 위해 item add, remove는 여기에도 두지만 하는 행동은 동일함
//public class ItemLocation
//{
//	private ShelfBase container;
//	private uint itemID;
//	//private int quantity;
//	//private int tobeQuantity;

//	public ItemLocation(ShelfBase shelf, uint itemID)
//	{
//		container = shelf;
//		this.itemID = itemID;
//	}

//	public ShelfBase Container => container;
//	private ItemStack itemStack => container.Stacks[itemID];
//	public int Quantity => itemStack.Quantity;
//	public int TobeQuantity => itemStack.TobeQuantity;
//	public uint ItemID => itemID;

//	// storing task가 아이템을 저장할 때
//	public int AddItem(int amount)
//	{
//		return itemStack.AddItem(amount);
//	}
	
//	// picking task가 아이템을 실제로 훑고 지나갔을 때
//	public int RemoveItem(int amount)
//	{
//		return itemStack.RemoveItem(amount);
//	}

//	// picking task가 아이템을 예약할 때
//	public int ReservePicking(int amount)
//	{
//		return itemStack.ReservePicking(amount);
//	}

//}
