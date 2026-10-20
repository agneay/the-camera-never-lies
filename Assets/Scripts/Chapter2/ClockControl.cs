/// One of the hall clock's controls: hour knob, minute knob, or the winding key.
public class ClockControl : Interactable
{
    public enum Mode { Hour, Minute, Wind }

    public ClockPuzzle clock;
    public Mode mode;

    public override bool CanInteract => clock && !clock.Solved;

    public override string Prompt => mode switch
    {
        Mode.Hour => $"Turn the hour hand   ({clock.TimeText})",
        Mode.Minute => $"Turn the minute hand   ({clock.TimeText})",
        _ => $"Wind the clock at {clock.TimeText}"
    };

    public override void Interact()
    {
        switch (mode)
        {
            case Mode.Hour: clock.AddHour(); break;
            case Mode.Minute: clock.AddMinutes(); break;
            default: clock.Wind(); break;
        }
    }
}
