using System.Collections;
using System.Linq;
using UnityEngine;

/// Chapter 2 autoplay ("-autoplay -chapter2"): ghost rules, death + retry, every puzzle (including wrong answers), escape.
public partial class AutoPlaytest
{
    IEnumerator RunChapter2()
    {
        yield return Wait(1f);
        GameManager.I.LoadChapter(GameManager.Chapter2Scene);
        yield return Wait(3f);

        var gm = GameManager.I;
        var p = PlayerController.I;
        var dir = Chapter2Director.I;
        var ghost = GhostHunter.I;
        dir.AutoHunts = false;      // the test schedules hunts itself
        ghost.Suspended = true;

        yield return Shot("c2_start");
        Check(gm.IsPlaying, "chapter 2 starts in Playing state");
        Check(CameraTool.I.Film == 4, "chapter 2 starts with 4 film");
        Check(ghost.State == GhostState.Dormant, "ghost starts dormant");

        var intro = Find<NoteInteractable>("IntroNote");
        p.LookAt(intro.transform.position);
        yield return Wait(0.4f);
        Check(p.Focused == intro, "intro note is focusable");
        intro.Interact();
        yield return Wait(0.6f);
        yield return Shot("c2_intro_note");
        UIManager.I.CloseReading();

        // Hallway photographs change as you pass them.
        p.Teleport(new Vector3(5f, 0.05f, 1f));
        yield return Wait(0.3f);
        p.Teleport(new Vector3(9.5f, 0.05f, 1f));
        yield return Wait(0.3f);
        p.LookAt(new Vector3(6.8f, 1.6f, 0.1f));
        yield return Wait(0.4f);
        yield return Shot("c2_sequence_photo");
        Check(dir.SequenceStage == 2, $"hallway photographs advanced (stage {dir.SequenceStage})");

        var writing = Find<Photographable>("HallWriting");
        yield return Photograph(new Vector3(9.5f, 0.05f, 0.4f), writing.FocusPoint, "c2_hall_writing");
        Check(EvidenceManager.I.Found.Contains(writing.clueId), "hall writing registered");
        Check(CameraTool.I.Film == 3, "photograph used one film");

        // Entering an apartment wakes it.
        p.Teleport(new Vector3(5f, 0.05f, 4f));
        p.LookAt(new Vector3(5f, 1.6f, 8f));
        yield return Wait(7f);
        Check(dir.Activated, "entering an apartment activates the Figure");
        yield return Shot("c2_apt_a");

        // --- Ghost rules: freeze, lunge, stun ---
        p.Teleport(new Vector3(6f, 0.05f, 1f));
        p.LookAt(new Vector3(16f, 1.6f, 1f));
        ghost.BeginHunt(new Vector3(16f, 0, 1f));
        ghost.Suspended = false;
        CameraTool.I.Raise();
        yield return Wait(0.8f);
        Vector3 before = ghost.transform.position;
        yield return Wait(1.2f);
        yield return Shot("c2_ghost_in_viewfinder");
        Check(ghost.IsObserved, "ghost is observed through the lens");
        Check((ghost.transform.position - before).magnitude < 0.05f, "ghost cannot move while observed");
        yield return Wait(2.2f);
        Check(ghost.DistanceToPlayer < 9.5f, $"staring too long makes it lunge closer ({ghost.DistanceToPlayer:0.0}m)");
        int film = CameraTool.I.Film;
        CameraTool.I.TryCapture();
        yield return Wait(0.2f);
        yield return Shot("c2_ghost_flash");
        Check(ghost.State == GhostState.Stunned, "flash photograph stuns it");
        Check(CameraTool.I.Film == film - 1, "stun cost one film");
        yield return Wait(1f);
        Check(ghost.State == GhostState.Dormant, "stunned ghost retreats");
        CameraTool.I.Lower();
        yield return Wait(0.6f);

        // --- It moves when not watched; it kills ---
        dir.SaveCheckpoint(false);
        var cp = p.Position;
        ghost.BeginHunt(new Vector3(12f, 0, 1f));
        p.LookAt(new Vector3(0f, 1.6f, 1f)); // back turned
        float d0 = ghost.DistanceToPlayer;
        yield return Wait(1.5f);
        Check(ghost.DistanceToPlayer < d0 - 1f, $"ghost approaches when unobserved ({d0:0.0}m -> {ghost.DistanceToPlayer:0.0}m)");
        float timeout = 12f;
        bool scareShot = false;
        while (gm.State != GameState.Dead && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;
            if (!scareShot && JumpScare.I.Playing) { scareShot = true; yield return Shot("c2_death_jumpscare"); }
            yield return null;
        }
        Check(gm.State == GameState.Dead, "ghost kills the player when it reaches them");
        yield return Wait(2.5f);
        yield return Shot("c2_death_screen");
        dir.Retry();
        yield return Wait(0.5f);
        Check(gm.IsPlaying && Vector3.Distance(p.Position, cp) < 0.5f, "retry returns to the checkpoint");
        Check(dir.Deaths == 1, "death counted");
        ghost.Suspended = true;

        // --- The Wrong Time ---
        var clock = FindAnyObjectByType<ClockPuzzle>();
        var decoy = Find<Transform>("ClockPhotoDecoy");
        p.Teleport(new Vector3(3.2f, 0.05f, 7.3f));
        p.LookAt(decoy.position);
        yield return Wait(0.4f);
        yield return Shot("c2_decoy_photo");
        p.Teleport(new Vector3(5f, 0.05f, 7.2f));
        p.LookAt(clock.transform.position + new Vector3(0, 2.18f, -0.25f));
        yield return Wait(0.4f);
        yield return Shot("c2_clock_normal");
        CameraTool.I.Raise();
        yield return Wait(0.8f);
        yield return Shot("c2_clock_lens");
        CameraTool.I.Lower();
        yield return Wait(0.4f);

        var wind = Find<ClockControl>("WindKey");
        p.LookAt(wind.transform.position);
        yield return Wait(0.3f);
        Check(p.Focused == wind, "clock winding key is interactable");
        dir.AutoHunts = true;
        wind.Interact(); // wrong time (12:00)
        yield return Wait(0.3f);
        yield return Shot("c2_wrong_clock_scare");
        yield return Wait(1f);
        Check(!clock.Solved, "wrong time does not open the clock");
        Check(ghost.State == GhostState.Hunting, "winding at the wrong time starts a hunt");
        dir.AutoHunts = false;
        if (ghost.State == GhostState.Hunting) ghost.EndHunt();

        var hourKnob = Find<ClockControl>("HourKnob");
        p.LookAt(hourKnob.transform.position);
        yield return Wait(0.3f);
        Check(p.Focused == hourKnob, "hour knob is interactable");
        for (int k = 0; k < 3; k++) clock.AddHour();       // 12 -> 3
        for (int k = 0; k < 8; k++) clock.AddMinutes();    // :00 -> :40
        Check(clock.TimeText == "3:40", "clock set to 3:40");
        clock.Wind();
        yield return Wait(2.6f);
        Check(clock.Solved, "3:40 opens the clock");
        p.LookAt(clock.reward.transform.position);
        yield return Wait(0.3f);
        yield return Shot("c2_clock_open");
        Check(p.Focused == clock.reward, "fuse reachable inside the clock");
        clock.reward.Interact();
        Check(gm.Has("fuse"), "fuse collected");

        // --- Kitchen: the true photo, film, the doll ---
        p.Teleport(new Vector3(16.2f, 0.05f, 14.1f));
        p.LookAt(new Vector3(15.92f, 1.35f, 15.18f));
        yield return Wait(0.4f);
        yield return Shot("c2_true_photo");
        var filmK = Find<PickupInteractable>("FilmKitchen");
        filmK.Interact();
        Check(CameraTool.I.Film >= 4, $"kitchen film collected (film {CameraTool.I.Film})");

        var doll = Find<Photographable>("Doll");
        dir.AutoHunts = true;
        yield return Photograph(new Vector3(18.5f, 0.05f, 11.2f), doll.FocusPoint, "c2_doll");
        yield return Wait(0.6f);
        Check(ghost.State == GhostState.Hunting, "photographing the doll unleashes a hunt");
        ghost.EndHunt();
        dir.AutoHunts = false;

        // --- The Missing Person ---
        var fam = Find<Photographable>("FamilyPhoto");
        p.Teleport(new Vector3(14f, 0.05f, 6.2f));
        p.LookAt(fam.FocusPoint);
        yield return Wait(0.4f);
        yield return Shot("c2_family_normal");
        CameraTool.I.Raise();
        yield return Wait(0.8f);
        yield return Shot("c2_family_lens");
        CameraTool.I.Lower();

        var wardrobe = FindAnyObjectByType<WardrobeInteractable>();
        p.Teleport(new Vector3(16.3f, 0.05f, 6.2f));
        p.LookAt(wardrobe.transform.position + Vector3.up * 1.2f);
        yield return Wait(0.3f);
        Check(p.Focused == wardrobe, "wardrobe is interactable");
        int filmBefore = CameraTool.I.Film;
        wardrobe.Interact();
        yield return Wait(0.3f);
        yield return Shot("c2_wardrobe_scare");
        yield return Wait(1.5f);
        Check(CameraTool.I.Film == filmBefore + 3, "wardrobe gives 3 film");

        var wrongBox = Find<MailboxInteractable>("Mailbox1C");
        var rightBox = Find<MailboxInteractable>("Mailbox3C");
        p.Teleport(new Vector3(10.5f, 0.05f, 1.3f));
        p.LookAt(wrongBox.transform.position + Vector3.up * 0.13f);
        yield return Wait(0.3f);
        yield return Shot("c2_mailboxes");
        Check(p.Focused == wrongBox, "mailbox 1C is interactable");
        dir.AutoHunts = true;
        wrongBox.Interact();
        yield return Wait(1.2f);
        Check(ghost.State == GhostState.Hunting && !gm.Has("stair_key"), "wrong mailbox (1C, the other Ada) starts a hunt");
        ghost.EndHunt();
        dir.AutoHunts = false;
        rightBox.Interact();
        yield return Wait(0.5f);
        Check(gm.Has("stair_key"), "mailbox 3C (Ada Kerr) holds the stairwell key");

        // --- Power, the final chase, escape ---
        var stairDoor = FindAnyObjectByType<StairDoorInteractable>();
        var fuseBox = FindAnyObjectByType<FuseBoxInteractable>();
        p.Teleport(new Vector3(23f, 0.05f, 12.2f));
        p.LookAt(fuseBox.transform.position);
        yield return Wait(0.3f);
        Check(p.Focused == fuseBox, "fuse box is interactable");
        dir.AutoHunts = true;
        fuseBox.Interact();
        yield return Wait(3.2f);
        Check(dir.FinalPhase && ghost.State == GhostState.Hunting, "restoring power starts the final hunt");
        yield return Shot("c2_final_phase");
        ghost.EndHunt();
        dir.AutoHunts = false;

        p.Teleport(new Vector3(23f, 0.05f, 13.2f));
        p.LookAt(stairDoor.transform.position + new Vector3(0.55f, 1.2f, 0f));
        yield return Wait(0.3f);
        Check(p.Focused == stairDoor, "stairwell door is interactable");
        stairDoor.Interact();
        timeout = 8f;
        while (!HorrorDirector_FlashOn(dir) && timeout > 0f) { timeout -= Time.unscaledDeltaTime; yield return null; }
        yield return Shot("c2_escape_flash");
        yield return Wait(9f);
        yield return Shot("c2_ending_card");
        Check(gm.State == GameState.Ending, "chapter 2 ending reached");

        Log($"Chapter 2: evidence {EvidenceManager.I.Found.Count}/{EvidenceManager.I.TotalClues}, deaths {dir.Deaths}, film {CameraTool.I.Film}");
        Log(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        yield return Wait(0.5f);
        Application.Quit();
    }

    static bool HorrorDirector_FlashOn(Chapter2Director d) => d.flashLight && d.flashLight.enabled;
}
