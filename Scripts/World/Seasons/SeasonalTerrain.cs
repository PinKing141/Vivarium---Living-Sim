using Godot;
using System;

public partial class SeasonalTerrain : Node3D
{
	[Export] public Node3D StandardMesh; 
	[Export] public Node3D SnowMesh;      

	private SeasonType lastSeason = SeasonType.Spring;

	public override void _Ready()
	{
		UpdateVisuals(WorldManager.CurrentSeason);
	}

	public override void _Process(double delta)
	{
		if (WorldManager.CurrentSeason != lastSeason)
		{
			UpdateVisuals(WorldManager.CurrentSeason);
		}
	}

	private void UpdateVisuals(SeasonType newSeason)
	{
		lastSeason = newSeason;

		// If it is Winter, hide the grass and show the snow
		if (newSeason == SeasonType.Winter)
		{
			if (StandardMesh != null) StandardMesh.Visible = false;
			if (SnowMesh != null) SnowMesh.Visible = true;
		}
		// For Spring, Summer, and Autumn, show the normal ground
		else
		{
			if (StandardMesh != null) StandardMesh.Visible = true;
			if (SnowMesh != null) SnowMesh.Visible = false;
		}
	}
}