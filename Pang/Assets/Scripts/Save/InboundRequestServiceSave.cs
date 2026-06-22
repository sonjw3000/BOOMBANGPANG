public partial class InboundRequestService
{
	public void ResetRuntimeState()
	{
		inboundRequests.Clear();
		itemPerReqLine.Clear();
	}
}
