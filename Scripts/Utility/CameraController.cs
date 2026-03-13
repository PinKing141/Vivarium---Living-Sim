using Godot;
using System;

public partial class CameraController : Camera3D
{
	[Export] public float PanSpeed = 15.0f;
	[Export] public float ZoomSpeed = 2.0f;
	
	// Limits to stop the camera flying too high or clipping into the ground
	[Export] public float MinHeight = 5.0f; 
	[Export] public float MaxHeight = 40.0f;

	public override void _Process(double delta)
	{
		Vector3 movement = Vector3.Zero;

		// Check for WASD or Arrow Key presses
		if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) movement.Z -= 1;
		if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) movement.Z += 1;
		if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) movement.X -= 1;
		if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) movement.X += 1;

		// Normalize so diagonal movement is not twice as fast
		if (movement != Vector3.Zero)
		{
			movement = movement.Normalized();
		}

		// Apply the movement
		// We use global positional movement so the camera glides flat over the terrain
		Position += movement * PanSpeed * (float)delta;
	}

	public override void _Input(InputEvent @event)
	{
		// Handle mouse wheel scrolling for zooming
		if (@event is InputEventMouseButton mouseEvent && mouseEvent.Pressed)
		{
			Vector3 newPosition = Position;

			if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
			{
				// Zoom In: Move down and slightly forward
				newPosition.Y -= ZoomSpeed;
				newPosition.Z -= ZoomSpeed;
			}
			else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
			{
				// Zoom Out: Move up and slightly backwards
				newPosition.Y += ZoomSpeed;
				newPosition.Z += ZoomSpeed;
			}

			// Clamp the height so we do not zoom infinitely into the void
			newPosition.Y = Mathf.Clamp(newPosition.Y, MinHeight, MaxHeight);
			
			Position = newPosition;
		}
	}
}
