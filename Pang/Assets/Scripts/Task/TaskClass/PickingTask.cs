using NUnit.Framework.Interfaces;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public sealed class PickingTask : WorkerTask
{
	public struct PickingLine
	{
		public int3 GoalPosition;
		public int ItemID;
		public int RequestedMount;
		public int ContainerID;
	}

	public class PickJob
	{
		public int JobID;
		public List<PickingLine> Lines;
	}

	private PickJob pickingData;
	private IBaseNode baseNode;

	public PickingTask(PickJob pickJob) : base(TaskType.Picking) => pickingData = pickJob;

	protected override void BuildTaskNode()
	{
		// todo
		// local bt에 토트의 사이즈를 확인하는 단계도 넣어야함
		// 토트 용량이 넘치면 시마이치고 토트를 보내야함
		// 해당 과정을 거친 후 본인의 작업을 하게 만들어야함
		// 일단은 대충 싸갈기자
	}

	public override void UpdateTaskNode(in BTContext ctx)
	{
		// 본인의 static bt를 돌려야 한다
		baseNode.Evaluate(ctx);
	}
}
