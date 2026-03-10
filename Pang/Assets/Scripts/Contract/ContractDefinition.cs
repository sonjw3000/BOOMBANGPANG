using UnityEngine;

namespace Assets.Scripts.Contract
{

	[CreateAssetMenu(menuName = "Contract/Contract Definition")]
	public class ContractDefinition : ScriptableObject
	{
		[Header("Contract Info")]
		public string ContractName;
		public uint ContractId;
		public ItemDefinition ItemToHandle;

		[Header("Delivery Info")]
		[Tooltip("Interval between deliveries, in months")]
		public int DeliveryInterval = 1;
		public int AmountPerDelivery = 100;

		[Header("Reputation Requirement")]
		[Range(-100f, 100f)] public float MinimumRequiredReputation = 0f;
		[Range(-100f, 100f)] public float MaximumRequiredReputation = 100f;

		[Header("Contract Duration and Income")]
		public int DurationInMonths = 12;
		public int IncomePerItem = 10;

		[Header("Monthly Reward Money")]
		public int RewardOnTime = 100;
		public int RewardLate = 50;
		public int RewardFailed = 200;

		[Header("Monthly Reward Reputation")]
		public float RepOnTime = 0.5f;
		public float RepLate= 0.25f;
		public float RepFailed = 0.5f;

		[Header("Releated Items")]
		[Tooltip("아직 미구현, 자리만 만들어둔 것")]
		public ItemDefinition[] RelatedItems;
	}
}
