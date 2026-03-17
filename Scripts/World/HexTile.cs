using Godot;
using System.Collections.Generic;

public class HexTile
{
	#region Data
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
	#endregion

	#region Construction
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
	#endregion

	#region Food
	public Node3D GetFoodSource()
	{
		return GetFoodSource(null);
	}

	public Node3D GetFoodSource(Animal requester)
	{
		InteractionManager interaction = InteractionManager.Singleton;

		foreach (Node3D plant in Vegetation)
		{
			if (!GodotObject.IsInstanceValid(plant))
				continue;

			if (interaction != null &&
				interaction.IsFoodReserved(plant) &&
				(requester == null || !interaction.IsFoodReservedBy(plant, requester)))
			{
				continue;
			}

			if (plant is BerryBush bush)
			{
				if (bush.HasFood()) return bush;
			}
			else if (plant.Name.ToString().Contains("Berry"))
			{
				return plant;
			}
		}
		return null;
	}

	public HexTile FindNearestFood(int maxRadius)
	{
		return FindNearestFood(maxRadius, null);
	}

	public HexTile FindNearestFood(int maxRadius, Animal requester)
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
			if (current != this && current.GetFoodSource(requester) != null) return current;

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

	public bool TryFindAndReserveFood(int maxRadius, Animal requester, out HexTile foodTile, out Node3D foodSource)
	{
		foodTile = null;
		foodSource = null;

		if (requester == null)
			return false;

		InteractionManager interaction = InteractionManager.Singleton;

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

			if (current != this)
			{
				Node3D candidate = current.GetFoodSource(requester);
				if (candidate != null)
				{
					if (interaction == null || interaction.TryReserveFood(requester, candidate))
					{
						foodTile = current;
						foodSource = candidate;
						return true;
					}
				}
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

		return false;
	}
	#endregion

	#region Water
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
	#endregion

	#region PredatorsAndPrey
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
	#endregion

	#region Neighbours
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
			if (neighbour != null && neighbour.IsWalkable) walkableNeighbours.Add(neighbour);
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
	#endregion

	#region Pathfinding
	public List<HexTile> FindPathTo(HexTile target, int maxNodes = 1000)
	{
		if (target == null)
			return null;

		if (target == this)
			return new List<HexTile>();

		if (!target.IsWalkable)
			return null;

		List<HexTile> openSet = new List<HexTile> { this };
		HashSet<HexTile> closedSet = new HashSet<HexTile>();
		Dictionary<HexTile, HexTile> cameFrom = new Dictionary<HexTile, HexTile>();
		Dictionary<HexTile, float> gScore = new Dictionary<HexTile, float> { { this, 0f } };
		Dictionary<HexTile, float> fScore = new Dictionary<HexTile, float> { { this, Heuristic(this, target) } };

		int expanded = 0;

		while (openSet.Count > 0)
		{
			if (maxNodes > 0 && expanded >= maxNodes)
				break;

			HexTile current = GetLowestScore(openSet, fScore);
			if (current == target)
				return ReconstructPath(cameFrom, current);

			openSet.Remove(current);
			closedSet.Add(current);
			expanded++;

			foreach (HexTile neighbour in current.GetWalkableNeighbours())
			{
				if (neighbour == null || closedSet.Contains(neighbour))
					continue;

				float tentative = gScore[current] + current.WorldPosition.DistanceTo(neighbour.WorldPosition);
				if (!gScore.ContainsKey(neighbour) || tentative < gScore[neighbour])
				{
					cameFrom[neighbour] = current;
					gScore[neighbour] = tentative;
					fScore[neighbour] = tentative + Heuristic(neighbour, target);
					if (!openSet.Contains(neighbour))
						openSet.Add(neighbour);
				}
			}
		}

		return null;
	}

	private static float Heuristic(HexTile from, HexTile to)
	{
		return from.WorldPosition.DistanceTo(to.WorldPosition);
	}

	private static HexTile GetLowestScore(List<HexTile> nodes, Dictionary<HexTile, float> fScore)
	{
		HexTile best = nodes[0];
		float bestScore = fScore.TryGetValue(best, out float score) ? score : float.MaxValue;

		for (int i = 1; i < nodes.Count; i++)
		{
			HexTile candidate = nodes[i];
			float candidateScore = fScore.TryGetValue(candidate, out float candidateValue) ? candidateValue : float.MaxValue;
			if (candidateScore < bestScore)
			{
				best = candidate;
				bestScore = candidateScore;
			}
		}

		return best;
	}

	private static List<HexTile> ReconstructPath(Dictionary<HexTile, HexTile> cameFrom, HexTile current)
	{
		List<HexTile> path = new List<HexTile>();

		while (cameFrom.TryGetValue(current, out HexTile previous))
		{
			path.Add(current);
			current = previous;
		}

		path.Reverse();
		return path;
	}
	#endregion
}
