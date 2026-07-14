using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

// 런치 패드 베이스


public partial class LaunchStation
	: MonoBehaviour
	, IFacility
	, IGridPlacementEffect
	, IInteractionPoint
{
	[SerializeField] private uint facilityRulePresetId;
	[SerializeField, Min(0)] private int powerConsumption;
	[SerializeField] private HealthState health = new();
	[SerializeField] private List<PlatformAddon> addons = new();

	private int3 gridPosition;
	private FacingDirection facingDirection;

	private List<InteractionPoint> interactionPoints = new();
	private Dictionary<InteractionKind, List<int3>> interactionPointMap = new();

	public int3 GridPosition => gridPosition;
	public FacingDirection Direction => facingDirection;
	public uint FacilityRulePresetId => facilityRulePresetId;
	public int PowerConsumption => powerConsumption;
	public WorkerStatusTarget BuildingTarget => WorkerStatusTarget.LaunchStation;
	public IReadOnlyList<InteractionPoint> InteractionPoints => interactionPoints;
	public float Health => health.Health;
	public float MaxHealth => health.MaxHealth;

	public float ApplyDamage(float amount) => health.ApplyDamage(amount);
	public void RestoreHealth(float value) => health.RestoreHealth(value);

	public void SetFacilityRulePresetId(uint presetId)
	{
		facilityRulePresetId = presetId;
	}
	
	private void Awake()
	{
		foreach (var addon in addons)
		{
			addon.SetPadBase(this);
		}
	}

	private void Start()
	{
		InitializeForSaveLoad();
	}

	public bool TryGetAddon<T>(out T addon) where T : PlatformAddon
	{
		foreach (var a in addons)
		{
			if (a.GetComponent<T>() == null)
				continue;
			addon = a as T;
			return true;
		}

		addon = null;
		return false;
	}

	public void OnPositionSet(in int3 position, FacingDirection direction)
	{
		enabled = true;
		gridPosition = position;
		facingDirection = direction;
	}

	public void ClearInteractionPoints()
	{
		interactionPoints.Clear();
		interactionPointMap.Clear();
	}

	public void AddInteractionPoint(InteractionKind interactionKind, in int3 point)
	{
		interactionPoints.Add(new(interactionKind, point));

		foreach (InteractionKind value in Enum.GetValues(typeof(InteractionKind)))
		{
			if (value == InteractionKind.None) continue;

			if (interactionKind.HasFlag(value))
			{
				if (!interactionPointMap.ContainsKey(value))
					interactionPointMap[value] = new List<int3>();

				interactionPointMap[value].Add(point);
			}
		}
	}

	public int3 GetClosestInteractionPoint(InteractionKind interactionKind, in int3 from)
	{
		float distance = float.PositiveInfinity;
		int3 closestPoint = default;

		foreach (int3 point in interactionPointMap[interactionKind])
		{
			float d = math.distance(point, from);
			if (distance > d)
			{
				distance = d;
				closestPoint = point;
			}
		}

		if (distance == float.PositiveInfinity)
		{
			Debug.LogError($"No interaction point for {interactionKind} in LaunchStation");
		}

		return closestPoint;
	}


	public void OnRemoved()
	{
	}

	public void OnDestroyedBy(in DestroyContext ctx)
	{
	}

	public bool IsInteractionAvailable(InteractionKind interactionKind)
	{
		return true;
	}

}
