using System.Collections.Generic;
using UnityEngine;

/// Chapter 2 — "Someone Was Here": the residential floor above the studio.
///
///   Hallway H1 runs east (x 0..24, z 0..2); H2 turns north (x 22..24, z 2..14) to the stairwell door.
///   Apartment A (x 1..9,  z 2..9)   — the hall clock          ("The Wrong Time")
///   Apartment B (x 10..18, z 2..9)  — the Kerr family photo   ("The Missing Person")
///   Apartment C (x 15..22, z 10..16) — the kitchen and the doll ("Do Not Photograph It")
public static partial class SceneBuilder
{
    static readonly Color Amber = new Color(1f, 0.62f, 0.2f);

    static void BuildChapter2()
    {
        var scene = BeginScene("Meshes_Chapter2");
        SetupRenderSettings();
        RenderSettings.ambientLight = new Color(0.075f, 0.075f, 0.085f);
        RenderSettings.fogDensity = 0.03f;
        RenderSettings.fogColor = new Color(0.01f, 0.012f, 0.015f);

        var hallLights = new List<FlickerLight>();
        var nodes = new Dictionary<string, GhostNode>();

        BuildCh2Hallway(hallLights, out var sequenceFrames, out var sequenceCam);
        BuildCh2ApartmentA();
        BuildCh2ApartmentB();
        BuildCh2ApartmentC(out var doll);
        BuildCh2Nodes(nodes);

        // The Figure, hunting.
        var ghostGo = new GameObject("Ghost");
        ghostGo.transform.position = new Vector3(23, 0, 13);
        var model = Figure("GhostModel", Vector3.zero, 0, Layers.CameraOnly);
        model.transform.SetParent(ghostGo.transform, false);
        model.transform.localPosition = Vector3.zero;
        var ghost = ghostGo.AddComponent<GhostHunter>();
        ghost.model = model;

        var seqFigure = Figure("SequenceFigure", new Vector3(10, 0, 1), -90, Layers.CameraOnly);
        var endFigure = Figure("EndingFigure", new Vector3(23, 0, 15.6f), 180, Layers.CameraOnly);
        var landing = Empty("StairLanding", new Vector3(23f, 0, 15.7f)).transform;

        var fl = Empty("FlashLight", new Vector3(23, 1.6f, 13.5f)).AddComponent<Light>();
        fl.type = LightType.Point;
        fl.color = new Color(1f, 0.97f, 0.92f);
        fl.range = 9f;
        fl.intensity = 7f;
        fl.shadows = LightShadows.Hard;
        fl.lightmapBakeType = LightmapBakeType.Realtime;
        fl.enabled = false;

        var sys = BuildPlayerAndSystems(new Vector3(1.2f, 0.05f, 1.0f), 90f);
        var cfg = sys.AddComponent<ChapterConfig>();
        cfg.number = 2;
        cfg.title = "Someone Was Here";
        cfg.startObjective = "Read the note left on the hall table.";
        cfg.startHint = "[Shift] Sprint     [Right Mouse] Camera     [E] Interact";
        cfg.startFilm = 4;
        cfg.clockStartMinutes = 7;
        cfg.startAfterMidnight = true;
        cfg.endLine1 = "It was never following you.";
        cfg.endLine2 = "It was following the camera.";
        cfg.teaser = "Chapter 3 — The Negative — is still developing.";

        var dir = sys.AddComponent<Chapter2Director>();
        dir.ghost = ghost;
        dir.aptA = Zone("ZoneAptA", new Vector3(5, 1.5f, 5.6f), new Vector3(7.6f, 3, 6.6f));
        dir.aptB = Zone("ZoneAptB", new Vector3(14, 1.5f, 5.6f), new Vector3(7.6f, 3, 6.6f));
        dir.aptC = Zone("ZoneAptC", new Vector3(18.5f, 1.5f, 13), new Vector3(6.6f, 3, 5.6f));
        dir.hallLights = hallLights.ToArray();
        dir.sequenceFrames = sequenceFrames.ToArray();
        dir.sequenceFigure = seqFigure;
        dir.sequenceCamera = sequenceCam;
        dir.dollNode = nodes["c_door"];
        dir.endingFigure = endFigure;
        dir.flashLight = fl;
        dir.stairLanding = landing;
        dir.doll = doll;

        EndScene(scene, Ch2Path);
    }

    // ------------------------------------------------------------------

    static void Arch(Vector3 center, bool alongX, float width = 1.2f)
    {
        DoorTrim(center, alongX, width, 2.2f);
    }

