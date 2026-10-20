using UnityEngine;

/// Common base for doors, drawers, notes and puzzle objects. Found by a centre-screen raycast.
public abstract class Interactable : MonoBehaviour
{
    public string prompt = "Interact";
    public virtual bool CanInteract => true;
    public virtual string Prompt => prompt;
    public abstract void Interact();
}
