using Godot;
using System;

public enum AnimalState
{
	Idle,
	Wandering,
	SearchingFood,
	Eating,
	Dead 
}

public partial class Animal : Node3D
{
	// --- STATS ---
	[Export] public float MaxHunger = 100f;
	public float CurrentHunger;

	[Export] public float MoveSpeed = 1.5f;
	[Export] public float HungerDrainRate = 2.0f; 

	// --- MODEL PLACEMENT ---
	[Export] public float YOffset = 0.2f; 
	[Export] public float ModelScale = 1.0f; 

	// --- ANIMATIONS ---
	[Export] public AnimationPlayer AnimPlayer;
	[Export] public string IdleAnim = "Idle"; 
	[Export] public string WalkAnim = "Walk"; 
	[Export] public string EatAnim = "Eat";     
	[Export] public string DeathAnim = "Death"; 

	// --- STATE MACHINE ---
	public AnimalState CurrentState;
	private double stateTimer = 0;

	// --- TILE NAVIGATION ---
	public HexTile CurrentTile;
	private HexTile targetTile;
	private Node3D targetFood; 

	public override void _Ready()
	{
		CurrentHunger = MaxHunger;
	}

	public void Init(HexTile startTile)
	{
		CurrentTile = startTile;
		
		GlobalPosition = startTile.WorldPosition + new Vector3(0, YOffset, 0);
		Scale = new Vector3(ModelScale, ModelScale, ModelScale); 
		
		CurrentTile.Animals.Add(this); 
		
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
					AnimPlayer.Play(IdleAnim);
					break;
				case AnimalState.Wandering:
				case AnimalState.SearchingFood:
					AnimPlayer.Play(WalkAnim); 
					break;
				case AnimalState.Eating:
					AnimPlayer.Play(EatAnim);
					break;
				case AnimalState.Dead:
					AnimPlayer.Play(DeathAnim);
					break;
			}
		}
	}

	public override void _Process(double delta)
	{
		if (CurrentState == AnimalState.Dead) return;

		CurrentHunger -= (float)delta * HungerDrainRate;

		if (CurrentHunger <= 0)
		{
			Die();
			return;
		}

		switch (CurrentState)
		{
			case AnimalState.Idle:
				ProcessIdle(delta);
				break;
			case AnimalState.Wandering:
			case AnimalState.SearchingFood: 
				ProcessMovement(delta);
				break;
			case AnimalState.Eating:
				ProcessEating(delta);
				break;
		}
	}

	private void ProcessIdle(double delta)
	{
		stateTimer -= delta;
		if (stateTimer <= 0)
		{
			ChooseNextAction();
		}
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
		
		GlobalPosition = GlobalPosition.MoveToward(targetPos, MoveSpeed * (float)delta);

		if (direction.LengthSquared() > 0.01f)
		{
			Vector3 lookPos = GlobalPosition + direction;
			lookPos.Y = GlobalPosition.Y; 
			
			Transform3D targetTransform = GlobalTransform.LookingAt(lookPos, Vector3.Up, true);
			GlobalTransform = GlobalTransform.InterpolateWith(targetTransform, 8.0f * (float)delta);
		}

		if (GlobalPosition.DistanceTo(targetPos) < 0.1f)
		{
			CurrentTile.Animals.Remove(this);
			CurrentTile = targetTile;
			CurrentTile.Animals.Add(this);

			targetTile = null;
			
			if (CurrentState == AnimalState.SearchingFood)
			{
				targetFood = CurrentTile.GetFoodSource();
				
				if (targetFood != null)
				{
					SetState(AnimalState.Eating);
					stateTimer = 3.0; 
				}
				else 
				{
					stateTimer = 1.0;
					SetState(AnimalState.Idle);
				}
			}
			else
			{
				if (GD.Randf() > 0.3f)
				{
					ChooseNextAction(); 
				}
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
			
			if (targetFood != null && GodotObject.IsInstanceValid(targetFood))
			{
				// NEW: Eat from the script if it exists, otherwise use fallback deletion
				if (targetFood is BerryBush bush)
				{
					bush.EatBerry();
				}
				else
				{
					CurrentTile.Vegetation.Remove(targetFood);
					targetFood.QueueFree(); 
				}
			}

			stateTimer = 1.0;
			SetState(AnimalState.Idle);
		}
	}

	private void ChooseNextAction()
	{
		if (CurrentHunger < (MaxHunger * 0.5f))
		{
			targetFood = CurrentTile.GetFoodSource();
			if (targetFood != null)
			{
				SetState(AnimalState.Eating);
				stateTimer = 3.0;
				return;
			}

			HexTile foodTile = CurrentTile.GetNeighbourWithFood();
			if (foodTile != null)
			{
				targetTile = foodTile;
				SetState(AnimalState.SearchingFood);
				return;
			}
		}

		targetTile = CurrentTile.GetRandomWalkableNeighbour();

		if (targetTile != null)
		{
			SetState(AnimalState.Wandering); 
		}
		else
		{
			stateTimer = 2.0;
			SetState(AnimalState.Idle); 
		}
	}

	private void Die()
	{
		CurrentHunger = 0;
		SetState(AnimalState.Dead);
		
		CurrentTile.Animals.Remove(this);
		GD.Print("An animal has died of starvation.");
	}
}