    static void BuildCh2Hallway(List<FlickerLight> hallLights, out List<Renderer> frames, out Transform sequenceCam)
    {
        // Floors / ceilings
        FloorCeiling("H1", new Vector3(12, 0, 1), new Vector2(24, 2), "Floor");
        FloorCeiling("H2", new Vector3(23, 0, 8.25f), new Vector2(2, 12.5f), "Floor");
        FloorCeiling("Landing", new Vector3(23, 0, 15.25f), new Vector2(2, 2.5f), "Concrete", "DarkPaint");
        Box("Runner", new Vector3(11.5f, 0.006f, 1f), new Vector3(21f, 0.012f, 0.9f), "Carpet", null, 1f, false);
        Box("Runner2", new Vector3(23f, 0.006f, 8f), new Vector3(0.9f, 0.012f, 11f), "Carpet", null, 1f, false);

        // Walls
        Wall("H1_S", new Vector3(0, 0, 0), new Vector3(24, 0, 0), "WallpaperBlue", "WallpaperBlue");
        Wall("H1_W", new Vector3(0, 0, 0), new Vector3(0, 0, 2), "WallpaperBlue", "WallpaperBlue");
        Wall("H1_N1", new Vector3(0, 0, 2), new Vector3(4.4f, 0, 2), "WallpaperBlue", "WallpaperRose");
        Wall("H1_N2", new Vector3(5.6f, 0, 2), new Vector3(9.5f, 0, 2), "WallpaperBlue", "WallpaperRose");
        Wall("H1_N3", new Vector3(9.5f, 0, 2), new Vector3(13.4f, 0, 2), "WallpaperBlue", "WallpaperGreen");
        Wall("H1_N4", new Vector3(14.6f, 0, 2), new Vector3(18f, 0, 2), "WallpaperBlue", "WallpaperGreen");
        Wall("H1_N5", new Vector3(18f, 0, 2), new Vector3(22f, 0, 2), "WallpaperBlue", "WallpaperBlue");
        Wall("H1_N_lintelA", new Vector3(4.4f, 0, 2), new Vector3(5.6f, 0, 2), "WallpaperBlue", "WallpaperRose", 2.2f, 3f);
        Wall("H1_N_lintelB", new Vector3(13.4f, 0, 2), new Vector3(14.6f, 0, 2), "WallpaperBlue", "WallpaperGreen", 2.2f, 3f);
        Wall("H_E", new Vector3(24, 0, 0), new Vector3(24, 0, 16.5f), "WallpaperBlue", "WallpaperBlue");
        Wall("H2_W1", new Vector3(22, 0, 2), new Vector3(22, 0, 10.4f), "Plaster", "WallpaperBlue");
        Wall("H2_W2", new Vector3(22, 0, 11.6f), new Vector3(22, 0, 14f), "Plaster", "WallpaperBlue");
        Wall("H2_W_lintel", new Vector3(22, 0, 10.4f), new Vector3(22, 0, 11.6f), "Plaster", "WallpaperBlue", 2.2f, 3f);
        Wall("Stair_S1", new Vector3(22, 0, 14), new Vector3(22.4f, 0, 14), "WallpaperBlue", "DarkPaint");
        Wall("Stair_S2", new Vector3(23.6f, 0, 14), new Vector3(24, 0, 14), "WallpaperBlue", "DarkPaint");
        Wall("Stair_lintel", new Vector3(22.4f, 0, 14), new Vector3(23.6f, 0, 14), "WallpaperBlue", "DarkPaint", 2.2f, 3f);
        Wall("Landing_W", new Vector3(22, 0, 14), new Vector3(22, 0, 16.5f), "Plaster", "DarkPaint");
        Wall("Landing_N", new Vector3(22, 0, 16.5f), new Vector3(24, 0, 16.5f), "DarkPaint", "DarkPaint");
        Box("StairWell", new Vector3(23f, 0.01f, 16.0f), new Vector3(1.7f, 0.02f, 0.8f), "Black", null, 1f, false);
        Prim(PrimitiveType.Cylinder, "Rail", new Vector3(23f, 0.95f, 15.55f), new Vector3(0.04f, 0.85f, 0.04f), "DarkMetal", null, Quaternion.Euler(0, 0, 90));

        Arch(new Vector3(5f, 0, 1.88f), true);
        Arch(new Vector3(14f, 0, 1.88f), true);
        Arch(new Vector3(22.12f, 0, 11f), false);

        // Lights: weak, unreliable.
        hallLights.Add(HangingBulb("H_Light1", new Vector3(3f, 2.55f, 1f), 0.95f, 5.5f, true));
        hallLights.Add(HangingBulb("H_Light2", new Vector3(11f, 2.55f, 1f), 0.85f, 5.5f, false, true));
        hallLights.Add(HangingBulb("H_Light3", new Vector3(19f, 2.55f, 1f), 0.9f, 5.5f, true));
        hallLights.Add(HangingBulb("H_Light4", new Vector3(23f, 2.55f, 6f), 0.85f, 5.5f, false, true));
        hallLights.Add(HangingBulb("H_Light5", new Vector3(23f, 2.55f, 12f), 0.9f, 5.5f, true));
        Text3D("ExitSign", "EXIT", new Vector3(23f, 2.45f, 13.86f), Quaternion.identity, 0.12f, new Color(1f, 0.15f, 0.1f), 0, null, FontStyle.Bold);
        PointLight("ExitGlow", new Vector3(23f, 2.35f, 13.6f), new Color(1f, 0.15f, 0.1f), 0.5f, 2.5f, false, 0.02f);

        // Service door (the way back is gone)
        var service = Door("ServiceDoor", new Vector3(0.13f, 0, 0.45f), false, 0, "DarkWood");
        var sd = service.AddComponent<DoorInteractable>();
        sd.neverOpens = true;
        sd.lockedMessage = "The service door has locked behind you. There's no going back to the darkroom.";
        DoorTrim(new Vector3(0.1f, 0, 1f), false, 1.1f, 2.2f);

        // Hall table + intro note
        Table("HallTable", new Vector3(2.4f, 0, 0.35f), new Vector3(0.8f, 0.05f, 0.4f), 0.8f);
        var note = Quad("IntroNote", new Vector3(2.4f, 0.806f, 0.35f), Quaternion.Euler(90, 85, 0), new Vector2(0.26f, 0.34f), "Paper", null, 0, true);
        var n = note.AddComponent<NoteInteractable>();
        n.noteTitle = "Pinned to the table, in the photographer's hand";
        n.body = "You came through the darkroom. Then you've seen it.\n\n" +
                 "It follows whoever holds the camera. Through the lens it cannot move — but it hates being watched, and it won't stay still for long. " +
                 "The flash drives it off. Film is scarce: don't waste it.\n\n" +
                 "The stairwell is the only way down. Its lock needs power, and it needs a key.\n\n" +
                 "Don't photograph the doll. And only trust photographs taken BEFORE midnight.\n\n— M.";
        n.flagOnRead = "read_intro2";
        n.objectiveOnRead = "Find a way down. The stairwell lock needs power (a fuse) and a key.";
        n.hintOnRead = "Every photograph costs film. You have 4.";

        // Mailboxes — "The Missing Person"
        Box("MailPanel", new Vector3(10.5f, 1.4f, 0.13f), new Vector3(2.2f, 1.35f, 0.06f), "Brass", null, 0.5f);
        Text3D("MailTitle", "MAIL", new Vector3(10.5f, 2.16f, 0.12f), Quaternion.LookRotation(Vector3.back), 0.08f, new Color(0.75f, 0.6f, 0.3f), 0, null, FontStyle.Bold);
        string[] cols = { "A", "B", "C" };
        for (int row = 0; row < 4; row++)
            for (int c = 0; c < 3; c++)
            {
                string id = $"{row + 1}{cols[c]}";
                float x = 11.15f - c * 0.65f; // viewer faces south, so A is on their left (+x)
                float y = 1.85f - row * 0.3f;
                var pivot = Empty("Mailbox" + id, new Vector3(x, y - 0.13f, 0.18f));
                Box("flap", new Vector3(0, 0.13f, 0), new Vector3(0.58f, 0.26f, 0.03f), "DarkMetal", pivot.transform, 0.5f);
                Text3D("label", id, new Vector3(-0.18f, 0.13f, 0.018f), Quaternion.LookRotation(Vector3.back), 0.07f, new Color(0.9f, 0.85f, 0.7f), 0, pivot.transform, FontStyle.Bold);
                Box("slot", new Vector3(0.08f, 0.18f, 0.017f), new Vector3(0.25f, 0.02f, 0.005f), "Black", pivot.transform, 0.5f, false);
                var mb = pivot.AddComponent<MailboxInteractable>();
                mb.id = id;
                mb.door = pivot.transform;
            }

        // Elevator (out of order)
        Box("ElevatorL", new Vector3(18.55f, 1.1f, 0.13f), new Vector3(0.9f, 2.2f, 0.05f), "Metal", null, 0.6f);
        Box("ElevatorR", new Vector3(19.45f, 1.1f, 0.13f), new Vector3(0.9f, 2.2f, 0.05f), "Metal", null, 0.6f);
        Text3D("ElevatorSign", "OUT OF ORDER", new Vector3(19f, 1.5f, 0.165f), Quaternion.LookRotation(Vector3.back), 0.09f, new Color(0.85f, 0.75f, 0.2f), 0, null, FontStyle.Bold);

        // Camera-only writing
        var writing = Empty("HallWriting", new Vector3(9.5f, 1.8f, 1.88f));
        Text3D("text", "IT FOLLOWS\nTHE CAMERA", Vector3.zero, Quaternion.identity, 0.24f, Red, Layers.CameraOnly, writing.transform, FontStyle.Bold);
        writing.layer = Layers.CameraOnly;
        MakePhotographable(writing, "PHOTO_HALL_WRITING", "Writing in the hallway",
            "Visible only through the lens, across the hallway wall: IT FOLLOWS THE CAMERA.", 7f);

        // The photographs that keep changing (rendered at runtime by the director)
        frames = new List<Renderer>();
        float[] fx = { 3.2f, 6.8f, 13.5f, 16.4f };
        for (int i = 0; i < fx.Length; i++)
        {
            var f = Frame("SequenceFrame" + i, new Vector3(fx[i], 1.6f, 0.12f), Vector3.back, new Vector2(0.56f, 0.32f), "OldPhoto" + i, (i - 1.5f) * 2f);
            frames.Add(f.transform.Find("picture").GetComponent<Renderer>());
        }
        sequenceCam = Empty("SequenceCamera", new Vector3(1.0f, 1.6f, 1.0f), null, Quaternion.LookRotation(Vector3.right)).transform;

        // Fuse box + film shelf in H2
        var fuse = Empty("FuseBox", new Vector3(23.86f, 1.45f, 12.2f), null, Quaternion.LookRotation(Vector3.right));
        Box("box", Vector3.zero, new Vector3(0.45f, 0.55f, 0.14f), "DarkMetal", fuse.transform, 0.5f);
        Text3D("label", "FUSES — STAIRWELL", new Vector3(0, 0.33f, -0.01f), Quaternion.identity, 0.045f, new Color(0.85f, 0.75f, 0.2f), 0, fuse.transform, FontStyle.Bold);
        fuse.AddComponent<FuseBoxInteractable>();

        Box("Shelf", new Vector3(23.8f, 1.0f, 5.2f), new Vector3(0.3f, 0.04f, 0.7f), "DarkWood", null, 0.8f);
        FilmPickup("FilmHall", new Vector3(23.78f, 1.05f, 5.2f), "film_hall", 2);

        // Stairwell door
        var stair = Door("StairDoor", new Vector3(22.45f, 0, 14f), true, 0, "DarkMetal");
        var sdi = stair.AddComponent<StairDoorInteractable>();
        sdi.openAngle = -95f;
        Box("sign", new Vector3(0.55f, 1.7f, -0.04f), new Vector3(0.44f, 0.11f, 0.01f), "Brass", stair.transform, 0.5f, false);
        Text3D("sign_text", "STAIRWELL", new Vector3(0.55f, 1.7f, -0.047f), Quaternion.identity, 0.055f, new Color(0.1f, 0.06f, 0.03f), 0, stair.transform, FontStyle.Bold);
        var prints = Empty("Handprints", new Vector3(0.55f, 1.25f, -0.05f), stair.transform);
        prints.layer = Layers.CameraOnly;
        Vector3[] hp = { new Vector3(-0.25f, 0.1f, 0), new Vector3(0.2f, -0.25f, 0), new Vector3(0.05f, 0.35f, 0), new Vector3(0.3f, 0.2f, 0) };
        for (int i = 0; i < hp.Length; i++)
            Quad("hand", hp[i] + Vector3.back * 0.001f * i, Quaternion.Euler(0, 0, i * 37 - 40), new Vector2(0.2f, 0.2f), "Handprint", prints.transform, Layers.CameraOnly);
        MakePhotographable(prints, "PHOTO_HANDPRINTS", "Handprints on the stairwell door",
            "Through the lens, the stairwell door is covered in handprints — all on this side, as if someone tried to get OUT.", 6f);
        DoorTrim(new Vector3(23f, 0, 13.88f), true, 1.2f, 2.2f);
    }

