using UnityEngine;

public enum ContractType
{
	Standard,
	Express,
}

[System.Serializable]
public struct ItemStackRequest
{
	public string ItemId;
	public int Quantity;
}

[CreateAssetMenu(menuName = "Contract/ContractDefinition")]
public class ContractDefinition : ScriptableObject
{
	public ContractType Type;
	[Range(-100f, 100f)] public float MinimumRequiredReputation = 0f;
	[Range(-100f, 100f)] public float MaximumRequiredReputation = 100f;

	[Header("Money")]
	public int RewardOnTime = 100;
	public int PenaltyLate = 80;
	public int PenaltyFailed = 200;

	[Header("Reputation")]
	public float RepOnTime = 0.5f;
	public float RepLate = -0.1f;
	public float RepFailed = -0.5f;

	[Header("Items")]
	public ItemStackRequest[] IncludedItems;
}

