using System.Collections;
using System.Linq;
using UnityEngine;

/// Chapter 2 — "Someone Was Here". Schedules hunts, handles stuns, deaths and checkpoints,
/// the wrong-answer / doll / wardrobe scares, the hallway photographs that keep changing, and the final chase.
public class Chapter2Director : MonoBehaviour
{
    public static Chapter2Director I;

    public GhostHunter ghost;
    public TriggerZone aptA, aptB, aptC;
    public FlickerLight[] hallLights;
    public Renderer[] sequenceFrames;
    public GameObject sequenceFigure;
    public Transform sequenceCamera;
    public float[] sequenceDistances = { 21f, 15f, 9f, 3.2f };
    public float[] sequenceTriggersX = { 4f, 9f, 13.5f, 18f };
    public GhostNode dollNode;
    public GameObject endingFigure;
    public Light flashLight;
    public Transform stairLanding;
    public Photographable doll;

    public bool AutoHunts { get; set; } = true;   // test hook
    public bool Activated { get; set; }
    public bool FinalPhase { get; private set; }
    public int SequenceStage { get; private set; }
    public int Deaths { get; private set; }

    float nextHuntAt = float.MaxValue, huntEndsAt;
    Vector3 cpPos;
    float cpYaw;
    int cpFilm;
    Texture2D[] sequenceTex;
    bool escaped;

    const float HuntLength = 32f;

    void Awake() => I = this;

    void Start()
    {
        CameraTool.I.OnCapture += OnCapture;
        PickupInteractable.Picked += OnPicked;
        GameManager.GameStarted += OnGameStarted;
        ghost.OnKill += () => StartCoroutine(Death());
        doll.onPhotographed += () => StartCoroutine(DollTrap());
        endingFigure.SetActive(false);
        flashLight.enabled = false;
        RenderSequence();
        SaveCheckpoint(false); // GameStarted may already have fired before this Start ran
    }

    void OnDestroy()
    {
        PickupInteractable.Picked -= OnPicked;
        GameManager.GameStarted -= OnGameStarted;
    }

    void OnGameStarted() => SaveCheckpoint(false);

    Vector3 PlayerPoint => PlayerController.I.Position + Vector3.up;

    void Update()
    {
        if (!GameManager.I.IsPlaying || escaped) return;
        var p = PlayerController.I;

        if (!Activated && (aptA.Contains(PlayerPoint) || aptB.Contains(PlayerPoint) || aptC.Contains(PlayerPoint)))
        {
            Activated = true;
            StartCoroutine(FirstManifestation());
        }

        if (AutoHunts && Activated)
        {
            if (ghost.State == GhostState.Dormant && Time.time > nextHuntAt) StartHunt(false);
            if (!FinalPhase && ghost.State == GhostState.Hunting && Time.time > huntEndsAt)
            {
                ghost.EndHunt();
                nextHuntAt = Time.time + Random.Range(22f, 32f);
                UIManager.I.Subtitle("[The footsteps fade. For now.]");
            }
        }

        // Heartbeat with proximity.
        float hb = ghost.State == GhostState.Hunting ? Mathf.Clamp01(1f - (ghost.DistanceToPlayer - 1.5f) / 11f) : 0f;
        AudioManager.I.SetHeartbeat(hb);

        // The hallway photographs: each time you pass one, it is closer.
        if (SequenceStage < sequenceTriggersX.Length && p.Position.z < 2.2f && p.Position.x > sequenceTriggersX[SequenceStage])
            AdvanceSequence();
    }

    // ---------------- Hunts ----------------

    public void StartHunt(bool near, float speedMultiplier = 1f, GhostNode at = null)
    {
        var p = PlayerController.I;
        GhostNode node = at;
        if (node == null)
        {
            float min = near ? 6f : 10f, max = near ? 12f : 22f;
            var candidates = ghost.Nodes.Where(n =>
            {
                float d = Vector3.Distance(n.Pos, p.Position);
                return d >= min && d <= max && !GhostHunter.HasLineOfSight(n.Pos + Vector3.up * 1.5f, p.HeadPosition);
            }).ToList();
            node = candidates.Count > 0 ? candidates[Random.Range(0, candidates.Count)]
                                        : ghost.Nodes.OrderByDescending(n => Vector3.Distance(n.Pos, p.Position)).First();
        }
        ghost.BeginHunt(node.Pos);
        ghost.SpeedMultiplier = FinalPhase ? 1.35f : speedMultiplier;
        huntEndsAt = Time.time + HuntLength;
        nextHuntAt = float.MaxValue;
        foreach (var l in hallLights) l.Burst(1f);
        AudioManager.I.Play2D(AudioManager.I.staticBurst, 0.7f);
        UIManager.I.Subtitle(FinalPhase ? "[It's coming. RUN.]" : "[Static. Something is looking for you.]", 3f);
    }

