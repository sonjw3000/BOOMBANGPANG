using UnityEngine;

public enum BuildingType
{
	Generic,
	Storage,
	Packing,
	Launch,
}

[DisallowMultipleComponent]
public sealed class Building : MonoBehaviour
{
	[SerializeField] private string displayName = string.Empty;
	[SerializeField] private BuildingType buildingType = BuildingType.Generic;
	[SerializeField] [HideInInspector] private uint runtimeBuildingId;

	private bool isRegistered;

	public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
	public BuildingType Type => buildingType;
	public uint RuntimeBuildingId => runtimeBuildingId;

	private void OnEnable()
	{
		TryRegister();
	}

	private void Start()
	{
		TryRegister();
	}

	private void OnDisable()
	{
		if (isRegistered == false || GameContext.HasInstance == false)
			return;

		GameContext.Instance.BuildingMgr.Unregister(this);
	}

	internal void AssignRuntimeBuildingId(uint id)
	{
		runtimeBuildingId = id;
	}

	internal void SetRegistered(bool registered)
	{
		isRegistered = registered;
	}

	private void TryRegister()
	{
		if (isRegistered || GameContext.HasInstance == false)
			return;

		GameContext.Instance.BuildingMgr.Register(this);
	}
}
