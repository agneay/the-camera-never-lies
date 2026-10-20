using UnityEngine;

/// First-person movement, mouse look, head bob, footsteps and centre-screen interaction.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public static PlayerController I;

    public Camera cam;
    public Transform head;
    public float walkSpeed = 2.6f;
    public float gravity = -15f;
    public float interactRange = 2.6f;

    public Interactable Focused { get; private set; }
    public float LookMultiplier { get; set; } = 1f;
    public static bool ScriptedLook; // set by AutoPlaytest so a real mouse cannot disturb it

    public float sprintSpeed = 4.3f;
    public float sprintSeconds = 3.5f;
    public float Stamina { get; private set; } = 1f;
    public bool Sprinting { get; private set; }
    bool exhausted;
    float staminaRegenAt;

    CharacterController cc;
    float yaw, pitch, vy, stepTimer, bobT;
    Vector3 headBase;

    void Awake()
    {
        I = this;
        cc = GetComponent<CharacterController>();
        headBase = head.localPosition;
        yaw = transform.eulerAngles.y;
    }

    public Vector3 Position => transform.position;
    public Vector3 HeadPosition => cam.transform.position;
    public Vector3 FlatForward
    {
        get
        {
            var f = cam.transform.forward;
            f.y = 0;
            return f.sqrMagnitude < 0.001f ? transform.forward : f.normalized;
        }
    }

    void Update()
    {
        var gm = GameManager.I;
        bool canAct = gm.IsPlaying;
        Vector2 input = Vector2.zero;

        if (canAct && !ScriptedLook && Time.frameCount > gm.StateFrame + 2)
        {
            float s = Settings.Sensitivity * LookMultiplier;
            yaw += Input.GetAxisRaw("Mouse X") * s;
            pitch -= Input.GetAxisRaw("Mouse Y") * s;
            pitch = Mathf.Clamp(pitch, -85f, 85f);
            input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        }

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        Vector3 move = transform.right * input.x + transform.forward * input.y;
        if (move.sqrMagnitude > 1f) move.Normalize();
        bool raised = CameraTool.I != null && CameraTool.I.Raised;
        bool wantSprint = canAct && Input.GetKey(KeyCode.LeftShift) && input.y > 0.1f && !raised;
        Sprinting = wantSprint && !exhausted && Stamina > 0f;
        if (Sprinting)
        {
            Stamina = Mathf.Max(0f, Stamina - Time.deltaTime / sprintSeconds);
            if (Stamina <= 0f) exhausted = true;
            staminaRegenAt = Time.time + 0.8f;
        }
        else if (Time.time > staminaRegenAt)
        {
            Stamina = Mathf.Min(1f, Stamina + Time.deltaTime / 6f);
            if (Stamina > 0.35f) exhausted = false;
        }
        float speed = Sprinting ? sprintSpeed : walkSpeed * (raised ? 0.6f : 1f);

        if (cc.isGrounded && vy < 0f) vy = -1f;
        vy += gravity * Time.deltaTime;
        cc.Move((move * speed + Vector3.up * vy) * Time.deltaTime);

        UpdateBobAndSteps();
        UpdateInteraction(gm);
    }

    void UpdateBobAndSteps()
    {
        Vector3 v = cc.velocity;
        float planar = new Vector2(v.x, v.z).magnitude;
        float amount = Settings.ReducedEffects ? 0f : 0.03f;

        if (planar > 0.2f && cc.isGrounded && Time.timeScale > 0f)
        {
            bobT += Time.deltaTime * planar * 2.4f;
            stepTimer -= Time.deltaTime;
            if (stepTimer <= 0f)
            {
                stepTimer = 1.4f / Mathf.Max(planar, 0.8f);
                AudioManager.I.Footstep(transform.position, Sprinting ? 0.45f : 0.28f);
            }
            head.localPosition = headBase + new Vector3(Mathf.Cos(bobT) * amount * 0.5f, Mathf.Abs(Mathf.Sin(bobT)) * amount, 0f);
        }
        else
        {
            stepTimer = Mathf.Min(stepTimer, 0.15f);
            head.localPosition = Vector3.Lerp(head.localPosition, headBase, Time.deltaTime * 6f);
        }
    }

    void UpdateInteraction(GameManager gm)
    {
        Focused = null;
        if (!gm.IsPlaying) return;

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        int mask = ~(Layers.PlayerBodyMask | Layers.CameraOnlyMask);
        if (Physics.Raycast(ray, out var hit, interactRange, mask, QueryTriggerInteraction.Ignore))
        {
            var it = hit.collider.GetComponentInParent<Interactable>();
            if (it != null && it.isActiveAndEnabled && it.CanInteract) Focused = it;
        }

        if (Focused != null && gm.InputReady && Input.GetKeyDown(KeyCode.E))
            Focused.Interact();
    }

    // --- Helpers used by scripted events and the autoplay test ---

    public void LookAt(Vector3 point)
    {
        Vector3 d = point - cam.transform.position;
        yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        pitch = -Mathf.Asin(Mathf.Clamp(d.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        head.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    public float Yaw => yaw;

    public void SetYaw(float y)
    {
        yaw = y;
        pitch = 0f;
    }

    public void RefillStamina()
    {
        Stamina = 1f;
        exhausted = false;
    }

    public void Teleport(Vector3 pos)
    {
        cc.enabled = false;
        transform.position = pos;
        cc.enabled = true;
    }
}
