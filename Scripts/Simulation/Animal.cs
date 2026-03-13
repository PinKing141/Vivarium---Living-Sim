using Godot;
using System;

// NEW: Diet types for the food chain
public enum DietType { Herbivore, Carnivore }

public enum AnimalState
{
	Idle,
	Wandering,
	SearchingFood,
	Hunting,   // NEW: Chasing live prey
	Fleeing,   // NEW: Running for your life
	Eating,
	Reproducing, 
	Dead 
}

public partial class Animal : Node3D
{
	// --- CORE BIOLOGY ---
	[Export] public DietType Diet = DietType.Herbivore; 

	// --- STATS ---
	[Export] public float MaxHunger = 100f;
	public float CurrentHunger;
	[Export] public float MoveSpeed = 1.5f;
	[Export] public float HungerDrainRate = 1.5f; 
	[Export] public int VisionRadius = 6; 
	[Export] public float EatingDistance = 0.8f; 

	// --- REPRODUCTION ---
	[Export(PropertyHint.File, "*.tscn")] public string SpeciesScenePath; 
	[Export] public float ReproductionThreshold = 85f; 
	[Export] public float ReproductionCost = 40f;      
	[Export] public float ReproductionCooldown = 45f;  
	private float timeSinceLastReproduction = 0f;

	// --- MODEL PLACEMENT ---
	[Export] public float YOffset = 0.2f; 
	[Export] public float ModelScale = 1.0f; 

	// --- ANIMATIONS ---
	[Export] public AnimationPlayer AnimPlayer;
	[Export] public string IdleAnim = "Idle"; 
	[Export] public string WalkAnim = "Walk"; 
	[Export] public string EatAnim = "Eat";     
	[Export] public string DeathAnim = "Death"; 

	// --- TILE NAVIGATION ---
	public AnimalState CurrentState;
	private double stateTimer = 0;
	public HexTile CurrentTile;
	private HexTile targetTile;
	
	private Node3D targetFood; // For Herbivores
	private Animal targetPrey; // For Carnivores

	public override void _Ready()
	{
		CurrentHunger = MaxHunger;
	}

	public void Init(HexTile startTile)
	{
		CurrentTile = startTile;
		GlobalPosition = startTile.WorldPosition + new Vector3(0, YOffset, 0);
		CurrentTile.Animals.Add(this); 
		
		timeSinceLastReproduction = (float)GD.RandRange(0, ReproductionCooldown / 2);
		stateTimer = GD.RandRange(1.0, 2.0);
		SetState(AnimalState.Idle);
	}

