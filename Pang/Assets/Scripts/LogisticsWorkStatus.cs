public enum LogisticsWorkState
{
	Idle = 0,
	Need,
	Waiting,
	Active,
}

public enum LogisticsBlockReason
{
	None = 0,
	SourceFull,
	DestinationFull,
	NoEligibleWorker,
	NoRoute,
}

public readonly struct LogisticsWorkStatus
{
	public LogisticsWorkState State { get; }
	public LogisticsBlockReason BlockReason { get; }

	public LogisticsWorkStatus(
		LogisticsWorkState state,
		LogisticsBlockReason blockReason = LogisticsBlockReason.None)
	{
		State = state;
		BlockReason = blockReason;
	}
}

public interface ILogisticsWorkStatusProvider
{
	LogisticsWorkStatus LogisticsWorkStatus { get; }
}
