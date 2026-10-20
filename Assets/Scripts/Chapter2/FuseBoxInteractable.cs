/// Restores power to the stairwell lock — and wakes the Figure for good.
public class FuseBoxInteractable : Interactable
{
    bool done;

    public override bool CanInteract => !done;
    public override string Prompt => GameManager.I.Has("fuse") ? "Insert the fuse" : "Open the fuse box";

    public override void Interact()
    {
        if (!GameManager.I.Has("fuse"))
        {
            AudioManager.I.PlayAt(AudioManager.I.rattle, transform.position, 0.6f);
            UIManager.I.Toast("The fuse box is dead. One fuse is missing.");
            return;
        }
        done = true;
        GameManager.I.Set("power");
        AudioManager.I.PlayAt(AudioManager.I.unlock, transform.position, 1f);
        Chapter2Director.I.PowerRestored();
    }
}
