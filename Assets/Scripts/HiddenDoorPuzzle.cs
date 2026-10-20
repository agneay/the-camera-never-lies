using System.Collections;
using UnityEngine;

/// "The Door That Isn't There": a solid wall that the camera shows as a doorway.
/// Photographing the symbol on the phantom door makes the doorway real.
public class HiddenDoorPuzzle : MonoBehaviour
{
    public Photographable symbol;
    public GameObject fakeWall;          // NormalOnly, blocks the player
    public GameObject[] phantomOnly;     // CameraOnly pieces that disappear once the door is real
    public GameObject[] becomeReal;      // CameraOnly pieces that move into normal reality
    public GameObject realDoor;          // hidden until solved

    bool solved;

    void Start()
    {
        symbol.onPhotographed += Solve;
        realDoor.SetActive(false);
    }

    void Solve()
    {
        if (solved) return;
        solved = true;
        StartCoroutine(Run());
    }

    IEnumerator Run()
    {
        yield return new WaitForSeconds(0.3f);
        var am = AudioManager.I;
        am.PlayAt(am.warp, symbol.transform.position, 1f, 2f);
        CameraTool.I.effect.GlitchPulse(0.9f, 1.6f);
        yield return new WaitForSeconds(1.45f);

        fakeWall.SetActive(false);
        foreach (var g in phantomOnly) if (g) g.SetActive(false);
        foreach (var g in becomeReal) if (g) Layers.SetRecursive(g, 0);
        realDoor.SetActive(true);

        GameManager.I.Set("door_revealed");
        GameManager.I.SetObjective("Go through the door that wasn't there.");
        UIManager.I.Toast("The wall is gone. The door is real now.", 4f);
    }
}
