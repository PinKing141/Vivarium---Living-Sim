using Godot;
using System;

public partial class SeasonalPlant : Node3D
{
	#region Exports
	// --- SEASONAL MODELS ---
	[Export] public Node3D SpringSummerMesh; // Green tree
	[Export] public Node3D AutumnMesh;       // Orange/red tree
	[Export] public Node3D WinterMesh;       // Snow tree
	#endregion

	#region State
	private SeasonType lastSeason = SeasonType.Spring;
	#endregion

	#region GodotLifecycle
	public override void _Ready()
	{
		// Set correct appearance immediately
		UpdateVisuals(SeasonManager.CurrentSeason);
	}

	public override void _Process(double delta)
	{
		// Only update when season actually changes
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

		if (SpringSummerMesh != null) SpringSummerMesh.Visible = false;
		if (AutumnMesh != null) AutumnMesh.Visible = false;
		if (WinterMesh != null) WinterMesh.Visible = false;

		switch (newSeason)
		{
			case SeasonType.Spring:
			case SeasonType.Summer:
				if (SpringSummerMesh != null)
					SpringSummerMesh.Visible = true;
				break;

			case SeasonType.Autumn:
				if (AutumnMesh != null)
					AutumnMesh.Visible = true;
				break;

			case SeasonType.Winter:
				if (WinterMesh != null)
					WinterMesh.Visible = true;
				break;
		}
	}
	#endregion
}
