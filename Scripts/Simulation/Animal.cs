using Godot;
using System;
using System.Collections.Generic;

#region ENUMS

// Diet categories used by the food system
public enum DietType { Herbivore, Carnivore, Omnivore }

// Core behaviour states for the animal AI
public enum AnimalState
{
	Idle,
	Wandering,
	SearchingFood,
	SearchingWater,
	SeekingMate,
	Drinking,
	Hunting,
	Fleeing,
	Eating,
	Reproducing,
	Dead
}

public enum AnimalSex
{
	Unknown,
	Male,
	Female
}

public enum LifeStage
{
	Baby,
	Young,
	Adult,
	Old
}

#endregion

public partial class Animal : Node3D, IPoolable
{

#region CORE ANIMAL DATA

	[Export] public DietType Diet = DietType.Herbivore;
	[Export] public AnimalSex Sex = AnimalSex.Unknown;

#endregion


#region NEED SYSTEMS (Hunger / Thirst)

	[Export] public float MaxHunger = 100f;
	public float CurrentHunger;
	[Export] public float HungerDrainRate = 1.5f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float FoodSearchThreshold = 0.6f;

	[Export] public float MaxThirst = 100f;
	public float CurrentThirst;
	[Export] public float ThirstDrainRate = 2.0f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float WaterSearchThreshold = 0.5f;
	[Export] public float DrinkDuration = 2.0f;

#endregion


#region LIFE CYCLE SYSTEM

	[Export] public float MaxAge = 300f;
	public float CurrentAge = 0f;
	[Export] public float BabyAge = 10f;
	[Export] public float YoungAge = 60f;
	[Export] public float OldAge = 240f;

	[Export] public float BabyScale = 0.5f;
	[Export] public float YoungScale = 0.8f;
	[Export] public float AdultScale = 1.0f;
	[Export] public float OldScale = 0.9f;

	public LifeStage CurrentLifeStage { get; private set; } = LifeStage.Adult;
	private float lifeScale = 1f;

#endregion


#region MOVEMENT SYSTEM

	[Export] public float MoveSpeed = 1.5f;
	[Export] public int VisionRadius = 6;
	[Export] public float EatingDistance = 0.8f;
	[Export] public float Acceleration = 1.6f;
	[Export] public float Deceleration = 2.2f;
	[Export] public float TurnSpeed = 4.0f;
	[Export] public float ArriveDistance = 1.2f;
	[Export] public float PathLookahead = 0.6f;
	[Export] public float WanderAmplitude = 0.25f;
	[Export] public float WanderFrequency = 0.3f;
	[Export] public float SeparationRadius = 1.1f;
	[Export] public float SeparationStrength = 0.7f;
	[Export] public float ObstacleAvoidRadius = 1.1f;
	[Export] public float ObstacleAvoidStrength = 0.8f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float RestChance = 0.6f;
	[Export] public float MinRestDuration = 2.5f;
	[Export] public float MaxRestDuration = 6.0f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float RestHungerThreshold = 0.8f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float RestThirstThreshold = 0.8f;
	[Export] public float MaxContinuousMoveTime = 8.0f;

#endregion


#region TERRITORY SYSTEM

	[Export] public bool HasTerritory = false;
	[Export] public float TerritoryRadius = 20f;

	public HexTile TerritoryCenter;

#endregion


#region REPRODUCTION SYSTEM

	[Export(PropertyHint.File, "*.tscn")]
	public string SpeciesScenePath;

	[Export] public float ReproductionThreshold = 85f;
	[Export] public float ReproductionCost = 40f;
	[Export] public float ReproductionCooldown = 45f;
	[Export] public float MinReproductiveAge = 30f;
	[Export] public float PregnancyDuration = 12f;
	[Export] public float MatingDuration = 1.0f;

	private float timeSinceLastReproduction = 0f;
	private bool isPregnant = false;
	private float pregnancyTimer = 0f;
	private GeneticProfile pendingMateGenes;

#endregion

#region GENETICS

	[ExportCategory("Genetics")]
	[Export] public bool EnableGenetics = true;
	[Export] public float MutationChance = 0.15f; // Per-trait chance
	[Export] public float MutationStrength = 0.1f; // +/- % change

	[Export] public float MinMoveSpeed = 0.4f;
	[Export] public float MaxMoveSpeed = 5f;
	[Export] public int MinVisionRadius = 2;
	[Export] public int MaxVisionRadius = 12;
	[Export] public float MinHungerDrainRate = 0.2f;
	[Export] public float MaxHungerDrainRate = 5f;
	[Export] public float MinMaxAge = 60f;
	[Export] public float MaxMaxAge = 2000f;
	[Export] public float MinReproductionThreshold = 40f;
	[Export] public float MaxReproductionThreshold = 95f;
	[Export] public float MinReproductionCooldown = 5f;
	[Export] public float MaxReproductionCooldown = 180f;
	[Export] public float MinReproductionCost = 5f;
	[Export] public float MaxReproductionCost = 80f;

	private GeneticProfile genetics;

#endregion

#region VISUAL / MODEL SETTINGS

	[Export] public float YOffset = 0.2f;
	[Export] public float ModelScale = 1.0f;

#endregion


#region ANIMATION SYSTEM

	[Export] public AnimationPlayer AnimPlayer;

	[Export] public string IdleAnim = "Idle";
	[Export] public string WalkAnim = "Walk";
	[Export] public string EatAnim = "Eat";
	[Export] public string DeathAnim = "Death";
	[Export] public float AnimationBlendTime = 0.12f;
	[Export] public float AnimationSpeedScale = 1.0f;

#endregion


#region AI STATE VARIABLES

	public AnimalState CurrentState;

	private double stateTimer = 0;
	private Vector3 velocity = Vector3.Zero;
	private Vector3 wanderOffset = Vector3.Zero;
	private Vector3 wanderTargetOffset = Vector3.Zero;
	private float wanderChangeTimer = 0f;
	private bool isRegistered = false;
	private SceneTreeTimer deathTimer;
	private float continuousMoveTime = 0f;

#endregion


#region TILE / WORLD REFERENCES

	public HexTile CurrentTile;
	private HexTile targetTile;
	private HexTile pathDestination;
	private readonly System.Collections.Generic.List<HexTile> currentPath = new System.Collections.Generic.List<HexTile>();

#endregion


#region INTERACTION TARGETS

	private Node3D targetFood;
	private HexTile targetFoodTile;
	private HexTile targetWaterTile;
	private Animal targetMate;
	private Animal targetPrey;

#endregion


#region INITIALISATION

