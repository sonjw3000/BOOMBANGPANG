using UnityEngine;

namespace Assets.Scripts.Contract.ItemContract
{

	public enum ContractType
	{
		Standard,
		Express
	}

	[System.Serializable]
	public class ContractTypeSpec
	{
		public int DeliveryTimeLimitWeeks = 5;
		public int BaseReward = 10;
		public int DelayPenalty = 5;
		public float ReputationChange = 1.0f;
	}

	[CreateAssetMenu(menuName = "Contract/Contract Definition")]
	public class ContractDefinition : ScriptableObject
	{
		[Header("Contract Info")]
		public string ContractName;
		public uint ContractId;
		public ItemDefinition ItemToHandle;

		[Header("Delivery Info")]
		public int DeliveryIntervalWeek = 1;
		public int ItemCountsPerDelivery = 100;

		[Header("Reputation Requirement")]
		[Range(-100f, 100f)] public float MinimumRequiredReputation = 0f;
		[Range(-100f, 100f)] public float MaximumRequiredReputation = 100f;

		[Header("Contract Duration")]
		public int ContractDuration = 12;

		[Header("Standard Specification")]
		public ContractTypeSpec StandardSpec = new ContractTypeSpec();

		[Header("Express Specification")]
		public ContractTypeSpec ExpressSpec = new ContractTypeSpec();

		[Header("Releated Items")]
		[Tooltip("아직 미구현, 자리만 만들어둔 것")]
		public ItemDefinition[] RelatedItems;
	}
}
