using Godot;

public class AnimalSnapshot
{
	public string SpeciesScenePath;
	public DietType Diet;
	public AnimalSex Sex;
	public Vector2I GridPosition;

	public float MoveSpeed;
	public int VisionRadius;
	public float EatingDistance;

	public float MaxHunger;
	public float MaxThirst;
	public float HungerDrainRate;
	public float ThirstDrainRate;
	public float FoodSearchThreshold;
	public float WaterSearchThreshold;

	public float MaxAge;
	public float BabyAge;
	public float YoungAge;
	public float OldAge;
	public float BabyScale;
	public float YoungScale;
	public float AdultScale;
	public float OldScale;
	public float ModelScale;
	public float YOffset;

	public float ReproductionThreshold;
	public float ReproductionCost;
	public float ReproductionCooldown;
	public float MinReproductiveAge;
	public float PregnancyDuration;
	public float MatingDuration;

	public float RestChance;
	public float MinRestDuration;
	public float MaxRestDuration;
	public float RestHungerThreshold;
	public float RestThirstThreshold;
	public float MaxContinuousMoveTime;

	public float CurrentAge;
	public float CurrentHunger;
	public float CurrentThirst;
	public float TimeSinceLastReproduction;
	public bool IsPregnant;
	public float PregnancyTimer;
}
