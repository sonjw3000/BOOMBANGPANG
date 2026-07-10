public interface IPowerConsumer : IFacility
{
	bool IsPowerActive { get; }
	int PowerConsumption { get; }
}
