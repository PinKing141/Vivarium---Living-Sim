using Godot;
using System.Collections.Generic;

public partial class PopulationManager : Node
{
#region Singleton
    public static PopulationManager Singleton { get; private set; }
#endregion

#region State
    private readonly List<Animal> activeAnimals = new List<Animal>();
    private readonly HashSet<Animal> activeAnimalSet = new HashSet<Animal>();
#endregion

#region Accessors
    // Expose the list safely so the Tick Manager can iterate over it
    public IReadOnlyList<Animal> ActiveAnimals => activeAnimals;
    public bool IsAnimalActive(Animal animal) => activeAnimalSet.Contains(animal);
#endregion

#region Signals
    // Signals to broadcast events to UI or other systems
    [Signal] public delegate void AnimalSpawnedEventHandler(Animal animal);
    [Signal] public delegate void AnimalDiedEventHandler(Animal animal, string causeOfDeath);
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

    public override void _ExitTree()
    {
        if (Singleton == this)
        {
            Singleton = null;
        }
        activeAnimals.Clear();
        activeAnimalSet.Clear();
    }
#endregion

#region Registration
    public void RegisterAnimal(Animal animal)
    {
        if (animal == null)
        {
            return;
        }

        if (activeAnimalSet.Add(animal))
        {
            activeAnimals.Add(animal);
            EmitSignal(SignalName.AnimalSpawned, animal);
        }
    }

    public void UnregisterAnimal(Animal animal, string causeOfDeath = "unknown")
    {
        RemoveAnimal(animal, true, causeOfDeath);
    }

    public void DespawnAnimal(Animal animal)
    {
        RemoveAnimal(animal, false, "despawned");
    }

    private void RemoveAnimal(Animal animal, bool emitSignal, string causeOfDeath)
    {
        if (animal == null)
        {
            return;
        }

        if (activeAnimalSet.Remove(animal))
        {
            activeAnimals.Remove(animal);
            if (emitSignal)
            {
                EmitSignal(SignalName.AnimalDied, animal, causeOfDeath);
            }
        }
    }
#endregion
}
