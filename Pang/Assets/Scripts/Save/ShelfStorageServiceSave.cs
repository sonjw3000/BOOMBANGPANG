public partial class ShelfStorageService
{
	public void ResetRuntimeState()
	{
		for (int i = 0; i < containers.Count; ++i)
			UnsubscribeContainer(containers[i]);

		containers.Clear();
		shelvesByItem.Clear();
	}
}
