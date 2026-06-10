using UnityEngine;
using UnityEngine.Serialization;


// 인게임 매니저들의 허브
// 현재는 boxbuffer만 존재

public class WMSystem : MonoBehaviour
{
	[SerializeField] private ItemLedger itemLedger;
	[FormerlySerializedAs("boxPoolRegistry")]
	[SerializeField] private BoxPoolManager boxPoolManager;

	[Header("Policy")]
	[SerializeField] private WorkPolicyService workPolicyService;

	public BoxPoolManager BoxPoolManager => boxPoolManager;
	public ItemLedger ItemLedger => itemLedger;

	public WorkPolicyService WorkPolicyService => workPolicyService;
}
