using Godot;
using System;

public partial class GameInitialiser : Node
{
#region References
	// Reference to your newly simplified WorldManager
	[Export] public WorldManager WorldManagerRef;
#endregion
	
#region Initial Population
	[ExportCategory("Initial Population")]
	[Export] public PackedScene RabbitScene;
	[Export] public int InitialRabbitCount = 20; 
	
	[Export] public PackedScene PredatorScene;
	[Export] public int InitialPredatorCount = 3; 
	
	[Export] public PackedScene OmnivoreScene;
	[Export] public int InitialOmnivoreCount = 2; 
#endregion

#region Lifecycle
	public override void _Ready()
	{
		// We use CallDeferred to wait one frame. This guarantees the WorldManager 
		// has completely finished generating the map grid before we try to spawn anything.
		CallDeferred(MethodName.SpawnInitialEntities);
	}
#endregion

#region Spawning
	private void SpawnInitialEntities()
	{
		if (WorldManagerRef == null)
		{
			GD.PrintErr("GameInitialiser: WorldManager reference is missing! Please assign it in the Inspector.");
			return;
		}

		SpawnSpecies(RabbitScene, InitialRabbitCount);
		SpawnSpecies(PredatorScene, InitialPredatorCount);
		SpawnSpecies(OmnivoreScene, InitialOmnivoreCount);
		
		GD.Print("Game Initialisation Complete!");
	}

	private void SpawnSpecies(PackedScene scene, int count)
	{
		if (scene == null) return;

		for (int i = 0; i < count; i++)
		{
			// Ask the WorldManager for a safe, walkable place to put the animal
			HexTile spawnTile = WorldManagerRef.GetRandomWalkableTile();
			
			if (spawnTile != null)
			{
				Node animalNode = null;
				if (ObjectPoolManager.Singleton != null)
				{
					animalNode = ObjectPoolManager.Singleton.Spawn(scene, this);
				}
				else
				{
					animalNode = scene.Instantiate();
				}

				if (animalNode == null)
				{
					continue;
				}

				// Add the animal to the scene tree if the pool did not handle it
				if (animalNode.GetParent() == null)
				{
					AddChild(animalNode);
				}
				
				// Initialize the animal with its starting tile
				if (animalNode is Animal animalScript)
				{
					animalScript.Init(spawnTile, false);
				}
				else if (animalNode is Node3D animalNode3D)
				{
					animalNode3D.GlobalPosition = spawnTile.WorldPosition;
				}
				else
				{
					GD.PrintErr("GameInitialiser: Spawned scene root is not Node3D; cannot set world position.");
				}
			}
		}
	}
#endregion
}
