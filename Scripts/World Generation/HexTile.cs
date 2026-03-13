using Godot;
using System.Collections.Generic;

public class HexTile
{
	public Vector2I GridPosition;
	public Vector3 WorldPosition;

	public BiomeType Biome;

	public float Elevation;
	public float Moisture;

	public Node3D TileObject;

	public List<HexTile> Neighbours = new List<HexTile>();
	
	// Phase 2 Tracking Lists
	public List<Node3D> Vegetation = new List<Node3D>();
	public List<Animal> Animals = new List<Animal>();

	// Tile Walkability
	public bool IsWalkable;

	public HexTile(Vector2I gridPos, Vector3 worldPos, BiomeType biome, float elevation, float moisture, Node3D tileObject)
	{
		GridPosition = gridPos;
		WorldPosition = worldPos;

		Biome = biome;

		Elevation = elevation;
		Moisture = moisture;

		TileObject = tileObject;

		// Set walkability (Animals cannot walk on Ocean or extremely steep Mountains)
		IsWalkable = (biome != BiomeType.Ocean && biome != BiomeType.Mountains);
	}

	// --- TILE QUERIES ---

	public Node3D GetFoodSource()
	{
		foreach (Node3D plant in Vegetation)
		{
			// NEW: Check if the plant has our new script attached
			if (plant is BerryBush bush)
			{
				// Only return it if it actually has berries left!
				if (bush.HasFood())
				{
					return bush;
				}
			}
			// Fallback: If you haven't attached the script yet, just check the name
			else if (plant.Name.ToString().Contains("Berry"))
			{
				return plant;
			}
		}
		
		return null;
	}

	public HexTile GetNeighbourWithFood()
	{
		foreach (HexTile neighbour in Neighbours)
		{
			if (neighbour.IsWalkable && neighbour.GetFoodSource() != null)
			{
				return neighbour; // Found a nearby tile with food!
			}
		}

		return null;
	}

	public List<HexTile> GetWalkableNeighbours()
	{
		List<HexTile> walkableNeighbours = new List<HexTile>();
		
		foreach (HexTile neighbour in Neighbours)
		{
			if (neighbour.IsWalkable)
			{
				walkableNeighbours.Add(neighbour);
			}
		}

		return walkableNeighbours;
	}

	public HexTile GetRandomWalkableNeighbour()
	{
		List<HexTile> walkable = GetWalkableNeighbours();
		
		if (walkable.Count > 0)
		{
			int randomIndex = (int)(GD.Randi() % walkable.Count);
			return walkable[randomIndex];
		}

		return null;
	}
}
