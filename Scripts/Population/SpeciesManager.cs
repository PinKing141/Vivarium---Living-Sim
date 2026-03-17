using Godot;
using System.Collections.Generic;

public partial class SpeciesManager : Node
{
#region Singleton
	public static SpeciesManager Singleton { get; private set; }
#endregion

#region Config
	[Export] public PopulationManager PopulationManagerRef;
	[Export] public float RateWindowSeconds = 60f;
#endregion

#region Signals
	[Signal] public delegate void SpeciesExtinctEventHandler(string speciesId);
	[Signal] public delegate void SpeciesStatsUpdatedEventHandler(string speciesId);
#endregion

#region State
	private readonly Dictionary<string, SpeciesStats> statsBySpecies = new Dictionary<string, SpeciesStats>();
	private readonly HashSet<Animal> trackedAnimals = new HashSet<Animal>();
#endregion

#region Lifecycle
	public override void _EnterTree()
	{
		if (Singleton == null || !GodotObject.IsInstanceValid(Singleton))
		{
			Singleton = this;
		}
		else if (Singleton != this)
		{
			QueueFree(); // Prevent duplicate managers
		}
	}

	public override void _Ready()
	{
		if (PopulationManagerRef == null)
		{
			PopulationManagerRef = PopulationManager.Singleton;
		}

		if (PopulationManagerRef == null)
		{
			GD.PrintErr("SpeciesManager: PopulationManager reference is missing.");
			return;
		}

		PopulationManagerRef.Connect(
			PopulationManager.SignalName.AnimalSpawned,
			Callable.From<Animal>(OnAnimalSpawned)
		);
		PopulationManagerRef.Connect(
			PopulationManager.SignalName.AnimalDied,
			Callable.From<Animal, string>(OnAnimalDied)
		);

		SeedFromExistingPopulation();
	}

	public override void _ExitTree()
	{
		if (Singleton == this)
		{
			Singleton = null;
		}

		statsBySpecies.Clear();
		trackedAnimals.Clear();
	}
#endregion

#region Queries
	public IReadOnlyDictionary<string, SpeciesStats> GetAllStats()
	{
		return statsBySpecies;
	}

	public SpeciesStats GetStats(string speciesId)
	{
		if (statsBySpecies.TryGetValue(speciesId, out SpeciesStats stats))
		{
			return stats;
		}

		return null;
	}

	public float GetBirthRatePerMinute(string speciesId)
	{
		if (!statsBySpecies.TryGetValue(speciesId, out SpeciesStats stats))
		{
			return 0f;
		}

		if (RateWindowSeconds <= 0f)
		{
			return 0f;
		}

		PruneOld(stats.BirthTimes);
		return (float)(stats.BirthTimes.Count / RateWindowSeconds * 60.0);
	}

	public float GetDeathRatePerMinute(string speciesId)
	{
		if (!statsBySpecies.TryGetValue(speciesId, out SpeciesStats stats))
		{
			return 0f;
		}

		if (RateWindowSeconds <= 0f)
		{
			return 0f;
		}

		PruneOld(stats.DeathTimes);
		return (float)(stats.DeathTimes.Count / RateWindowSeconds * 60.0);
	}
#endregion

#region Handlers
	private void SeedFromExistingPopulation()
	{
		foreach (Animal animal in PopulationManagerRef.ActiveAnimals)
		{
			OnAnimalSpawned(animal);
		}
	}

	private void OnAnimalSpawned(Animal animal)
	{
		if (animal == null)
		{
			return;
		}

		if (!trackedAnimals.Add(animal))
		{
			return;
		}

		string speciesId = GetSpeciesId(animal);
		SpeciesStats stats = GetOrCreateStats(speciesId);

		stats.CurrentCount++;
		stats.TotalBorn++;
		stats.IsExtinct = false;
		stats.LastSeenSeconds = GetNowSeconds();
		EnqueueEvent(stats.BirthTimes);

		EmitSignal(SignalName.SpeciesStatsUpdated, speciesId);
	}

	private void OnAnimalDied(Animal animal, string causeOfDeath)
	{
		if (animal == null)
		{
			return;
		}

		trackedAnimals.Remove(animal);

		string speciesId = GetSpeciesId(animal);
		SpeciesStats stats = GetOrCreateStats(speciesId);

		if (stats.CurrentCount > 0)
		{
			stats.CurrentCount--;
		}

		stats.TotalDied++;
		stats.LastSeenSeconds = GetNowSeconds();
		EnqueueEvent(stats.DeathTimes);

		if (stats.CurrentCount <= 0 && !stats.IsExtinct)
		{
			stats.IsExtinct = true;
			EmitSignal(SignalName.SpeciesExtinct, speciesId);
			GD.Print($"Species extinct: {speciesId}");
		}

		EmitSignal(SignalName.SpeciesStatsUpdated, speciesId);
	}
#endregion

#region Helpers
	private void EnqueueEvent(Queue<double> queue)
	{
		queue.Enqueue(GetNowSeconds());
		PruneOld(queue);
	}

	private void PruneOld(Queue<double> queue)
	{
		if (RateWindowSeconds <= 0f)
		{
			queue.Clear();
			return;
		}

		double cutoff = GetNowSeconds() - RateWindowSeconds;
		while (queue.Count > 0 && queue.Peek() < cutoff)
		{
			queue.Dequeue();
		}
	}

	private SpeciesStats GetOrCreateStats(string speciesId)
	{
		if (!statsBySpecies.TryGetValue(speciesId, out SpeciesStats stats))
		{
			stats = new SpeciesStats(speciesId);
			statsBySpecies.Add(speciesId, stats);
		}

		return stats;
	}

	private string GetSpeciesId(Animal animal)
	{
		if (!string.IsNullOrEmpty(animal.SpeciesScenePath))
		{
			return animal.SpeciesScenePath;
		}

		if (!string.IsNullOrEmpty(animal.SceneFilePath))
		{
			return animal.SceneFilePath;
		}

		if (!string.IsNullOrEmpty(animal.Name))
		{
			return animal.Name;
		}

		return animal.GetType().Name;
	}

	private double GetNowSeconds()
	{
		return Time.GetTicksMsec() / 1000.0;
	}
#endregion

#region Data
	public class SpeciesStats
	{
		public string SpeciesId { get; }
		public int CurrentCount { get; set; }
		public int TotalBorn { get; set; }
		public int TotalDied { get; set; }
		public bool IsExtinct { get; set; }
		public double LastSeenSeconds { get; set; }

		public Queue<double> BirthTimes { get; } = new Queue<double>();
		public Queue<double> DeathTimes { get; } = new Queue<double>();

		public SpeciesStats(string speciesId)
		{
			SpeciesId = speciesId;
		}
	}
#endregion
}