    static void FilmPickup(string name, Vector3 pos, string flag, int amount)
    {
        var go = Empty(name, pos);
        Prim(PrimitiveType.Cylinder, "canister", new Vector3(0, 0.035f, 0), new Vector3(0.05f, 0.035f, 0.05f), "DarkMetal", go.transform);
        Prim(PrimitiveType.Cylinder, "cap", new Vector3(0, 0.072f, 0), new Vector3(0.052f, 0.004f, 0.052f), "Brass", go.transform);
        Prim(PrimitiveType.Cylinder, "canister2", new Vector3(0.07f, 0.035f, 0.02f), new Vector3(0.05f, 0.035f, 0.05f), "DarkMetal", go.transform);
        go.AddComponent<BoxCollider>().size = new Vector3(0.22f, 0.16f, 0.16f);
        var p = go.AddComponent<PickupInteractable>();
        p.flag = flag;
        p.itemName = $"{amount} rolls of film";
        p.film = amount;
    }

    static void BuildCh2ApartmentA()
    {
        FloorCeiling("AptA", new Vector3(5, 0, 5.5f), new Vector2(8, 7), "Carpet");
        Wall("A_W", new Vector3(1, 0, 2), new Vector3(1, 0, 9), "WallpaperRose", "WallpaperRose");
        Wall("A_N", new Vector3(1, 0, 9), new Vector3(9, 0, 9), "WallpaperRose", "WallpaperRose");
        Wall("A_E", new Vector3(9, 0, 2), new Vector3(9, 0, 9), "WallpaperRose", "WallpaperRose");
        HangingBulb("A_Light", new Vector3(5f, 2.45f, 5.8f), 0.8f, 6f, true);

        // Sofa, coffee table, armchair, lamp
        var sofa = Empty("Sofa", new Vector3(1.7f, 0, 5.2f));
        Box("seat", new Vector3(0.05f, 0.24f, 0), new Vector3(0.8f, 0.48f, 2.0f), "Fabric", sofa.transform, 1f);
        Box("back", new Vector3(-0.32f, 0.62f, 0), new Vector3(0.2f, 0.75f, 2.0f), "Fabric", sofa.transform, 1f);
        Box("arm1", new Vector3(0, 0.5f, -0.95f), new Vector3(0.8f, 0.3f, 0.15f), "Fabric", sofa.transform, 1f);
        Box("arm2", new Vector3(0, 0.5f, 0.95f), new Vector3(0.8f, 0.3f, 0.15f), "Fabric", sofa.transform, 1f);
        Table("CoffeeTable", new Vector3(3.1f, 0, 5.2f), new Vector3(0.6f, 0.04f, 1.1f), 0.42f);
        Chair("Armchair", new Vector3(4.2f, 0, 3.4f), -40, true);
        var lamp = Empty("FloorLamp", new Vector3(8.4f, 0, 2.6f));
        Prim(PrimitiveType.Cylinder, "pole", new Vector3(0, 0.75f, 0), new Vector3(0.03f, 0.75f, 0.03f), "Brass", lamp.transform);
        Prim(PrimitiveType.Cylinder, "shade", new Vector3(0, 1.55f, 0), new Vector3(0.36f, 0.12f, 0.36f), "Paper", lamp.transform);
        PointLight("FloorLampLight", new Vector3(8.4f, 1.45f, 2.6f), new Color(1f, 0.7f, 0.42f), 0.75f, 4.5f, false, 0.08f);
        ScatterPhotos(new Vector3(3.5f, 0, 7f), 1.2f, 7, 31);

        // Note
        Table("SideTable", new Vector3(7.6f, 0, 4.4f), new Vector3(0.6f, 0.04f, 0.6f), 0.7f);
        var note = Quad("AptANote", new Vector3(7.6f, 0.705f, 4.4f), Quaternion.Euler(90, -20, 0), new Vector2(0.26f, 0.34f), "Paper", null, 0, true);
        var n = note.AddComponent<NoteInteractable>();
        n.noteTitle = "Apartment 3A — a note on the side table";
        n.body = "Ada wound the hall clock every night. The night she disappeared, it stopped — and I hid the spare stairwell fuse inside it.\n\n" +
                 "The case only opens if you wind it at the hour she stopped it.\n\n" +
                 "The lens will show you a time. Don't believe it. Every photograph since midnight has lied to me.\n\n" +
                 "There is one picture of the clock taken that evening, BEFORE midnight. I stuck it to the fridge.";
        n.flagOnRead = "read_clock_note";
        n.objectiveOnRead = "Set the hall clock in 3A to the hour Ada stopped it. Find the photo taken before midnight.";

        // Decoy photograph — taken after midnight, shows the lie (9:20)
        var decoy = Frame("ClockPhotoDecoy", new Vector3(3.2f, 1.6f, 8.88f), Vector3.forward, new Vector2(0.3f, 0.38f), "ClockPhotoFalse", 3f);
        Text3D("stamp", "00:31", new Vector3(0.08f, -0.15f, -0.03f), Quaternion.identity, 0.04f, Amber, 0, decoy.transform, FontStyle.Bold);
        Frame("AptAFrame2", new Vector3(7.2f, 1.7f, 8.88f), Vector3.forward, new Vector2(0.45f, 0.34f), "OldPhoto5", -4f);
        Frame("AptAFrame3", new Vector3(1.12f, 1.65f, 7.6f), Vector3.left, new Vector2(0.36f, 0.46f), "Portrait2");

        // --- The hall clock ---
        var clock = Empty("HallClock", new Vector3(5f, 0, 8.65f));
        var ct = clock.transform;
        Box("base", new Vector3(0, 0.25f, 0), new Vector3(0.72f, 0.5f, 0.44f), "DarkWood", ct, 0.6f);
        Box("body", new Vector3(0, 1.15f, 0.01f), new Vector3(0.6f, 1.3f, 0.38f), "DarkWood", ct, 0.6f);
        Box("head", new Vector3(0, 2.18f, 0), new Vector3(0.72f, 0.76f, 0.44f), "DarkWood", ct, 0.6f);
        Box("crown", new Vector3(0, 2.6f, 0), new Vector3(0.78f, 0.08f, 0.47f), "DarkWood", ct, 0.6f);
        Quad("face", new Vector3(0, 2.18f, -0.225f), Quaternion.identity, new Vector2(0.56f, 0.56f), "ClockFace", ct);

        var hourPivot = Empty("HourHand", new Vector3(0, 2.18f, -0.232f), ct);
        Box("hand", new Vector3(0, 0.075f, 0), new Vector3(0.026f, 0.15f, 0.006f), "Black", hourPivot.transform, 1f, false, Layers.NormalOnly);
        var minutePivot = Empty("MinuteHand", new Vector3(0, 2.18f, -0.238f), ct);
        Box("hand", new Vector3(0, 0.11f, 0), new Vector3(0.016f, 0.22f, 0.006f), "Black", minutePivot.transform, 1f, false, Layers.NormalOnly);
        // The lie: through the lens the hands read 9:20.
        var ghostHour = Empty("LensHourHand", new Vector3(0, 2.18f, -0.244f), ct, Quaternion.Euler(0, 0, -(9 * 30f + 10f)));
        Box("hand", new Vector3(0, 0.075f, 0), new Vector3(0.026f, 0.15f, 0.006f), "Black", ghostHour.transform, 1f, false, Layers.CameraOnly);
        var ghostMin = Empty("LensMinuteHand", new Vector3(0, 2.18f, -0.25f), ct, Quaternion.Euler(0, 0, -120f));
        Box("hand", new Vector3(0, 0.11f, 0), new Vector3(0.016f, 0.22f, 0.006f), "Black", ghostMin.transform, 1f, false, Layers.CameraOnly);
        var lensFace = Empty("ClockLens", new Vector3(0, 2.18f, -0.25f), ct);
        lensFace.layer = Layers.CameraOnly;
        MakePhotographable(lensFace, "PHOTO_CLOCK_LENS", "The hall clock, through the lens",
            "Through the lens the stopped clock reads 9:20.\n\nBut this photograph was taken after midnight.", 6f);

        // Pendulum door (opens to reveal the fuse)
        var doorHinge = Empty("PendulumDoor", new Vector3(-0.26f, 0.6f, -0.2f), ct);
        Box("glass", new Vector3(0.26f, 0.5f, 0), new Vector3(0.5f, 0.98f, 0.025f), "Glass", doorHinge.transform, 0.5f);
        Box("frame_t", new Vector3(0.26f, 1.0f, -0.005f), new Vector3(0.52f, 0.04f, 0.03f), "Brass", doorHinge.transform, 0.5f, false);
        Box("frame_b", new Vector3(0.26f, 0.0f, -0.005f), new Vector3(0.52f, 0.04f, 0.03f), "Brass", doorHinge.transform, 0.5f, false);
        Prim(PrimitiveType.Cylinder, "pendulum_rod", new Vector3(0, 1.35f, -0.12f), new Vector3(0.012f, 0.3f, 0.012f), "Brass", ct);
        Prim(PrimitiveType.Cylinder, "pendulum_bob", new Vector3(0, 1.02f, -0.12f), new Vector3(0.16f, 0.01f, 0.16f), "Brass", ct, Quaternion.Euler(90, 0, 0));

        var fuse = Empty("Fuse", new Vector3(0.12f, 0.75f, -0.19f), ct, Quaternion.Euler(0, 0, 90));
        Prim(PrimitiveType.Cylinder, "glass", Vector3.zero, new Vector3(0.035f, 0.05f, 0.035f), "Appliance", fuse.transform);
        Prim(PrimitiveType.Cylinder, "cap1", new Vector3(0, 0.05f, 0), new Vector3(0.04f, 0.012f, 0.04f), "Brass", fuse.transform);
        Prim(PrimitiveType.Cylinder, "cap2", new Vector3(0, -0.05f, 0), new Vector3(0.04f, 0.012f, 0.04f), "Brass", fuse.transform);
        fuse.AddComponent<BoxCollider>().size = new Vector3(0.07f, 0.16f, 0.03f);
        var fusePick = fuse.AddComponent<PickupInteractable>();
        fusePick.flag = "fuse";
        fusePick.itemName = "the stairwell fuse";
        fusePick.message = "Taken: a heavy old fuse, labelled STAIRWELL.";

        var puzzle = clock.AddComponent<ClockPuzzle>();
        puzzle.hourHand = hourPivot.transform;
        puzzle.minuteHand = minutePivot.transform;
        puzzle.door = doorHinge.transform;
        puzzle.doorOpenAngle = 100f;
        puzzle.reward = fusePick;

        void Control(string name, Vector3 pos, Vector3 size, ClockControl.Mode mode)
        {
            var go = Empty(name, pos, ct);
            go.AddComponent<BoxCollider>().size = size;
            var cc = go.AddComponent<ClockControl>();
            cc.clock = puzzle;
            cc.mode = mode;
        }
        var knobL = Prim(PrimitiveType.Sphere, "knob_hour", new Vector3(-0.39f, 2.18f, -0.05f), Vector3.one * 0.08f, "Brass", ct);
        var knobR = Prim(PrimitiveType.Sphere, "knob_minute", new Vector3(0.39f, 2.18f, -0.05f), Vector3.one * 0.08f, "Brass", ct);
        Text3D("knob_hour_label", "H", new Vector3(-0.39f, 2.3f, -0.225f), Quaternion.identity, 0.05f, new Color(0.8f, 0.7f, 0.4f), 0, ct, FontStyle.Bold);
        Text3D("knob_min_label", "M", new Vector3(0.39f, 2.3f, -0.225f), Quaternion.identity, 0.05f, new Color(0.8f, 0.7f, 0.4f), 0, ct, FontStyle.Bold);
        Control("HourKnob", knobL.transform.localPosition, new Vector3(0.14f, 0.3f, 0.5f), ClockControl.Mode.Hour);
        Control("MinuteKnob", knobR.transform.localPosition, new Vector3(0.14f, 0.3f, 0.5f), ClockControl.Mode.Minute);
        Box("winding_key", new Vector3(0, 1.86f, -0.235f), new Vector3(0.08f, 0.05f, 0.03f), "Brass", ct, 0.5f, false);
        Control("WindKey", new Vector3(0, 1.86f, -0.24f), new Vector3(0.24f, 0.12f, 0.06f), ClockControl.Mode.Wind);
    }

