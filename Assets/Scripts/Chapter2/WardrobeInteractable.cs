using System.Collections;
using UnityEngine;

/// Film is inside. So is something else.
public class WardrobeInteractable : Interactable
{
    public Transform doorL, doorR;
    public int film = 3;

    bool opened;

    public override bool CanInteract => !opened;
    public override string Prompt => "Open the wardrobe";

    public override void Interact()
    {
        opened = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        AudioManager.I.PlayAt(AudioManager.I.creak, transform.position + Vector3.up, 0.6f);
        Quaternion la = doorL.localRotation, ra = doorR.localRotation;
        Quaternion lb = la * Quaternion.Euler(0, 100, 0), rb = ra * Quaternion.Euler(0, -100, 0);
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.18f)
        {
            doorL.localRotation = Quaternion.Slerp(la, lb, t);
            doorR.localRotation = Quaternion.Slerp(ra, rb, t);
            yield return null;
        }
        Chapter2Director.I.Scare(0.55f);
        yield return new WaitForSeconds(0.9f);
        CameraTool.I.AddFilm(film);
        UIManager.I.Subtitle("[Inside: three rolls of film. And something that was waiting.]", 4f);
    }
}
