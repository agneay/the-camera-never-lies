using System.Collections;
using UnityEngine;

/// "The Wrong Time": set the hall clock to the hour Ada stopped it.
/// The live lens shows 9:20 — but it's after midnight, and photographs after midnight lie.
/// The one print taken before midnight (in the kitchen) shows the real time: 3:40.
/// Winding it at the wrong time angers the Figure.
public class ClockPuzzle : MonoBehaviour
{
    public Transform hourHand, minuteHand;
    public Transform door;
    public float doorOpenAngle = -110f;
    public PickupInteractable reward;
    public int targetHour = 3, targetMinute = 40;

    public int Hour { get; private set; } = 12;
    public int Minute { get; private set; }
    public bool Solved { get; private set; }

    void Start()
    {
        reward.available = false;
        Apply();
    }

    public void AddHour() { Hour = Hour % 12 + 1; Apply(); Tick(); }
    public void AddMinutes() { Minute = (Minute + 5) % 60; Apply(); Tick(); }

    void Tick() => AudioManager.I.PlayAt(AudioManager.I.click, hourHand.position, 0.6f, 0.5f);

    void Apply()
    {
        minuteHand.localRotation = Quaternion.Euler(0, 0, -Minute * 6f);
        hourHand.localRotation = Quaternion.Euler(0, 0, -((Hour % 12) * 30f + Minute * 0.5f));
    }

    public string TimeText => $"{Hour}:{Minute:00}";

    public void Wind()
    {
        if (Solved) return;
        if (Hour % 12 == targetHour % 12 && Minute == targetMinute)
        {
            Solved = true;
            StartCoroutine(Open());
        }
        else
        {
            AudioManager.I.PlayAt(AudioManager.I.chime, hourHand.position, 1f, 3f, 0.8f);
            Chapter2Director.I.WrongAnswer(transform.position + Vector3.up * 1.5f, "The clock strikes thirteen.");
        }
    }

    IEnumerator Open()
    {
        var am = AudioManager.I;
        am.PlayAt(am.chime, hourHand.position, 1f, 3f);
        UIManager.I.Toast("The pendulum swings once. Something unlatches.");
        yield return new WaitForSeconds(1.2f);
        am.PlayAt(am.creak, door.position, 0.7f);
        Quaternion a = door.localRotation, b = a * Quaternion.Euler(0, doorOpenAngle, 0);
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.9f)
        {
            door.localRotation = Quaternion.Slerp(a, b, Mathf.SmoothStep(0, 1, t));
            yield return null;
        }
        reward.available = true;
        GameManager.I.SetObjective("Take the fuse from inside the clock.");
    }
}
