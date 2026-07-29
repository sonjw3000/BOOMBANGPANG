public class RobotWorkerUIProvider : WorkerUIProviderBase<RobotWorker>
{
	protected override string ResourceLabel => "Battery";
	protected override float ResourceValue => currentTarget != null ? currentTarget.BatteryLevel : 0.0f;
	protected override IWearable Wearable => currentTarget;
}
