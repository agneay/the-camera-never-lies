using UnityEngine;

/// Hinged door with explicit states: locked (optionally by a flag/key), unlocked, open.
public class DoorInteractable : Interactable
{
    public float openAngle = 95f;
    public bool locked;
    public string requiredFlag;
    public bool neverOpens;
    public string lockedMessage = "It's locked.";
    public string unlockMessage = "";
    public string flagOnOpen;
    public string objectiveOnOpen;

    bool open;
    float t;
    float closedYaw;

    void Start() => closedYaw = transform.localEulerAngles.y;

    public bool IsOpen => open;
    public override bool CanInteract => !open;

    public override string Prompt
    {
        get
        {
            if (locked && !string.IsNullOrEmpty(requiredFlag) && GameManager.I.Has(requiredFlag)) return "Unlock door";
            return "Open door";
        }
    }

    public override void Interact()
    {
        var am = AudioManager.I;
        if (neverOpens)
        {
            am.PlayAt(am.rattle, transform.position + Vector3.up, 0.8f);
            UIManager.I.Toast(lockedMessage);
            return;
        }
        if (locked)
        {
            if (!string.IsNullOrEmpty(requiredFlag) && GameManager.I.Has(requiredFlag))
            {
                locked = false;
                am.PlayAt(am.unlock, transform.position + Vector3.up, 0.9f);
                if (!string.IsNullOrEmpty(unlockMessage)) UIManager.I.Toast(unlockMessage);
            }
            else
            {
                am.PlayAt(am.rattle, transform.position + Vector3.up, 0.8f);
                UIManager.I.Toast(lockedMessage);
                return;
            }
        }
        Open();
    }

    public void Open()
    {
        if (open) return;
        open = true;
        AudioManager.I.PlayAt(AudioManager.I.creak, transform.position + Vector3.up, 0.8f);
        if (!string.IsNullOrEmpty(flagOnOpen)) GameManager.I.Set(flagOnOpen);
        if (!string.IsNullOrEmpty(objectiveOnOpen)) GameManager.I.SetObjective(objectiveOnOpen);
    }

    void Update()
    {
        if (!open || t >= 1f) return;
        t = Mathf.Min(1f, t + Time.deltaTime / 1.8f);
        float e = 1f - Mathf.Pow(1f - t, 3f);
        transform.localRotation = Quaternion.Euler(0f, closedYaw + openAngle * e, 0f);
    }
}
