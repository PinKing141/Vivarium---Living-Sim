using Godot;

public partial class VegetationSpawner : Node3D
{
	#region Exports
	[Export] public PackedScene[] OakTrees;
	[Export] public PackedScene[] Bushes;
	[Export] public PackedScene[] BerryBushes;
	[Export] public PackedScene[] Cactus;
	#endregion

	#region Spawning
	public void SpawnVegetation(HexTile tile, float density)
	{
		SpawnVegetation(tile, density, null);
	}

	public void SpawnVegetation(HexTile tile, float density, RandomNumberGenerator rng)
	{
		float random = rng != null ? rng.Randf() : GD.Randf();
		PackedScene chosen = null;

		switch (tile.Biome)
		{
			case BiomeType.Forest:
				if (density > -0.15f)
				{
					if (random < 0.45f)
						chosen = PickScene(OakTrees, rng);
					else if (random < 0.70f)
						chosen = PickScene(Bushes, rng);
					else if (random < 0.80f)
						chosen = PickScene(BerryBushes, rng);
				}
				break;

			case BiomeType.Grassland:
				if (density > 0.0f)
				{
					if (random < 0.35f)
						chosen = PickScene(Bushes, rng);
					else if (random < 0.40f)
						chosen = PickScene(BerryBushes, rng);
				}
				break;

			case BiomeType.Desert:
				if (density > 0.1f)
				{
					if (random < 0.35f)
						chosen = PickScene(Cactus, rng);
				}
				break;
		}

		if (chosen == null)
			return;

		Node3D plant = null;
		if (ObjectPoolManager.Singleton != null)
		{
			plant = ObjectPoolManager.Singleton.Spawn(chosen, this) as Node3D;
		}
		else
		{
			plant = (Node3D)chosen.Instantiate();
		}

		if (plant == null)
			return;

		plant.Position = tile.WorldPosition;
		plant.RotationDegrees = new Vector3(0, GD.Randf() * 360f, 0);

		if (plant.GetParent() == null)
		{
			AddChild(plant);
		}

		// Add the spawned plant to the tile's inventory
		tile.Vegetation.Add(plant);
	}
	#endregion

	#region Helpers
	private PackedScene PickScene(PackedScene[] scenes, RandomNumberGenerator rng)
	{
		if (scenes == null || scenes.Length == 0)
		{
			return null;
		}

		int index = rng != null
			? (int)rng.RandiRange(0, scenes.Length - 1)
			: (int)GD.RandRange(0, scenes.Length - 1);
		return scenes[index];
	}
	#endregion
}
