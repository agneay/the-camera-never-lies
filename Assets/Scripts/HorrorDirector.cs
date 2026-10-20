using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// Triggers authored audio/visual events from player progress. No AI: just explicit, once-only conditions.
///  1. Studio: a shutter clicks behind the player and the lights stutter.
///  2. Corridor (signature scare): the figure is at the end of the corridor in the viewfinder; the saved photo
///     then changes to show it standing directly behind the player.
///  3. Key pickup (delayed scare): an earlier photograph in the evidence gallery now contains the figure.
///  4. Darkroom (ending): a still-wet print shows the player right now, and something across the table.
///  Plus an adaptive touch: obsessive camera raising earns footsteps behind you.
public class HorrorDirector : MonoBehaviour
{
    public static HorrorDirector I;

    public TriggerZone studioZone, corridorZone, darkroomZone;
    public FlickerLight[] studioLights;
    public GameObject corridorFigure, behindFigure, hauntFigure, endingFigure;
    public Light flashLight;
    public Transform corridorEnd;

    bool studioDone, corridorHintDone, corridorScareDone, darkroomEntered, endingStarted;
    bool behindActive;
    float behindTimer, behindSeen;
    readonly List<float> raiseTimes = new List<float>();
    float lastAdaptive = -999f;

    void Awake() => I = this;

    void Start()
    {
        CameraTool.I.OnCapture += OnCapture;
        CameraTool.I.OnRaise += OnRaise;
        PickupInteractable.Picked += OnPickup;
        behindFigure.SetActive(false);
        hauntFigure.SetActive(false);
        endingFigure.SetActive(false);
        flashLight.enabled = false;
    }

    Vector3 PlayerPoint => PlayerController.I.Position + Vector3.up;

    void Update()
    {
        if (behindActive) UpdateBehindFigure();
        if (!GameManager.I.IsPlaying) return;

        if (!studioDone && studioZone.Contains(PlayerPoint)) StartCoroutine(StudioEvent());
        if (!corridorHintDone && corridorZone.Contains(PlayerPoint)) StartCoroutine(CorridorHint());
        if (!darkroomEntered && darkroomZone.Contains(PlayerPoint))
        {
            darkroomEntered = true;
            GameManager.I.SetObjective("Examine the photograph left on the developing table.");
        }
    }

    // ---------- Event 1: studio ----------

    IEnumerator StudioEvent()
    {
        studioDone = true;
        GameManager.I.Set("studio_entered");
        GameManager.I.SetObjective("Search the studio.");
        yield return new WaitForSeconds(2.8f);

        var p = PlayerController.I;
        var am = AudioManager.I;
        am.PlayAt(am.shutter, p.HeadPosition - p.FlatForward * 2.5f, 1f, 2f);
        UIManager.I.Subtitle("[A camera shutter clicks behind you]");
        foreach (var l in studioLights) l.Burst(1.3f);
        yield return new WaitForSeconds(0.6f);
        am.PlayAt(am.whoosh, p.HeadPosition - p.FlatForward * 2f, 0.4f);
    }

    // ---------- Event 2: corridor ----------

    IEnumerator CorridorHint()
    {
        corridorHintDone = true;
        yield return new WaitForSeconds(1.2f);
        AudioManager.I.PlayAt(AudioManager.I.shutter, corridorEnd.position, 0.9f, 3f);
        UIManager.I.Subtitle("[A shutter clicks at the far end of the corridor]");
    }

    void OnCapture(PhotoEntry entry)
    {
        if (corridorScareDone || !corridorZone.Contains(PlayerPoint)) return;
        Vector3 toEnd = corridorEnd.position - PlayerController.I.HeadPosition;
        toEnd.y = 0;
        if (Vector3.Dot(PlayerController.I.FlatForward, toEnd.normalized) < 0.6f) return;
        StartCoroutine(CorridorScare(entry));
    }

