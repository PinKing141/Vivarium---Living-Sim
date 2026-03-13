using Godot;
using System;

public partial class BerryBush : Node3D
{
	[Export] public int MaxBerries = 5;
	[Export] public float RegrowTime = 30f;

	private int currentBerries;
	private float timer = 0f;

	private Node3D bushWithBerries;
	private Node3D bushEmpty;

	public override void _Ready()
	{
		bushWithBerries = GetNode<Node3D>("Bush_WithBerries");
		bushEmpty = GetNode<Node3D>("Bush_NoBerries");

		currentBerries = MaxBerries;

		UpdateVisual();
	}

	public override void _Process(double delta)
	{
		if (currentBerries < MaxBerries)
		{
			timer += (float)delta;

			if (timer >= RegrowTime)
			{
				currentBerries = MaxBerries;
				timer = 0;
				UpdateVisual();
			}
		}
	}

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

	private void UpdateVisual()
	{
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
}
