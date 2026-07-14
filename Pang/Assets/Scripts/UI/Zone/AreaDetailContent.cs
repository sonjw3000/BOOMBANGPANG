using Assets.Scripts.UI;
using UnityEngine;
using UnityEngine.Serialization;

public class AreaDetailContent : DetailContent<AreaSelectionProxy>
{
	protected override bool UseDefaultTabs => false;

	private enum AreaDetailTab
	{
		Info,
		Action,
	}

	[SerializeField] private RectTransform infoTabRoot;
	[SerializeField] private RectTransform actionTabRoot;
	[SerializeField] private AreaDetailLayoutView layoutView;
	[FormerlySerializedAs("deleteZoneButton")]
	[SerializeField] private TextButtonView deleteAreaButton;

	private UIWindow window;

	protected override void LinkData()
	{
		window ??= GetComponentInParent<UIWindow>(true);
		deleteAreaButton?.Configure("Delete Area", () => provider?.DeleteObject());
		SetupTabs();
		SetTab((int)AreaDetailTab.Info);
		UpdateData();
	}

	protected override void UpdateData()
	{
		Area area = (provider as AreaUIProvider)?.Target?.Area;
		if (area == null || layoutView == null)
			return;

		if (layoutView.NameText != null)
			layoutView.NameText.text = area.DisplayName;
		if (layoutView.TypeText != null)
			layoutView.TypeText.text = area.Type.ToString();
		if (layoutView.BoundsText != null)
			layoutView.BoundsText.text = $"Bounds: {area.Bounds.width}x{area.Bounds.height} @ {area.Bounds.xMin}, {area.Bounds.yMin}  Floor: {area.Floor}";
		layoutView.HideLegacyFacilitySection();
	}

	private void SetupTabs()
	{
		if (window == null)
			return;

		window.ClearTabs();
		window.AddTab("Info", SetTab);
		window.AddTab("Action", SetTab);
		window.UpdateTabVisuals(0);
	}

	private void SetTab(int tabIndex)
	{
		infoTabRoot?.gameObject.SetActive(tabIndex == (int)AreaDetailTab.Info);
		actionTabRoot?.gameObject.SetActive(tabIndex == (int)AreaDetailTab.Action);
		window?.UpdateTabVisuals(tabIndex);
	}
}