    static void BuildCh2ApartmentB()
    {
        FloorCeiling("AptB", new Vector3(14, 0, 5.5f), new Vector2(8, 7), "Floor");
        Wall("B_W", new Vector3(10, 0, 2), new Vector3(10, 0, 9), "WallpaperGreen", "WallpaperGreen");
        Wall("B_N", new Vector3(10, 0, 9), new Vector3(18, 0, 9), "WallpaperGreen", "WallpaperGreen");
        Wall("B_E", new Vector3(18, 0, 2), new Vector3(18, 0, 9), "WallpaperGreen", "WallpaperGreen");
        HangingBulb("B_Light", new Vector3(14f, 2.45f, 5.2f), 0.75f, 6f, false, true);

        // Bed + bedside lamp
        var bed = Empty("Bed", new Vector3(11.2f, 0, 6.8f));
        Box("frame", new Vector3(0, 0.22f, 0), new Vector3(1.4f, 0.44f, 2.1f), "DarkWood", bed.transform, 0.8f);
        Box("sheets", new Vector3(0, 0.5f, 0.05f), new Vector3(1.36f, 0.14f, 1.95f), "Sheets", bed.transform, 1f, false);
        Box("pillow", new Vector3(0, 0.62f, 0.85f), new Vector3(1.0f, 0.12f, 0.35f), "Sheets", bed.transform, 1f, false);
        Box("headboard", new Vector3(0, 0.7f, 1.05f), new Vector3(1.4f, 1.0f, 0.06f), "DarkWood", bed.transform, 0.8f);
        Table("Nightstand", new Vector3(12.4f, 0, 8.4f), new Vector3(0.45f, 0.04f, 0.4f), 0.55f);
        var lb = Prim(PrimitiveType.Sphere, "bedside_bulb", new Vector3(12.4f, 0.7f, 8.4f), Vector3.one * 0.12f, "Bulb");
        PointLight("BedsideLight", new Vector3(12.4f, 0.85f, 8.3f), new Color(1f, 0.68f, 0.4f), 0.6f, 3.5f, false, 0.1f, true, lb.GetComponent<Renderer>());

        // Family photo — "The Missing Person"
        var fam = Empty("FamilyPhoto", new Vector3(14f, 1.7f, 8.87f));
        Box("frame", Vector3.zero, new Vector3(1.44f, 1.0f, 0.05f), "DarkWood", fam.transform, 0.5f, false);
        Quad("picture", new Vector3(0, 0, -0.028f), Quaternion.identity, new Vector2(1.3f, 0.86f), "FamilyPhoto", fam.transform);
        Quad("fifth", new Vector3(0, 0, -0.033f), Quaternion.identity, new Vector2(1.3f, 0.86f), "FamilyFifth", fam.transform, Layers.CameraOnly);
        Text3D("name", "ADA", new Vector3(0.17f, 0.2f, -0.04f), Quaternion.Euler(0, 0, -8), 0.15f, Red, Layers.CameraOnly, fam.transform, FontStyle.Bold);
        Box("plate", new Vector3(0, -0.58f, -0.01f), new Vector3(0.56f, 0.08f, 0.02f), "Brass", fam.transform, 0.5f, false);
        Text3D("plate_text", "THE KERR FAMILY  ·  1979", new Vector3(0, -0.58f, -0.022f), Quaternion.identity, 0.04f, new Color(0.12f, 0.08f, 0.04f), 0, fam.transform, FontStyle.Bold);
        MakePhotographable(fam, "PHOTO_FAMILY_FIFTH", "The Kerr family photo",
            "Four people in the frame. Through the lens, a fifth stands in the middle, pale and smiling. Someone scratched a name under her: ADA.", 6f);

        // Letter + ledger
        Table("Dresser", new Vector3(10.6f, 0, 3.2f), new Vector3(0.5f, 0.05f, 1.0f), 0.85f);
        var letter = Quad("AptBNote", new Vector3(10.6f, 0.856f, 3.2f), Quaternion.Euler(90, 95, 0), new Vector2(0.26f, 0.34f), "Paper", null, 0, true);
        var ln = letter.AddComponent<NoteInteractable>();
        ln.noteTitle = "Apartment 3B — a letter, never sent";
        ln.body = "Mother says there were always four of us in the family photograph.\n\nI count five.\n\n" +
                  "Whoever she is, she kept getting our post. The building ledger on the desk lists every mailbox on this floor — " +
                  "even the people nobody remembers.";
        ln.objectiveOnRead = "Who is the fifth person in the Kerr family photo? Find her mailbox.";

        Table("Desk", new Vector3(16.9f, 0, 3.3f), new Vector3(1.1f, 0.05f, 0.6f), 0.76f);
        Chair("DeskChair", new Vector3(16.9f, 0, 4.0f), 180);
        var ledger = Quad("TenantLedger", new Vector3(16.8f, 0.79f, 3.3f), Quaternion.Euler(90, 180, 0), new Vector2(0.32f, 0.42f), "Paper", null, 0, true);
        var led = ledger.AddComponent<NoteInteractable>();
        led.noteTitle = "Tenant ledger — third floor mailboxes";
        led.body = "1A   Moreau, R.                 1B   Lindqvist, T.\n" +
                   "1C   Hale, Ada                   2A   Kerr, Abe\n" +
                   "2B   Voss, M.                      2C   (vacant)\n" +
                   "3A   Kerr, Ruth                  3B   Hale, E.\n" +
                   "3C   Kerr, Ada                    4A   (vacant)\n" +
                   "4B   Okafor, J.                    4C   Kerr, Lena\n\n" +
                   "Wrong boxes are not empty. — M.";

        // Wardrobe (film + a scare)
        var wr = Empty("Wardrobe", new Vector3(17.6f, 0, 6.2f), null, Quaternion.LookRotation(Vector3.right));
        Box("body", new Vector3(0, 1.0f, 0), new Vector3(1.2f, 2.0f, 0.6f), "DarkWood", wr.transform, 0.8f);
        var hl = Empty("DoorL", new Vector3(-0.6f, 0, -0.31f), wr.transform);
        Box("panel", new Vector3(0.3f, 1.0f, 0), new Vector3(0.6f, 1.92f, 0.03f), "DarkWood", hl.transform, 0.8f, false);
        var hr = Empty("DoorR", new Vector3(0.6f, 0, -0.31f), wr.transform);
        Box("panel", new Vector3(-0.3f, 1.0f, 0), new Vector3(0.6f, 1.92f, 0.03f), "DarkWood", hr.transform, 0.8f, false);
        var w = wr.AddComponent<WardrobeInteractable>();
        w.doorL = hl.transform;
        w.doorR = hr.transform;

        Frame("AptBFrame1", new Vector3(10.12f, 1.7f, 6.5f), Vector3.left, new Vector2(0.4f, 0.3f), "OldPhoto4");
        ScatterPhotos(new Vector3(14.5f, 0, 4f), 1f, 6, 41);
    }

