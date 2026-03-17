using Godot;
using System;

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
	[Export] public int MapWidth = 50;
	[Export] public int MapHeight = 50;
	[Export] public float TileSpacing = 2.0f;

	[Export] public float ElevationScale = 0.05f;
	[Export] public float MoistureScale = 0.08f;

	[Export] public VegetationSpawner VegetationSpawner;

	// --- TERRAIN TILES ---
	[Export] public PackedScene GrassTile;
	[Export] public PackedScene WaterTile;
	[Export] public PackedScene SandTile;
	[Export] public PackedScene StoneTile;
	#endregion

	#region Fields
	private FastNoiseLite elevationNoise;
	private FastNoiseLite moistureNoise;
	private FastNoiseLite vegetationNoise;
	private FastNoiseLite blendNoise;
	private bool warnedMissingTileScene = false;

	// Exposed grid so other systems can read map data
	public HexTile[,] worldGrid { get; private set; }
	#endregion


	#region GodotLifecycle
	public override void _Ready()
	{
		if (MapWidth <= 0 || MapHeight <= 0)
		{
			GD.PrintErr("WorldManager: MapWidth and MapHeight must be > 0.");
			return;
		}

		worldGrid = new HexTile[MapWidth, MapHeight];

		GenerateNoise();
		GenerateWorld();
		BuildTileNeighbours();
	}
	#endregion


	// ---------- NOISE GENERATION ----------
	#region NoiseGeneration
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
		Vector3 worldPosition = GetHexPosition(gridX, gridZ);

		float baseElevation = elevationNoise.GetNoise2D(gridX, gridZ);
		float baseMoisture = moistureNoise.GetNoise2D(gridX, gridZ);
		float vegetationDensity = vegetationNoise.GetNoise2D(gridX, gridZ);

		float blendWobble = blendNoise.GetNoise2D(gridX, gridZ) * 0.08f;

		float finalElevation = baseElevation + blendWobble;
		float finalMoisture = baseMoisture + blendWobble;

		BiomeType biome = GetBiome(finalElevation, finalMoisture);
		PackedScene tileScene = GetTileFromBiome(biome);

		if (tileScene == null)
		{
			if (!warnedMissingTileScene)
			{
				GD.PrintErr($"WorldManager: Missing terrain tile scene for biome {biome}.");
				warnedMissingTileScene = true;
			}
			return;
		}

		Node3D tileInstance = (Node3D)tileScene.Instantiate();
		tileInstance.Position = worldPosition;
		AddChild(tileInstance);

		HexTile tile = new HexTile(
			new Vector2I(gridX, gridZ),
			worldPosition,
			biome,
			finalElevation,
			finalMoisture,
			tileInstance
		);

		worldGrid[gridX, gridZ] = tile;

		if (VegetationSpawner != null)
			VegetationSpawner.SpawnVegetation(tile, vegetationDensity);
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


	// ---------- BIOME SELECTION ----------
	#region BiomeSelection
	private BiomeType GetBiome(float elevation, float moisture)
	{
		// Softer overlap bands; blendNoise will push tiles across edges.
		if (elevation < -0.18f) return BiomeType.Ocean;
		if (elevation < 0.00f) return BiomeType.Beach;
		if (elevation > 0.40f) return BiomeType.Mountains;

		if (moisture < -0.22f) return BiomeType.Desert;
		if (moisture > 0.22f) return BiomeType.Forest;

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
	#region PublicApi
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

			HexTile tile = worldGrid[randomX, randomZ];

			if (tile != null && tile.IsWalkable)
				return tile;
		}

		GD.PrintErr("Failed to find walkable tile.");
		return null;
	}
	#endregion
}
