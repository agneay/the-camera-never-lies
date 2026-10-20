using System.Collections;
using UnityEngine;

/// "The Four Portraits": the code is the camera-only numbers on the portraits, ordered oldest sitter first.
/// Wrong combinations do nothing harmful, so the puzzle can always be retried.
public class CombinationLock : MonoBehaviour
{
    public DialInteractable[] dials;
    public int[] code = { 7, 2, 9, 4 };
    public Transform drawer;
    public Vector3 openOffset = new Vector3(0f, 0f, 0.45f);
    public PickupInteractable reward;

    public bool Solved { get; private set; }

    void Start()
    {
        if (reward) reward.available = false;
    }

    public void Check()
    {
        if (Solved) return;
        for (int i = 0; i < dials.Length; i++)
            if (dials[i].value != code[i]) return;
        Solved = true;
        StartCoroutine(Open());
    }

    IEnumerator Open()
    {
        yield return new WaitForSeconds(0.35f);
        AudioManager.I.PlayAt(AudioManager.I.unlock, transform.position + Vector3.up * 0.6f, 1f);
        UIManager.I.Toast("Something clicks inside the cabinet.");
        yield return new WaitForSeconds(0.4f);
        AudioManager.I.PlayAt(AudioManager.I.drawer, transform.position + Vector3.up * 0.9f, 0.9f);

        Vector3 a = drawer.localPosition, b = a + openOffset;
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.8f)
        {
            drawer.localPosition = Vector3.Lerp(a, b, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        drawer.localPosition = b;

        GameManager.I.Set("drawer_open");
        if (reward) reward.available = true;
        GameManager.I.SetObjective("Take what was hidden in the drawer.");
    }
}
