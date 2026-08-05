using System.Collections.Generic;
using System.Text;

public class RobotWorkerUIProvider : WorkerUIProviderBase<RobotWorker>
{
	protected override string ResourceLabel => "Battery";
	protected override float ResourceValue => currentTarget != null ? currentTarget.BatteryLevel : 0.0f;
	protected override IWearable Wearable => currentTarget;
	protected override string ExtraProfileLabel => "Navigation";
	protected override string ExtraProfileDisplay => BuildNavigationDisplay();

	private string BuildNavigationDisplay()
	{
		if (currentTarget == null)
			return "Unavailable";

		StringBuilder builder = new();
		builder.Append(currentTarget.NavigationDependency);
		builder.Append(" · Compute ");
		builder.Append(currentTarget.RequiredNavigationCompute);
		builder.Append(" · Region ");
		if (currentTarget.NavigationRegionId > 0)
			builder.Append(currentTarget.NavigationRegionId);
		else
			builder.Append("None");

		if (GameContext.HasInstance &&
			GameContext.Instance.RobotNavigationSvc.TryGetRobotComputeShares(currentTarget, out IReadOnlyDictionary<uint, int> shares) &&
			shares != null && shares.Count > 0)
		{
			List<uint> hubIds = new(shares.Keys);
			hubIds.Sort();
			builder.Append(" · ");
			for (int i = 0; i < hubIds.Count; ++i)
			{
				if (i > 0)
					builder.Append(", ");
				builder.Append("H");
				builder.Append(hubIds[i]);
				builder.Append(":");
				builder.Append(shares[hubIds[i]]);
			}
		}

		if (currentTarget.IsWaitingForNavigation)
		{
			builder.Append(" · Waiting: ");
			builder.Append(currentTarget.NavigationWaitReason);
		}
		if (currentTarget.IsManualNavigation)
			builder.Append(" · MANUAL NAVIGATION");
		return builder.ToString();
	}
}
