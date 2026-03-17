using Godot;
using System.Collections.Generic;

public partial class InteractionManager : Node
{
#region Singleton
    // Singleton instance for easy access
    public static InteractionManager Singleton { get; private set; }
#endregion

#region Signals
    [Signal] public delegate void PlantConsumedEventHandler(Node3D plant, Animal consumer);
#endregion
    
#region State
    // Register food sources with the manager
    private Dictionary<HexTile, List<Node3D>> registeredFood = new Dictionary<HexTile, List<Node3D>>();
    private readonly Dictionary<Node3D, Animal> reservedFood = new Dictionary<Node3D, Animal>();
    private readonly Dictionary<Animal, Node3D> reservationByAnimal = new Dictionary<Animal, Node3D>();
#endregion
    
#region Lifecycle
    public override void _EnterTree()
    {
        if (Singleton == null)
        {
            Singleton = this;
        }
        else
        {
            QueueFree(); // Prevent duplicates
        }
    }
#endregion

#region Food Registration
    public void RegisterFoodSource(HexTile tile, Node3D plant)
    {
        if (!registeredFood.ContainsKey(tile))
            registeredFood[tile] = new List<Node3D>();
            
        registeredFood[tile].Add(plant);
    }
    
    public void UnregisterFoodSource(HexTile tile, Node3D plant)
    {
        if (registeredFood.ContainsKey(tile))
            registeredFood[tile].Remove(plant);

        ReleaseReservation(plant);
    }
#endregion
    
#region Consumption
    // Animals request food consumption through the manager
    public bool ConsumeFood(Animal animal, Node3D foodSource)
    {
        if (animal == null || foodSource == null)
            return false;

        if (IsFoodReserved(foodSource) && !IsFoodReservedBy(foodSource, animal))
            return false;

        if (foodSource is BerryBush bush)
        {
            if (bush.HasFood())
            {
                bush.EatBerry();
                EmitSignal(SignalName.PlantConsumed, foodSource, animal);
                ReleaseReservation(animal);
                return true;
            }
        }
        ReleaseReservation(animal);
        return false;
    }
#endregion

#region Reservation
    public bool TryReserveFood(Animal animal, Node3D foodSource)
    {
        if (animal == null || foodSource == null)
            return false;

        if (!GodotObject.IsInstanceValid(foodSource))
            return false;

        if (foodSource is BerryBush bush && !bush.HasFood())
            return false;

        if (reservedFood.TryGetValue(foodSource, out Animal current))
        {
            return current == animal;
        }

        ReleaseReservation(animal);
        reservedFood[foodSource] = animal;
        reservationByAnimal[animal] = foodSource;
        return true;
    }

    public void ReleaseReservation(Animal animal)
    {
        if (animal == null)
            return;

        if (reservationByAnimal.TryGetValue(animal, out Node3D food))
        {
            reservationByAnimal.Remove(animal);
            if (food != null && reservedFood.TryGetValue(food, out Animal current) && current == animal)
                reservedFood.Remove(food);
        }
    }

    public void ReleaseReservation(Node3D foodSource)
    {
        if (foodSource == null)
            return;

        if (reservedFood.TryGetValue(foodSource, out Animal owner))
        {
            reservedFood.Remove(foodSource);
            if (owner != null)
                reservationByAnimal.Remove(owner);
        }
    }
#endregion

#region Queries
    public bool IsFoodReserved(Node3D foodSource)
    {
        return foodSource != null && reservedFood.ContainsKey(foodSource);
    }

    public bool IsFoodReservedBy(Node3D foodSource, Animal animal)
    {
        return foodSource != null &&
               animal != null &&
               reservedFood.TryGetValue(foodSource, out Animal current) &&
               current == animal;
    }
#endregion
}