    IEnumerator FirstManifestation()
    {
        yield return new WaitForSeconds(2.5f);
        var p = PlayerController.I;
        foreach (var l in hallLights) l.SetOff(true);
        AudioManager.I.Play2D(AudioManager.I.staticBurst, 0.6f);
        yield return new WaitForSeconds(1.2f);
        AudioManager.I.PlayAt(AudioManager.I.whisper, p.HeadPosition - p.FlatForward * 1.2f, 1f, 1f);
        UIManager.I.Subtitle("[Someone whispers your name. Right behind you.]", 3.5f);
        yield return new WaitForSeconds(1.5f);
        foreach (var l in hallLights) l.SetOff(false);
        GameManager.I.SetObjective("It knows you're here. Keep the lens on it — and find the fuse and the key.");
        UIManager.I.Hint("Through the lens it cannot move. A flash photograph drives it away. Watch your film.", 8f);
        nextHuntAt = Time.time + 8f;
    }

    void OnCapture(PhotoEntry entry)
    {
        if (ghost.State != GhostState.Hunting) return;
        var cam = CameraTool.I.cam;
        Vector3 head = ghost.transform.position + Vector3.up * 1.6f;
        Vector3 v = cam.WorldToViewportPoint(head);
        bool inFrame = v.z > 0f && v.z < 9f && v.x > 0.1f && v.x < 0.9f && v.y > 0f && v.y < 1.05f;
        if (!inFrame || !GhostHunter.HasLineOfSight(cam.transform.position, head)) return;

        entry.title = "It";
        entry.desc = "It was standing right there. The flash hit it and it screamed.\n\nIt will be back.";
        entry.special = true;
        ghost.Stun();
        nextHuntAt = Time.time + (FinalPhase ? 7f : Random.Range(24f, 30f));
        UIManager.I.Subtitle("[It shrieks and scatters in the flash]", 3f);
    }

    // ---------------- Scares ----------------

    public void Scare(float hold) => StartCoroutine(JumpScare.I.Play(hold, false));

    public void WrongAnswer(Vector3 at, string message)
    {
        UIManager.I.Toast(message, 3.5f);
        StartCoroutine(WrongRoutine());
    }

    IEnumerator WrongRoutine()
    {
        yield return JumpScare.I.Play(0.55f, false);
        if (AutoHunts && ghost.State == GhostState.Dormant) StartHunt(true, 1.15f);
    }

    IEnumerator DollTrap()
    {
        UIManager.I.Toast("You were told not to photograph her.", 3.5f);
        yield return JumpScare.I.Play(0.7f, false);
        if (ghost.State == GhostState.Hunting) ghost.EndHunt();
        StartHunt(true, 1.3f, dollNode);
    }

    // ---------------- Death & checkpoints ----------------

    IEnumerator Death()
    {
        Deaths++;
        GameManager.I.SetState(GameState.Cutscene);
        ghost.EndHunt();
        AudioManager.I.SetHeartbeat(0f);
        yield return JumpScare.I.Play(1.0f, true);
        GameManager.I.SetState(GameState.Dead);
    }

    public void Retry()
    {
        ghost.EndHunt();
        var p = PlayerController.I;
        p.Teleport(cpPos);
        p.SetYaw(cpYaw);
        p.RefillStamina();
        CameraTool.I.Film = Mathf.Max(cpFilm, 2);
        foreach (var l in hallLights) l.SetOff(false);
        GameManager.I.SetState(GameState.Playing);
        UIManager.I.FadeFromBlack(1.5f);
        nextHuntAt = Time.time + (FinalPhase ? 6f : 18f);
        UIManager.I.Hint("Keep the lens on it. Don't stare too long. Flash it if it gets close.", 6f);
    }

    public void SaveCheckpoint(bool announce = true)
    {
        var p = PlayerController.I;
        cpPos = p.Position;
        cpYaw = p.Yaw;
        cpFilm = Mathf.Max(0, CameraTool.I.Film);
        if (announce) UIManager.I.Toast("Checkpoint", 2f);
    }

    void OnPicked(string flag)
    {
        if (flag == "fuse")
        {
            SaveCheckpoint();
            UpdateExitObjective();
        }
    }

    public void OnKeyFound()
    {
        SaveCheckpoint();
        UpdateExitObjective();
    }