    IEnumerator CorridorScare(PhotoEntry entry)
    {
        corridorScareDone = true;
        entry.special = true;
        entry.title = "Corridor";
        entry.desc = "A person at the far end of the corridor, facing the camera. When I lowered the camera, the corridor was empty.";
        corridorFigure.SetActive(false); // it's in the photo, and nowhere else
        GameManager.I.StrikeMidnight();
        entry.time = "00:00";

        var am = AudioManager.I;
        am.SetDrone(true);
        UIManager.I.ShowReview(entry, 4.8f, true);
        yield return new WaitForSeconds(1.9f);

        // Re-render the photograph from *in front of* the player, with the figure right behind them.
        var p = PlayerController.I;
        Vector3 fwd = p.FlatForward;
        Vector3 head = p.HeadPosition;
        behindFigure.transform.position = p.Position - fwd * 0.75f;
        behindFigure.transform.rotation = Quaternion.LookRotation(fwd);
        behindFigure.SetActive(true);

        float dist = 2.3f;
        if (Physics.Raycast(head, fwd, out var hit, dist, Layers.NormalViewMask, QueryTriggerInteraction.Ignore))
            dist = Mathf.Max(0.9f, hit.distance - 0.2f);
        Vector3 camPos = head + fwd * dist + Vector3.up * 0.05f;
        Quaternion rot = Quaternion.LookRotation(head - Vector3.up * 0.2f - camPos);
        var tex = PhotoRenderer.I.Render(camPos, rot, 58f, Layers.ViewfinderMask | Layers.PlayerBodyMask);

        Texture2D old = entry.tex;
        entry.tex = tex;
        Destroy(old);
        entry.title = "Corridor  (changed)";
        entry.desc = "When I took this, there was someone at the far end of the corridor.\n\n" +
                     "Now the photograph shows ME, from the front. And it is standing right behind me.";
        entry.isNew = true;

        am.Play2D(am.sting, 0.9f);
        CameraTool.I.effect.GlitchPulse(1f, 0.6f);
        am.PlayAt(am.whisper, p.HeadPosition - fwd * 0.6f, 0.9f, 1f);
        UIManager.I.Subtitle("[Breathing, very close behind you]", 4f);

        behindActive = true;
        behindTimer = 12f;
        behindSeen = 0f;
        yield return new WaitForSeconds(3f);
        UIManager.I.Hint("Don't trust any photograph taken after midnight.", 5f);
    }

    void UpdateBehindFigure()
    {
        behindTimer -= Time.deltaTime;
        var cam = CameraTool.I;
        bool seen = false;
        if (cam.Raised)
        {
            Vector3 v = cam.cam.WorldToViewportPoint(behindFigure.transform.position + Vector3.up * 1.7f);
            seen = v.z > 0f && v.x > 0.2f && v.x < 0.8f && v.y > 0.1f && v.y < 0.9f;
        }
        if (seen) behindSeen += Time.deltaTime;

        if (behindSeen > 0.5f || behindTimer <= 0f)
        {
            behindActive = false;
            behindFigure.SetActive(false);
            var am = AudioManager.I;
            if (behindSeen > 0.5f)
            {
                am.PlayAt(am.whoosh, behindFigure.transform.position + Vector3.up * 1.5f, 1f);
                cam.effect.GlitchPulse(0.8f, 0.4f);
            }
            StartCoroutine(DroneOffLater(6f));
        }
    }

    IEnumerator DroneOffLater(float t)
    {
        yield return new WaitForSeconds(t);
        if (!endingStarted) AudioManager.I.SetDrone(false);
    }

    // ---------- Event 3: delayed / misleading image ----------

    void OnDestroy() => PickupInteractable.Picked -= OnPickup;

    public void OnPickup(string flag)
    {
        if (flag == "darkroom_key") StartCoroutine(HauntPhoto());
    }

    IEnumerator HauntPhoto()
    {
        yield return new WaitForSeconds(3.5f);
        var candidates = EvidenceManager.I.Photos.Where(e => !e.special && e.fov > 0f).ToList();
        if (candidates.Count == 0) yield break;

        // Pick the earlier photo with the most open space to "stand" in.
        PhotoEntry best = null;
        float bestDist = -1f;
        Vector3 bestPos = Vector3.zero;
        foreach (var e in candidates)
        {
            Vector3 f = e.rot * Vector3.forward;
            f.y = 0;
            if (f.sqrMagnitude < 0.01f) continue;
            f.Normalize();
            float d = 6f;
            if (Physics.Raycast(e.pos, f, out var hit, 6f, Layers.NormalViewMask, QueryTriggerInteraction.Ignore))
                d = hit.distance - 0.6f;
            if (d > bestDist) { bestDist = d; best = e; bestPos = e.pos + f * Mathf.Clamp(d, 1.2f, 4.5f); }
        }
        if (best == null) yield break;

        bestPos.y = 0f;
        hauntFigure.transform.position = bestPos;
        Vector3 look = best.pos - bestPos;
        look.y = 0;
        hauntFigure.transform.rotation = Quaternion.LookRotation(look.normalized);
        hauntFigure.SetActive(true);
        var tex = PhotoRenderer.I.Render(best.pos, best.rot, best.fov, Layers.ViewfinderMask);
        hauntFigure.SetActive(false);

        Destroy(best.tex);
        best.tex = tex;
        best.special = true;
        best.isNew = true;
        best.title += "  (changed)";
        best.desc += "\n\nThere is someone in this photograph now. There was no one there when I took it.";

        var am = AudioManager.I;
        am.PlayAt(am.shutter, PlayerController.I.Position + Vector3.up * 4f + Random.insideUnitSphere * 3f, 0.7f, 2f);
        UIManager.I.Subtitle("[Somewhere above you, a shutter clicks]");
        yield return new WaitForSeconds(1.5f);
        UIManager.I.Toast("One of your photographs has changed.   [Tab] Evidence", 5f);
        StartCoroutine(FootstepsAbove());
    }

    IEnumerator FootstepsAbove()
    {
        var p = PlayerController.I;
        Vector3 start = p.Position + Vector3.up * 3.6f + p.FlatForward * 3f;
        Vector3 dir = Quaternion.Euler(0, 70, 0) * p.FlatForward;
        for (int i = 0; i < 7; i++)
        {
            AudioManager.I.Footstep(start + dir * (i * 0.7f), 0.8f);
            yield return new WaitForSeconds(0.62f);
        }
    }

