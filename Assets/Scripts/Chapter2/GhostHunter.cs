using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GhostState { Dormant, Hunting, Stunned, Killing }

/// The Figure, hunting. Rules the player can learn:
///  • It is invisible to the naked eye (you only glimpse it when it's very close). The lens shows it.
///  • While it is in the viewfinder it cannot move — but stare for too long and it lunges closer.
///  • A flash photograph (costs film) drives it away for a while.
///  • Its breathing, footsteps and your heartbeat get louder as it closes in. If it reaches you, you die.
public class GhostHunter : MonoBehaviour
{
    public static GhostHunter I;

    public GameObject model;
    public float stalkSpeed = 1.5f;
    public float chaseSpeed = 2.3f;
    public float killDistance = 0.9f;
    public float maxObserveSeconds = 3f;
    public float lungeDistance = 1.6f;

    public GhostState State { get; private set; } = GhostState.Dormant;
    public float SpeedMultiplier { get; set; } = 1f;
    public bool Suspended { get; set; }   // test hook: freezes the AI entirely
    public bool IsObserved { get; private set; }
    public float DistanceToPlayer { get; private set; } = 99f;

    public System.Action OnKill;

    GhostNode[] nodes;
    List<GhostNode> path = new List<GhostNode>();
    int pathIndex;
    float repathAt, stepTimer, glimpseAt, observedFor;
    AudioSource breath;
    bool glimpsing;

    static readonly int WallMask = 1 << 0;

    void Awake() => I = this;

    void Start()
    {
        nodes = FindObjectsByType<GhostNode>();
        GhostNode.Symmetrize(nodes);
        breath = AudioManager.I.Loop3D(AudioManager.I.breath, transform, 0f, 1.5f);
        model.SetActive(false);
    }

    public bool Active => State == GhostState.Hunting;

    // ---------------- Control ----------------

    public void BeginHunt(Vector3 at)
    {
        transform.position = new Vector3(at.x, 0f, at.z);
        State = GhostState.Hunting;
        observedFor = 0f;
        path.Clear();
        repathAt = 0f;
        Layers.SetRecursive(model, Layers.CameraOnly);
        model.SetActive(true);
        breath.volume = 0.9f;
    }

    public void EndHunt()
    {
        State = GhostState.Dormant;
        model.SetActive(false);
        breath.volume = 0f;
        SpeedMultiplier = 1f;
    }

    public void Stun()
    {
        if (State != GhostState.Hunting) return;
        StartCoroutine(StunRoutine());
    }