	private void SetState(AnimalState newState)
	{
		CurrentState = newState;

		if (AnimPlayer != null)
		{
			switch (CurrentState)
			{
				case AnimalState.Idle: 
				case AnimalState.Reproducing: AnimPlayer.Play(IdleAnim); break;
				case AnimalState.Wandering:
				case AnimalState.SearchingFood: 
				case AnimalState.Hunting:     // Chasing uses walking anim for now
				case AnimalState.Fleeing:     // Running uses walking anim for now
					AnimPlayer.Play(WalkAnim); break;
				case AnimalState.Eating: AnimPlayer.Play(EatAnim); break;
				case AnimalState.Dead: AnimPlayer.Play(DeathAnim); break;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (CurrentState == AnimalState.Dead) return;

		CurrentHunger -= (float)delta * HungerDrainRate;
		timeSinceLastReproduction += (float)delta; 

		if (CurrentHunger <= 0)
		{
			DieStarvation();
			return;
		}

		switch (CurrentState)
		{
			case AnimalState.Idle: ProcessIdle(delta); break;
			case AnimalState.Wandering:
			case AnimalState.SearchingFood: 
			case AnimalState.Hunting:
			case AnimalState.Fleeing:
				ProcessMovement(delta); break;
			case AnimalState.Eating: ProcessEating(delta); break;
			case AnimalState.Reproducing: ProcessReproducing(delta); break;
		}
	}

	private void ProcessIdle(double delta)
	{
		stateTimer -= delta;
		if (stateTimer <= 0) ChooseNextAction();
	}

	private void ProcessMovement(double delta)
	{
		if (targetTile == null)
		{
			ChooseNextAction();
			return;
		}

		Vector3 targetPos = targetTile.WorldPosition + new Vector3(0, YOffset, 0);
		Vector3 direction = (targetPos - GlobalPosition).Normalized();
		
		bool isFinalFoodStep = (CurrentState == AnimalState.SearchingFood && targetTile.GetFoodSource() != null);
		float stopDistance = isFinalFoodStep ? EatingDistance : 0.1f;

		if (GlobalPosition.DistanceTo(targetPos) > stopDistance)
		{
			GlobalPosition = GlobalPosition.MoveToward(targetPos, MoveSpeed * (float)delta);
		}

		if (direction.LengthSquared() > 0.01f)
		{
			Vector3 lookPos = GlobalPosition + direction;
			lookPos.Y = GlobalPosition.Y; 
			Transform3D targetTransform = GlobalTransform.LookingAt(lookPos, Vector3.Up, true);
			GlobalTransform = GlobalTransform.InterpolateWith(targetTransform, 8.0f * (float)delta);
		}

		if (GlobalPosition.DistanceTo(targetPos) <= stopDistance + 0.05f)
		{
			CurrentTile.Animals.Remove(this);
			CurrentTile = targetTile;
			CurrentTile.Animals.Add(this);
			targetTile = null;
			
			if (CurrentState == AnimalState.SearchingFood)
			{
				if (CurrentTile.GetFoodSource() != null)
				{
					targetFood = CurrentTile.GetFoodSource();
					SetState(AnimalState.Eating);
					stateTimer = 3.0; 
				}
				else ChooseNextAction();
			}
			else if (CurrentState == AnimalState.Hunting)
			{
				Animal prey = CurrentTile.GetPrey();
				if (prey != null)
				{
					targetPrey = prey;
					SetState(AnimalState.Eating);
					stateTimer = 2.0; // Eat meat faster than berries
				}
				else ChooseNextAction(); // Keep tracking them!
			}
			else if (CurrentState == AnimalState.Fleeing)
			{
				ChooseNextAction(); // Keep running non-stop!
			}
			else
			{
				if (GD.Randf() > 0.3f) ChooseNextAction(); 
				else
				{
					stateTimer = GD.RandRange(0.5, 1.5); 
					SetState(AnimalState.Idle); 
				}
			}
		}
	}

	private void ProcessEating(double delta)
	{
		stateTimer -= delta;
		if (stateTimer <= 0)
		{
			CurrentHunger = MaxHunger;
			
			if (Diet == DietType.Herbivore)
			{
				if (targetFood != null && GodotObject.IsInstanceValid(targetFood))
				{
					if (targetFood is BerryBush bush) bush.EatBerry();
					else
					{
						CurrentTile.Vegetation.Remove(targetFood);
						targetFood.QueueFree(); 
					}
				}
			}
			else if (Diet == DietType.Carnivore)
			{
				if (targetPrey != null && GodotObject.IsInstanceValid(targetPrey) && targetPrey.CurrentState != AnimalState.Dead)
				{
					targetPrey.BeKilled();
				}
			}

			ChooseNextAction();
		}
	}

	private void ProcessReproducing(double delta)
	{
		stateTimer -= delta;
		if (stateTimer <= 0)
		{
			GiveBirth();
			stateTimer = 1.0;
			SetState(AnimalState.Idle);
		}
	}

	private void GiveBirth()
	{
		CurrentHunger -= ReproductionCost;
		timeSinceLastReproduction = 0;

		if (!string.IsNullOrEmpty(SpeciesScenePath))
		{
			PackedScene speciesScene = GD.Load<PackedScene>(SpeciesScenePath);
			if (speciesScene != null)
			{
				Animal baby = (Animal)speciesScene.Instantiate();
				GetParent().AddChild(baby);
				HexTile spawnTile = CurrentTile.GetRandomWalkableNeighbour();
				if (spawnTile == null) spawnTile = CurrentTile; 
				baby.Init(spawnTile);
				GD.Print($"A new {Diet} was born! Population is growing.");
			}
		}
	}

	private void ChooseNextAction()
	{
		// 1. SURVIVAL CHECK: Flee from predators!
		if (Diet == DietType.Herbivore)
		{
			Animal predator = CurrentTile.FindNearestPredator(VisionRadius / 2); // Smell danger close by
			if (predator != null)
			{
				targetTile = CurrentTile.GetNeighbourFurthestFrom(predator.CurrentTile);
				if (targetTile != null)
				{
					SetState(AnimalState.Fleeing);
					return;
				}
			}
		}

		// 2. REPRODUCTION CHECK
		if (CurrentHunger >= ReproductionThreshold && timeSinceLastReproduction >= ReproductionCooldown)
		{
			SetState(AnimalState.Reproducing);
			stateTimer = 2.0; 
			return;
		}

		// 3. HUNGER CHECK
		if (CurrentHunger < (MaxHunger * 0.5f))
		{
			if (Diet == DietType.Herbivore)
			{
				targetFood = CurrentTile.GetFoodSource();
				if (targetFood != null)
				{
					SetState(AnimalState.Eating);
					stateTimer = 3.0;
					return;
				}

				HexTile nearestFoodTile = CurrentTile.FindNearestFood(VisionRadius);
				if (nearestFoodTile != null)
				{
					targetTile = CurrentTile.GetNeighbourClosestTo(nearestFoodTile);
					if (targetTile != null)
					{
						SetState(AnimalState.SearchingFood);
						return;
					}
				}
			}
			else if (Diet == DietType.Carnivore)
			{
				Animal localPrey = CurrentTile.GetPrey();
				if (localPrey != null)
				{
					targetPrey = localPrey;
					SetState(AnimalState.Eating);
					stateTimer = 2.0;
					return;
				}

				Animal nearestPrey = CurrentTile.FindNearestPrey(VisionRadius);
				if (nearestPrey != null)
				{
					targetTile = CurrentTile.GetNeighbourClosestTo(nearestPrey.CurrentTile);
					if (targetTile != null)
					{
						SetState(AnimalState.Hunting);
						return;
					}
				}
			}
		}

		// 4. WANDER
		targetTile = CurrentTile.GetRandomWalkableNeighbour();
		if (targetTile != null) SetState(AnimalState.Wandering); 
		else
		{
			stateTimer = 2.0;
			SetState(AnimalState.Idle); 
		}
	}

	// NEW: Triggered externally when a predator catches this animal
	public void BeKilled()
	{
		CurrentHunger = 0;
		SetState(AnimalState.Dead);
		CurrentTile.Animals.Remove(this);
		GD.Print($"{Name} was eaten by a predator! Circle of life.");
		QueueFree(); // Removes the prey from the world forever
	}

	private void DieStarvation()
	{
		CurrentHunger = 0;
		SetState(AnimalState.Dead);
		CurrentTile.Animals.Remove(this);
		GD.Print($"A {Diet} has died of starvation.");
	}
}
