using Godot;
using System;
using System.Collections.Generic;

#region Biomes
public enum BiomeType
{
	Ocean,
	Beach,
	Grassland,
	Forest,
	Desert,
	Mountains
}
#endregion

public partial class WorldManager : Node3D
{
	#region Exports
	// --- MAP SETTINGS ---
	[Export] public int WorldSeed = 0;
	[Export] public int MapWidth = 50;
	[Export] public int MapHeight = 50;
	[Export] public float TileSpacing = 2.0f;

	[Export] public float ElevationScale = 0.05f;
	[Export] public float MoistureScale = 0.08f;

	[ExportCategory("Biome Regions")]
	[Export] public float MacroBiomeScale = 0.015f;
	[Export] public float MacroTemperatureScale = 0.012f;
	[Export] public float MacroBiomeBlend = 0.65f;
	[Export] public float MicroVariationStrength = 0.12f;
	[Export] public float LatitudeInfluence = 0.35f;
	[Export] public float TemperatureMoistureBias = 0.35f;
	[Export] public float BiomeWarpScale = 0.03f;
	[Export] public float BiomeWarpStrength = 6.0f;

	[ExportCategory("Biome Thresholds")]
	[Export] public float OceanThreshold = -0.18f;
	[Export] public float BeachThreshold = 0.00f;
	[Export] public float MountainThreshold = 0.40f;
	[Export] public float DesertMoistureThreshold = -0.22f;
	[Export] public float ForestMoistureThreshold = 0.22f;
	[Export] public float ColdTemperatureThreshold = -0.25f;
	[Export] public float HotTemperatureThreshold = 0.35f;

	[ExportCategory("Chunk Loading")]
	[Export] public bool EnableChunkLoading = true;
	[Export] public int ChunkSize = 16;
	[Export] public int ChunkLoadRadius = 2;
	[Export] public int ChunkUnloadRadius = 3;
	[Export] public float ChunkUpdateInterval = 0.25f;
	[Export] public bool EnableAnimalCache = true;
	[Export] public float CachedAnimalTickInterval = 1.0f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float CachedWanderMoveChance = 0.25f;
	[Export(PropertyHint.Range, "0,1,0.01")] public float CachedNeedDrainScale = 0.6f;

	[Export] public VegetationSpawner VegetationSpawner;

	// --- TERRAIN TILES ---
	[Export] public PackedScene GrassTile;
	[Export] public PackedScene WaterTile;
	[Export] public PackedScene SandTile;
	[Export] public PackedScene StoneTile;
	#endregion

	#region Fields
	public static WorldManager Singleton { get; private set; }

	private FastNoiseLite elevationNoise;
	private FastNoiseLite moistureNoise;
	private FastNoiseLite macroMoistureNoise;
	private FastNoiseLite macroTemperatureNoise;
	private FastNoiseLite vegetationNoise;
	private FastNoiseLite blendNoise;
	private FastNoiseLite biomeWarpNoise;
	private bool warnedMissingTileScene = false;
	private int resolvedSeed = 0;

	private readonly HashSet<Vector2I> loadedChunks = new HashSet<Vector2I>();
	private Vector2I lastCameraChunk = new Vector2I(int.MinValue, int.MinValue);
	private float chunkUpdateTimer = 0f;
	private float cachedAnimalTimer = 0f;
	private readonly Dictionary<Vector2I, List<AnimalSnapshot>> cachedAnimals = new Dictionary<Vector2I, List<AnimalSnapshot>>();

	// Exposed grid so other systems can read map data
	public HexTile[,] worldGrid { get; private set; }
	#endregion


	#region GodotLifecycle
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
		if (MapWidth <= 0 || MapHeight <= 0)
		{
			GD.PrintErr("WorldManager: MapWidth and MapHeight must be > 0.");
			return;
		}

		worldGrid = new HexTile[MapWidth, MapHeight];

		GenerateNoise();

