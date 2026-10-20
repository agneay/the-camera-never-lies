using UnityEngine;

/// A collectible item (a key, a fuse, a roll of film). Sets a flag and broadcasts the pickup to the chapter director.
public class PickupInteractable : Interactable
{
    public static event System.Action<string> Picked;

    public string flag = "item";
    public string itemName = "item";
    public string message = "";
    public string objectiveOnPickup;
    public int film;
    public bool available = true;

    public override bool CanInteract => available;
    public override string Prompt => "Take " + itemName;

    public override void Interact()
    {
        GameManager.I.Set(flag);
        AudioManager.I.PlayAt(AudioManager.I.click, transform.position, 0.8f, 0.5f);
        if (film > 0) CameraTool.I.AddFilm(film);
        if (!string.IsNullOrEmpty(message)) UIManager.I.Toast(message, 4f);
        if (!string.IsNullOrEmpty(objectiveOnPickup)) GameManager.I.SetObjective(objectiveOnPickup);
        gameObject.SetActive(false);
        Picked?.Invoke(flag);
    }
}
