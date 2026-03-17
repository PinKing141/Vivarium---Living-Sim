using Godot;
using System;

public partial class SeasonManager : Node
{
#region State
    // Keeping this static for now so other scripts can easily read the current season
    public static SeasonType CurrentSeason { get; private set; } = SeasonType.Spring;
    
    [Export] public float SeasonDuration = 60f;
    private float seasonTimer = 0f;
#endregion
    
#region Signals
    [Signal] public delegate void SeasonChangedEventHandler(SeasonType newSeason);
#endregion
    
#region Lifecycle
    public override void _Process(double delta)
    {
        seasonTimer += (float)delta;
        if (seasonTimer >= SeasonDuration)
        {
            seasonTimer = 0f;
            AdvanceSeason();
        }
    }
#endregion
    
#region Logic
    private void AdvanceSeason()
    {
        CurrentSeason = (SeasonType)(((int)CurrentSeason + 1) % 4);
        
        // Emit the signal so UI and plants can listen to it
        EmitSignal(SignalName.SeasonChanged, Variant.From(CurrentSeason));
        
        GD.Print("The season is now: " + CurrentSeason.ToString());
    }
#endregion
}
