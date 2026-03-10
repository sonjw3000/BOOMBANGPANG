using System.Collections.Generic;

namespace Assets.Scripts.Contract
{
	public enum Status
	{
		Success,
		Failed,
		Delayed,
	}

	public class ContractRuntime
	{
		public ContractDefinition Definition { get; private set; }
		public int RemainingDuration { get; set; }
		public bool AutoRenewal { get; set; } = true;

		private readonly Dictionary<Status, int> resultHistory = new();

		public ContractRuntime(ContractDefinition definition, int duration)
		{
			Definition = definition;
			RemainingDuration = duration;

			foreach (Status status in System.Enum.GetValues(typeof(Status)))
			{
				resultHistory[status] = 0;
			}
		}
	}
}
