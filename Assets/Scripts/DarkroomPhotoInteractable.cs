/// The still-developing photograph that triggers the Chapter 1 ending.
public class DarkroomPhotoInteractable : Interactable
{
    bool used;

    public override bool CanInteract => !used;
    public override string Prompt => "Examine the photograph";

    public override void Interact()
    {
        used = true;
        HorrorDirector.I.BeginEnding();
    }
}
