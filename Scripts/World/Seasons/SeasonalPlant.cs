using Godot;
using System;

public partial class SeasonalPlant : Node3D
{
	// --- SEASONAL MODELS ---
	[Export] public Node3D SpringSummerMesh; // Your standard green tree
	[Export] public Node3D AutumnMesh;       // Your orange/red tree
	[Export] public Node3D WinterMesh;       // Your snow-covered tree

	// We track the last season so we only update the visuals when a change actually happens
	private SeasonType lastSeason = SeasonType.Spring;

	public override void _Ready()
	{
		// Set the correct visual state the moment the plant spawns
		UpdateVisuals(WorldManager.CurrentSeason);
	}

	public override void _Process(double delta)
	{
		// Check if the global season has changed since the last frame
		if (WorldManager.CurrentSeason != lastSeason)
		{
			UpdateVisuals(WorldManager.CurrentSeason);
		}
	}

	private void UpdateVisuals(SeasonType newSeason)
	{
		lastSeason = newSeason;

		// First, hide everything
		if (SpringSummerMesh != null) SpringSummerMesh.Visible = false;
		if (AutumnMesh != null) AutumnMesh.Visible = false;
		if (WinterMesh != null) WinterMesh.Visible = false;

		// Then, reveal only the correct season
		switch (newSeason)
		{
			case SeasonType.Spring:
			case SeasonType.Summer:
				if (SpringSummerMesh != null) SpringSummerMesh.Visible = true;
				break;
			case SeasonType.Autumn:
				if (AutumnMesh != null) AutumnMesh.Visible = true;
				break;
			case SeasonType.Winter:
				if (WinterMesh != null) WinterMesh.Visible = true;
				break;
		}
	}
}