using UnityEngine;


// 인게임 매니저들의 허브
// 현재는 boxbuffer만 존재

public class WMSystem : MonoBehaviour
{
	[SerializeField] private ItemLedger itemLedger;
	[SerializeField] private BoxPoolService boxPoolRegistry;

	public BoxPoolService BoxPoolMgr => boxPoolRegistry;
	public ItemLedger ItemLedger => itemLedger;

}