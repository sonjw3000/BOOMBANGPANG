namespace UniverseLogistics.UI.Toolkit
{
	public enum UITooltipTone
	{
		Default,
		Locked,
	}

	public readonly struct UITooltipContent
	{
		public string Title { get; }
		public string Description { get; }
		public string Requirement { get; }
		public UITooltipTone Tone { get; }

		public bool HasContent =>
			string.IsNullOrWhiteSpace(Title) == false ||
			string.IsNullOrWhiteSpace(Description) == false ||
			string.IsNullOrWhiteSpace(Requirement) == false;

		public bool HasRequirement => string.IsNullOrWhiteSpace(Requirement) == false;

		public UITooltipContent(
			string title,
			string description,
			string requirement = "",
			UITooltipTone tone = UITooltipTone.Default)
		{
			Title = title ?? string.Empty;
			Description = description ?? string.Empty;
			Requirement = requirement ?? string.Empty;
			Tone = tone;
		}

		public static UITooltipContent DescriptionOnly(string title, string description)
		{
			return new UITooltipContent(title, description);
		}

		public static UITooltipContent Locked(string title, string description, string requirement)
		{
			return new UITooltipContent(title, description, requirement, UITooltipTone.Locked);
		}
	}
}