    void UpdateExitObjective()
    {
        var gm = GameManager.I;
        bool fuse = gm.Has("fuse") || gm.Has("power"), key = gm.Has("stair_key");
        if (FinalPhase)
            gm.SetObjective(key ? "RUN. Unlock the stairwell door at the end of the hall." : "It's hunting you. Find the stairwell key in the mailboxes — then run.");
        else if (fuse && key) gm.SetObjective("Restore power at the fuse box by the stairwell. Then get out.");
        else if (fuse) gm.SetObjective("You have the fuse. Now find the stairwell key.");
        else if (key) gm.SetObjective("You have the key. The stairwell lock still needs power — find a fuse.");
    }

    // ---------------- Final phase & escape ----------------

    public void PowerRestored()
    {
        StartCoroutine(PowerRoutine());
    }

    IEnumerator PowerRoutine()
    {
        FinalPhase = true;
        Activated = true;
        SaveCheckpoint();
        var am = AudioManager.I;
        am.PlayAt(am.warp, PlayerController.I.HeadPosition, 0.8f, 2f);
        foreach (var l in hallLights) l.Burst(2f);
        UIManager.I.Toast("Power hums back into the stairwell lock. Every light in the hall flares.", 4f);
        UpdateExitObjective();
        yield return new WaitForSeconds(2.5f);
        if (ghost.State == GhostState.Hunting) ghost.EndHunt();
        if (AutoHunts) StartHunt(false);
        UIManager.I.Hint("[Shift] Sprint", 4f);
    }

    public void Escape()
    {
        if (escaped) return;
        escaped = true;
        StartCoroutine(EscapeRoutine());
    }

    IEnumerator EscapeRoutine()
    {
        var gm = GameManager.I;
        gm.SetState(GameState.Cutscene);
        ghost.EndHunt();
        AudioManager.I.SetHeartbeat(0f);
        var am = AudioManager.I;
        yield return new WaitForSeconds(1.6f);

        am.StopAll();
        foreach (var l in FindObjectsByType<FlickerLight>()) l.SetOff(true);
        RenderSettings.ambientLight = Color.black;
        UIManager.I.Subtitle("[The stairwell is pitch black. Something is on the stairs.]", 3f);
        yield return new WaitForSeconds(2.2f);

        // One last photograph, by flash.
        var p = PlayerController.I;
        Vector3 fwd = p.FlatForward;
        endingFigure.transform.SetPositionAndRotation(stairLanding.position, Quaternion.LookRotation(-fwd));
        Layers.SetRecursive(endingFigure, 0);
        endingFigure.SetActive(true);
        flashLight.transform.position = p.HeadPosition + fwd * 0.3f;
        flashLight.intensity = Settings.ReducedEffects ? 2.5f : 7f;
        flashLight.enabled = true;
        am.Play2D(am.shutter, 1f);
        am.Play2D(am.scream, 0.9f);
        yield return new WaitForSeconds(Settings.ReducedEffects ? 0.6f : 0.25f);
        flashLight.enabled = false;
        yield return new WaitForSeconds(1.3f);

        gm.SetState(GameState.Ending);
        UIManager.I.BeginEnding();
    }

    // ---------------- The hallway photographs ----------------

    void RenderSequence()
    {
        if (sequenceFrames == null || sequenceFrames.Length == 0) return;
        sequenceTex = new Texture2D[sequenceDistances.Length + 1];
        Vector3 origin = sequenceCamera.position;
        Vector3 fwd = sequenceCamera.forward;
        for (int i = 0; i < sequenceDistances.Length; i++)
        {
            Vector3 pos = origin + fwd * sequenceDistances[i];
            pos.y = 0f;
            sequenceFigure.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(-new Vector3(fwd.x, 0, fwd.z)));
            sequenceFigure.SetActive(true);
            sequenceTex[i] = PhotoRenderer.I.Render(origin, sequenceCamera.rotation, 55f, Layers.ViewfinderMask);
        }
        sequenceFigure.SetActive(false);
        sequenceTex[sequenceDistances.Length] = PhotoRenderer.I.Render(origin, sequenceCamera.rotation, 55f, Layers.ViewfinderMask);
        ApplySequence(0);
    }

    void ApplySequence(int stage)
    {
        foreach (var r in sequenceFrames) r.material.mainTexture = sequenceTex[Mathf.Min(stage, sequenceTex.Length - 1)];
    }

    void AdvanceSequence()
    {
        SequenceStage++;
        ApplySequence(SequenceStage);
        if (SequenceStage == 1) return;
        var p = PlayerController.I;
        AudioManager.I.PlayAt(AudioManager.I.shutter, p.HeadPosition - p.FlatForward * 3f, 0.5f, 2f);
        if (SequenceStage == 2) UIManager.I.Subtitle("[Somewhere behind you, a shutter clicks]");
        if (SequenceStage == sequenceTriggersX.Length) UIManager.I.Subtitle("[The photographs on the wall are empty now]");
    }
}
