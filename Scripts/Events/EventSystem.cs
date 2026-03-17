using Godot;
using System;

public partial class EventSystem : Node
{
#region Singleton
	public static EventSystem Singleton { get; private set; }
#endregion

#region References
	[Export] public PopulationManager PopulationManagerRef;
	[Export] public InteractionManager InteractionManagerRef;
	[Export] public SeasonManager SeasonManagerRef;
#endregion

#region Signals
	[Signal] public delegate void AnimalBornEventHandler(Animal animal);
	[Signal] public delegate void AnimalDeathEventHandler(Animal animal, string causeOfDeath);
	[Signal] public delegate void PlantConsumedEventHandler(Node3D plant, Animal consumer);
	[Signal] public delegate void SeasonChangedEventHandler(SeasonType newSeason);
#endregion

#region Events
	public static event Action<Animal> OnAnimalBorn;
	public static event Action<Animal, string> OnAnimalDeath;
	public static event Action<Node3D, Animal> OnPlantConsumed;
	public static event Action<SeasonType> OnSeasonChanged;
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
			QueueFree();
		}
	}

	public override void _Ready()
	{
		if (PopulationManagerRef == null)
		{
			PopulationManagerRef = PopulationManager.Singleton;
		}

		if (InteractionManagerRef == null)
		{
			InteractionManagerRef = InteractionManager.Singleton;
		}

		if (PopulationManagerRef != null)
		{
			PopulationManagerRef.Connect(
				PopulationManager.SignalName.AnimalSpawned,
				Callable.From<Animal>(HandleAnimalBorn)
			);
			PopulationManagerRef.Connect(
				PopulationManager.SignalName.AnimalDied,
				Callable.From<Animal, string>(HandleAnimalDeath)
			);
		}
		else
		{
			GD.PrintErr("EventSystem: PopulationManager reference is missing.");
		}

		if (InteractionManagerRef != null)
		{
			InteractionManagerRef.Connect(
				InteractionManager.SignalName.PlantConsumed,
				Callable.From<Node3D, Animal>(HandlePlantConsumed)
			);
		}
		else
		{
			GD.PrintErr("EventSystem: InteractionManager reference is missing.");
		}

		if (SeasonManagerRef != null)
		{
			SeasonManagerRef.Connect(
				SeasonManager.SignalName.SeasonChanged,
				Callable.From<SeasonType>(HandleSeasonChanged)
			);
		}
		else
		{
			GD.PrintErr("EventSystem: SeasonManager reference is missing.");
		}
	}

	public override void _ExitTree()
	{
		if (Singleton == this)
		{
			Singleton = null;
		}
	}
#endregion

#region Handlers
	private void HandleAnimalBorn(Animal animal)
	{
		EmitSignal(SignalName.AnimalBorn, animal);
		OnAnimalBorn?.Invoke(animal);
	}

	private void HandleAnimalDeath(Animal animal, string causeOfDeath)
	{
		EmitSignal(SignalName.AnimalDeath, animal, causeOfDeath);
		OnAnimalDeath?.Invoke(animal, causeOfDeath);
	}

	private void HandlePlantConsumed(Node3D plant, Animal consumer)
	{
		EmitSignal(SignalName.PlantConsumed, plant, consumer);
		OnPlantConsumed?.Invoke(plant, consumer);
	}

	private void HandleSeasonChanged(SeasonType newSeason)
	{
		EmitSignal(SignalName.SeasonChanged, Variant.From(newSeason));
		OnSeasonChanged?.Invoke(newSeason);
	}
#endregion
}