	public override void _Ready()
	{
		EnsureGeneticsInitialized();
		EnsureSexAssigned(false);
		UpdateLifeStage();

		// Register this animal in the global population tracker
		EnsureRegistered();

		if (AnimPlayer != null)
		{
			AnimPlayer.SpeedScale = AnimationSpeedScale;
		}

		CurrentHunger = MaxHunger;
		CurrentThirst = MaxThirst;
	}

#endregion


#region SPAWN INITIALISATION

	public void Init(HexTile startTile, bool isNewborn = false)
	{
		ResetTransientState();

		EnsureGeneticsInitialized();
		EnsureSexAssigned(isNewborn);

		CurrentHunger = MaxHunger;
		CurrentThirst = MaxThirst;

		CurrentTile = startTile;

		TerritoryCenter = startTile;

		if (isNewborn)
		{
			CurrentAge = 0f;
			timeSinceLastReproduction = 0f;
		}
		else
		{
			CurrentAge = (float)GD.RandRange(0, MaxAge * 0.75f);
			timeSinceLastReproduction = (float)GD.RandRange(0, ReproductionCooldown / 2);
		}

		UpdateLifeStage();

		GlobalPosition = startTile.WorldPosition + new Vector3(0, GetScaledYOffset(), 0);

		if (CurrentTile != null && !CurrentTile.Animals.Contains(this))
		{
			CurrentTile.Animals.Add(this);
		}

		EnsureRegistered();

		stateTimer = GD.RandRange(1.0, 2.0);

		SetState(AnimalState.Idle);
	}

#endregion


#region POOLING

	public void OnAcquireFromPool()
	{
		ClearDeathTimer();
		ResetTransientState();
	}

	public void OnReleaseToPool()
	{
		ClearDeathTimer();
		ResetTransientState();
		if (AnimPlayer != null)
		{
			AnimPlayer.Stop();
		}
	}

	private void ResetTransientState()
	{
		ClearFoodTarget();
		ClearWaterTarget();
		ClearMateTarget();
		targetPrey = null;
		ClearPath();

		velocity = Vector3.Zero;
		wanderOffset = Vector3.Zero;
		wanderTargetOffset = Vector3.Zero;
		wanderChangeTimer = 0f;
		continuousMoveTime = 0f;
		stateTimer = 0f;

		isPregnant = false;
		pregnancyTimer = 0f;
		pendingMateGenes = null;

		if (CurrentTile != null)
		{
			CurrentTile.Animals.Remove(this);
			CurrentTile = null;
		}

		TerritoryCenter = null;
		CurrentState = AnimalState.Idle;
	}

	public AnimalSnapshot CreateSnapshot()
	{
		var snapshot = new AnimalSnapshot
		{
			SpeciesScenePath = SpeciesScenePath,
			Diet = Diet,
			Sex = Sex,
			GridPosition = CurrentTile != null ? CurrentTile.GridPosition : Vector2I.Zero,
			MoveSpeed = MoveSpeed,
			VisionRadius = VisionRadius,
			EatingDistance = EatingDistance,
			MaxHunger = MaxHunger,
			MaxThirst = MaxThirst,
			HungerDrainRate = HungerDrainRate,
			ThirstDrainRate = ThirstDrainRate,
			FoodSearchThreshold = FoodSearchThreshold,
			WaterSearchThreshold = WaterSearchThreshold,
			MaxAge = MaxAge,
			BabyAge = BabyAge,
			YoungAge = YoungAge,
			OldAge = OldAge,
			BabyScale = BabyScale,
			YoungScale = YoungScale,
			AdultScale = AdultScale,
			OldScale = OldScale,
			ModelScale = ModelScale,
			YOffset = YOffset,
			ReproductionThreshold = ReproductionThreshold,
			ReproductionCost = ReproductionCost,
			ReproductionCooldown = ReproductionCooldown,
			MinReproductiveAge = MinReproductiveAge,
			PregnancyDuration = PregnancyDuration,
			MatingDuration = MatingDuration,
			RestChance = RestChance,
			MinRestDuration = MinRestDuration,
			MaxRestDuration = MaxRestDuration,
			RestHungerThreshold = RestHungerThreshold,
			RestThirstThreshold = RestThirstThreshold,
			MaxContinuousMoveTime = MaxContinuousMoveTime,
			CurrentAge = CurrentAge,
			CurrentHunger = CurrentHunger,
			CurrentThirst = CurrentThirst,
			TimeSinceLastReproduction = timeSinceLastReproduction,
			IsPregnant = isPregnant,
			PregnancyTimer = pregnancyTimer
		};

		return snapshot;
	}

	public void ApplySnapshot(AnimalSnapshot snapshot, HexTile tile)
	{
		if (snapshot == null || tile == null)
		{
			return;
		}

		ResetTransientState();
		genetics = null;
		isRegistered = false;

		SpeciesScenePath = snapshot.SpeciesScenePath;
		Diet = snapshot.Diet;
		Sex = snapshot.Sex;
		MoveSpeed = snapshot.MoveSpeed;
		VisionRadius = snapshot.VisionRadius;
		EatingDistance = snapshot.EatingDistance;
		MaxHunger = snapshot.MaxHunger;
		MaxThirst = snapshot.MaxThirst;
		HungerDrainRate = snapshot.HungerDrainRate;
		ThirstDrainRate = snapshot.ThirstDrainRate;
		FoodSearchThreshold = snapshot.FoodSearchThreshold;
		WaterSearchThreshold = snapshot.WaterSearchThreshold;
		MaxAge = snapshot.MaxAge;
		BabyAge = snapshot.BabyAge;
		YoungAge = snapshot.YoungAge;
		OldAge = snapshot.OldAge;
		BabyScale = snapshot.BabyScale;
		YoungScale = snapshot.YoungScale;
		AdultScale = snapshot.AdultScale;
		OldScale = snapshot.OldScale;
		ModelScale = snapshot.ModelScale;
		YOffset = snapshot.YOffset;
		ReproductionThreshold = snapshot.ReproductionThreshold;
		ReproductionCost = snapshot.ReproductionCost;
		ReproductionCooldown = snapshot.ReproductionCooldown;
		MinReproductiveAge = snapshot.MinReproductiveAge;
		PregnancyDuration = snapshot.PregnancyDuration;
		MatingDuration = snapshot.MatingDuration;
		RestChance = snapshot.RestChance;
		MinRestDuration = snapshot.MinRestDuration;
		MaxRestDuration = snapshot.MaxRestDuration;
		RestHungerThreshold = snapshot.RestHungerThreshold;
		RestThirstThreshold = snapshot.RestThirstThreshold;
		MaxContinuousMoveTime = snapshot.MaxContinuousMoveTime;

		EnsureGeneticsInitialized();

		CurrentAge = snapshot.CurrentAge;
		CurrentHunger = Mathf.Clamp(snapshot.CurrentHunger, 0f, MaxHunger);
		CurrentThirst = Mathf.Clamp(snapshot.CurrentThirst, 0f, MaxThirst);
		timeSinceLastReproduction = snapshot.TimeSinceLastReproduction;
		isPregnant = snapshot.IsPregnant;
		pregnancyTimer = snapshot.PregnancyTimer;

		CurrentTile = tile;
		TerritoryCenter = tile;
		if (CurrentTile != null && !CurrentTile.Animals.Contains(this))
		{
			CurrentTile.Animals.Add(this);
		}

		UpdateLifeStage();
		GlobalPosition = tile.WorldPosition + new Vector3(0, GetScaledYOffset(), 0);

		stateTimer = GD.RandRange(0.6f, 1.4f);
		SetState(AnimalState.Idle);
		EnsureRegistered();
	}

