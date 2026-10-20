using UnityEngine;

/// A readable note. Reading can set a flag, update the objective and show a hint.
public class NoteInteractable : Interactable
{
    public string noteTitle = "Note";
    [TextArea(4, 12)] public string body = "";
    public string flagOnRead;
    public string objectiveOnRead;
    public string hintOnRead;

    public bool HasBeenRead { get; private set; }

    public override string Prompt => HasBeenRead ? "Read again" : "Read";

    public override void Interact()
    {
        AudioManager.I.Play2D(AudioManager.I.paper, 0.7f);
        UIManager.I.ShowNote(noteTitle, body, OnClosed);
    }

    void OnClosed()
    {
        if (HasBeenRead) return;
        HasBeenRead = true;
        if (!string.IsNullOrEmpty(flagOnRead)) GameManager.I.Set(flagOnRead);
        if (!string.IsNullOrEmpty(objectiveOnRead)) GameManager.I.SetObjective(objectiveOnRead);
        if (!string.IsNullOrEmpty(hintOnRead)) UIManager.I.Hint(hintOnRead, 8f);
    }
}