    IEnumerator StunRoutine()
    {
        State = GhostState.Stunned;
        var am = AudioManager.I;
        am.PlayAt(am.scream, transform.position + Vector3.up * 1.7f, 1f, 3f);
        CameraTool.I.effect.GlitchPulse(0.9f, 0.6f);
        // Visible to everyone for a moment, writhing in the flash.
        Layers.SetRecursive(model, 0);
        float t = 0f;
        Vector3 basePos = transform.position;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            transform.position = basePos + (Settings.ReducedEffects ? Vector3.zero : Random.insideUnitSphere * 0.06f);
            yield return null;
        }
        transform.position = basePos;
        EndHunt();
    }

    // ---------------- Behaviour ----------------

    void Update()
    {
        var p = PlayerController.I;
        Vector3 flat = p.Position - transform.position;
        flat.y = 0f;
        DistanceToPlayer = flat.magnitude;

        if (State != GhostState.Hunting || Suspended || !GameManager.I.IsPlaying) { IsObserved = false; return; }

        // Always face the player.
        if (flat.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(flat.normalized), Time.deltaTime * 6f);

        IsObserved = CheckObserved();
        if (IsObserved)
        {
            observedFor += Time.deltaTime;
            if (observedFor > maxObserveSeconds) Lunge(flat);
        }
        else
        {
            observedFor = Mathf.Max(0f, observedFor - Time.deltaTime * 0.5f);
            Move(p, flat);
        }

        UpdateGlimpse();

        if (DistanceToPlayer < killDistance)
        {
            State = GhostState.Killing;
            breath.volume = 0f;
            OnKill?.Invoke();
        }
    }

    bool CheckObserved()
    {
        var cam = CameraTool.I;
        if (!cam.Raised || cam.RaiseBlend < 0.5f || DistanceToPlayer > 14f) return false;
        Vector3 head = transform.position + Vector3.up * 1.6f;
        Vector3 v = cam.cam.WorldToViewportPoint(head);
        if (v.z <= 0f || v.x < 0.08f || v.x > 0.92f || v.y < 0f || v.y > 1.05f) return false;
        return HasLineOfSight(cam.cam.transform.position, head);
    }

    public static bool HasLineOfSight(Vector3 a, Vector3 b) =>
        !Physics.Linecast(a, b, WallMask, QueryTriggerInteraction.Ignore);

    void Move(PlayerController p, Vector3 flat)
    {
        float speed = (DistanceToPlayer < 8f ? chaseSpeed : stalkSpeed) * SpeedMultiplier;
        speed *= 0.7f + 0.6f * Mathf.PerlinNoise(Time.time * 1.7f, 3.3f); // uneven, unnatural gait

        Vector3 target;
        if (DistanceToPlayer < 9f && HasLineOfSight(transform.position + Vector3.up * 1.5f, p.HeadPosition))
        {
            target = p.Position;
            path.Clear();
        }
        else
        {
            if (Time.time > repathAt || path.Count == 0)
            {
                repathAt = Time.time + 0.8f;
                path = GhostNode.Path(Nearest(transform.position, false), Nearest(p.Position, true));
                pathIndex = 0;
            }
            if (path.Count == 0) target = p.Position;
            else
            {
                target = path[pathIndex].Pos;
                Vector3 d = target - transform.position;
                d.y = 0;
                if (d.magnitude < 0.3f && pathIndex < path.Count - 1) pathIndex++;
                if (pathIndex >= path.Count - 1 && d.magnitude < 0.3f) target = p.Position;
            }
        }

        Vector3 step = target - transform.position;
        step.y = 0f;
        if (step.magnitude > 0.01f)
            transform.position += step.normalized * Mathf.Min(step.magnitude, speed * Time.deltaTime);

        stepTimer -= Time.deltaTime;
        if (stepTimer <= 0f)
        {
            stepTimer = 0.62f / Mathf.Max(0.5f, SpeedMultiplier);
            AudioManager.I.PlayAt(AudioManager.I.ghostStep, transform.position, 0.9f, 1.5f, 0.85f + Random.value * 0.2f);
        }
    }

    void Lunge(Vector3 flat)
    {
        observedFor = 0f;
        float move = Mathf.Min(lungeDistance, DistanceToPlayer - 1.3f);
        if (move > 0f) transform.position += flat.normalized * move;
        var am = AudioManager.I;
        am.PlayAt(am.whoosh, transform.position + Vector3.up * 1.5f, 1f, 2f);
        CameraTool.I.effect.GlitchPulse(0.8f, 0.35f);
        UIManager.I.Subtitle("[It doesn't like being watched]", 2.5f);
    }

    /// Up close, it flickers into normal sight for a split second.
    void UpdateGlimpse()
    {
        if (glimpsing) return;
        if (DistanceToPlayer < 6f && !CameraTool.I.Raised && Time.time > glimpseAt)
        {
            glimpseAt = Time.time + Random.Range(1.4f, 2.6f);
            StartCoroutine(Glimpse());
        }
    }

    IEnumerator Glimpse()
    {
        glimpsing = true;
        Layers.SetRecursive(model, 0);
        yield return new WaitForSeconds(0.09f);
        if (State == GhostState.Hunting) Layers.SetRecursive(model, Layers.CameraOnly);
        glimpsing = false;
    }

    GhostNode Nearest(Vector3 pos, bool requireSight)
    {
        GhostNode best = null, bestAny = null;
        float bd = float.MaxValue, ba = float.MaxValue;
        foreach (var n in nodes)
        {
            float d = (n.Pos - pos).sqrMagnitude;
            if (d < ba) { ba = d; bestAny = n; }
            if (requireSight && d < bd && HasLineOfSight(n.Pos + Vector3.up * 1.2f, pos + Vector3.up * 1.2f)) { bd = d; best = n; }
        }
        return best ? best : bestAny;
    }

    public GhostNode[] Nodes => nodes;
}