	public void DespawnForChunkUnload()
	{
		InteractionManager.Singleton?.ReleaseReservation(this);

		if (isRegistered)
		{
			PopulationManager.Singleton?.DespawnAnimal(this);
			isRegistered = false;
		}

		if (ObjectPoolManager.Singleton != null)
		{
			ObjectPoolManager.Singleton.Release(this);
		}
		else
		{
			QueueFree();
		}
	}

	private void ClearDeathTimer()
	{
		if (deathTimer == null)
		{
			return;
		}

		deathTimer.Timeout -= OnDeathTimer;
		deathTimer = null;
	}

#endregion


#region REGISTRATION

	private void EnsureRegistered()
	{
		if (isRegistered)
		{
			return;
		}

		PopulationManager.Singleton?.RegisterAnimal(this);
		isRegistered = true;
	}

	private void Unregister(string cause)
	{
		if (!isRegistered)
		{
			return;
		}

		PopulationManager.Singleton?.UnregisterAnimal(this, cause);
		isRegistered = false;
	}

#endregion


#region STATE MACHINE

	private void SetState(AnimalState newState)
	{
		CurrentState = newState;

		if (AnimPlayer == null) return;

		if (CurrentState == AnimalState.Idle ||
			CurrentState == AnimalState.Eating ||
			CurrentState == AnimalState.Drinking ||
			CurrentState == AnimalState.Reproducing ||
			CurrentState == AnimalState.Dead)
		{
			velocity = Vector3.Zero;
		}

		switch (CurrentState)
		{
			case AnimalState.Idle:
			case AnimalState.Reproducing:
				PlayAnim(IdleAnim);
				break;

			case AnimalState.Wandering:
			case AnimalState.SearchingFood:
			case AnimalState.SearchingWater:
			case AnimalState.SeekingMate:
			case AnimalState.Hunting:
			case AnimalState.Fleeing:
				PlayAnim(WalkAnim);
				break;

			case AnimalState.Eating:
			case AnimalState.Drinking:
				PlayAnim(EatAnim);
				break;

			case AnimalState.Dead:
				PlayAnim(DeathAnim);
				break;
		}

		if (CurrentState != AnimalState.Wandering)
		{
			wanderOffset = Vector3.Zero;
			wanderTargetOffset = Vector3.Zero;
			wanderChangeTimer = 0f;
		}
	}

#endregion

#region ANIMATION HELPERS

	private void PlayAnim(string animName)
	{
		if (AnimPlayer == null || string.IsNullOrEmpty(animName))
			return;

		if (!AnimPlayer.HasAnimation(animName))
		{
			GD.PrintErr($"Animation not found: \"{animName}\" on {Name}");
			return;
		}

		if (AnimPlayer.CurrentAnimation == animName && AnimPlayer.IsPlaying())
			return;

		AnimPlayer.SpeedScale = AnimationSpeedScale;
		AnimPlayer.Play(animName, AnimationBlendTime);
	}

#endregion

#region ANIMATION AUTO-ASSIGN

	private void AutoAssignAnimations()
	{
		List<string> anims = GetAnimationNames();
		if (anims.Count == 0)
			return;

		IdleAnim = ResolveAnimationName(IdleAnim, anims, new string[] { "idle", "stand", "rest", "breath" });
		WalkAnim = ResolveAnimationName(WalkAnim, anims, new string[] { "walk", "run", "gallop", "trot", "move" });
		EatAnim = ResolveAnimationName(EatAnim, anims, new string[] { "eat", "eating", "bite", "chew", "feed", "drink" });
		DeathAnim = ResolveAnimationName(DeathAnim, anims, new string[] { "die", "death", "dead", "lay", "lying", "laying", "sleep" });
	}

	private List<string> GetAnimationNames()
	{
		List<string> names = new List<string>();
		if (AnimPlayer == null)
			return names;

		foreach (var anim in AnimPlayer.GetAnimationList())
		{
			if (anim != null)
				names.Add(anim.ToString());
		}

		return names;
	}

