using UnityEngine;

/// One digit wheel of the cabinet's combination lock.
public class DialInteractable : Interactable
{
    public CombinationLock lockRef;
    public TextMesh label;
    public int value;

    public override bool CanInteract => lockRef != null && !lockRef.Solved;
    public override string Prompt => $"Turn dial  ({value})";

    public override void Interact()
    {
        value = (value + 1) % 10;
        if (label) label.text = value.ToString();
        AudioManager.I.PlayAt(AudioManager.I.click, transform.position, 0.7f, 0.5f);
        lockRef.Check();
    }
}
