using Godot;
using System;

public partial class DayNightManager : Node3D
{
#region Config
	[Export] public float DayLengthSeconds = 240f;
	[Export] public DirectionalLight3D SunLight;
	[Export] public WorldEnvironment WorldEnvironment;

	[Export] public float SunriseStart = 0.2f;
	[Export] public float SunriseEnd = 0.3f;
	[Export] public float SunsetStart = 0.7f;
	[Export] public float SunsetEnd = 0.8f;

	[Export] public float NightLightEnergy = 0.2f;
	[Export] public float DayLightEnergy = 1.2f;
	[Export] public float NightAmbientEnergy = 0.15f;
	[Export] public float DayAmbientEnergy = 0.45f;

	[Export] public Color NightLightColor = new Color(0.35f, 0.45f, 0.7f);
	[Export] public Color DayLightColor = new Color(1f, 0.956f, 0.839f);
	[Export] public Color DawnDuskColor = new Color(1f, 0.7f, 0.45f);
#endregion

#region Signals
	[Signal] public delegate void DayStartedEventHandler();
	[Signal] public delegate void NightStartedEventHandler();
	[Signal] public delegate void TimeOfDayChangedEventHandler(float timeOfDay);
#endregion

#region Events
	public static event Action OnDayStarted;
	public static event Action OnNightStarted;
	public static event Action<float> OnTimeOfDayChanged;
#endregion

#region State
	public float TimeOfDay { get; private set; } = 0f;
	public bool IsNight { get; private set; } = false;

	private bool lastNightState = false;
#endregion

#region Lifecycle
	public override void _Ready()
	{
		if (SunLight == null)
		{
			SunLight = GetNodeOrNull<DirectionalLight3D>("../DirectionalLight3D");
		}

		if (WorldEnvironment == null)
		{
			WorldEnvironment = GetNodeOrNull<WorldEnvironment>("../WorldEnvironment");
		}

		ApplyLighting();
	}

	public override void _Process(double delta)
	{
		if (DayLengthSeconds <= 0f)
			return;

		TimeOfDay = (TimeOfDay + (float)(delta / DayLengthSeconds)) % 1f;

		bool nightNow = TimeOfDay < SunriseStart || TimeOfDay > SunsetEnd;
		if (nightNow != lastNightState)
		{
			lastNightState = nightNow;
			IsNight = nightNow;

			if (IsNight)
			{
				EmitSignal(SignalName.NightStarted);
				OnNightStarted?.Invoke();
			}
			else
			{
				EmitSignal(SignalName.DayStarted);
				OnDayStarted?.Invoke();
			}
		}

		ApplyLighting();
		EmitSignal(SignalName.TimeOfDayChanged, TimeOfDay);
		OnTimeOfDayChanged?.Invoke(TimeOfDay);
	}
#endregion

#region Lighting
	private void ApplyLighting()
	{
		if (SunLight != null)
		{
			float sunAngle = (TimeOfDay * 360f) - 90f;
			SunLight.RotationDegrees = new Vector3(sunAngle, SunLight.RotationDegrees.Y, SunLight.RotationDegrees.Z);

			float dayFactor = GetDayFactor();
			SunLight.LightEnergy = Mathf.Lerp(NightLightEnergy, DayLightEnergy, dayFactor);
			SunLight.LightColor = GetLightColor(dayFactor);
		}

		if (WorldEnvironment != null && WorldEnvironment.Environment != null)
		{
			float dayFactor = GetDayFactor();
		WorldEnvironment.Environment.AmbientLightEnergy =
				Mathf.Lerp(NightAmbientEnergy, DayAmbientEnergy, dayFactor);
		}
	}

	private float GetDayFactor()
	{
		if (TimeOfDay < SunriseStart || TimeOfDay > SunsetEnd)
			return 0f;

		if (TimeOfDay >= SunriseStart && TimeOfDay <= SunriseEnd)
		{
			return Mathf.InverseLerp(SunriseStart, SunriseEnd, TimeOfDay);
		}

		if (TimeOfDay >= SunsetStart && TimeOfDay <= SunsetEnd)
		{
			return 1f - Mathf.InverseLerp(SunsetStart, SunsetEnd, TimeOfDay);
		}

		return 1f;
	}

	private Color GetLightColor(float dayFactor)
	{
		if (dayFactor <= 0f)
			return NightLightColor;

		if (dayFactor >= 1f)
			return DayLightColor;

		float dawnDuskBlend = Mathf.Abs(dayFactor - 0.5f) * 2f;
		Color warm = DawnDuskColor.Lerp(DayLightColor, dawnDuskBlend);
		return NightLightColor.Lerp(warm, dayFactor);
	}
#endregion
}
