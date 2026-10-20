/// The only way down: needs power for the electric lock and the stairwell key.
public class StairDoorInteractable : DoorInteractable
{
    public override string Prompt => "Open the stairwell door";

    public override void Interact()
    {
        var gm = GameManager.I;
        bool power = gm.Has("power"), key = gm.Has("stair_key");
        if (power && key)
        {
            AudioManager.I.PlayAt(AudioManager.I.unlock, transform.position, 1f);
            Open();
            Chapter2Director.I.Escape();
            return;
        }
        AudioManager.I.PlayAt(AudioManager.I.rattle, transform.position, 0.8f);
        string msg = !power && !key ? "Locked. The electric lock is dead, and it needs a key."
                   : !power ? "The key turns, but the electric lock has no power."
                   : "The lock hums with power. It still needs a key.";
        UIManager.I.Toast(msg, 3.5f);
    }
}