    static void BuildCh2ApartmentC(out Photographable dollPhoto)
    {
        FloorCeiling("AptC", new Vector3(18.5f, 0, 13), new Vector2(7, 6), "KitchenTile");
        Wall("C_W", new Vector3(15, 0, 10), new Vector3(15, 0, 16), "Plaster", "Plaster");
        Wall("C_N", new Vector3(15, 0, 16), new Vector3(22, 0, 16), "Plaster", "Plaster");
        Wall("C_S", new Vector3(15, 0, 10), new Vector3(22, 0, 10), "Plaster", "Plaster");
        PointLight("C_Light", new Vector3(18.5f, 2.6f, 13f), new Color(0.82f, 0.9f, 1f), 1.35f, 7f, true, 0.06f, true);
        Box("C_Tube", new Vector3(18.5f, 2.95f, 13f), new Vector3(1.2f, 0.06f, 0.15f), "Softbox", null, 1f, false);

        // Fridge with the one honest photograph (taken before midnight: 3:40)
        Box("Fridge", new Vector3(16f, 0.9f, 15.55f), new Vector3(0.8f, 1.8f, 0.7f), "Appliance", null, 1f);
        Box("FridgeHandle", new Vector3(16.3f, 1.2f, 15.18f), new Vector3(0.03f, 0.4f, 0.04f), "Metal", null, 1f, false);
        Quad("ClockPhotoTrue", new Vector3(15.92f, 1.35f, 15.185f), Quaternion.Euler(0, 0, 4), new Vector2(0.24f, 0.3f), "ClockPhotoTrue");
        Text3D("TrueStamp", "23:12", new Vector3(15.98f, 1.235f, 15.181f), Quaternion.Euler(0, 0, 4), 0.032f, Amber, 0, null, FontStyle.Bold);
        Prim(PrimitiveType.Sphere, "magnet", new Vector3(15.92f, 1.48f, 15.18f), new Vector3(0.04f, 0.04f, 0.02f), "DollDress");

        // Counter, stove, table
        Box("Counter", new Vector3(19.4f, 0.45f, 15.6f), new Vector3(4.2f, 0.9f, 0.6f), "DarkWood", null, 0.8f);
        Box("CounterTop", new Vector3(19.4f, 0.92f, 15.6f), new Vector3(4.25f, 0.04f, 0.65f), "Appliance", null, 1f, false);
        Box("Stove", new Vector3(21.1f, 0.94f, 15.6f), new Vector3(0.6f, 0.02f, 0.5f), "DarkMetal", null, 1f, false);
        Table("KitchenTable", new Vector3(18.6f, 0, 12.6f), new Vector3(1.2f, 0.05f, 0.8f), 0.76f);
        Chair("KChair1", new Vector3(18.6f, 0, 11.9f), 0);
        Chair("KChair2", new Vector3(19.5f, 0, 13.2f), -70, true);
        FilmPickup("FilmKitchen", new Vector3(20.3f, 0.94f, 15.55f), "film_kitchen", 2);
        ScatterPhotos(new Vector3(17.5f, 0, 14f), 0.8f, 5, 51);

        // The doll — do NOT photograph it
        var doll = Empty("Doll", new Vector3(15.7f, 0, 11.3f), null, Quaternion.Euler(0, 90, 0));
        var dt = doll.transform;
        Box("chair_seat", new Vector3(0, 0.62f, 0), new Vector3(0.4f, 0.04f, 0.4f), "DarkWood", dt, 0.8f);
        Box("chair_back", new Vector3(0, 1.0f, -0.19f), new Vector3(0.4f, 0.76f, 0.04f), "DarkWood", dt, 0.8f, false);
        foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box("leg", new Vector3(s.x * 0.17f, 0.3f, s.y * 0.17f), new Vector3(0.04f, 0.6f, 0.04f), "DarkWood", dt, 0.8f, false);
        Prim(PrimitiveType.Cylinder, "dress", new Vector3(0, 0.8f, 0), new Vector3(0.24f, 0.16f, 0.24f), "DollDress", dt);
        Prim(PrimitiveType.Sphere, "torso", new Vector3(0, 0.98f, 0), new Vector3(0.15f, 0.18f, 0.12f), "DollDress", dt);
        Prim(PrimitiveType.Sphere, "head", new Vector3(0, 1.16f, 0.01f), new Vector3(0.16f, 0.17f, 0.16f), "Porcelain", dt);
        Prim(PrimitiveType.Sphere, "hair", new Vector3(0, 1.2f, -0.03f), new Vector3(0.18f, 0.16f, 0.17f), "FigureBody", dt);
        Prim(PrimitiveType.Sphere, "eye_l", new Vector3(-0.035f, 1.17f, 0.075f), new Vector3(0.03f, 0.03f, 0.015f), "Black", dt);
        Prim(PrimitiveType.Sphere, "eye_r", new Vector3(0.035f, 1.17f, 0.075f), new Vector3(0.03f, 0.03f, 0.015f), "Black", dt);
        Prim(PrimitiveType.Sphere, "mouth", new Vector3(0, 1.115f, 0.075f), new Vector3(0.025f, 0.01f, 0.01f), "DollDress", dt);
        dollPhoto = doll.AddComponent<Photographable>();
        dollPhoto.clueId = "PHOTO_DOLL";
        dollPhoto.title = "The doll";
        dollPhoto.description = "You were told not to.";
        dollPhoto.countsAsClue = false;
        dollPhoto.maxDistance = 7f;
        dollPhoto.focusOffset = new Vector3(0, 1.1f, 0);

        var writing = Empty("KitchenWriting", new Vector3(15.12f, 1.9f, 13.4f), null, Quaternion.LookRotation(Vector3.left));
        Text3D("text", "SHE SEES\nTHROUGH THE LENS", Vector3.zero, Quaternion.identity, 0.16f, Red, Layers.CameraOnly, writing.transform, FontStyle.Bold);
        writing.layer = Layers.CameraOnly;
        MakePhotographable(writing, "PHOTO_KITCHEN_WRITING", "Writing above the doll",
            "Only the lens shows it, scrawled above the doll's chair: SHE SEES THROUGH THE LENS.", 7f);
    }

