using Godot;
using System;

public partial class BerryBush : Node3D, IPoolable
{
	#region Exports
	[Export] public int MaxBerries = 5;
	[Export] public float RegrowTime = 30f;
	#endregion

	#region State
	private int currentBerries;
	private float timer = 0f;

	private Node3D bushWithBerries;
	private Node3D bushEmpty;
	#endregion

	#region GodotLifecycle
	public override void _Ready()
	{
		CacheMeshes();

		currentBerries = MaxBerries;

		UpdateVisual();
	}
	#endregion

	#region Pooling
	public void OnAcquireFromPool()
	{
		CacheMeshes();
		currentBerries = MaxBerries;
		timer = 0f;
		UpdateVisual();
	}

	public void OnReleaseToPool()
	{
		timer = 0f;
	}
	#endregion

	// Simulation manager calls this instead of Godot calling _Process
	#region Simulation
	public void SimulationTick(double delta)
	{
		if (currentBerries < MaxBerries)
		{
			timer += (float)delta;

			if (timer >= RegrowTime)
			{
				currentBerries = MaxBerries;
				timer = 0f;
				UpdateVisual();
			}
		}
	}
	#endregion

	#region Food
	public bool HasFood()
	{
		return currentBerries > 0;
	}

	public void EatBerry()
	{
		if (currentBerries <= 0)
			return;

		currentBerries--;

		if (currentBerries == 0)
			UpdateVisual();
	}
	#endregion

	#region Visuals
	private void CacheMeshes()
	{
		if (bushWithBerries == null)
		{
			bushWithBerries = GetNodeOrNull<Node3D>("Bush_WithBerries");
		}

		if (bushEmpty == null)
		{
			bushEmpty = GetNodeOrNull<Node3D>("Bush_NoBerries");
		}
	}

	private void UpdateVisual()
	{
		if (bushWithBerries == null || bushEmpty == null)
		{
			return;
		}

		if (currentBerries > 0)
		{
			bushWithBerries.Visible = true;
			bushEmpty.Visible = false;
		}
		else
		{
			bushWithBerries.Visible = false;
			bushEmpty.Visible = true;
		}
	}
	#endregion
}
