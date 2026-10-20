using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

/// Plays Chapter 1 start to finish without input when launched with "-autoplay [-shots <dir>]".
/// Logs every checkpoint as [AUTOPLAY] and writes screenshots, so builds can be verified headlessly.
public partial class AutoPlaytest : MonoBehaviour
{
    static bool ran;
    string shotDir;
    int shotIndex;
    int failures;

    void Start()
    {
        var args = Environment.GetCommandLineArgs();
        bool trailer = args.Contains("-trailer");
        if (!(args.Contains("-autoplay") || trailer) || ran) { enabled = false; return; }
        ran = true;
        PlayerController.ScriptedLook = true;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject); // survives the restart check at the end
        int i = Array.IndexOf(args, "-shots");
        shotDir = i >= 0 && i + 1 < args.Length ? args[i + 1] : Path.Combine(Application.persistentDataPath, "autoplay");
        Directory.CreateDirectory(shotDir);
        StartCoroutine(trailer ? RunTrailer() : args.Contains("-chapter2") ? RunChapter2() : Run());
    }

    void Log(string s) => Debug.Log("[AUTOPLAY] " + s);

    void Check(bool ok, string what)
    {
        if (!ok) failures++;
        Log((ok ? "PASS " : "FAIL ") + what);
        if (!ok) LogHit();
    }

    void LogHit()
    {
        var cam = PlayerController.I.cam.transform;
        if (Physics.Raycast(cam.position, cam.forward, out var hit, 5f, ~(Layers.PlayerBodyMask | Layers.CameraOnlyMask), QueryTriggerInteraction.Ignore))
            Log($"   ray hit '{hit.collider.name}' (parent '{hit.collider.transform.parent?.name}') at {hit.distance:0.00}m, cam {cam.position} fwd {cam.forward}");
        else Log("   ray hit nothing");
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        string path = Path.Combine(shotDir, $"{shotIndex++:00}_{name}.png");
        ScreenCapture.CaptureScreenshot(path);
        yield return null;
        yield return null;
    }

    static T Find<T>(string name) where T : Component =>
        FindObjectsByType<T>(FindObjectsInactive.Include).First(c => c.name == name);

    IEnumerator Wait(float s) { yield return new WaitForSecondsRealtime(s); }

    IEnumerator Photograph(Vector3 standAt, Vector3 lookAt, string shotName)
    {
        var p = PlayerController.I;
        p.Teleport(standAt);
        p.LookAt(lookAt);
        yield return Wait(0.3f);
        CameraTool.I.Raise();
        yield return Wait(0.8f);
        yield return Shot(shotName + "_viewfinder");
        CameraTool.I.TryCapture();
        yield return Wait(0.5f);
        yield return Shot(shotName + "_captured");
        yield return Wait(1.3f);
        CameraTool.I.Lower();
        yield return Wait(0.5f);
    }

    IEnumerator Run()
    {
        var gm = GameManager.I;
        var p = PlayerController.I;
        yield return Wait(1.5f);
        yield return Shot("menu");

        gm.StartGame();
        yield return Wait(3f);
        yield return Shot("reception");
        Check(gm.IsPlaying, "game starts in Playing state");

        // Note
        var note = Find<NoteInteractable>("ReceptionNote");
        p.LookAt(note.transform.position);
        yield return Wait(0.4f);
        Check(p.Focused == note, "reception note is focusable from the start position");
        note.Interact();
        yield return Wait(0.8f);
        yield return Shot("note");
        UIManager.I.CloseReading();
        Check(gm.Has("read_intro"), "note sets read_intro");
        yield return Wait(0.5f);

        // Camera-only writing + footprints
        var writing = Find<Photographable>("WallWriting");
        yield return Photograph(new Vector3(3f, 0.05f, 2.4f), writing.FocusPoint, "wall_writing");
        Check(EvidenceManager.I.Found.Contains(writing.clueId), "wall writing registered as evidence");

        // The Door That Isn't There
        var symbol = Find<Photographable>("EyeSymbol");
        p.Teleport(new Vector3(4.6f, 0.05f, 2.6f));
        p.LookAt(symbol.FocusPoint);
        yield return Wait(0.5f);
        yield return Shot("hidden_wall_normal");
        yield return Photograph(new Vector3(4.6f, 0.05f, 2.6f), symbol.FocusPoint, "eye_symbol");
        yield return Wait(1.5f);
        Check(gm.Has("door_revealed"), "photographing the symbol reveals the door");
        yield return Shot("door_revealed");

        var studioDoor = Find<DoorInteractable>("StudioDoor");
        p.Teleport(new Vector3(4.9f, 0.05f, 4f));
        p.LookAt(studioDoor.transform.position + Vector3.up * 1.2f + Vector3.forward * 0.5f);
        yield return Wait(0.3f);
        Check(p.Focused == studioDoor, "revealed door is interactable");
        studioDoor.Interact();
        yield return Wait(2f);
        Check(studioDoor.IsOpen, "studio door opens");

        // Studio event
        p.Teleport(new Vector3(7.5f, 0.05f, 4f));
        p.LookAt(new Vector3(12f, 1.5f, 5f));
        yield return Wait(3.4f);
        yield return Shot("studio_event");
        Check(gm.Has("studio_entered"), "studio event fired");

        var studioNote = Find<NoteInteractable>("StudioNote");
        studioNote.Interact();
        yield return Wait(0.6f);
        yield return Shot("studio_note");
        UIManager.I.CloseReading();

        // Portraits + backdrop clue
        var backdrop = Find<Photographable>("BackdropWriting");
        yield return Photograph(new Vector3(10.5f, 0.05f, 4f), backdrop.FocusPoint, "backdrop");
        Check(EvidenceManager.I.Found.Contains(backdrop.clueId), "backdrop clue registered");

        for (int k = 1; k <= 4; k++)
        {
            var portrait = Find<Photographable>("Portrait" + k);
            Vector3 stand = portrait.FocusPoint + new Vector3(0f, 0f, -2.6f);
            stand.y = 0.05f;
            yield return Photograph(stand, portrait.FocusPoint, "portrait" + k);
            Check(EvidenceManager.I.Found.Contains(portrait.clueId), $"portrait {k} registered");
        }

        // Gallery
        gm.OpenGallery();
        yield return Wait(0.6f);
        yield return Shot("gallery");
        gm.Resume();
        yield return Wait(0.3f);

        // Combination lock: try a wrong code first, then the right one.
        var lockRef = FindAnyObjectByType<CombinationLock>();
        p.Teleport(new Vector3(11f, 0.05f, 1.9f));
        p.LookAt(lockRef.dials[1].transform.position);
        yield return Wait(0.4f);
        Check(p.Focused is DialInteractable, "dial is interactable");
        lockRef.dials[0].Interact();
        yield return Wait(0.3f);
        Check(!lockRef.Solved, "wrong code does not open the drawer");
        for (int d = 0; d < 4; d++)
            while (lockRef.dials[d].value != lockRef.code[d]) lockRef.dials[d].Interact();
        yield return Wait(2f);
        Check(lockRef.Solved && gm.Has("drawer_open"), "correct code opens the drawer");
        p.LookAt(lockRef.reward.transform.position);
        yield return Wait(0.3f);
        yield return Shot("drawer_open");
        Check(p.Focused == lockRef.reward, "key is reachable once the drawer is open");
        lockRef.reward.Interact();
        Check(gm.Has("darkroom_key"), "key collected");
        yield return Wait(6f);
        var changed = EvidenceManager.I.Photos.Count(e => e.title.Contains("(changed)"));
        Check(changed == 1, "one earlier photograph was altered after taking the key");
        gm.OpenGallery();
        yield return Wait(0.5f);
        yield return Shot("gallery_changed");
        gm.Resume();

        // Corridor signature scare
        p.Teleport(new Vector3(17.5f, 0.05f, 8f));
        p.LookAt(new Vector3(29.5f, 1.6f, 8f));
        yield return Wait(2.2f);
        yield return Shot("corridor_normal");
        CameraTool.I.Raise();
        yield return Wait(0.9f);
        yield return Shot("corridor_viewfinder");
        CameraTool.I.TryCapture();
        yield return Wait(0.8f);
        yield return Shot("corridor_review_before");
        yield return Wait(1.8f);
        yield return Shot("corridor_review_after");
        Check(gm.AfterMidnight, "corridor capture strikes midnight");
        CameraTool.I.Lower();
        yield return Wait(2f);
        p.LookAt(p.HeadPosition - p.FlatForward * 3f);
        CameraTool.I.Raise();
        yield return Wait(0.35f);
        yield return Shot("behind_you");
        yield return Wait(1f);
        CameraTool.I.Lower();
        yield return Wait(1f);

        // Darkroom
        var darkDoor = Find<DoorInteractable>("DarkroomDoor");
        p.Teleport(new Vector3(28f, 0.05f, 7.7f));
        p.LookAt(darkDoor.transform.position + new Vector3(0.5f, 1.2f, 0f));
        yield return Wait(0.3f);
        Check(p.Focused == darkDoor, "darkroom door is interactable");
        darkDoor.Interact();
        yield return Wait(2f);
        Check(darkDoor.IsOpen, "key opens the darkroom");

        var photo = FindAnyObjectByType<DarkroomPhotoInteractable>();
        p.Teleport(new Vector3(28f, 0.05f, 10.3f));
        p.LookAt(photo.transform.position);
        yield return Wait(1.5f);
        yield return Shot("darkroom");
        Check(p.Focused == photo, "developing photograph is interactable");
        photo.Interact();
        yield return Wait(0.8f);
        yield return Shot("ending_print");
        UIManager.I.CloseReading();

        var flash = HorrorDirector.I.flashLight;
        float timeout = 10f;
        while (!flash.enabled && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        yield return Shot("ending_flash");
        Check(timeout > 0f, "ending flash fires");
        yield return Wait(9f);
        yield return Shot("ending_card");
        Check(gm.State == GameState.Ending, "ending card reached");

        Log($"Evidence {EvidenceManager.I.Found.Count}/{EvidenceManager.I.TotalClues}, photos {EvidenceManager.I.Photos.Count}");
        Log(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");

        // Restart support
        gm.Restart();
        yield return Wait(2f);
        Log("restart -> state " + GameManager.I.State);
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, $"{shotIndex++:00}_after_restart.png"));
        yield return Wait(0.5f);
        Application.Quit();
    }
}
