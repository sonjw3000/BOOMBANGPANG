using System.Collections.Generic;
using Unity.Mathematics;
using Unity.VisualScripting;
using UnityEngine;

public partial class Rocket : CapsuleDock
{
	public enum RocketState
	{
		Landing,
		OnPad,
		Launching,
		Deactivated
	}

	[SerializeField] private float fallingSpeed = 5.0f;
	[SerializeField] private float launchSpeed = 10.0f;
	[SerializeField] private float launchHeight = 100.0f;
	[SerializeField] private int3 landingPoint;
	[SerializeField] private RocketLandingOutcome landingOutcome;
	private Vector3 forwardVector = new Vector3(0, 1, 0);
	private RocketState state = RocketState.Landing;

	public override WorkerStatusTarget BuildingTarget => WorkerStatusTarget.Rocket;
	public override CapsuleDockState DockState => CapsuleDockState.InboundSource;

	private RocketService RocketSvc => GameContext.Instance.RocketSvc;
	private InboundWorkflowService InboundWorkflowService => GameContext.HasInstance ? GameContext.Instance.IBWorkflowSvc : null;

	private DeliveryService DeliveryService => GameContext.Instance.DeliveryService;
	private BoxManager BoxMgr => GameContext.Instance.BoxMgr;
	public int3 LandingPos => landingPoint;
	public RocketState State => state;
	public RocketLandingOutcome LandingOutcome => landingOutcome;


	public void Update()
	{
		switch (state)
		{
			case RocketState.Landing:
				UpdateLanding();
				break;
			case RocketState.Launching:
				UpdateLaunching();
				break;
		}
	}

	private void UpdateLanding()
	{
		// land rocket
		transform.position += forwardVector * fallingSpeed * Time.deltaTime;

		if (transform.position.y <= landingPoint.y)
		{
			transform.position = new Vector3(
				LandingPos.x,
				LandingPos.y,
				LandingPos.z
				);
			state = RocketState.OnPad;
			RocketSvc.OnRocketLanding(this);
			//gameObject.SetActive(false);
		}
	}

	private void UpdateLaunching()
	{
		transform.position += Vector3.up * launchSpeed * Time.deltaTime;

		if (transform.position.y >= launchHeight)
		{
			state = RocketState.Deactivated;
			RocketSvc.DisableRocket(this);
		}
	}

	public void Launch()
	{
		state = RocketState.Launching;
		this.enabled = true;
	}

	public void InitializePosition(in int3 landingPoint, in Vector3 forwardVector, float fallingSpeed)
	{
		this.landingPoint = landingPoint;
		this.forwardVector = forwardVector.normalized;
		this.fallingSpeed = fallingSpeed;
		this.state = RocketState.Landing;
		landingOutcome = default;
	}

	public void SetLandingOutcome(in RocketLandingOutcome outcome)
	{
		landingOutcome = outcome;
	}

	public void ApplyLandingOutcome()
	{
		if (landingOutcome.HasOverride)
			OnLandingOverrideApplied();

		if (landingOutcome.Severity == RocketLandingSeverity.Hard)
			OnHardLandingApplied();
		else
			OnSoftLandingApplied();
	}

	// Hook for future cargo-quality changes on normal landings.
	private void OnSoftLandingApplied() { }

	// Hook for future cargo-quality changes on hard landings.
	private void OnHardLandingApplied()
	{
		if (DockedCapsule == null || InboundWorkflowService == null)
			return;

		DockedCapsule.ApplyDamage(
			InboundWorkflowService.DamageRate,
			InboundWorkflowService.DamagePercent);
	}

	// Hook for future extra effects when the landing rocket overrides other objects.
	private void OnLandingOverrideApplied() { }

	public void SetupPayloadByDelivery()
	{
		if (TryUndockCapsule(out CargoCapsule existingCapsule))
			BoxMgr.DisableBox(existingCapsule);

		CargoCapsule capsule = null;
		if (BoxMgr.GetNewBox(BoxType.Capsule, out BoxBase newBox))
			capsule = newBox as CargoCapsule;
		capsule?.SetLogisticsState(CapsuleLogisticsState.IBStandby);
		if (capsule == null || TryDockCapsule(capsule) == false)
		{
			if (capsule != null)
				BoxMgr.DisableBox(capsule);

			Debug.LogError("[Rocket] Failed to prepare inbound cargo capsule.");
			return;
		}

		int guard = 0;
		const int maxIterations = 1024;
		while (true)
		{
			if (++guard > maxIterations)
			{
				Debug.LogError($"[Rocket] Aborted SetupPayloadByDelivery after {maxIterations} iterations.");
				break;
			}

			if (DeliveryService.TryPeek(out var request) == false)
				break;

			int added = capsule.AddItem(request.TargetItem.ItemID, request.Quantity);

			if (added != request.Quantity)
			{
				// can't handle the whole request
				request.ReduceAmount(added);
				break;
			}

			DeliveryService.AcceptDelivery();
		}
		
	}

	public void SetupPayload(List<ItemStack> payload)
	{
		if (TryUndockCapsule(out CargoCapsule existingCapsule))
			BoxMgr.DisableBox(existingCapsule);

		if (payload == null || payload.Count <= 0)
			return;

		CargoCapsule capsule = null;
		if (BoxMgr.GetNewBox(BoxType.Capsule, out BoxBase newBox))
			capsule = newBox as CargoCapsule;
		capsule?.SetLogisticsState(CapsuleLogisticsState.IBStandby);
		if (capsule == null || TryDockCapsule(capsule) == false)
		{
			if (capsule != null)
				BoxMgr.DisableBox(capsule);
			return;
		}

		for (int i = 0; i < payload.Count; ++i)
		{
			ItemStack stack = payload[i];
			capsule.AddStack(stack);
			if (stack != null && stack.Quantity <= 0)
				stack.Recycle();
		}
	}

	public IReadOnlyList<ItemStack> GetPayload()
	{
		return DockedCapsule != null ? DockedCapsule.Stacks : System.Array.Empty<ItemStack>();
	}

	public override void OnDestroyedBy(in DestroyContext ctx)
	{
		if (ctx.IsOverride &&
			ctx.overriddenByObject != null &&
			ctx.overriddenByObject.TryGetComponent<Rocket>(out var overridingRocket))
		{
			OnOverriddenByRocket(overridingRocket, in ctx);
			return;
		}
	}

	// Hook for future cargo-quality changes on the overridden rocket.
	private void OnOverriddenByRocket(Rocket overridingRocket, in DestroyContext ctx) { }

}
