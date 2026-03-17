using Godot;
using System;

public partial class SeasonalTerrain : Node3D
{
	#region Exports
	[Export] public Node3D StandardMesh;
	[Export] public Node3D SnowMesh;
	#endregion

	#region State
	private SeasonType lastSeason = SeasonType.Spring;
	#endregion

	#region GodotLifecycle
	public override void _Ready()
	{
		UpdateVisuals(SeasonManager.CurrentSeason);
	}

	public override void _Process(double delta)
	{
		if (SeasonManager.CurrentSeason != lastSeason)
		{
			UpdateVisuals(SeasonManager.CurrentSeason);
		}
	}
	#endregion

	#region Visuals
	private void UpdateVisuals(SeasonType newSeason)
	{
		lastSeason = newSeason;

		if (newSeason == SeasonType.Winter)
		{
			if (StandardMesh != null) StandardMesh.Visible = false;
			if (SnowMesh != null) SnowMesh.Visible = true;
		}
		else
		{
			if (StandardMesh != null) StandardMesh.Visible = true;
			if (SnowMesh != null) SnowMesh.Visible = false;
		}
	}
	#endregion
}
