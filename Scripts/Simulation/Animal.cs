using Godot;
using System;

// NEW: Added Omnivore to the Diet Types
public enum DietType { Herbivore, Carnivore, Omnivore }

public enum AnimalState
{
	Idle,
	Wandering,
	SearchingFood,
	SearchingWater,
	Drinking,
	Hunting,   
	Fleeing,   
	Eating,
	Reproducing, 
	Dead 
}

public partial class Animal : Node3D
{
	[Export] public DietType Diet = DietType.Herbivore; 

	[Export] public float MaxHunger = 100f;
	public float CurrentHunger;
	[Export] public float HungerDrainRate = 1.5f; 

	[Export] public float MaxThirst = 100f;
	public float CurrentThirst;
	[Export] public float ThirstDrainRate = 2.0f; 

	[Export] public float MaxAge = 300f; 
	public float CurrentAge = 0f;

	[Export] public float MoveSpeed = 1.5f;
	[Export] public int VisionRadius = 6; 
	[Export] public float EatingDistance = 0.8f; 

	[Export] public bool HasTerritory = false; 
	[Export] public float TerritoryRadius = 20f; 
	public HexTile TerritoryCenter;

	[Export(PropertyHint.File, "*.tscn")] public string SpeciesScenePath; 
	[Export] public float ReproductionThreshold = 85f; 
	[Export] public float ReproductionCost = 40f;      
	[Export] public float ReproductionCooldown = 45f;  
	private float timeSinceLastReproduction = 0f;

	[Export] public float YOffset = 0.2f; 
	[Export] public float ModelScale = 1.0f; 

	[Export] public AnimationPlayer AnimPlayer;
	[Export] public string IdleAnim = "Idle"; 
	[Export] public string WalkAnim = "Walk"; 
	[Export] public string EatAnim = "Eat";     
	[Export] public string DeathAnim = "Death"; 

	public AnimalState CurrentState;
	private double stateTimer = 0;
	public HexTile CurrentTile;
	private HexTile targetTile;
	
	private Node3D targetFood; 
	private Animal targetPrey; 

	public override void _Ready()
	{
		CurrentHunger = MaxHunger;
		CurrentThirst = MaxThirst;
	}

	public void Init(HexTile startTile, bool isNewborn = false)
	{
		CurrentTile = startTile;
		GlobalPosition = startTile.WorldPosition + new Vector3(0, YOffset, 0);
		CurrentTile.Animals.Add(this); 
		
		TerritoryCenter = startTile;

		if (isNewborn) CurrentAge = 0f;
		else CurrentAge = (float)GD.RandRange(0, MaxAge * 0.75f);

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
				case AnimalState.Reproducing: 
					AnimPlayer.Play(IdleAnim); break;
				case AnimalState.Wandering:
				case AnimalState.SearchingFood: 
				case AnimalState.SearchingWater:
				case AnimalState.Hunting:     
				case AnimalState.Fleeing:     
					AnimPlayer.Play(WalkAnim); break;
				case AnimalState.Eating: 
				case AnimalState.Drinking:
					AnimPlayer.Play(EatAnim); break;
				case AnimalState.Dead: 
					AnimPlayer.Play(DeathAnim); break;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (CurrentState == AnimalState.Dead) 
		{
			stateTimer -= delta;
			if (stateTimer <= 0) QueueFree(); 
			return;
		}

		CurrentHunger -= (float)delta * HungerDrainRate;
		CurrentThirst -= (float)delta * ThirstDrainRate;
		CurrentAge += (float)delta; 
		timeSinceLastReproduction += (float)delta; 

		if (CurrentAge >= MaxAge) { Die("old age"); return; }
		if (CurrentHunger <= 0) { Die("starvation"); return; }
		if (CurrentThirst <= 0) { Die("dehydration"); return; }

		switch (CurrentState)
		{
			case AnimalState.Idle: ProcessIdle(delta); break;
			case AnimalState.Wandering:
			case AnimalState.SearchingFood: 
			case AnimalState.SearchingWater:
			case AnimalState.Hunting:
			case AnimalState.Fleeing:
				ProcessMovement(delta); break;
			case AnimalState.Eating: ProcessEating(delta); break;
			case AnimalState.Drinking: ProcessDrinking(delta); break;
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
			else if (CurrentState == AnimalState.SearchingWater)
			{
				if (CurrentTile.IsNextToWater())
				{
					SetState(AnimalState.Drinking);
					stateTimer = 2.0; 
				}
				else ChooseNextAction();
			}
			else if (CurrentState == AnimalState.Hunting)
			{
				Animal prey = CurrentTile.GetPrey();
				if (prey != null)
				{
					targetPrey = prey;
					targetPrey.BeKilled(); 
					SetState(AnimalState.Eating);
					stateTimer = 3.0; 
				}
				else ChooseNextAction(); 
			}
			else if (CurrentState == AnimalState.Fleeing)
			{
				ChooseNextAction(); 
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
			
			// Safely process eating a plant
			if (targetFood != null)
			{
				if (GodotObject.IsInstanceValid(targetFood))
				{
					if (targetFood is BerryBush bush) bush.EatBerry();
					else
					{
						CurrentTile.Vegetation.Remove(targetFood);
						targetFood.QueueFree(); 
					}
				}
				targetFood = null; // Clear it so omnivores don't get confused next time
			}
			// Safely process eating meat
			else if (targetPrey != null)
			{
				CurrentThirst = MaxThirst; 
				if (GodotObject.IsInstanceValid(targetPrey))
				{
					targetPrey.QueueFree(); 
				}
				targetPrey = null; // Clear it
			}

			ChooseNextAction();
		}
	}

	private void ProcessDrinking(double delta)
	{
		stateTimer -= delta;
		if (stateTimer <= 0)
		{
			CurrentThirst = MaxThirst;
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
		CurrentThirst -= ReproductionCost; 
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
				
				baby.Init(spawnTile, true); 
				GD.Print($"A new {Diet} was born! Population is growing.");
			}
		}
	}

