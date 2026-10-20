using System.Collections.Generic;
using UnityEngine;

/// Something the camera can register as evidence. Each clue has a stable ID (e.g. PHOTO_PORTRAIT_01).
public class Photographable : MonoBehaviour
{
    public static readonly List<Photographable> All = new List<Photographable>();

    public string clueId = "PHOTO_UNKNOWN";
    public string title = "Photograph";
    [TextArea] public string description = "";
    public float maxDistance = 8f;
    public bool countsAsClue = true;
    public Vector3 focusOffset;

    public bool IsPhotographed { get; private set; }
    public event System.Action onPhotographed;

    public Vector3 FocusPoint => transform.TransformPoint(focusOffset);

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Photographed()
    {
        bool first = !IsPhotographed;
        IsPhotographed = true;
        if (first) onPhotographed?.Invoke();
    }
}
