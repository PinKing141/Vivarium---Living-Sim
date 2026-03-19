using Godot;
using System;

public partial class CameraController : Camera3D
{
	#region Exports
	[Export] public float PanSpeed = 15.0f;
	[Export] public float ZoomSpeed = 2.0f;
	[Export] public float FastMoveMultiplier = 3.0f;
	[Export] public float LookSensitivity = 0.15f;
	[Export] public float MinPitch = -80.0f;
	[Export] public float MaxPitch = 80.0f;
	[Export] public bool HoldRightMouseToLook = true;
	[Export] public bool HoldLeftMouseToLook = true;
	[Export] public bool UseHeightClamp = true;
	[Export] public bool OrbitMode = false;
	[Export] public Key OrbitToggleKey = Key.O;
	[Export] public float OrbitDistance = 12.0f;
	[Export] public float OrbitMinDistance = 3.0f;
	[Export] public float OrbitMaxDistance = 80.0f;
	[Export] public float OrbitPanSpeed = 12.0f;
	[Export] public float OrbitFocusPlaneY = 0.0f;
	[Export] public bool HoldMiddleMouseToPan = true;
	[Export] public float PanDragSensitivity = 0.02f;
	[Export] public float TouchLookSensitivity = 0.18f;
	[Export] public float TouchZoomSensitivity = 12.0f;
	[Export] public bool InvertPanY = true;
	
	// Limits to stop the camera flying too high or clipping into the ground
	[Export] public float MinHeight = 5.0f; 
	[Export] public float MaxHeight = 40.0f;
	#endregion

	#region State
	private bool isLooking = false;
	private bool isPanning = false;
	private float yaw;
	private float pitch;
	private Vector3 focusPoint;
	#endregion

	#region GodotLifecycle
	public override void _Ready()
	{
		yaw = Rotation.Y;
		pitch = Rotation.X;
		focusPoint = GlobalPosition + (-GlobalTransform.Basis.Z) * OrbitDistance;
	}

