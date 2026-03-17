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
		float random = GD.Randf();
		PackedScene chosen = null;

		switch (tile.Biome)
		{
			case BiomeType.Forest:
				if (density > -0.15f)
				{
					if (random < 0.45f)
						chosen = OakTrees[GD.RandRange(0, OakTrees.Length - 1)];
					else if (random < 0.70f)
						chosen = Bushes[GD.RandRange(0, Bushes.Length - 1)];
					else if (random < 0.80f)
						chosen = BerryBushes[GD.RandRange(0, BerryBushes.Length - 1)];
				}
				break;

			case BiomeType.Grassland:
				if (density > 0.0f)
				{
					if (random < 0.35f)
						chosen = Bushes[GD.RandRange(0, Bushes.Length - 1)];
					else if (random < 0.40f)
						chosen = BerryBushes[GD.RandRange(0, BerryBushes.Length - 1)];
				}
				break;

			case BiomeType.Desert:
				if (density > 0.1f)
				{
					if (random < 0.35f)
						chosen = Cactus[GD.RandRange(0, Cactus.Length - 1)];
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
}