    // ---------- Adaptive: punish obsessive checking ----------

    void OnRaise()
    {
        if (!GameManager.I.Has("studio_entered") || endingStarted) return;
        raiseTimes.Add(Time.time);
        raiseTimes.RemoveAll(t => Time.time - t > 25f);
        if (raiseTimes.Count >= 6 && Time.time - lastAdaptive > 90f)
        {
            lastAdaptive = Time.time;
            raiseTimes.Clear();
            StartCoroutine(FootstepsBehind());
        }
    }

    IEnumerator FootstepsBehind()
    {
        yield return new WaitForSeconds(0.8f);
        var p = PlayerController.I;
        for (int i = 0; i < 4; i++)
        {
            AudioManager.I.Footstep(p.Position - p.FlatForward * (3.5f - i * 0.6f), 0.6f);
            yield return new WaitForSeconds(0.55f);
        }
        UIManager.I.Subtitle("[Footsteps behind you. Then nothing.]");
    }

    // ---------- Event 4: ending ----------

    public void BeginEnding()
    {
        if (endingStarted) return;
        endingStarted = true;
        CameraTool.I.Lower();

        var p = PlayerController.I;
        Vector3 fwd = p.FlatForward;
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        Vector3 head = p.HeadPosition;

        // The figure stands across the table, where the player is looking.
        float fd = 1.35f;
        if (Physics.Raycast(head - Vector3.up * 0.3f, fwd, out var hit, 2f, Layers.NormalViewMask, QueryTriggerInteraction.Ignore))
            fd = Mathf.Min(fd, hit.distance - 0.35f);
        Vector3 figPos = p.Position + fwd * Mathf.Max(0.9f, fd);
        figPos.y = 0f;
        endingFigure.transform.SetPositionAndRotation(figPos, Quaternion.LookRotation(-fwd));
        Layers.SetRecursive(endingFigure, Layers.CameraOnly);
        endingFigure.SetActive(true);

        // The print: taken from behind the player's shoulder, at this exact moment.
        Vector3 back = -fwd;
        float bd = 1.5f;
        if (Physics.Raycast(head, back, out var bh, bd, Layers.NormalViewMask, QueryTriggerInteraction.Ignore))
            bd = Mathf.Max(0.5f, bh.distance - 0.15f);
        Vector3 camPos = head + back * bd + Vector3.up * 0.25f + right * 0.25f;
        Vector3 target = Vector3.Lerp(head, figPos + Vector3.up * 1.6f, 0.55f);
        var tex = PhotoRenderer.I.Render(camPos, Quaternion.LookRotation(target - camPos), 60f,
            Layers.ViewfinderMask | Layers.PlayerBodyMask);
        endingFigure.SetActive(false);

        EvidenceManager.I.AddSpecial(tex, "The darkroom print",
            "The paper is still wet. It shows me, standing exactly where I am standing now.\n\nIt was taken from behind me. On the other side of the table, something is waiting.");

        AudioManager.I.Play2D(AudioManager.I.paper, 0.7f);
        UIManager.I.ShowInspect(tex,
            "The print is still wet. Dated today — 00:00.\nIt shows you, standing exactly where you are standing now.",
            () => StartCoroutine(EndingSequence(figPos, fwd)));
    }

    IEnumerator EndingSequence(Vector3 figPos, Vector3 fwd)
    {
        var gm = GameManager.I;
        gm.SetState(GameState.Cutscene);
        var am = AudioManager.I;

        yield return new WaitForSeconds(0.6f);
        am.StopAll();
        foreach (var l in FindObjectsByType<FlickerLight>(FindObjectsInactive.Exclude)) l.SetOff(true);
        foreach (var l in FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l != flashLight) l.enabled = false;
        RenderSettings.ambientLight = Color.black;
        am.PlayAt(am.whoosh, figPos + Vector3.up * 1.5f, 0.3f);

        yield return new WaitForSeconds(1.6f);
        am.Play2D(am.heartbeat, 0.8f);
        UIManager.I.Subtitle("[Your heartbeat]", 2.5f);
        yield return new WaitForSeconds(2.2f);

        // Observing it makes it real.
        Layers.SetRecursive(endingFigure, 0);
        endingFigure.SetActive(true);
        var p = PlayerController.I;
        flashLight.transform.position = p.HeadPosition + fwd * 0.2f;
        flashLight.intensity = Settings.ReducedEffects ? 2.5f : 7f;
        flashLight.enabled = true;
        am.Play2D(am.shutter, 1f);
        am.Play2D(am.sting, 1f);
        CameraTool.I.effect.flash = 0f;
        yield return new WaitForSeconds(Settings.ReducedEffects ? 0.6f : 0.22f);
        flashLight.enabled = false;

        yield return new WaitForSeconds(1.4f);
        gm.SetState(GameState.Ending);
        UIManager.I.BeginEnding();
    }
}