	public override void _Process(double delta)
	{
		if (OrbitMode)
		{
			Vector3 movement = Vector3.Zero;
			Vector3 forward = -GlobalTransform.Basis.Z;
			forward.Y = 0f;
			forward = forward == Vector3.Zero ? Vector3.Forward : forward.Normalized();
			Vector3 right = GlobalTransform.Basis.X;
			right.Y = 0f;
			right = right == Vector3.Zero ? Vector3.Right : right.Normalized();

			if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) movement += forward;
			if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) movement -= forward;
			if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) movement -= right;
			if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) movement += right;
			if (Input.IsKeyPressed(Key.E)) movement += Vector3.Up;
			if (Input.IsKeyPressed(Key.Q)) movement -= Vector3.Up;

			if (movement != Vector3.Zero)
			{
				movement = movement.Normalized();
			}

			float speed = OrbitPanSpeed;
			if (Input.IsKeyPressed(Key.Shift))
			{
				speed *= FastMoveMultiplier;
			}

			focusPoint += movement * speed * (float)delta;

			Vector3 direction = new Vector3(
				Mathf.Cos(pitch) * Mathf.Sin(yaw),
				Mathf.Sin(pitch),
				Mathf.Cos(pitch) * Mathf.Cos(yaw)
			).Normalized();

			float distance = Mathf.Clamp(OrbitDistance, OrbitMinDistance, OrbitMaxDistance);
			OrbitDistance = distance;
			GlobalPosition = focusPoint - direction * OrbitDistance;
			Rotation = new Vector3(pitch, yaw, 0f);

			if (UseHeightClamp)
			{
				Vector3 clamped = Position;
				clamped.Y = Mathf.Clamp(clamped.Y, MinHeight, MaxHeight);
				Position = clamped;
			}
		}
		else
		{
			Vector3 movement = Vector3.Zero;
			Vector3 forward = -GlobalTransform.Basis.Z;
			forward.Y = 0f;
			forward = forward == Vector3.Zero ? Vector3.Forward : forward.Normalized();
			Vector3 right = GlobalTransform.Basis.X;
			right.Y = 0f;
			right = right == Vector3.Zero ? Vector3.Right : right.Normalized();
			Vector3 up = Vector3.Up;

			// Check for WASD or Arrow Key presses
			if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up)) movement += forward;
			if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down)) movement -= forward;
			if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left)) movement -= right;
			if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) movement += right;
			if (Input.IsKeyPressed(Key.E)) movement += up;
			if (Input.IsKeyPressed(Key.Q)) movement -= up;

			// Normalize so diagonal movement is not twice as fast
			if (movement != Vector3.Zero)
			{
				movement = movement.Normalized();
			}

			float speed = PanSpeed;
			if (Input.IsKeyPressed(Key.Shift))
			{
				speed *= FastMoveMultiplier;
			}

			Position += movement * speed * (float)delta;

			if (UseHeightClamp)
			{
				Vector3 clamped = Position;
				clamped.Y = Mathf.Clamp(clamped.Y, MinHeight, MaxHeight);
				Position = clamped;
			}
		}
	}
	#endregion

	#region Input
	public override void _Input(InputEvent @event)
	{
		if (@event is InputEventKey toggleKey && toggleKey.Pressed && toggleKey.Keycode == OrbitToggleKey)
		{
			OrbitMode = !OrbitMode;
			if (OrbitMode)
			{
				focusPoint = GetFocusPointFromView();
				Vector3 toCamera = GlobalPosition - focusPoint;
				OrbitDistance = Mathf.Clamp(toCamera.Length(), OrbitMinDistance, OrbitMaxDistance);
				Vector3 dir = toCamera.Normalized();
				pitch = Mathf.Asin(dir.Y);
				yaw = Mathf.Atan2(dir.X, dir.Z);
			}
		}

		if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
		{
			isLooking = false;
			isPanning = false;
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}

		// Handle mouse buttons
		if (@event is InputEventMouseButton mouseEvent)
		{
			if (mouseEvent.ButtonIndex == MouseButton.Right && HoldRightMouseToLook)
			{
				isLooking = mouseEvent.Pressed;
				Input.MouseMode = mouseEvent.Pressed
					? Input.MouseModeEnum.Captured
					: Input.MouseModeEnum.Visible;
			}

			if (mouseEvent.ButtonIndex == MouseButton.Left && HoldLeftMouseToLook)
			{
				isLooking = mouseEvent.Pressed;
			}

			if (mouseEvent.ButtonIndex == MouseButton.Middle && HoldMiddleMouseToPan)
			{
				isPanning = mouseEvent.Pressed;
			}

			if (mouseEvent.Pressed)
			{
				// Handle mouse wheel scrolling for zooming
				Vector3 newPosition = Position;

				if (mouseEvent.ButtonIndex == MouseButton.WheelUp)
				{
					if (OrbitMode)
					{
						OrbitDistance = Mathf.Max(OrbitMinDistance, OrbitDistance - ZoomSpeed);
					}
					else
					{
						newPosition += -GlobalTransform.Basis.Z * ZoomSpeed;
					}
				}
				else if (mouseEvent.ButtonIndex == MouseButton.WheelDown)
				{
					if (OrbitMode)
					{
						OrbitDistance = Mathf.Min(OrbitMaxDistance, OrbitDistance + ZoomSpeed);
					}
					else
					{
						newPosition -= -GlobalTransform.Basis.Z * ZoomSpeed;
					}
				}

				// Clamp the height so we do not zoom infinitely into the void
				if (!OrbitMode && UseHeightClamp)
				{
					newPosition.Y = Mathf.Clamp(newPosition.Y, MinHeight, MaxHeight);
				}

				if (!OrbitMode)
				{
					Position = newPosition;
				}
			}
		}

		if (@event is InputEventMouseMotion motion && (!HoldRightMouseToLook || isLooking))
		{
			ApplyLook(motion.Relative, LookSensitivity);
		}

		if (@event is InputEventMouseMotion panMotion && isPanning && !isLooking)
		{
			ApplyPan(panMotion.Relative, PanDragSensitivity);
		}

		if (@event is InputEventPanGesture panGesture)
		{
			ApplyLook(panGesture.Delta, TouchLookSensitivity);
		}

		if (@event is InputEventMagnifyGesture magnifyGesture)
		{
			float delta = (1f - magnifyGesture.Factor) * TouchZoomSensitivity;
			if (OrbitMode)
			{
				OrbitDistance = Mathf.Clamp(OrbitDistance + delta, OrbitMinDistance, OrbitMaxDistance);
			}
			else
			{
				Position += -GlobalTransform.Basis.Z * delta;
			}
		}
	}
	#endregion

	#region Helpers
	private void ApplyLook(Vector2 delta, float sensitivity)
	{
		float yawDelta = Mathf.DegToRad(delta.X * sensitivity);
		float pitchDelta = Mathf.DegToRad(delta.Y * sensitivity);

		yaw -= yawDelta;
		pitch -= pitchDelta;

		float minPitchRad = Mathf.DegToRad(MinPitch);
		float maxPitchRad = Mathf.DegToRad(MaxPitch);
		pitch = Mathf.Clamp(pitch, minPitchRad, maxPitchRad);

		Rotation = new Vector3(pitch, yaw, 0f);
	}

	private void ApplyPan(Vector2 delta, float sensitivity)
	{
		float x = delta.X * sensitivity;
		float y = delta.Y * sensitivity * (InvertPanY ? -1f : 1f);

		Vector3 right = GlobalTransform.Basis.X;
		right.Y = 0f;
		if (right != Vector3.Zero)
		{
			right = right.Normalized();
		}

		Vector3 forward = -GlobalTransform.Basis.Z;
		forward.Y = 0f;
		if (forward != Vector3.Zero)
		{
			forward = forward.Normalized();
		}

		Vector3 pan = right * x + forward * y;

		if (OrbitMode)
		{
			focusPoint += pan;
		}
		else
		{
			Position += pan;
		}
	}

	private Vector3 GetFocusPointFromView()
	{
		Vector3 origin = GlobalPosition;
		Vector3 dir = -GlobalTransform.Basis.Z;

		if (Mathf.Abs(dir.Y) < 0.0001f)
		{
			return origin + dir * OrbitDistance;
		}

		float t = (OrbitFocusPlaneY - origin.Y) / dir.Y;
		if (t > 0f)
		{
			return origin + dir * t;
		}

		return origin + dir * OrbitDistance;
	}
	#endregion
}
