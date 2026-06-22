namespace Assets.Scripts.Contract
{
	public partial class ContractRuntime
	{
		public ContractRuntimeSaveData CaptureState()
		{
			return new ContractRuntimeSaveData
			{
				ContractId = Definition.ContractId,
				Type = Type,
				RemainingDuration = remainDuration,
				DeliveryDelta = deliveryDelta,
				AutoRenewal = AutoRenewal,
			};
		}

		public void RestoreState(int remainingDuration, int deliveryDelta, bool autoRenewal)
		{
			remainDuration = remainingDuration;
			this.deliveryDelta = deliveryDelta;
			AutoRenewal = autoRenewal;
		}
	}
}
