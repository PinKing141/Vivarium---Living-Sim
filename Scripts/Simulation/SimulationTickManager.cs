using Godot;
using System.Collections.Generic;

public partial class SimulationTickManager : Node
{
#region Config
	[Export] public float TickRate = 0.016f; // ~60 simulation ticks per second
	[Export] public float OffscreenTickRate = 0.25f;
	[Export] public float OffscreenScreenMargin = 64f;
#endregion

#region State
	private float tickTimer = 0f;
	private bool warnedInvalidTickRate = false;
	private readonly Dictionary<Animal, float> offscreenAccumulated = new Dictionary<Animal, float>();
#endregion

#region Environment
	// Environment objects that need simulation updates (plants, bushes, etc.)
	public List<BerryBush> allEnvironment = new List<BerryBush>();
#endregion


#region Lifecycle
	public override void _Process(double delta)
	{
		if (TickRate <= 0f)
		{
			if (!warnedInvalidTickRate)
			{
				GD.PrintErr("SimulationTickManager: TickRate must be > 0.");
				warnedInvalidTickRate = true;
			}
			tickTimer = 0f;
			return;
		}

		warnedInvalidTickRate = false;
		tickTimer += (float)delta;

		// FIX 1: Prevent the "Death Spiral"
		// Cap the accumulated time to ensure we never run more than 10 ticks in a single frame.
		// This stops the game from freezing after heavy loads like initialisation.
		float maxAccumulation = TickRate * 10f;
		if (tickTimer > maxAccumulation)
		{
			tickTimer = maxAccumulation;
		}

		// Run simulation ticks when enough time accumulates
		while (tickTimer >= TickRate)
		{
			ExecuteSimulationTick(TickRate);
			tickTimer -= TickRate;
		}
	}
#endregion


#region Tick
	private void ExecuteSimulationTick(double tickDelta)
	{
		// -------- Phase 1: Environment Updates --------
		for (int i = allEnvironment.Count - 1; i >= 0; i--)
		{
			var env = allEnvironment[i];

			if (GodotObject.IsInstanceValid(env))
			{
				env.SimulationTick(tickDelta);
			}
			else
			{
				allEnvironment.RemoveAt(i);
			}
		}


		// -------- Phase 2: Animal Updates --------
		if (PopulationManager.Singleton != null)
		{
			var population = PopulationManager.Singleton;
			var camera = GetViewport().GetCamera3D();
			bool useLod = camera != null && OffscreenTickRate > TickRate;
			// FIX 2: Iterate over a copy of the list
			// This ensures that if animals die and are removed from PopulationManager 
			// during this tick, it won't shift the indices and cause an out-of-bounds crash.
			var animalsCopy = new List<Animal>(population.ActiveAnimals);

			for (int i = animalsCopy.Count - 1; i >= 0; i--)
			{
				var animal = animalsCopy[i];

				// Ensure the animal hasn't been destroyed or eaten by another animal earlier in this same tick
				if (GodotObject.IsInstanceValid(animal) && population.IsAnimalActive(animal))
				{
					if (!useLod || IsOnScreen(animal, camera))
					{
						offscreenAccumulated.Remove(animal);
						animal.SimulationTick(tickDelta);
					}
					else
					{
						float accumulated = 0f;
						if (offscreenAccumulated.TryGetValue(animal, out float value))
						{
							accumulated = value;
						}

						accumulated += (float)tickDelta;
						if (accumulated >= OffscreenTickRate)
						{
							offscreenAccumulated[animal] = 0f;
							animal.SimulationTick(accumulated);
						}
						else
						{
							offscreenAccumulated[animal] = accumulated;
						}
					}
				}
				else
				{
					offscreenAccumulated.Remove(animal);
				}
			}

			if (offscreenAccumulated.Count > 0)
			{
				var keys = new List<Animal>(offscreenAccumulated.Keys);
				for (int i = 0; i < keys.Count; i++)
				{
					var animal = keys[i];
					if (!GodotObject.IsInstanceValid(animal) || !population.IsAnimalActive(animal))
					{
						offscreenAccumulated.Remove(animal);
					}
				}
			}
		}


		// -------- Phase 3: Interactions (future) --------
		// Example: predator attacks, mating detection


		// -------- Phase 4: Cleanup / ecosystem balancing (future) --------
	}
#endregion

#region LOD
	private bool IsOnScreen(Animal animal, Camera3D camera)
	{
		if (animal == null || camera == null)
			return true;

		Vector3 worldPos = animal.GlobalPosition;
		if (camera.IsPositionBehind(worldPos))
			return false;

		Rect2 viewRect = camera.GetViewport().GetVisibleRect();
		if (OffscreenScreenMargin > 0f)
		{
			viewRect = viewRect.Grow(OffscreenScreenMargin);
		}

		Vector2 screenPos = camera.UnprojectPosition(worldPos);
		return viewRect.HasPoint(screenPos);
	}
#endregion
}
