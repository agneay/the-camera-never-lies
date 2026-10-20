using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PhotoEntry
{
    public Texture2D tex;
    public string title;
    public string desc;
    public string time;
    public Vector3 pos;
    public Quaternion rot;
    public float fov;
    public readonly List<string> clues = new List<string>();
    public bool isNew = true;
    public bool special;   // altered by a supernatural event
}

/// Stores photographs and discovered clue IDs. Kept separate from puzzle logic so one photo can serve many uses.
public class EvidenceManager : MonoBehaviour
{
    public static EvidenceManager I;

    public readonly List<PhotoEntry> Photos = new List<PhotoEntry>();
    public readonly HashSet<string> Found = new HashSet<string>();
    public int TotalClues { get; private set; }

    const int MaxPhotos = 30;

    void Awake() => I = this;

    void Start()
    {
        TotalClues = FindObjectsByType<Photographable>(FindObjectsInactive.Include)
            .Where(p => p.countsAsClue).Select(p => p.clueId).Distinct().Count();
    }

    public int NewCount => Photos.Count(p => p.isNew);

    public PhotoEntry AddPhoto(Texture2D tex, List<Photographable> targets, Vector3 pos, Quaternion rot, float fov)
    {
        var e = new PhotoEntry { tex = tex, pos = pos, rot = rot, fov = fov, time = GameManager.I.ClockText };
        bool newClue = false;

        if (targets.Count > 0)
        {
            e.title = targets[0].title;
            e.desc = string.Join("\n\n", targets.Select(t => t.description));
            foreach (var t in targets)
            {
                e.clues.Add(t.clueId);
                if (t.countsAsClue && Found.Add(t.clueId)) newClue = true;
            }
            if (targets.Count > 1) e.title += $"  (+{targets.Count - 1})";
        }
        else
        {
            e.title = "Untitled photograph";
            e.desc = "Nothing unusual. Nothing you can see, anyway.";
        }

        Photos.Add(e);
        Trim();
        if (newClue) UIManager.I.Toast("New evidence recorded   [Tab] Evidence");
        return e;
    }

    public PhotoEntry AddSpecial(Texture2D tex, string title, string desc)
    {
        var e = new PhotoEntry { tex = tex, title = title, desc = desc, time = GameManager.I.ClockText, special = true };
        Photos.Add(e);
        return e;
    }

    void Trim()
    {
        while (Photos.Count > MaxPhotos)
        {
            var junk = Photos.FirstOrDefault(p => p.clues.Count == 0 && !p.special);
            if (junk == null) break;
            Photos.Remove(junk);
            Destroy(junk.tex);
        }
    }
}
