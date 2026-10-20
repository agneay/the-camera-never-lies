using System.Collections;
using UnityEngine;

/// "The Missing Person": the lens shows a fifth person, ADA, in the Kerr family photo.
/// The tenant ledger gives Ada Kerr's mailbox. Every wrong box is a jump scare and a hunt.
public class MailboxInteractable : Interactable
{
    public static bool Solved;
    public static string Answer = "3C";

    public string id = "1A";
    public Transform door;

    bool opened;

    void Awake() => Solved = false;

    public override bool CanInteract => !opened && !Solved;
    public override string Prompt => $"Open mailbox {id}";

    public override void Interact()
    {
        opened = true;
        StartCoroutine(Swing());
        if (id == Answer)
        {
            Solved = true;
            GameManager.I.Set("stair_key");
            UIManager.I.Toast("Inside, wrapped in a letter to Ada: a key stamped STAIRWELL.", 5f);
            Chapter2Director.I.OnKeyFound();
        }
        else
        {
            Chapter2Director.I.WrongAnswer(transform.position, "Something inside the box grabs at your fingers.");
        }
    }

    IEnumerator Swing()
    {
        AudioManager.I.PlayAt(AudioManager.I.creak, transform.position, 0.4f, 1f, 1.8f);
        Quaternion a = door.localRotation, b = a * Quaternion.Euler(100f, 0, 0);
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.35f)
        {
            door.localRotation = Quaternion.Slerp(a, b, t);
            yield return null;
        }
    }
}
