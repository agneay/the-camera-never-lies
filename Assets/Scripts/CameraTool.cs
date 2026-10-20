using System.Collections.Generic;
using UnityEngine;

/// The dual-reality camera: viewfinder toggle, zoom, capture, target detection and evidence registration.
public class CameraTool : MonoBehaviour
{
    public static CameraTool I;

    public Camera cam;
    public ViewfinderEffect effect;
    public float baseFov = 65f;
    public float viewfinderFov = 50f;
    public float cooldown = 1.1f;

    public bool Raised { get; private set; }
    public float RaiseBlend { get; private set; }
    public Photographable FocusTarget { get; private set; }
    public float ZoomFactor => baseFov / Mathf.Max(1f, cam.fieldOfView);
    public float CooldownLeft => Mathf.Max(0f, cooldown - (Time.time - lastCapture));

    public System.Action<PhotoEntry> OnCapture;
    public System.Action OnRaise;

    float zoomFov;
    float lastCapture = -10f;
    float flash;
    bool firstRaiseDone, firstCaptureDone;

    void Awake()
    {
        I = this;
        zoomFov = viewfinderFov;
        cam.cullingMask = Layers.NormalViewMask;
    }

    void Update()
    {
        var gm = GameManager.I;
        if (gm.InputReady && !PlayerController.ScriptedLook)
        {
            if (Input.GetMouseButtonDown(1)) { if (Raised) Lower(); else Raise(); }
            if (Raised && Input.GetMouseButtonDown(0)) TryCapture();
            if (Raised) zoomFov = Mathf.Clamp(zoomFov - Input.mouseScrollDelta.y * 3f, 26f, 58f);
        }

        RaiseBlend = Mathf.MoveTowards(RaiseBlend, Raised ? 1f : 0f, Time.deltaTime * 5f);
        // Swap what reality we render at the darkest point of the raise "blink".
        cam.cullingMask = RaiseBlend > 0.5f ? Layers.ViewfinderMask : Layers.NormalViewMask;
        cam.fieldOfView = Mathf.Lerp(baseFov, zoomFov, RaiseBlend);
        PlayerController.I.LookMultiplier = Mathf.Lerp(1f, cam.fieldOfView / baseFov, RaiseBlend);

        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
        effect.viewfinder = RaiseBlend;
        effect.blink = 1f - Mathf.Abs(RaiseBlend * 2f - 1f);
        effect.flash = flash * (Settings.ReducedEffects ? 0.25f : 0.75f);

        if (Raised)
        {
            var list = FindTargets();
            FocusTarget = list.Count > 0 ? list[0] : null;
        }
        else FocusTarget = null;
    }

    public void Raise()
    {
        if (Raised) return;
        Raised = true;
        zoomFov = viewfinderFov;
        AudioManager.I.Play2D(AudioManager.I.toggleUp, 0.6f);
        OnRaise?.Invoke();
        if (!firstRaiseDone)
        {
            firstRaiseDone = true;
            UIManager.I.Hint("[Left Mouse] Take a photograph     [Wheel] Zoom     [Right Mouse] Lower camera", 7f);
        }
    }

    public void Lower()
    {
        if (!Raised) return;
        Raised = false;
        AudioManager.I.Play2D(AudioManager.I.toggleDown, 0.5f);
    }

    /// Remaining exposures; -1 means unlimited (Chapter 1).
    public int Film { get; set; } = -1;

    public void AddFilm(int n)
    {
        if (Film < 0) return;
        Film += n;
        UIManager.I.Toast($"+{n} film   ({Film} left)");
    }

    public void TryCapture()
    {
        if (!Raised || CooldownLeft > 0f) return;
        if (Film == 0)
        {
            lastCapture = Time.time;
            AudioManager.I.Play2D(AudioManager.I.click, 0.6f);
            UIManager.I.Toast("Out of film.");
            return;
        }
        if (Film > 0) Film--;
        lastCapture = Time.time;
        flash = 1f;
        AudioManager.I.Play2D(AudioManager.I.shutter, 0.9f);

        var targets = FindTargets();
        Transform t = cam.transform;
        var tex = PhotoRenderer.I.Render(t.position, t.rotation, cam.fieldOfView, Layers.ViewfinderMask);
        var entry = EvidenceManager.I.AddPhoto(tex, targets, t.position, t.rotation, cam.fieldOfView);

        foreach (var p in targets) p.Photographed();
        UIManager.I.ShowReview(entry, 1.8f, false);
        OnCapture?.Invoke(entry);

        if (!firstCaptureDone)
        {
            firstCaptureDone = true;
            UIManager.I.Hint("Photographs are kept as evidence.  [Tab] Open evidence", 6f);
        }
    }

    readonly List<Photographable> found = new List<Photographable>();

    /// Forgiving target detection: anything in the central area of the frame, in range and in line of sight.
    public List<Photographable> FindTargets()
    {
        found.Clear();
        Vector3 origin = cam.transform.position;
        int losMask = ~(Layers.PlayerBodyMask | Layers.CameraOnlyMask);

        foreach (var p in Photographable.All)
        {
            if (!p.isActiveAndEnabled) continue;
            Vector3 v = cam.WorldToViewportPoint(p.FocusPoint);
            if (v.z < 0.3f || v.z > p.maxDistance) continue;
            if (v.x < 0.18f || v.x > 0.82f || v.y < 0.15f || v.y > 0.85f) continue;

            Vector3 dir = p.FocusPoint - origin;
            float d = dir.magnitude;
            if (Physics.Raycast(origin, dir / d, out var hit, d - 0.25f, losMask, QueryTriggerInteraction.Ignore)
                && !hit.transform.IsChildOf(p.transform))
                continue;

            found.Add(p);
        }

        found.Sort((a, b) => CentreDistance(a).CompareTo(CentreDistance(b)));
        return found;
    }

    float CentreDistance(Photographable p)
    {
        Vector3 v = cam.WorldToViewportPoint(p.FocusPoint);
        return new Vector2(v.x - 0.5f, v.y - 0.5f).sqrMagnitude;
    }
}