		if (EnableChunkLoading)
		{
			UpdateLoadedChunks(true);
		}
		else
		{
			GenerateWorld();
			BuildTileNeighbours();
		}
	}

	public override void _ExitTree()
	{
		if (Singleton == this)
		{
			Singleton = null;
		}
	}

	public override void _Process(double delta)
	{
		if (!EnableChunkLoading)
		{
			return;
		}

		chunkUpdateTimer += (float)delta;
		if (chunkUpdateTimer >= ChunkUpdateInterval)
		{
			chunkUpdateTimer = 0f;
			UpdateLoadedChunks(false);
		}

		if (EnableAnimalCache && CachedAnimalTickInterval > 0f)
		{
			cachedAnimalTimer += (float)delta;
			if (cachedAnimalTimer >= CachedAnimalTickInterval)
			{
				float step = cachedAnimalTimer;
				cachedAnimalTimer = 0f;
				SimulateCachedAnimals(step);
			}
		}
	}
	#endregion


	// ---------- NOISE GENERATION ----------
	#region NoiseGeneration
	private void GenerateNoise()
	{
		GD.Randomize();

		resolvedSeed = WorldSeed != 0 ? WorldSeed : (int)GD.Randi();
		var rng = new RandomNumberGenerator();
		rng.Seed = (ulong)resolvedSeed;

		elevationNoise = new FastNoiseLite();
		elevationNoise.Seed = (int)rng.Randi();
		elevationNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		elevationNoise.Frequency = ElevationScale;

		moistureNoise = new FastNoiseLite();
		moistureNoise.Seed = (int)rng.Randi();
		moistureNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		moistureNoise.Frequency = MoistureScale;

		macroMoistureNoise = new FastNoiseLite();
		macroMoistureNoise.Seed = (int)rng.Randi();
		macroMoistureNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		macroMoistureNoise.Frequency = MacroBiomeScale;

		macroTemperatureNoise = new FastNoiseLite();
		macroTemperatureNoise.Seed = (int)rng.Randi();
		macroTemperatureNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		macroTemperatureNoise.Frequency = MacroTemperatureScale;

		vegetationNoise = new FastNoiseLite();
		vegetationNoise.Seed = (int)rng.Randi();
		vegetationNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		vegetationNoise.Frequency = 0.12f;

		blendNoise = new FastNoiseLite();
		blendNoise.Seed = (int)rng.Randi();
		blendNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		blendNoise.Frequency = 0.35f;

		biomeWarpNoise = new FastNoiseLite();
		biomeWarpNoise.Seed = (int)rng.Randi();
		biomeWarpNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		biomeWarpNoise.Frequency = BiomeWarpScale;
	}
	#endregion


	// ---------- WORLD GENERATION ----------
	#region WorldGeneration
	private void GenerateWorld()
	{
		for (int z = 0; z < MapHeight; z++)
		{
			for (int x = 0; x < MapWidth; x++)
			{
				SpawnTile(x, z);
			}
		}
	}


	private void SpawnTile(int gridX, int gridZ)
	{
		HexTile tile = EnsureTileData(gridX, gridZ);
		if (tile == null)
		{
			return;
		}

		EnsureTileVisuals(tile, gridX, gridZ);
	}

	private HexTile EnsureTileData(int gridX, int gridZ)
	{
		if (gridX < 0 || gridX >= MapWidth || gridZ < 0 || gridZ >= MapHeight)
		{
			return null;
		}

		HexTile tile = worldGrid[gridX, gridZ];
		if (tile != null)
		{
			return tile;
		}

		tile = CreateTileData(gridX, gridZ);
		worldGrid[gridX, gridZ] = tile;
		return tile;
	}

	private HexTile CreateTileData(int gridX, int gridZ)
	{
		Vector3 worldPosition = GetHexPosition(gridX, gridZ);

		float baseElevation = elevationNoise.GetNoise2D(gridX, gridZ);
		float microMoisture = moistureNoise.GetNoise2D(gridX, gridZ);

		Vector2 warped = GetBiomeWarp(gridX, gridZ);
		float macroMoisture = macroMoistureNoise.GetNoise2D(warped.X, warped.Y);
		float macroTemperature = macroTemperatureNoise.GetNoise2D(warped.X, warped.Y);
		float latitude = GetLatitude(gridZ);

		float blendValue = blendNoise.GetNoise2D(gridX, gridZ);
		float elevationWobble = blendValue * 0.08f;
		float microVariation = blendValue * MicroVariationStrength;

		float finalElevation = baseElevation + elevationWobble;
		float macroBlend = Mathf.Clamp(MacroBiomeBlend, 0f, 1f);
		float finalMoisture = Mathf.Lerp(microMoisture, macroMoisture, macroBlend) + microVariation;
		float finalTemperature = macroTemperature + (latitude * LatitudeInfluence) + (microVariation * 0.5f);

		finalMoisture = Mathf.Clamp(finalMoisture, -1f, 1f);
		finalTemperature = Mathf.Clamp(finalTemperature, -1f, 1f);

		BiomeType biome = GetBiome(finalElevation, finalMoisture, finalTemperature);

		return new HexTile(
			new Vector2I(gridX, gridZ),
			worldPosition,
			biome,
			finalElevation,
			finalMoisture,
			finalTemperature,
			null
		);
	}

	private void EnsureTileVisuals(HexTile tile, int gridX, int gridZ)
	{
		if (tile == null)
		{
			return;
		}

		if (tile.TileObject != null && GodotObject.IsInstanceValid(tile.TileObject))
		{
			return;
		}

		PackedScene tileScene = GetTileFromBiome(tile.Biome);
		if (tileScene == null)
		{
			if (!warnedMissingTileScene)
			{
				GD.PrintErr($"WorldManager: Missing terrain tile scene for biome {tile.Biome}.");
				warnedMissingTileScene = true;
			}
			return;
		}

		Node3D tileInstance = null;
		if (ObjectPoolManager.Singleton != null)
		{
			tileInstance = ObjectPoolManager.Singleton.Spawn(tileScene, this) as Node3D;
		}
		else
		{
			tileInstance = (Node3D)tileScene.Instantiate();
		}

		if (tileInstance == null)
		{
			return;
		}

		if (tileInstance.GetParent() == null)
		{
			AddChild(tileInstance);
		}

		tileInstance.Position = tile.WorldPosition;
		tile.TileObject = tileInstance;

		if (VegetationSpawner != null)
		{
			float vegetationDensity = vegetationNoise.GetNoise2D(gridX, gridZ);
			var rng = new RandomNumberGenerator();
			rng.Seed = GetTileSeed(gridX, gridZ, 7001);
			VegetationSpawner.SpawnVegetation(tile, vegetationDensity, rng);
		}
	}

	private void DespawnTileVisuals(HexTile tile)
	{
		if (tile == null)
		{
			return;
		}

		if (tile.Vegetation != null && tile.Vegetation.Count > 0)
		{
			for (int i = tile.Vegetation.Count - 1; i >= 0; i--)
			{
				Node3D plant = tile.Vegetation[i];
				if (plant == null || !GodotObject.IsInstanceValid(plant))
				{
					tile.Vegetation.RemoveAt(i);
					continue;
				}

				if (ObjectPoolManager.Singleton != null)
				{
					ObjectPoolManager.Singleton.Release(plant);
				}
				else
				{
					plant.QueueFree();
				}
			}

			tile.Vegetation.Clear();
		}

		if (tile.TileObject != null)
		{
			if (ObjectPoolManager.Singleton != null)
			{
				ObjectPoolManager.Singleton.Release(tile.TileObject);
			}
			else if (GodotObject.IsInstanceValid(tile.TileObject))
			{
				tile.TileObject.QueueFree();
			}

			tile.TileObject = null;
		}
	}

	private void DespawnTileAnimals(HexTile tile)
	{
		if (tile == null || tile.Animals == null || tile.Animals.Count == 0)
		{
			return;
		}

		var animals = new List<Animal>(tile.Animals);
		tile.Animals.Clear();

		for (int i = 0; i < animals.Count; i++)
		{
			Animal animal = animals[i];
			if (animal == null || !GodotObject.IsInstanceValid(animal))
			{
				continue;
			}

			animal.DespawnForChunkUnload();
		}
	}

	private void CacheTileAnimals(HexTile tile)
	{
		if (!EnableAnimalCache)
		{
			DespawnTileAnimals(tile);
			return;
		}

		if (tile == null || tile.Animals == null || tile.Animals.Count == 0)
		{
			return;
		}

		Vector2I gridPos = tile.GridPosition;
		if (!cachedAnimals.TryGetValue(gridPos, out List<AnimalSnapshot> list))
		{
			list = new List<AnimalSnapshot>();
			cachedAnimals[gridPos] = list;
		}

		var animals = new List<Animal>(tile.Animals);
		tile.Animals.Clear();

		for (int i = 0; i < animals.Count; i++)
		{
			Animal animal = animals[i];
			if (animal == null || !GodotObject.IsInstanceValid(animal))
			{
				continue;
			}

			AnimalSnapshot snapshot = animal.CreateSnapshot();
			snapshot.GridPosition = gridPos;
			list.Add(snapshot);

			animal.DespawnForChunkUnload();
		}
	}
	#endregion


	// ---------- CACHED ANIMAL SIMULATION ----------
	#region CachedAnimals
	private void RestoreCachedAnimalsForTile(HexTile tile)
	{
		if (!EnableAnimalCache || tile == null)
		{
			return;
		}

		Vector2I gridPos = tile.GridPosition;
		if (!cachedAnimals.TryGetValue(gridPos, out List<AnimalSnapshot> list) || list.Count == 0)
		{
			return;
		}

		for (int i = 0; i < list.Count; i++)
		{
			AnimalSnapshot snapshot = list[i];
			SpawnCachedAnimalSnapshot(snapshot, tile);
		}

		cachedAnimals.Remove(gridPos);
	}

	private void SimulateCachedAnimals(float delta)
	{
		if (cachedAnimals.Count == 0)
		{
			return;
		}

		var keys = new List<Vector2I>(cachedAnimals.Keys);
		var moves = new List<(AnimalSnapshot snapshot, Vector2I to)>();
		var births = new List<(AnimalSnapshot snapshot, Vector2I pos)>();

		for (int k = 0; k < keys.Count; k++)
		{
			Vector2I key = keys[k];
			if (!cachedAnimals.TryGetValue(key, out List<AnimalSnapshot> list))
			{
				continue;
			}

			for (int i = list.Count - 1; i >= 0; i--)
			{
				AnimalSnapshot snapshot = list[i];
				if (snapshot == null)
				{
					list.RemoveAt(i);
					continue;
				}

				Vector2I newPos = key;
				bool died = SimulateCachedAnimal(snapshot, list, key, delta, births, out newPos);
				if (died)
				{
					list.RemoveAt(i);
					continue;
				}

				if (newPos != key)
				{
					list.RemoveAt(i);
					moves.Add((snapshot, newPos));
				}
			}

			if (list.Count == 0)
			{
				cachedAnimals.Remove(key);
			}
		}

		for (int i = 0; i < moves.Count; i++)
		{
			var move = moves[i];
			AddCachedAnimal(move.to, move.snapshot);
		}

		for (int i = 0; i < births.Count; i++)
		{
			var birth = births[i];
			AddCachedAnimal(birth.pos, birth.snapshot);
		}
	}

	private bool SimulateCachedAnimal(
		AnimalSnapshot snapshot,
		List<AnimalSnapshot> tileList,
		Vector2I tilePos,
		float delta,
		List<(AnimalSnapshot snapshot, Vector2I pos)> births,
		out Vector2I newPos)
	{
		newPos = tilePos;

		if (worldGrid == null || tilePos.X < 0 || tilePos.Y < 0 || tilePos.X >= MapWidth || tilePos.Y >= MapHeight)
		{
			return false;
		}

		HexTile tile = EnsureTileData(tilePos.X, tilePos.Y);
		if (tile == null)
		{
			return false;
		}

		float drainScale = Mathf.Clamp(CachedNeedDrainScale, 0f, 1f);
		snapshot.CurrentAge += delta;
		snapshot.TimeSinceLastReproduction += delta;
		snapshot.CurrentHunger -= delta * snapshot.HungerDrainRate * drainScale;
		snapshot.CurrentThirst -= delta * snapshot.ThirstDrainRate * drainScale;

		if (snapshot.CurrentAge >= snapshot.MaxAge || snapshot.CurrentHunger <= 0f || snapshot.CurrentThirst <= 0f)
		{
			return true;
		}

		float hungerRatio = snapshot.MaxHunger > 0f ? snapshot.CurrentHunger / snapshot.MaxHunger : 0f;
		float thirstRatio = snapshot.MaxThirst > 0f ? snapshot.CurrentThirst / snapshot.MaxThirst : 0f;

		if (snapshot.IsPregnant)
		{
			snapshot.PregnancyTimer += delta;
			if (snapshot.PregnancyDuration > 0f && snapshot.PregnancyTimer >= snapshot.PregnancyDuration)
			{
				snapshot.PregnancyTimer = 0f;
				snapshot.IsPregnant = false;
				snapshot.TimeSinceLastReproduction = 0f;
				snapshot.CurrentHunger -= snapshot.ReproductionCost;
				snapshot.CurrentThirst -= snapshot.ReproductionCost;

				AnimalSnapshot baby = CreateOffspringSnapshot(snapshot);
				births.Add((baby, tilePos));
			}
		}
		else
		{
			if (snapshot.Sex == AnimalSex.Female &&
				snapshot.CurrentAge >= snapshot.MinReproductiveAge &&
				snapshot.TimeSinceLastReproduction >= snapshot.ReproductionCooldown)
			{
				float threshold = Mathf.Clamp(snapshot.ReproductionThreshold / 100f, 0f, 1f);
				if (hungerRatio >= threshold && thirstRatio >= threshold)
				{
					if (HasCachedMate(tileList, snapshot))
					{
						snapshot.IsPregnant = true;
						snapshot.PregnancyTimer = 0f;
					}
				}
			}
		}

		if (snapshot.CurrentHunger <= 0f || snapshot.CurrentThirst <= 0f)
		{
			return true;
		}

		if (thirstRatio < snapshot.WaterSearchThreshold)
		{
			if (tile.IsNextToWater())
			{
				snapshot.CurrentThirst = snapshot.MaxThirst;
			}
			else
			{
				HexTile waterTile = tile.FindNearestWater(snapshot.VisionRadius);
				if (waterTile != null)
				{
					HexTile step = tile.GetNeighbourClosestTo(waterTile) ?? waterTile;
					newPos = step.GridPosition;
				}
			}
			return false;
		}

		if (hungerRatio < snapshot.FoodSearchThreshold)
		{
			if (snapshot.Diet == DietType.Carnivore)
			{
				if (TryHuntPrey(tileList, snapshot))
				{
					snapshot.CurrentHunger = snapshot.MaxHunger;
				}
				else
				{
					HexTile neighbour = tile.GetRandomWalkableNeighbour();
					if (neighbour != null)
					{
						newPos = neighbour.GridPosition;
					}
				}
			}
			else if (CanGrazeInBiome(snapshot, tile.Biome))
			{
				snapshot.CurrentHunger = snapshot.MaxHunger;
			}
			else
			{
				HexTile neighbour = tile.GetRandomWalkableNeighbour();
				if (neighbour != null)
				{
					newPos = neighbour.GridPosition;
				}
			}

			return false;
		}

		if (GD.Randf() < CachedWanderMoveChance)
		{
			HexTile neighbour = tile.GetRandomWalkableNeighbour();
			if (neighbour != null)
			{
				newPos = neighbour.GridPosition;
			}
		}

		return false;
	}

	private bool TryHuntPrey(List<AnimalSnapshot> tileList, AnimalSnapshot hunter)
	{
		if (tileList == null)
		{
			return false;
		}

		for (int i = tileList.Count - 1; i >= 0; i--)
		{
			AnimalSnapshot prey = tileList[i];
			if (prey == null || prey == hunter)
			{
				continue;
			}

			if (prey.Diet != DietType.Herbivore)
			{
				continue;
			}

			tileList.RemoveAt(i);
			return true;
		}

		return false;
	}

	private bool HasCachedMate(List<AnimalSnapshot> tileList, AnimalSnapshot snapshot)
	{
		if (tileList == null)
		{
			return false;
		}

		for (int i = 0; i < tileList.Count; i++)
		{
			AnimalSnapshot other = tileList[i];
			if (other == null || other == snapshot)
			{
				continue;
			}

			if (other.SpeciesScenePath != snapshot.SpeciesScenePath)
			{
				continue;
			}

			if (other.Sex == AnimalSex.Unknown || snapshot.Sex == AnimalSex.Unknown)
			{
				continue;
			}

			if (other.Sex == snapshot.Sex)
			{
				continue;
			}

			if (other.CurrentAge < other.MinReproductiveAge)
			{
				continue;
			}

			return true;
		}

		return false;
	}

	private bool CanGrazeInBiome(AnimalSnapshot snapshot, BiomeType biome)
	{
		if (biome == BiomeType.Ocean || biome == BiomeType.Mountains)
		{
			return false;
		}

		if (snapshot.Diet == DietType.Carnivore)
		{
			return false;
		}

		return biome == BiomeType.Grassland || biome == BiomeType.Forest || biome == BiomeType.Beach;
	}

	private AnimalSnapshot CreateOffspringSnapshot(AnimalSnapshot parent)
	{
		var baby = new AnimalSnapshot
		{
			SpeciesScenePath = parent.SpeciesScenePath,
			Diet = parent.Diet,
			Sex = GD.Randf() < 0.5f ? AnimalSex.Male : AnimalSex.Female,
			GridPosition = parent.GridPosition,
			MoveSpeed = parent.MoveSpeed,
			VisionRadius = parent.VisionRadius,
			EatingDistance = parent.EatingDistance,
			MaxHunger = parent.MaxHunger,
			MaxThirst = parent.MaxThirst,
			HungerDrainRate = parent.HungerDrainRate,
			ThirstDrainRate = parent.ThirstDrainRate,
			FoodSearchThreshold = parent.FoodSearchThreshold,
			WaterSearchThreshold = parent.WaterSearchThreshold,
			MaxAge = parent.MaxAge,
			BabyAge = parent.BabyAge,
			YoungAge = parent.YoungAge,
			OldAge = parent.OldAge,
			BabyScale = parent.BabyScale,
			YoungScale = parent.YoungScale,
			AdultScale = parent.AdultScale,
			OldScale = parent.OldScale,
			ModelScale = parent.ModelScale,
			YOffset = parent.YOffset,
			ReproductionThreshold = parent.ReproductionThreshold,
			ReproductionCost = parent.ReproductionCost,
			ReproductionCooldown = parent.ReproductionCooldown,
			MinReproductiveAge = parent.MinReproductiveAge,
			PregnancyDuration = parent.PregnancyDuration,
			MatingDuration = parent.MatingDuration,
			RestChance = parent.RestChance,
			MinRestDuration = parent.MinRestDuration,
			MaxRestDuration = parent.MaxRestDuration,
			RestHungerThreshold = parent.RestHungerThreshold,
			RestThirstThreshold = parent.RestThirstThreshold,
			MaxContinuousMoveTime = parent.MaxContinuousMoveTime,
			CurrentAge = 0f,
			CurrentHunger = parent.MaxHunger,
			CurrentThirst = parent.MaxThirst,
			TimeSinceLastReproduction = 0f,
			IsPregnant = false,
			PregnancyTimer = 0f
		};

		return baby;
	}

	private void AddCachedAnimal(Vector2I pos, AnimalSnapshot snapshot)
	{
		if (snapshot == null)
		{
			return;
		}

		snapshot.GridPosition = pos;
		Vector2I chunkCoord = GetChunkCoordFromGrid(pos.X, pos.Y);
		if (loadedChunks.Contains(chunkCoord))
		{
			HexTile tile = EnsureTileData(pos.X, pos.Y);
			SpawnCachedAnimalSnapshot(snapshot, tile);
			return;
		}

		if (!cachedAnimals.TryGetValue(pos, out List<AnimalSnapshot> list))
		{
			list = new List<AnimalSnapshot>();
			cachedAnimals[pos] = list;
		}

		list.Add(snapshot);
	}

	private void SpawnCachedAnimalSnapshot(AnimalSnapshot snapshot, HexTile tile)
	{
		if (snapshot == null || tile == null || string.IsNullOrEmpty(snapshot.SpeciesScenePath))
		{
			return;
		}

		PackedScene scene = GD.Load<PackedScene>(snapshot.SpeciesScenePath);
		if (scene == null)
		{
			return;
		}

		Node parent = GetAnimalParent();
		Animal animal = null;
		if (ObjectPoolManager.Singleton != null)
		{
			animal = ObjectPoolManager.Singleton.Spawn<Animal>(scene, parent);
		}
		else
		{
			animal = scene.Instantiate<Animal>();
		}

		if (animal == null)
		{
			return;
		}

		if (animal.GetParent() == null && parent != null)
		{
			parent.AddChild(animal);
		}

		animal.ApplySnapshot(snapshot, tile);
	}

	private Node GetAnimalParent()
	{
		Node scene = GetTree()?.CurrentScene;
		return scene ?? this;
	}
	#endregion


	// ---------- NEIGHBOUR BUILDING ----------
	#region Neighbours
	private void BuildTileNeighbours()
	{
		for (int z = 0; z < MapHeight; z++)
		{
			for (int x = 0; x < MapWidth; x++)
			{
				HexTile tile = worldGrid[x, z];
				if (tile == null)
				{
					continue;
				}

				foreach (Vector2I offset in GetHexOffsets(z))
				{
					int nx = x + offset.X;
					int nz = z + offset.Y;

					if (nx < 0 || nx >= MapWidth || nz < 0 || nz >= MapHeight)
						continue;

					HexTile neighbour = worldGrid[nx, nz];
					if (neighbour != null)
					{
						tile.Neighbours.Add(neighbour);
					}
				}
			}
		}
	}


	private Vector2I[] GetHexOffsets(int row)
	{
		if (row % 2 == 0)
		{
			return new Vector2I[]
			{
				new Vector2I(-1,0),
				new Vector2I(1,0),
				new Vector2I(0,-1),
				new Vector2I(-1,-1),
				new Vector2I(0,1),
				new Vector2I(-1,1)
			};
		}
		else
		{
			return new Vector2I[]
			{
				new Vector2I(-1,0),
				new Vector2I(1,0),
				new Vector2I(1,-1),
				new Vector2I(0,-1),
				new Vector2I(1,1),
				new Vector2I(0,1)
			};
		}
	}
	#endregion


	// ---------- HEX POSITION ----------
	#region Positioning
	private Vector3 GetHexPosition(int x, int z)
	{
		float xOffset = x * TileSpacing;
		float zOffset = z * TileSpacing * 0.866f;

		if (z % 2 == 1)
			xOffset += TileSpacing * 0.5f;

		return new Vector3(xOffset, 0, zOffset);
	}
	#endregion


	// ---------- BIOME REGION HELPERS ----------
	#region BiomeRegions
	private Vector2 GetBiomeWarp(int x, int z)
	{
		if (biomeWarpNoise == null || BiomeWarpStrength <= 0f)
		{
			return new Vector2(x, z);
		}

		float warpX = biomeWarpNoise.GetNoise2D(x, z) * BiomeWarpStrength;
		float warpZ = biomeWarpNoise.GetNoise2D(x + 1000, z + 1000) * BiomeWarpStrength;
		return new Vector2(x + warpX, z + warpZ);
	}

	private float GetLatitude(int z)
	{
		if (MapHeight <= 1)
		{
			return 0f;
		}

		float t = (float)z / (MapHeight - 1);
		return Mathf.Lerp(1f, -1f, t);
	}
	#endregion

	// ---------- BIOME SELECTION ----------
	#region BiomeSelection
	private BiomeType GetBiome(float elevation, float moisture, float temperature)
	{
		if (elevation < OceanThreshold) return BiomeType.Ocean;
		if (elevation < BeachThreshold) return BiomeType.Beach;
		if (elevation > MountainThreshold) return BiomeType.Mountains;

		float climateMoisture = moisture - (temperature * TemperatureMoistureBias);

		if (temperature <= ColdTemperatureThreshold)
		{
			if (climateMoisture > ForestMoistureThreshold)
				return BiomeType.Forest;
			return BiomeType.Grassland;
		}

		if (temperature >= HotTemperatureThreshold && climateMoisture < DesertMoistureThreshold)
			return BiomeType.Desert;

		if (climateMoisture < DesertMoistureThreshold) return BiomeType.Desert;
		if (climateMoisture > ForestMoistureThreshold) return BiomeType.Forest;

		return BiomeType.Grassland;
	}


	private PackedScene GetTileFromBiome(BiomeType biome)
	{
		switch (biome)
		{
			case BiomeType.Ocean: return WaterTile;
			case BiomeType.Beach:
			case BiomeType.Desert: return SandTile;
			case BiomeType.Forest:
			case BiomeType.Grassland: return GrassTile;
			case BiomeType.Mountains: return StoneTile;
			default: return GrassTile;
		}
	}
	#endregion


	// ---------- PUBLIC SPAWN HELPER ----------
	#region Helpers
	private ulong GetTileSeed(int gridX, int gridZ, int salt)
	{
		unchecked
		{
			int hash = resolvedSeed;
			hash = (hash * 397) ^ gridX;
			hash = (hash * 397) ^ gridZ;
			hash = (hash * 397) ^ salt;
			return (ulong)(uint)hash;
		}
	}
	#endregion

	// ---------- PUBLIC SPAWN HELPER ----------
	#region ChunkLoading
	private void UpdateLoadedChunks(bool force)
	{
		if (!EnableChunkLoading)
		{
			return;
		}

		if (ChunkSize <= 0)
		{
			ChunkSize = 16;
		}

		int loadRadius = Mathf.Max(0, ChunkLoadRadius);
		int unloadRadius = Mathf.Max(loadRadius, ChunkUnloadRadius);

		Vector2I cameraChunk = GetCameraChunk();
		if (!force && cameraChunk == lastCameraChunk)
		{
			return;
		}

		lastCameraChunk = cameraChunk;

		for (int cz = cameraChunk.Y - loadRadius; cz <= cameraChunk.Y + loadRadius; cz++)
		{
			for (int cx = cameraChunk.X - loadRadius; cx <= cameraChunk.X + loadRadius; cx++)
			{
				Vector2I chunk = new Vector2I(cx, cz);
				if (!IsChunkInBounds(chunk))
				{
					continue;
				}

				if (!loadedChunks.Contains(chunk))
				{
					LoadChunk(chunk);
				}
			}
		}

		if (loadedChunks.Count > 0)
		{
			var toUnload = new List<Vector2I>();
			foreach (Vector2I chunk in loadedChunks)
			{
				if (!IsChunkInRange(chunk, cameraChunk, unloadRadius))
				{
					toUnload.Add(chunk);
				}
			}

			for (int i = 0; i < toUnload.Count; i++)
			{
				UnloadChunk(toUnload[i]);
			}
		}
	}

	private void LoadChunk(Vector2I chunkCoord)
	{
		if (!IsChunkInBounds(chunkCoord))
		{
			return;
		}

		if (!loadedChunks.Add(chunkCoord))
		{
			return;
		}

		GetChunkBounds(chunkCoord, out int startX, out int endX, out int startZ, out int endZ);
		for (int z = startZ; z <= endZ; z++)
		{
			for (int x = startX; x <= endX; x++)
			{
				HexTile tile = EnsureTileData(x, z);
				EnsureTileVisuals(tile, x, z);
				RestoreCachedAnimalsForTile(tile);
			}
		}
	}

	private void UnloadChunk(Vector2I chunkCoord)
	{
		if (!loadedChunks.Remove(chunkCoord))
		{
			return;
		}

		GetChunkBounds(chunkCoord, out int startX, out int endX, out int startZ, out int endZ);
		for (int z = startZ; z <= endZ; z++)
		{
			for (int x = startX; x <= endX; x++)
			{
				HexTile tile = worldGrid[x, z];
				if (tile == null)
				{
					continue;
				}

				CacheTileAnimals(tile);
				DespawnTileVisuals(tile);
			}
		}
	}

	private Vector2I GetCameraChunk()
	{
		Camera3D camera = GetViewport().GetCamera3D();
		if (camera == null)
		{
			return new Vector2I(0, 0);
		}

		Vector2I grid = WorldToGrid(camera.GlobalPosition);
		return GetChunkCoordFromGrid(grid.X, grid.Y);
	}

	private Vector2I WorldToGrid(Vector3 worldPos)
	{
		float approxZ = worldPos.Z / (TileSpacing * 0.866f);
		int gridZ = Mathf.RoundToInt(approxZ);
		float xOffset = (gridZ % 2 == 1) ? TileSpacing * 0.5f : 0f;
		int gridX = Mathf.RoundToInt((worldPos.X - xOffset) / TileSpacing);

		gridX = Mathf.Clamp(gridX, 0, MapWidth - 1);
		gridZ = Mathf.Clamp(gridZ, 0, MapHeight - 1);

		return new Vector2I(gridX, gridZ);
	}

	private Vector2I GetChunkCoordFromGrid(int gridX, int gridZ)
	{
		int size = Mathf.Max(1, ChunkSize);
		int cx = Mathf.FloorToInt((float)gridX / size);
		int cz = Mathf.FloorToInt((float)gridZ / size);
		return new Vector2I(cx, cz);
	}

	private bool IsChunkInBounds(Vector2I chunkCoord)
	{
		if (MapWidth <= 0 || MapHeight <= 0)
		{
			return false;
		}

		int size = Mathf.Max(1, ChunkSize);
		int maxChunkX = Mathf.FloorToInt((MapWidth - 1) / (float)size);
		int maxChunkZ = Mathf.FloorToInt((MapHeight - 1) / (float)size);

		return chunkCoord.X >= 0 && chunkCoord.Y >= 0 &&
			   chunkCoord.X <= maxChunkX && chunkCoord.Y <= maxChunkZ;
	}

	private bool IsChunkInRange(Vector2I chunkCoord, Vector2I center, int radius)
	{
		return Mathf.Abs(chunkCoord.X - center.X) <= radius &&
			   Mathf.Abs(chunkCoord.Y - center.Y) <= radius;
	}

	private void GetChunkBounds(Vector2I chunkCoord, out int startX, out int endX, out int startZ, out int endZ)
	{
		int size = Mathf.Max(1, ChunkSize);
		startX = chunkCoord.X * size;
		startZ = chunkCoord.Y * size;

		endX = Mathf.Min(startX + size - 1, MapWidth - 1);
		endZ = Mathf.Min(startZ + size - 1, MapHeight - 1);

		startX = Mathf.Max(startX, 0);
		startZ = Mathf.Max(startZ, 0);
	}
	#endregion

	// ---------- PUBLIC SPAWN HELPER ----------
	#region PublicApi
	public HexTile GetTile(int gridX, int gridZ)
	{
		return EnsureTileData(gridX, gridZ);
	}

	public IEnumerable<HexTile> GetNeighbours(Vector2I gridPos)
	{
		foreach (Vector2I offset in GetHexOffsets(gridPos.Y))
		{
			int nx = gridPos.X + offset.X;
			int nz = gridPos.Y + offset.Y;

			if (nx < 0 || nx >= MapWidth || nz < 0 || nz >= MapHeight)
			{
				continue;
			}

			yield return EnsureTileData(nx, nz);
		}
	}

	public HexTile GetRandomWalkableTile()
	{
		if (MapWidth <= 0 || MapHeight <= 0 || worldGrid == null)
		{
			GD.PrintErr("WorldManager: Map dimensions are invalid or world grid not initialized.");
			return null;
		}

		int maxAttempts = 1000;
		int attempts = 0;

		while (attempts < maxAttempts)
		{
			attempts++;

			int randomX = (int)(GD.Randi() % MapWidth);
			int randomZ = (int)(GD.Randi() % MapHeight);

			HexTile tile = EnsureTileData(randomX, randomZ);

			if (tile != null && tile.IsWalkable)
			{
				if (EnableChunkLoading)
				{
					LoadChunk(GetChunkCoordFromGrid(randomX, randomZ));
				}
				return tile;
			}
		}

		GD.PrintErr("Failed to find walkable tile.");
		return null;
	}
	#endregion
}
