using UnityEngine;
using UnityEngine.Serialization;


// 인게임 매니저들의 허브
// 현재는 boxbuffer만 존재

public class WMSystem : MonoBehaviour
{
	[SerializeField] private ItemLedger itemLedger;
	[FormerlySerializedAs("boxPoolManager")]
	[FormerlySerializedAs("boxPoolRegistry")]
	[SerializeField] private BoxPoolService boxPoolService;

	[Header("Policy")]
	[SerializeField] private WorkPolicyService workPolicyService;

	public BoxPoolService BoxPoolService => boxPoolService;
	public ItemLedger ItemLedger => itemLedger;

	public WorkPolicyService WorkPolicyService => workPolicyService;
}
