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
	public List<Node3D> Vegetation = new List<Node3D>();
	public List<Animal> Animals = new List<Animal>(); 

	public bool IsWalkable;

	public HexTile(Vector2I gridPos, Vector3 worldPos, BiomeType biome, float elevation, float moisture, Node3D tileObject)
	{
		GridPosition = gridPos;
		WorldPosition = worldPos;
		Biome = biome;
		Elevation = elevation;
		Moisture = moisture;
		TileObject = tileObject;
		IsWalkable = (biome != BiomeType.Ocean && biome != BiomeType.Mountains);
	}

	public Node3D GetFoodSource()
	{
		foreach (Node3D plant in Vegetation)
		{
			if (plant is BerryBush bush) { if (bush.HasFood()) return bush; }
			else if (plant.Name.ToString().Contains("Berry")) return plant;
		}
		return null;
	}

	public HexTile FindNearestFood(int maxRadius)
	{
		Queue<HexTile> queue = new Queue<HexTile>();
		Queue<int> depths = new Queue<int>();
		HashSet<HexTile> visited = new HashSet<HexTile>();

		queue.Enqueue(this);
		depths.Enqueue(0);
		visited.Add(this);

		while (queue.Count > 0)
		{
			HexTile current = queue.Dequeue();
			int currentDepth = depths.Dequeue();

			if (currentDepth > maxRadius) break;
			if (current != this && current.GetFoodSource() != null) return current;

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

	public bool IsNextToWater()
	{
		foreach (HexTile n in Neighbours)
		{
			if (n.Biome == BiomeType.Ocean) return true;
		}
		return false;
	}

	public HexTile FindNearestWater(int maxRadius)
	{
		Queue<HexTile> queue = new Queue<HexTile>();
		Queue<int> depths = new Queue<int>();
		HashSet<HexTile> visited = new HashSet<HexTile>();

		queue.Enqueue(this);
		depths.Enqueue(0);
		visited.Add(this);

		while (queue.Count > 0)
		{
			HexTile current = queue.Dequeue();
			int currentDepth = depths.Dequeue();

			if (currentDepth > maxRadius) break;
			if (current.IsNextToWater()) return current;

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

	public Animal GetPrey()
	{
		foreach (Animal animal in Animals)
		{
			// Omnivores and Carnivores will only hunt Herbivores
			if (animal.Diet == DietType.Herbivore && animal.CurrentState != AnimalState.Dead)
				return animal;
		}
		return null;
	}

	public Animal FindNearestPrey(int maxRadius)
	{
		Queue<HexTile> queue = new Queue<HexTile>();
		Queue<int> depths = new Queue<int>();
		HashSet<HexTile> visited = new HashSet<HexTile>();

		queue.Enqueue(this);
		depths.Enqueue(0);
		visited.Add(this);

		while (queue.Count > 0)
		{
			HexTile current = queue.Dequeue();
			int currentDepth = depths.Dequeue();

			if (currentDepth > maxRadius) break;

			Animal prey = current.GetPrey();
			if (prey != null && prey != this.GetPrey()) return prey; 

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

	public Animal FindNearestPredator(int maxRadius)
	{
		Queue<HexTile> queue = new Queue<HexTile>();
		Queue<int> depths = new Queue<int>();
		HashSet<HexTile> visited = new HashSet<HexTile>();

		queue.Enqueue(this);
		depths.Enqueue(0);
		visited.Add(this);

		while (queue.Count > 0)
		{
			HexTile current = queue.Dequeue();
			int currentDepth = depths.Dequeue();

			if (currentDepth > maxRadius) break;

			foreach (Animal animal in current.Animals)
			{
				// NEW: Treat both Carnivores AND Omnivores as dangerous predators!
				if ((animal.Diet == DietType.Carnivore || animal.Diet == DietType.Omnivore) && animal.CurrentState != AnimalState.Dead)
					return animal;
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

	public HexTile GetNeighbourClosestTo(HexTile targetTile)
	{
		HexTile bestNeighbour = null;
		float shortestDistance = float.MaxValue;

		foreach (HexTile neighbour in GetWalkableNeighbours())
		{
			float dist = neighbour.WorldPosition.DistanceTo(targetTile.WorldPosition);
			if (dist < shortestDistance)
			{
				shortestDistance = dist;
				bestNeighbour = neighbour;
			}
		}
		return bestNeighbour;
	}

	public HexTile GetNeighbourFurthestFrom(HexTile dangerTile)
	{
		HexTile bestNeighbour = null;
		float longestDistance = -1f;

		foreach (HexTile neighbour in GetWalkableNeighbours())
		{
			float dist = neighbour.WorldPosition.DistanceTo(dangerTile.WorldPosition);
			if (dist > longestDistance)
			{
				longestDistance = dist;
				bestNeighbour = neighbour;
			}
		}
		return bestNeighbour;
	}

	public List<HexTile> GetWalkableNeighbours()
	{
		List<HexTile> walkableNeighbours = new List<HexTile>();
		foreach (HexTile neighbour in Neighbours)
		{
			if (neighbour.IsWalkable) walkableNeighbours.Add(neighbour);
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