	private void ChooseNextAction()
	{
		if (Diet == DietType.Herbivore)
		{
			Animal predator = CurrentTile.FindNearestPredator(VisionRadius / 2); 
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

		if (CurrentHunger >= ReproductionThreshold && CurrentThirst >= ReproductionThreshold && timeSinceLastReproduction >= ReproductionCooldown)
		{
			SetState(AnimalState.Reproducing);
			stateTimer = 2.0; 
			return;
		}

		if (CurrentThirst < (MaxThirst * 0.4f))
		{
			if (CurrentTile.IsNextToWater())
			{
				SetState(AnimalState.Drinking);
				stateTimer = 2.0;
				return;
			}

			HexTile nearestWater = CurrentTile.FindNearestWater(VisionRadius);
			if (nearestWater != null)
			{
				targetTile = CurrentTile.GetNeighbourClosestTo(nearestWater);
				if (targetTile != null)
				{
					SetState(AnimalState.SearchingWater);
					return;
				}
			}
		}

		if (CurrentHunger < (MaxHunger * 0.5f))
		{
			// --- HERBIVORE ---
			if (Diet == DietType.Herbivore)
			{
				targetFood = CurrentTile.GetFoodSource();
				if (targetFood != null) { SetState(AnimalState.Eating); stateTimer = 3.0; return; }

				HexTile nearestFoodTile = CurrentTile.FindNearestFood(VisionRadius);
				if (nearestFoodTile != null)
				{
					targetTile = CurrentTile.GetNeighbourClosestTo(nearestFoodTile);
					if (targetTile != null) { SetState(AnimalState.SearchingFood); return; }
				}
			}
			// --- CARNIVORE ---
			else if (Diet == DietType.Carnivore)
			{
				Animal localPrey = CurrentTile.GetPrey();
				if (localPrey != null)
				{
					targetPrey = localPrey;
					targetPrey.BeKilled(); 
					SetState(AnimalState.Eating);
					stateTimer = 3.0;
					return;
				}

				Animal nearestPrey = CurrentTile.FindNearestPrey(VisionRadius);
				if (nearestPrey != null)
				{
					targetTile = CurrentTile.GetNeighbourClosestTo(nearestPrey.CurrentTile);
					if (targetTile != null) { SetState(AnimalState.Hunting); return; }
				}
			}
			// --- OMNIVORE ---
			else if (Diet == DietType.Omnivore)
			{
				// 1. Is there anything right beneath us?
				targetFood = CurrentTile.GetFoodSource();
				if (targetFood != null) { SetState(AnimalState.Eating); stateTimer = 3.0; return; }

				Animal localPrey = CurrentTile.GetPrey();
				if (localPrey != null)
				{
					targetPrey = localPrey;
					targetPrey.BeKilled();
					SetState(AnimalState.Eating);
					stateTimer = 3.0;
					return;
				}

				// 2. Scan the surroundings for BOTH plants and prey
				HexTile nearestFoodTile = CurrentTile.FindNearestFood(VisionRadius);
				Animal nearestPrey = CurrentTile.FindNearestPrey(VisionRadius);

				float foodDist = nearestFoodTile != null ? GlobalPosition.DistanceTo(nearestFoodTile.WorldPosition) : float.MaxValue;
				float preyDist = nearestPrey != null ? GlobalPosition.DistanceTo(nearestPrey.CurrentTile.WorldPosition) : float.MaxValue;

				// 3. Go to whichever is closer!
				if (nearestFoodTile != null && foodDist <= preyDist)
				{
					targetTile = CurrentTile.GetNeighbourClosestTo(nearestFoodTile);
					if (targetTile != null) { SetState(AnimalState.SearchingFood); return; }
				}
				else if (nearestPrey != null)
				{
					targetTile = CurrentTile.GetNeighbourClosestTo(nearestPrey.CurrentTile);
					if (targetTile != null) { SetState(AnimalState.Hunting); return; }
				}
			}
		}

		if (HasTerritory && TerritoryCenter != null)
		{
			float distanceFromHome = GlobalPosition.DistanceTo(TerritoryCenter.WorldPosition);
			if (distanceFromHome > TerritoryRadius)
			{
				targetTile = CurrentTile.GetNeighbourClosestTo(TerritoryCenter);
				if (targetTile != null) 
				{
					SetState(AnimalState.Wandering);
					return;
				}
			}
		}

		targetTile = CurrentTile.GetRandomWalkableNeighbour();
		if (targetTile != null) SetState(AnimalState.Wandering); 
		else
		{
			stateTimer = 2.0;
			SetState(AnimalState.Idle); 
		}
	}

	public void BeKilled()
	{
		CurrentHunger = 0;
		CurrentThirst = 0;
		SetState(AnimalState.Dead);
		stateTimer = 10.0; 
		CurrentTile.Animals.Remove(this);
		GD.Print($"{Name} was hunted down!");
	}

	private void Die(string cause)
	{
		CurrentHunger = 0;
		CurrentThirst = 0;
		SetState(AnimalState.Dead);
		stateTimer = 15.0; 
		CurrentTile.Animals.Remove(this);
		GD.Print($"A {Diet} has died of {cause}.");
	}
}