	private string ResolveAnimationName(string current, List<string> anims, string[] keywords)
	{
		if (!string.IsNullOrEmpty(current) && AnimPlayer != null && AnimPlayer.HasAnimation(current))
		{
			return current;
		}

		foreach (string keyword in keywords)
		{
			for (int i = 0; i < anims.Count; i++)
			{
				string anim = anims[i];
				if (anim.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
				{
					if (current != anim)
						GD.Print($"Animation mapped for {Name}: \"{current}\" -> \"{anim}\"");
					return anim;
				}
			}
		}

		string fallback = anims[0];
		if (current != fallback)
			GD.Print($"Animation mapped for {Name}: \"{current}\" -> \"{fallback}\"");
		return fallback;
	}

#endregion


#region MAIN SIMULATION LOOP

	public void SimulationTick(double delta)
	{
		if (CurrentState == AnimalState.Dead)
		{
			return;
		}

		UpdateMoveFatigue(delta);

		DrainNeeds(delta);
		UpdateLifeCycle(delta);

		switch (CurrentState)
		{
			case AnimalState.Idle:
				ProcessIdle(delta);
				break;

			case AnimalState.Wandering:
			case AnimalState.SearchingFood:
			case AnimalState.SearchingWater:
			case AnimalState.SeekingMate:
			case AnimalState.Hunting:
			case AnimalState.Fleeing:
				ProcessMovement(delta);
				break;

			case AnimalState.Eating:
				ProcessEating(delta);
				break;

			case AnimalState.Drinking:
				ProcessDrinking(delta);
				break;

			case AnimalState.Reproducing:
				ProcessReproducing(delta);
				break;
		}
	}

#endregion


#region NEED DRAIN SYSTEM

	private void DrainNeeds(double delta)
	{
		CurrentHunger -= (float)delta * HungerDrainRate;
		CurrentThirst -= (float)delta * ThirstDrainRate;
	}

#endregion


#region LIFE CYCLE CHECKS

	private void UpdateLifeCycle(double delta)
	{
		CurrentAge += (float)delta;
		timeSinceLastReproduction += (float)delta;

		UpdateLifeStage();
		UpdatePregnancy(delta);

		if (CurrentAge >= MaxAge)
		{
			Die("old age");
			return;
		}

		if (CurrentHunger <= 0)
		{
			Die("starvation");
			return;
		}

		if (CurrentThirst <= 0)
		{
			Die("dehydration");
			return;
		}
	}

#endregion

#region LIFE STAGES

	private void UpdateLifeStage()
	{
		LifeStage nextStage = GetLifeStageForAge();
		if (nextStage != CurrentLifeStage)
		{
			CurrentLifeStage = nextStage;
			ApplyLifeStageScale();
		}
	}

	private LifeStage GetLifeStageForAge()
	{
		float babyEnd = Mathf.Max(0f, BabyAge);
		float youngEnd = Mathf.Max(babyEnd, YoungAge);
		float oldStart = Mathf.Max(youngEnd, OldAge);

		if (CurrentAge < babyEnd)
			return LifeStage.Baby;
		if (CurrentAge < youngEnd)
			return LifeStage.Young;
		if (CurrentAge >= oldStart)
			return LifeStage.Old;

		return LifeStage.Adult;
	}

	private void ApplyLifeStageScale()
	{
		float stageScale = AdultScale;
		switch (CurrentLifeStage)
		{
			case LifeStage.Baby:
				stageScale = BabyScale;
				break;
			case LifeStage.Young:
				stageScale = YoungScale;
				break;
			case LifeStage.Adult:
				stageScale = AdultScale;
				break;
			case LifeStage.Old:
				stageScale = OldScale;
				break;
		}

		lifeScale = stageScale;
		Scale = Vector3.One * (ModelScale * lifeScale);

		if (CurrentTile != null)
		{
			GlobalPosition = new Vector3(GlobalPosition.X, CurrentTile.WorldPosition.Y + GetScaledYOffset(), GlobalPosition.Z);
		}
	}

	private float GetScaledYOffset()
	{
		return YOffset * lifeScale;
	}

#endregion

#region PREGNANCY

	private void UpdatePregnancy(double delta)
	{
		if (!isPregnant)
			return;

		if (PregnancyDuration <= 0f)
		{
			isPregnant = false;
			GiveBirth(pendingMateGenes);
			pendingMateGenes = null;
			return;
		}

		pregnancyTimer += (float)delta;
		if (pregnancyTimer >= PregnancyDuration)
		{
			pregnancyTimer = 0f;
			isPregnant = false;
			GiveBirth(pendingMateGenes);
			pendingMateGenes = null;
		}
	}

	private void BeginPregnancy(Animal mate)
	{
		if (isPregnant)
			return;

		EnsureGeneticsInitialized();
		if (mate != null)
			mate.EnsureGeneticsInitialized();

		pendingMateGenes = mate != null ? CloneGenetics(mate.genetics) : null;
		pregnancyTimer = 0f;
		isPregnant = true;
	}

#endregion


#region IDLE BEHAVIOUR

	private void ProcessIdle(double delta)
	{
		stateTimer -= delta;

		if (stateTimer <= 0)
			ChooseNextAction();
	}

#endregion


#region MOVEMENT SYSTEM

	private void ProcessMovement(double delta)
	{
		if (CurrentState == AnimalState.SearchingFood && !IsFoodTargetValid())
		{
			ClearFoodTarget();
			ClearPath();
			ChooseNextAction();
			return;
		}

		if (CurrentState == AnimalState.SearchingWater && !IsWaterTargetValid())
		{
			ClearWaterTarget();
			ClearPath();
			ChooseNextAction();
			return;
		}

		if (CurrentState == AnimalState.SeekingMate && !IsMateTargetValid())
		{
			ClearMateTarget();
			ClearPath();
			ChooseNextAction();
			return;
		}

		if (targetTile == null)
		{
			if (pathDestination != null)
			{
				if (pathDestination == CurrentTile)
				{
					ClearPath();
					ChooseNextAction();
					return;
				}

				if (SetPathTo(pathDestination))
					return;
			}

			ChooseNextAction();
			return;
		}

		if (!targetTile.IsWalkableFor(this))
		{
			ClearPath();
			if (pathDestination != null && pathDestination != CurrentTile)
			{
				if (SetPathTo(pathDestination))
					return;
			}

			ChooseNextAction();
			return;
		}

		Vector3 targetPos = GetSteeringTarget((float)delta);
		ApplySteering(targetPos, (float)delta);

		if (HasCrossedMidpoint())
		{
			CurrentTile.Animals.Remove(this);

			CurrentTile = targetTile;

			CurrentTile.Animals.Add(this);

			AdvancePath();

			if (currentPath.Count == 0 &&
				CurrentState == AnimalState.SearchingFood &&
				CurrentTile == targetFoodTile &&
				IsFoodTargetValid())
			{
				SetState(AnimalState.Eating);
				return;
			}

			if (currentPath.Count == 0 &&
				CurrentState == AnimalState.SearchingWater &&
				IsWaterTargetValid() &&
				CurrentTile.IsNextToWater())
			{
				stateTimer = DrinkDuration;
				SetState(AnimalState.Drinking);
				return;
			}

			if (CurrentState == AnimalState.SeekingMate && IsMateTargetValid())
			{
				UpdateMatePath();
				if (IsMateInRange(targetMate))
				{
					BeginMatingWith(targetMate);
					return;
				}
			}

			if (currentPath.Count == 0)
			{
				ChooseNextAction();
			}
		}
	}

#endregion


#region FOOD SYSTEM

	private void ProcessEating(double delta)
	{
		if (targetFood != null)
		{
			if (InteractionManager.Singleton.ConsumeFood(this, targetFood))
			{
				CurrentHunger = MaxHunger;

				ClearFoodTarget();

				SetState(AnimalState.Idle);
			}
			else
			{
				ClearFoodTarget();

				ChooseNextAction();
			}
		}
		else
		{
			ClearFoodTarget();
			ChooseNextAction();
		}
	}

#endregion


#region DRINKING SYSTEM

	private void ProcessDrinking(double delta)
	{
		stateTimer -= delta;

		if (stateTimer <= 0)
		{
			CurrentThirst = MaxThirst;

			ClearWaterTarget();
			ChooseNextAction();
		}
	}

#endregion


#region REPRODUCTION SYSTEM

	private void ProcessReproducing(double delta)
	{
		stateTimer -= delta;

		if (stateTimer <= 0)
		{
			CompleteMating();

			stateTimer = 1.0;

			SetState(AnimalState.Idle);
		}
	}

#endregion


#region REST SYSTEM

	private void UpdateMoveFatigue(double delta)
	{
		if (IsMovingState(CurrentState))
		{
			continuousMoveTime += (float)delta;
		}
		else
		{
			continuousMoveTime = 0f;
		}
	}

	private bool IsMovingState(AnimalState state)
	{
		return state == AnimalState.Wandering ||
			state == AnimalState.SearchingFood ||
			state == AnimalState.SearchingWater ||
			state == AnimalState.SeekingMate ||
			state == AnimalState.Hunting ||
			state == AnimalState.Fleeing;
	}

	private bool ShouldRest()
	{
		float hungerRatio = MaxHunger > 0f ? CurrentHunger / MaxHunger : 0f;
		float thirstRatio = MaxThirst > 0f ? CurrentThirst / MaxThirst : 0f;
		if (hungerRatio < RestHungerThreshold || thirstRatio < RestThirstThreshold)
		{
			return false;
		}

		if (MaxContinuousMoveTime > 0f && continuousMoveTime >= MaxContinuousMoveTime)
		{
			return true;
		}

		float chance = Mathf.Clamp(RestChance, 0f, 1f);
		return GD.Randf() < chance;
	}

#endregion


#region AI DECISION SYSTEM

	private void ChooseNextAction()
	{
		ClearPath();

		if (TryAcquireWaterTarget())
			return;

		if (TryAcquireFoodTarget())
			return;

		if (TryAcquireMateTarget())
			return;

		if (ShouldRest())
		{
			float minRest = Mathf.Min(MinRestDuration, MaxRestDuration);
			float maxRest = Mathf.Max(MinRestDuration, MaxRestDuration);
			stateTimer = (float)GD.RandRange(minRest, maxRest);
			SetState(AnimalState.Idle);
			return;
		}

		if (CurrentTile == null)
		{
			stateTimer = GD.RandRange(1.0, 2.0);
			SetState(AnimalState.Idle);
			return;
		}

		HexTile wanderTile = CurrentTile.GetRandomWalkableNeighbour();

		if (wanderTile != null && SetPathTo(wanderTile))
		{
			SetState(AnimalState.Wandering);
		}
		else
		{
			stateTimer = GD.RandRange(1.0, 2.0);

			SetState(AnimalState.Idle);
		}
	}

#endregion


#region BIRTH SYSTEM

	private void GiveBirth(GeneticProfile mateGenes)
	{
		if (Sex != AnimalSex.Female)
			return;

		CurrentHunger -= ReproductionCost;
		CurrentThirst -= ReproductionCost;

		timeSinceLastReproduction = 0;

		if (string.IsNullOrEmpty(SpeciesScenePath))
			return;

		PackedScene speciesScene = GD.Load<PackedScene>(SpeciesScenePath);

		if (speciesScene == null)
			return;

		EnsureGeneticsInitialized();

		Animal baby = null;
		if (ObjectPoolManager.Singleton != null)
		{
			baby = ObjectPoolManager.Singleton.Spawn<Animal>(speciesScene, GetParent());
		}
		else
		{
			baby = speciesScene.Instantiate<Animal>();
		}

		if (baby == null)
			return;

		baby.SetGenetics(CreateOffspringGenes(mateGenes));
		baby.Sex = AnimalSex.Unknown;

		if (baby.GetParent() == null && GetParent() != null)
		{
			GetParent().AddChild(baby);
		}

		HexTile spawnTile = CurrentTile.GetRandomWalkableNeighbour();

		if (spawnTile == null)
			spawnTile = CurrentTile;

		baby.Init(spawnTile, true);

		GD.Print($"A new {Diet} was born!");
	}

#endregion

#region GENETIC HELPERS

	private void EnsureGeneticsInitialized()
	{
		if (genetics != null)
		{
			return;
		}

		genetics = new GeneticProfile
		{
			MoveSpeed = MoveSpeed,
			VisionRadius = VisionRadius,
			HungerDrainRate = HungerDrainRate,
			MaxAge = MaxAge,
			ReproductionThreshold = ReproductionThreshold,
			ReproductionCooldown = ReproductionCooldown,
			ReproductionCost = ReproductionCost
		};

		ApplyGenes(genetics);
	}

	private void SetGenetics(GeneticProfile profile)
	{
		if (profile == null)
		{
			return;
		}

		genetics = profile;
		ApplyGenes(genetics);
	}

	private GeneticProfile CreateOffspringGenes(GeneticProfile mateGenes)
	{
		GeneticProfile baseProfile = genetics;
		if (mateGenes != null)
		{
			baseProfile = new GeneticProfile
			{
				MoveSpeed = (genetics.MoveSpeed + mateGenes.MoveSpeed) * 0.5f,
				VisionRadius = Mathf.RoundToInt((genetics.VisionRadius + mateGenes.VisionRadius) * 0.5f),
				HungerDrainRate = (genetics.HungerDrainRate + mateGenes.HungerDrainRate) * 0.5f,
				MaxAge = (genetics.MaxAge + mateGenes.MaxAge) * 0.5f,
				ReproductionThreshold = (genetics.ReproductionThreshold + mateGenes.ReproductionThreshold) * 0.5f,
				ReproductionCooldown = (genetics.ReproductionCooldown + mateGenes.ReproductionCooldown) * 0.5f,
				ReproductionCost = (genetics.ReproductionCost + mateGenes.ReproductionCost) * 0.5f
			};
		}

		GeneticProfile child = new GeneticProfile
		{
			MoveSpeed = MutateFloat(baseProfile.MoveSpeed, MinMoveSpeed, MaxMoveSpeed),
			VisionRadius = MutateInt(baseProfile.VisionRadius, MinVisionRadius, MaxVisionRadius),
			HungerDrainRate = MutateFloat(baseProfile.HungerDrainRate, MinHungerDrainRate, MaxHungerDrainRate),
			MaxAge = MutateFloat(baseProfile.MaxAge, MinMaxAge, MaxMaxAge),
			ReproductionThreshold = MutateFloat(baseProfile.ReproductionThreshold, MinReproductionThreshold, MaxReproductionThreshold),
			ReproductionCooldown = MutateFloat(baseProfile.ReproductionCooldown, MinReproductionCooldown, MaxReproductionCooldown),
			ReproductionCost = MutateFloat(baseProfile.ReproductionCost, MinReproductionCost, MaxReproductionCost)
		};

		return child;
	}

	private float MutateFloat(float value, float min, float max)
	{
		float clamped = Mathf.Clamp(value, min, max);

		if (!EnableGenetics || MutationChance <= 0f || MutationStrength <= 0f)
		{
			return clamped;
		}

		if (GD.Randf() > MutationChance)
		{
			return clamped;
		}

		float factor = 1f + (float)GD.RandRange(-MutationStrength, MutationStrength);
		return Mathf.Clamp(clamped * factor, min, max);
	}

	private int MutateInt(int value, int min, int max)
	{
		int clamped = Mathf.Clamp(value, min, max);

		if (!EnableGenetics || MutationChance <= 0f || MutationStrength <= 0f)
		{
			return clamped;
		}

		if (GD.Randf() > MutationChance)
		{
			return clamped;
		}

		float factor = 1f + (float)GD.RandRange(-MutationStrength, MutationStrength);
		int mutated = Mathf.RoundToInt(clamped * factor);
		return Mathf.Clamp(mutated, min, max);
	}

	private void ApplyGenes(GeneticProfile profile)
	{
		MoveSpeed = Mathf.Clamp(profile.MoveSpeed, MinMoveSpeed, MaxMoveSpeed);
		VisionRadius = Mathf.Clamp(profile.VisionRadius, MinVisionRadius, MaxVisionRadius);
		HungerDrainRate = Mathf.Clamp(profile.HungerDrainRate, MinHungerDrainRate, MaxHungerDrainRate);
		MaxAge = Mathf.Clamp(profile.MaxAge, MinMaxAge, MaxMaxAge);
		ReproductionThreshold = Mathf.Clamp(profile.ReproductionThreshold, MinReproductionThreshold, MaxReproductionThreshold);
		ReproductionCooldown = Mathf.Clamp(profile.ReproductionCooldown, MinReproductionCooldown, MaxReproductionCooldown);
		ReproductionCost = Mathf.Clamp(profile.ReproductionCost, MinReproductionCost, MaxReproductionCost);
	}

	private void EnsureSexAssigned(bool isNewborn)
	{
		if (Sex == AnimalSex.Unknown)
		{
			Sex = GD.Randf() < 0.5f ? AnimalSex.Male : AnimalSex.Female;
		}
	}

	private GeneticProfile CloneGenetics(GeneticProfile source)
	{
		if (source == null)
			return null;

		return new GeneticProfile
		{
			MoveSpeed = source.MoveSpeed,
			VisionRadius = source.VisionRadius,
			HungerDrainRate = source.HungerDrainRate,
			MaxAge = source.MaxAge,
			ReproductionThreshold = source.ReproductionThreshold,
			ReproductionCooldown = source.ReproductionCooldown,
			ReproductionCost = source.ReproductionCost
		};
	}

	private class GeneticProfile
	{
		public float MoveSpeed;
		public int VisionRadius;
		public float HungerDrainRate;
		public float MaxAge;
		public float ReproductionThreshold;
		public float ReproductionCooldown;
		public float ReproductionCost;
	}

#endregion


#region DEATH SYSTEM

	public void BeKilled()
	{
		InteractionManager.Singleton?.ReleaseReservation(this);
		Unregister("hunted");

		CurrentHunger = 0;
		CurrentThirst = 0;

		SetState(AnimalState.Dead);

		StartDeathTimer(10.0f);

		if (CurrentTile != null)
		{
			CurrentTile.Animals.Remove(this);
			CurrentTile = null;
		}

		GD.Print($"{Name} was hunted down!");
	}

	private void Die(string cause)
	{
		InteractionManager.Singleton?.ReleaseReservation(this);
		Unregister(cause);

		CurrentHunger = 0;
		CurrentThirst = 0;

		SetState(AnimalState.Dead);

		StartDeathTimer(15.0f);

		if (CurrentTile != null)
		{
			CurrentTile.Animals.Remove(this);
			CurrentTile = null;
		}

		GD.Print($"A {Diet} has died of {cause}.");
	}

	private void StartDeathTimer(float seconds)
	{
		ClearDeathTimer();

		if (seconds <= 0f)
		{
			OnDeathTimer();
			return;
		}

		if (GetTree() == null)
		{
			OnDeathTimer();
			return;
		}

		deathTimer = GetTree().CreateTimer(seconds);
		if (deathTimer != null)
		{
			deathTimer.Timeout += OnDeathTimer;
		}
	}

	private void OnDeathTimer()
	{
		ClearDeathTimer();

		if (ObjectPoolManager.Singleton != null)
		{
			ObjectPoolManager.Singleton.Release(this);
		}
		else
		{
			QueueFree();
		}
	}

#endregion

#region FOOD TARGETING

	private bool TryAcquireFoodTarget()
	{
		if (CurrentTile == null)
			return false;

		float hungerRatio = MaxHunger > 0f ? CurrentHunger / MaxHunger : 0f;
		if (hungerRatio >= Mathf.Clamp(FoodSearchThreshold, 0f, 1f))
			return false;

		if (InteractionManager.Singleton == null)
			return false;

		if (CurrentTile.TryFindAndReserveFood(VisionRadius, this, out HexTile foodTile, out Node3D foodSource))
		{
			targetFoodTile = foodTile;
			targetFood = foodSource;
			if (SetPathTo(foodTile))
			{
				SetState(AnimalState.SearchingFood);
				return true;
			}

			ClearFoodTarget();
			return false;
		}

		return false;
	}

	private bool IsFoodTargetValid()
	{
		if (targetFood == null || targetFoodTile == null)
			return false;

		if (!GodotObject.IsInstanceValid(targetFood))
			return false;

		if (targetFood is BerryBush bush && !bush.HasFood())
			return false;

		InteractionManager interaction = InteractionManager.Singleton;
		if (interaction != null && !interaction.IsFoodReservedBy(targetFood, this))
			return false;

		return true;
	}

	private void ClearFoodTarget()
	{
		InteractionManager.Singleton?.ReleaseReservation(this);
		targetFood = null;
		targetFoodTile = null;
	}

#endregion

#region MATING

	private bool TryAcquireMateTarget()
	{
		if (!IsReadyToReproduce())
			return false;

		if (CurrentTile == null)
			return false;

		Animal mate = FindNearestMate(VisionRadius);
		if (mate == null)
			return false;

		targetMate = mate;

		if (IsMateInRange(targetMate))
		{
			BeginMatingWith(targetMate);
			return true;
		}

		if (SetPathTo(mate.CurrentTile))
		{
			SetState(AnimalState.SeekingMate);
			return true;
		}

		ClearMateTarget();
		return false;
	}

	private bool IsMateTargetValid()
	{
		return IsReadyToReproduce() && IsMateCompatible(targetMate);
	}

	private void ClearMateTarget()
	{
		targetMate = null;
	}

	private void BeginMatingWith(Animal mate)
	{
		if (!IsMateCompatible(mate))
			return;

		targetMate = mate;
		stateTimer = MatingDuration;
		SetState(AnimalState.Reproducing);

		mate.OnMateEngaged(this);
	}

	private void OnMateEngaged(Animal mate)
	{
		if (!IsMateCompatible(mate))
			return;

		targetMate = mate;
		stateTimer = MatingDuration;
		SetState(AnimalState.Reproducing);
	}

	private void CompleteMating()
	{
		if (Sex == AnimalSex.Female && IsMateCompatible(targetMate))
		{
			BeginPregnancy(targetMate);
		}
		else
		{
			timeSinceLastReproduction = 0f;
		}

		if (targetMate != null)
		{
			if (targetMate.Sex != AnimalSex.Female)
				targetMate.timeSinceLastReproduction = 0f;
			targetMate.ClearMateTarget();
		}

		ClearMateTarget();
	}

	private bool IsReadyToReproduce()
	{
		if (CurrentState == AnimalState.Dead)
			return false;

		if (isPregnant)
			return false;

		if (timeSinceLastReproduction < ReproductionCooldown)
			return false;

		if (CurrentAge < MinReproductiveAge)
			return false;

		float threshold = Mathf.Clamp(ReproductionThreshold / 100f, 0f, 1f);
		float hungerRatio = MaxHunger > 0f ? CurrentHunger / MaxHunger : 0f;
		float thirstRatio = MaxThirst > 0f ? CurrentThirst / MaxThirst : 0f;

		return hungerRatio >= threshold && thirstRatio >= threshold;
	}

	private bool IsMateCompatible(Animal other)
	{
		if (other == null || !GodotObject.IsInstanceValid(other))
			return false;

		if (other == this)
			return false;

		if (other.CurrentState == AnimalState.Dead)
			return false;

		if (!IsSameSpecies(other))
			return false;

		if (Sex == AnimalSex.Unknown || other.Sex == AnimalSex.Unknown)
			return false;

		if (Sex == other.Sex)
			return false;

		return other.IsReadyToReproduce();
	}

	private bool IsSameSpecies(Animal other)
	{
		if (!string.IsNullOrEmpty(SpeciesScenePath) && !string.IsNullOrEmpty(other.SpeciesScenePath))
			return SpeciesScenePath == other.SpeciesScenePath;

		return GetType() == other.GetType();
	}

	private bool IsMateInRange(Animal other)
	{
		if (other == null || other.CurrentTile == null || CurrentTile == null)
			return false;

		if (other.CurrentTile == CurrentTile)
			return true;

		return CurrentTile.IsNeighbour(other.CurrentTile);
	}

	private void UpdateMatePath()
	{
		if (targetMate == null || targetMate.CurrentTile == null)
			return;

		if (pathDestination != targetMate.CurrentTile)
			SetPathTo(targetMate.CurrentTile);
	}

	private Animal FindNearestMate(int maxRadius)
	{
		Queue<HexTile> queue = new Queue<HexTile>();
		Queue<int> depths = new Queue<int>();
		HashSet<HexTile> visited = new HashSet<HexTile>();

		queue.Enqueue(CurrentTile);
		depths.Enqueue(0);
		visited.Add(CurrentTile);

		while (queue.Count > 0)
		{
			HexTile current = queue.Dequeue();
			int currentDepth = depths.Dequeue();

			if (currentDepth > maxRadius) break;

			foreach (Animal other in current.Animals)
			{
				if (IsMateCompatible(other))
					return other;
			}

			foreach (HexTile neighbour in current.GetWalkableNeighbours())
			{
				if (!visited.Contains(neighbour))
				{
					visited.Add(neighbour);
					queue.Enqueue(neighbour);
					depths.Enqueue(currentDepth + 1);
				}
			}
		}

		return null;
	}

#endregion

#region WATER TARGETING

	private bool TryAcquireWaterTarget()
	{
		if (CurrentTile == null)
			return false;

		float thirstRatio = MaxThirst > 0f ? CurrentThirst / MaxThirst : 0f;
		if (thirstRatio >= Mathf.Clamp(WaterSearchThreshold, 0f, 1f))
			return false;

		if (CurrentTile.IsNextToWater())
		{
			targetWaterTile = CurrentTile;
			stateTimer = DrinkDuration;
			SetState(AnimalState.Drinking);
			return true;
		}

		HexTile waterTile = CurrentTile.FindNearestWater(VisionRadius);
		if (waterTile == null)
			return false;

		targetWaterTile = waterTile;
		if (SetPathTo(waterTile))
		{
			SetState(AnimalState.SearchingWater);
			return true;
		}

		ClearWaterTarget();
		return false;
	}

	private bool IsWaterTargetValid()
	{
		return targetWaterTile != null;
	}

	private void ClearWaterTarget()
	{
		targetWaterTile = null;
	}

#endregion

#region PATHFINDING

	private bool SetPathTo(HexTile destination)
	{
		if (CurrentTile == null || destination == null)
			return false;

		if (destination == CurrentTile)
		{
			pathDestination = destination;
			currentPath.Clear();
			targetTile = null;
			return true;
		}

		List<HexTile> path = CurrentTile.FindPathTo(destination, this, 2000);
		if (path == null || path.Count == 0)
		{
			return false;
		}

		pathDestination = destination;
		currentPath.Clear();
		currentPath.AddRange(path);
		targetTile = currentPath[0];
		return true;
	}

	private void AdvancePath()
	{
		if (currentPath.Count > 0)
		{
			currentPath.RemoveAt(0);
		}

		if (currentPath.Count > 0)
		{
			targetTile = currentPath[0];
		}
		else
		{
			targetTile = null;
			pathDestination = null;
		}
	}

	private void ClearPath()
	{
		currentPath.Clear();
		targetTile = null;
		pathDestination = null;
	}

#endregion

#region STEERING

	private Vector3 GetSteeringTarget(float delta)
	{
		Vector3 basePos = targetTile.WorldPosition + new Vector3(0f, GetScaledYOffset(), 0f);
		Vector3 flatToTarget = basePos - GlobalPosition;
		flatToTarget.Y = 0f;

		float distance = flatToTarget.Length();
		if (distance > 0.0001f)
		{
			Vector3 dir = flatToTarget / distance;

			if (PathLookahead > 0f)
			{
				float lookahead = Mathf.Min(PathLookahead, distance * 0.4f);
				basePos += dir * lookahead;
			}

			if (CurrentState == AnimalState.Wandering)
			{
				Vector3 side = new Vector3(-dir.Z, 0f, dir.X);
				float changeRate = Mathf.Max(0.01f, WanderFrequency);
				wanderChangeTimer -= delta;
				if (wanderChangeTimer <= 0f)
				{
					float interval = 1f / changeRate;
					wanderChangeTimer = interval * (float)GD.RandRange(0.6f, 1.4f);
					float lateral = (float)GD.RandRange(-1.0f, 1.0f);
					float forward = (float)GD.RandRange(-0.5f, 0.5f);
					wanderTargetOffset = (side * lateral + dir * forward) * WanderAmplitude;
				}

				float smooth = 1f - Mathf.Exp(-changeRate * delta);
				wanderOffset = wanderOffset.Lerp(wanderTargetOffset, smooth);
				basePos += wanderOffset;
			}
		}

		return basePos;
	}

	private void ApplySteering(Vector3 targetPos, float delta)
	{
		Vector3 toTarget = targetPos - GlobalPosition;
		toTarget.Y = 0f;

		float distance = toTarget.Length();
		if (distance < 0.001f)
		{
			velocity = velocity.MoveToward(Vector3.Zero, Deceleration * delta);
			return;
		}

		Vector3 dir = toTarget / distance;
		float arrive = Mathf.Max(ArriveDistance, 0.05f);
		float desiredSpeed = MoveSpeed;
		if (distance < arrive)
		{
			desiredSpeed *= distance / arrive;
		}

		Vector3 desiredVelocity = dir * desiredSpeed;
		Vector3 avoidance = ComputeAvoidance();
		if (avoidance.LengthSquared() > 0.0001f)
		{
			Vector3 blended = desiredVelocity + avoidance * ObstacleAvoidStrength;
			if (blended.LengthSquared() > 0.0001f)
			{
				desiredVelocity = blended.Normalized() * desiredSpeed;
			}
		}

		float accel = desiredSpeed > velocity.Length() ? Acceleration : Deceleration;
		velocity = velocity.MoveToward(desiredVelocity, accel * delta);

		GlobalPosition += velocity * delta;
		GlobalPosition = new Vector3(GlobalPosition.X, targetPos.Y, GlobalPosition.Z);

		if (velocity.LengthSquared() > 0.0001f)
		{
			Vector3 lookDir = velocity;
			lookDir.Y = 0f;
			if (lookDir.LengthSquared() > 0.0001f)
			{
				Vector3 lookPos = GlobalPosition + lookDir;
				Transform3D targetTransform = GlobalTransform.LookingAt(lookPos, Vector3.Up, true);
				float t = Mathf.Clamp(TurnSpeed * delta, 0f, 1f);
				GlobalTransform = GlobalTransform.InterpolateWith(targetTransform, t);
			}
		}
	}

	private bool HasCrossedMidpoint()
	{
		if (CurrentTile == null || targetTile == null || CurrentTile == targetTile)
			return false;

		Vector3 targetPos = new Vector3(targetTile.WorldPosition.X, 0f, targetTile.WorldPosition.Z);
		Vector3 currentPos = new Vector3(GlobalPosition.X, 0f, GlobalPosition.Z);
		float arrive = Mathf.Max(ArriveDistance * 0.5f, 0.15f);
		if ((currentPos - targetPos).LengthSquared() <= arrive * arrive)
			return true;

		Vector3 from = CurrentTile.WorldPosition;
		Vector3 to = targetTile.WorldPosition;
		Vector3 segment = new Vector3(to.X - from.X, 0f, to.Z - from.Z);
		if (segment.LengthSquared() < 0.0001f)
			return false;

		Vector3 mid = new Vector3(from.X + segment.X * 0.5f, 0f, from.Z + segment.Z * 0.5f);
		Vector3 pos = currentPos;
		Vector3 dir = segment.Normalized();

		return (pos - mid).Dot(dir) >= 0f;
	}

#endregion

#region AVOIDANCE

	private Vector3 ComputeAvoidance()
	{
		Vector3 avoidance = Vector3.Zero;

		if (SeparationRadius > 0.01f)
		{
			foreach (Animal other in EnumerateNearbyAnimals())
			{
				if (other == null || other == this)
					continue;

				Vector3 away = GlobalPosition - other.GlobalPosition;
				away.Y = 0f;
				float dist = away.Length();
				if (dist > 0.001f && dist < SeparationRadius)
				{
					avoidance += (away / (dist * dist)) * SeparationStrength;
				}
			}
		}

		if (ObstacleAvoidRadius > 0.01f)
		{
			foreach (Node3D plant in EnumerateNearbyVegetation())
			{
				if (plant == null || !GodotObject.IsInstanceValid(plant))
					continue;

				if (IsPassableVegetation(plant))
					continue;

				Vector3 away = GlobalPosition - plant.GlobalPosition;
				away.Y = 0f;
				float dist = away.Length();
				if (dist > 0.001f && dist < ObstacleAvoidRadius)
				{
					avoidance += away / (dist * dist);
				}
			}
		}

		return avoidance;
	}

	private IEnumerable<Animal> EnumerateNearbyAnimals()
	{
		if (CurrentTile == null)
			yield break;

		foreach (Animal a in CurrentTile.Animals)
			yield return a;

		foreach (HexTile neighbour in CurrentTile.GetNeighbours())
		{
			if (neighbour == null)
				continue;

			foreach (Animal a in neighbour.Animals)
				yield return a;
		}
	}

	private IEnumerable<Node3D> EnumerateNearbyVegetation()
	{
		if (CurrentTile == null)
			yield break;

		foreach (Node3D v in CurrentTile.Vegetation)
			yield return v;

		foreach (HexTile neighbour in CurrentTile.GetNeighbours())
		{
			if (neighbour == null)
				continue;

			foreach (Node3D v in neighbour.Vegetation)
				yield return v;
		}
	}

	private bool IsPassableVegetation(Node3D plant)
	{
		return HexTile.IsPassableVegetation(plant);
	}

#endregion

}
