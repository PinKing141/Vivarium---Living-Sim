using Godot;
using System;

public enum BiomeType
{
	Ocean,
	Beach,
	Grassland,
	Forest,
	Desert,
	Mountains
}

public partial class WorldManager : Node3D
{
	// --- MAP SETTINGS ---
	[Export] public int MapWidth = 50;
	[Export] public int MapHeight = 50;
	[Export] public float TileSpacing = 2.0f;

	// --- NOISE SCALES ---
	[Export] public float ElevationScale = 0.05f;
	[Export] public float MoistureScale = 0.08f;

	// --- EXTERNAL SYSTEMS ---
	[Export] public VegetationSpawner VegetationSpawner;
	
	// --- ANIMAL SETTINGS ---
	[Export] public PackedScene RabbitScene;
	[Export] public int InitialRabbitCount = 15; // NEW: Customise how many rabbits spawn!

	// --- TERRAIN TILES ---
	[Export] public PackedScene GrassTile;
	[Export] public PackedScene WaterTile;
	[Export] public PackedScene SandTile;
	[Export] public PackedScene StoneTile;

	// --- NOISE GENERATORS ---
	private FastNoiseLite elevationNoise;
	private FastNoiseLite moistureNoise;
	private FastNoiseLite vegetationNoise;
	private FastNoiseLite blendNoise;

	// --- MEMORY GRID ---
	private HexTile[,] worldGrid;

	public override void _Ready()
	{
		worldGrid = new HexTile[MapWidth, MapHeight];

		GenerateNoise();
		GenerateWorld();

		// Connect all tiles together
		BuildTileNeighbours();

		// NEW: Spawn the colony!
		SpawnAnimals();
	}

	private void GenerateNoise()
	{
		GD.Randomize();

		elevationNoise = new FastNoiseLite();
		elevationNoise.Seed = (int)GD.Randi();
		elevationNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		elevationNoise.Frequency = ElevationScale;

		moistureNoise = new FastNoiseLite();
		moistureNoise.Seed = (int)GD.Randi();
		moistureNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		moistureNoise.Frequency = MoistureScale;

		vegetationNoise = new FastNoiseLite();
		vegetationNoise.Seed = (int)GD.Randi();
		vegetationNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		vegetationNoise.Frequency = 0.12f; 

		blendNoise = new FastNoiseLite();
		blendNoise.Seed = (int)GD.Randi();
		blendNoise.NoiseType = FastNoiseLite.NoiseTypeEnum.Perlin;
		blendNoise.Frequency = 0.35f; 
	}

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
		Vector3 worldPosition = GetHexPosition(gridX, gridZ);

		// Sample base noise
		float baseElevation = elevationNoise.GetNoise2D(gridX, gridZ);
		float baseMoisture = moistureNoise.GetNoise2D(gridX, gridZ);
		float vegetationDensity = vegetationNoise.GetNoise2D(gridX, gridZ);

		// Biome Blending wobble
		float blendWobble = blendNoise.GetNoise2D(gridX, gridZ) * 0.08f;
		float finalElevation = baseElevation + blendWobble;
		float finalMoisture = baseMoisture + blendWobble;

		BiomeType biome = GetBiome(finalElevation, finalMoisture);
		PackedScene tileScene = GetTileFromBiome(biome);

		if (tileScene == null) return;

		Node3D tileInstance = (Node3D)tileScene.Instantiate();
		tileInstance.Position = worldPosition;
		AddChild(tileInstance);

		// Store the tile in memory
		HexTile tile = new HexTile(
			new Vector2I(gridX, gridZ),
			worldPosition,
			biome,
			finalElevation,
			finalMoisture,
			tileInstance
		);
		worldGrid[gridX, gridZ] = tile;

		// Spawn and track vegetation
		if (VegetationSpawner != null)
		{
			VegetationSpawner.SpawnVegetation(tile, vegetationDensity);
		}
	}

	private void BuildTileNeighbours()
	{
		for (int z = 0; z < MapHeight; z++)
		{
			for (int x = 0; x < MapWidth; x++)
			{
				HexTile tile = worldGrid[x, z];

				foreach (Vector2I offset in GetHexOffsets(z))
				{
					int nx = x + offset.X;
					int nz = z + offset.Y;

					if (nx < 0 || nz < 0 || nx >= MapWidth || nz >= MapHeight)
						continue;

					tile.Neighbours.Add(worldGrid[nx, nz]);
				}
			}
		}
	}

	private Vector2I[] GetHexOffsets(int row)
	{
		if (row % 2 == 0) // Even rows
		{
			return new Vector2I[]
			{
				new Vector2I(-1, 0), new Vector2I(1, 0),
				new Vector2I(0, -1), new Vector2I(-1, -1),
				new Vector2I(0, 1),  new Vector2I(-1, 1)
			};
		}
		else // Odd rows
		{
			return new Vector2I[]
			{
				new Vector2I(-1, 0), new Vector2I(1, 0),
				new Vector2I(1, -1), new Vector2I(0, -1),
				new Vector2I(1, 1),  new Vector2I(0, 1)
			};
		}
	}

	private Vector3 GetHexPosition(int x, int z)
	{
		float xOffset = x * TileSpacing;
		float zOffset = z * TileSpacing * 0.866f; // √3 / 2

		if (z % 2 == 1)
			xOffset += TileSpacing * 0.5f;

		return new Vector3(xOffset, 0, zOffset);
	}

	private BiomeType GetBiome(float elevation, float moisture)
	{
		if (elevation < -0.15f) return BiomeType.Ocean;
		if (elevation < -0.05f) return BiomeType.Beach;
		if (elevation > 0.35f)  return BiomeType.Mountains;
		if (moisture < -0.15f)  return BiomeType.Desert;
		if (moisture > 0.25f)   return BiomeType.Forest;
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

	// --- NEW ANIMAL SPAWN LOGIC ---
	private void SpawnAnimals()
	{
		if (RabbitScene == null) return;

		int spawned = 0;
		int maxAttempts = 1000; // Failsafe to prevent freezing if the map is 100% ocean
		int attempts = 0;

		while (spawned < InitialRabbitCount && attempts < maxAttempts)
		{
			attempts++;

			// Pick a random X and Z coordinate
			int randomX = (int)(GD.Randi() % MapWidth);
			int randomZ = (int)(GD.Randi() % MapHeight);

			HexTile randomTile = worldGrid[randomX, randomZ];

			// Ensure the randomly selected tile isn't ocean or mountain
			if (randomTile != null && randomTile.IsWalkable)
			{
				Animal rabbit = (Animal)RabbitScene.Instantiate();
				AddChild(rabbit);
				rabbit.Init(randomTile);
				
				spawned++;
			}
		}

		GD.Print($"Successfully spawned {spawned} rabbits into the world!");
	}
}