    static void BuildCh2Nodes(Dictionary<string, GhostNode> nodes)
    {
        var root = new GameObject("GhostNodes").transform;
        GhostNode N(string id, float x, float z)
        {
            var go = new GameObject("node_" + id);
            go.transform.SetParent(root, false);
            go.transform.position = new Vector3(x, 0, z);
            var n = go.AddComponent<GhostNode>();
            nodes[id] = n;
            return n;
        }
        void L(string a, string b) => nodes[a].links.Add(nodes[b]);

        N("h0", 1.5f, 1); N("h1", 5, 1); N("h2", 9, 1); N("h3", 14, 1); N("h4", 18.5f, 1); N("h5", 23, 1);
        N("v0", 23, 4.5f); N("v1", 23, 8); N("v2", 23, 11); N("v3", 23, 13.3f);
        N("a_door", 5, 3); N("a_mid", 5, 5.5f); N("a_w", 3, 7.6f); N("a_e", 7.5f, 7.4f);
        N("b_door", 14, 3); N("b_mid", 14, 5.5f); N("b_w", 11.5f, 4f); N("b_e", 16.2f, 7.6f);
        N("c_door", 21, 11); N("c_mid", 18.5f, 11.2f); N("c_n", 18f, 14.6f); N("c_w", 16.6f, 13.6f);

        L("h0", "h1"); L("h1", "h2"); L("h2", "h3"); L("h3", "h4"); L("h4", "h5"); L("h5", "v0");
        L("v0", "v1"); L("v1", "v2"); L("v2", "v3");
        L("h1", "a_door"); L("a_door", "a_mid"); L("a_mid", "a_w"); L("a_mid", "a_e");
        L("h3", "b_door"); L("b_door", "b_mid"); L("b_mid", "b_w"); L("b_mid", "b_e");
        L("v2", "c_door"); L("c_door", "c_mid"); L("c_mid", "c_n"); L("c_mid", "c_w"); L("c_n", "c_w");
    }
}
