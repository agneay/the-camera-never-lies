using UnityEngine;

/// Per-scene chapter data: titles, opening objective, film budget and what comes next.
public class ChapterConfig : MonoBehaviour
{
    public static ChapterConfig I;

    public int number = 1;
    public string title = "The Empty Studio";
    public string startObjective = "";
    public string startHint = "";
    public int startFilm = -1;          // -1 = unlimited
    public float clockStartMinutes = 23 * 60 + 47;
    public bool startAfterMidnight;
    public string nextScene = "";
    public string nextTitle = "";
    public string endLine1 = "The camera never lies.";
    public string endLine2 = "— does it?";
    public string teaser = "";

    void Awake() => I = this;

    public static string NumberWord(int n) => n switch { 1 => "ONE", 2 => "TWO", 3 => "THREE", _ => n.ToString() };
}
