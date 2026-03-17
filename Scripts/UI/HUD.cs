using Godot;

public partial class HUD : CanvasLayer
{
	#region Exports
	[Export] public PopulationManager PopulationManagerRef;
	[Export] public SeasonManager SeasonManagerRef;
	[Export] public DayNightManager DayNightManagerRef;
	[Export] public Label HudLabel;
	#endregion

	#region State
	private int animalCount = 0;
	private SeasonType currentSeason = SeasonType.Spring;
	private float timeOfDay = 0f;
	private bool isNight = false;
	#endregion

	#region GodotLifecycle
	public override void _Ready()
	{
		if (PopulationManagerRef == null)
		{
			PopulationManagerRef = PopulationManager.Singleton;
		}

		if (SeasonManagerRef == null)
		{
			SeasonManagerRef = GetNodeOrNull<SeasonManager>("../SeasonManager");
		}

		if (DayNightManagerRef == null)
		{
			DayNightManagerRef = GetNodeOrNull<DayNightManager>("../DayNightManager");
		}

		if (HudLabel == null)
		{
			HudLabel = GetNodeOrNull<Label>("Control/HudLabel");
		}

		if (PopulationManagerRef != null)
		{
			PopulationManagerRef.Connect(
				PopulationManager.SignalName.AnimalSpawned,
				Callable.From<Animal>(OnAnimalSpawned)
			);
			PopulationManagerRef.Connect(
				PopulationManager.SignalName.AnimalDied,
				Callable.From<Animal, string>(OnAnimalDied)
			);
		}

		if (SeasonManagerRef != null)
		{
			SeasonManagerRef.Connect(
				SeasonManager.SignalName.SeasonChanged,
				Callable.From<SeasonType>(OnSeasonChanged)
			);
		}

		if (DayNightManagerRef != null)
		{
			DayNightManagerRef.Connect(
				DayNightManager.SignalName.TimeOfDayChanged,
				Callable.From<float>(OnTimeOfDayChanged)
			);
			DayNightManagerRef.Connect(
				DayNightManager.SignalName.DayStarted,
				Callable.From(OnDayStarted)
			);
			DayNightManagerRef.Connect(
				DayNightManager.SignalName.NightStarted,
				Callable.From(OnNightStarted)
			);
		}

		Refresh();
	}
	#endregion

	#region SignalHandlers
	private void OnAnimalSpawned(Animal animal)
	{
		Refresh();
	}

	private void OnAnimalDied(Animal animal, string causeOfDeath)
	{
		Refresh();
	}

	private void OnSeasonChanged(SeasonType newSeason)
	{
		currentSeason = newSeason;
		UpdateHudText();
	}

	private void OnTimeOfDayChanged(float newTimeOfDay)
	{
		timeOfDay = newTimeOfDay;
		UpdateHudText();
	}

	private void OnDayStarted()
	{
		isNight = false;
		UpdateHudText();
	}

	private void OnNightStarted()
	{
		isNight = true;
		UpdateHudText();
	}
	#endregion

	#region Helpers
	private void Refresh()
	{
		currentSeason = SeasonManager.CurrentSeason;
		if (DayNightManagerRef != null)
		{
			timeOfDay = DayNightManagerRef.TimeOfDay;
			isNight = DayNightManagerRef.IsNight;
		}
		animalCount = PopulationManagerRef != null ? PopulationManagerRef.ActiveAnimals.Count : 0;
		UpdateHudText();
	}

	private void UpdateHudText()
	{
		if (HudLabel == null)
			return;

		string timeString = FormatTime(timeOfDay);
		string dayState = isNight ? "Night" : "Day";
		HudLabel.Text = $"Season: {currentSeason}\nTime: {timeString} ({dayState})\nAnimals: {animalCount}";
	}

	private string FormatTime(float time01)
	{
		float wrapped = time01 % 1f;
		int totalMinutes = Mathf.RoundToInt(wrapped * 24f * 60f);
		int hours = totalMinutes / 60;
		int minutes = totalMinutes % 60;
		return $"{hours:00}:{minutes:00}";
	}
	#endregion
}
