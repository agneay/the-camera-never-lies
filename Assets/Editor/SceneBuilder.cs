using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// Builds every chapter level (geometry, props, lights, puzzles, entity, player, systems) from code.
/// Run from the menu "Camera Never Lies/Rebuild Scenes" or with -executeMethod SceneBuilder.BuildAll.
/// Chapter 2 lives in SceneBuilder.Chapter2.cs.
public static partial class SceneBuilder
{
    const string GenDir = "Assets/Generated";
    const string MatDir = GenDir + "/Materials";
    public const string Ch1Path = "Assets/Scenes/Chapter1.unity";
    public const string Ch2Path = "Assets/Scenes/Chapter2.unity";
    public static readonly string[] ScenePaths = { Ch1Path, Ch2Path };

    static readonly Dictionary<string, Material> M = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> boxCache = new Dictionary<string, Mesh>();
    static Mesh meshContainer;
    static Font font;
    static Transform env;

    // Cross-references collected while building.
    static readonly List<FlickerLight> studioLights = new List<FlickerLight>();
    static GameObject corridorFigure, behindFigure, hauntFigure, endingFigure;
    static TriggerZone studioZone, corridorZone, darkroomZone;
    static Transform corridorEnd;
    static Light flashLight;

    static readonly Color Red = new Color(0.75f, 0.09f, 0.06f);

    [MenuItem("Camera Never Lies/Rebuild Scenes")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(MatDir);
        Directory.CreateDirectory("Assets/Scenes");
        AssetDatabase.DeleteAsset("Assets/Scenes/Main.unity");
        AssetDatabase.DeleteAsset(GenDir + "/Meshes.asset");
        EnsureLayers();
        CreateMaterials();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildChapter1();
        BuildChapter2();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Ch1Path, true), new EditorBuildSettingsScene(Ch2Path, true) };

        PlayerSettings.productName = "The Camera Never Lies";
        PlayerSettings.companyName = "Prototype";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneBuilder] Scenes built: " + string.Join(", ", ScenePaths));
    }

    /// Fresh empty scene with its own mesh container asset.
    static UnityEngine.SceneManagement.Scene BeginScene(string meshAssetName)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        boxCache.Clear();
        string meshPath = $"{GenDir}/{meshAssetName}.asset";
        AssetDatabase.DeleteAsset(meshPath);
        meshContainer = new Mesh { name = "_container" };
        AssetDatabase.CreateAsset(meshContainer, meshPath);
        env = new GameObject("Environment").transform;
        return scene;
    }

    static void EndScene(UnityEngine.SceneManagement.Scene scene, string path)
    {
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, path);
    }

    static void BuildChapter1()
    {
        var scene = BeginScene("Meshes_Chapter1");
        studioLights.Clear();
        SetupRenderSettings();

        BuildReception();
        BuildStudio();
        BuildCorridor();
        BuildDarkroom();

        var sys = BuildPlayerAndSystems(new Vector3(3f, 0.05f, 1.3f), 0f);
        var cfg = sys.AddComponent<ChapterConfig>();
        cfg.number = 1;
        cfg.title = "The Empty Studio";
        cfg.startObjective = "Read the message left on the reception desk.";
        cfg.startHint = "[W A S D] Move     [Mouse] Look     [E] Interact";
        cfg.nextScene = GameManager.Chapter2Scene;
        cfg.nextTitle = "Chapter 2: Someone Was Here";
        cfg.endLine1 = "The camera never lies.";
        cfg.endLine2 = "— does it?";

        var hd = sys.AddComponent<HorrorDirector>();
        hd.studioZone = studioZone;
        hd.corridorZone = corridorZone;
        hd.darkroomZone = darkroomZone;
        hd.studioLights = studioLights.ToArray();
        hd.corridorFigure = corridorFigure;
        hd.behindFigure = behindFigure;
        hd.hauntFigure = hauntFigure;
        hd.endingFigure = endingFigure;
        hd.flashLight = flashLight;
        hd.corridorEnd = corridorEnd;

        EndScene(scene, Ch1Path);
    }

    // ======================================================================
    // Setup
    // ======================================================================

    static void EnsureLayers()
    {
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        layers.GetArrayElementAtIndex(Layers.CameraOnly).stringValue = "CameraOnly";
        layers.GetArrayElementAtIndex(Layers.NormalOnly).stringValue = "NormalOnly";
        layers.GetArrayElementAtIndex(Layers.PlayerBody).stringValue = "PlayerBody";
        tm.ApplyModifiedProperties();
    }

    static void SetupRenderSettings()
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.11f, 0.1f, 0.095f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.022f;
        RenderSettings.fogColor = new Color(0.015f, 0.013f, 0.012f);
        RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
        RenderSettings.reflectionIntensity = 0.2f;
    }

    static Material Std(string name, Color c, Texture2D tex = null, float gloss = 0.15f, float metal = 0f)
    {
        var m = new Material(Shader.Find("Standard")) { name = name, color = c };
        if (tex) m.mainTexture = tex;
        m.SetFloat("_Glossiness", gloss);
        m.SetFloat("_Metallic", metal);
        return Save(m);
    }

    static Material Emissive(string name, Color c, Color emission)
    {
        var m = new Material(Shader.Find("Standard")) { name = name, color = c };
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", emission);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return Save(m);
    }

    static Material Unlit(string name, string shader, Color c, Texture2D tex = null)
    {
        var m = new Material(Shader.Find(shader)) { name = name, color = c };
        if (tex) m.mainTexture = tex;
        return Save(m);
    }

    static Material Save(Material m)
    {
        string path = $"{MatDir}/{m.name}.mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(m, path);
        M[m.name] = m;
        return m;
    }

    static void CreateMaterials()
    {
        M.Clear();
        var plaster = ProcTex.Plaster("plaster", new Color(0.6f, 0.55f, 0.47f), 1);
        var ceiling = ProcTex.Plaster("ceiling", new Color(0.42f, 0.4f, 0.37f), 2);
        var paper1 = ProcTex.Wallpaper("wallpaper_green", new Color(0.33f, 0.37f, 0.3f), new Color(0.39f, 0.42f, 0.34f), 8, 3, false);
        var paper2 = ProcTex.Wallpaper("wallpaper_red", new Color(0.33f, 0.15f, 0.12f), new Color(0.4f, 0.2f, 0.15f), 6, 4, true);
        var dark = ProcTex.Plaster("darkpaint", new Color(0.16f, 0.17f, 0.16f), 5);
        var floor = ProcTex.WoodPlanks("floor_planks", 6);
        var wood = ProcTex.Wood("dark_wood", new Color(0.2f, 0.12f, 0.08f), 7);
        var concrete = ProcTex.Concrete("concrete", 8);
        var note = ProcTex.Paper("note_paper", 9);

        Std("Plaster", Color.white, plaster, 0.05f);
        Std("Ceiling", Color.white, ceiling, 0.02f);
        Std("WallpaperGreen", Color.white, paper1, 0.08f);
        Std("WallpaperRed", Color.white, paper2, 0.08f);
        Std("DarkPaint", Color.white, dark, 0.05f);
        Std("Floor", Color.white, floor, 0.3f);
        Std("DarkWood", Color.white, wood, 0.35f);
        Std("Concrete", Color.white, concrete, 0.1f);
        Std("Paper", Color.white, note, 0f);
        Std("Metal", new Color(0.35f, 0.35f, 0.36f), null, 0.5f, 0.8f);
        Std("DarkMetal", new Color(0.12f, 0.12f, 0.13f), null, 0.45f, 0.7f);
        Std("Brass", new Color(0.62f, 0.48f, 0.22f), null, 0.55f, 0.85f);
        Std("Fabric", new Color(0.09f, 0.085f, 0.08f), null, 0.05f);
        Std("Skin", new Color(0.55f, 0.43f, 0.36f), null, 0.2f);
        Std("Backdrop", new Color(0.5f, 0.5f, 0.49f), null, 0.02f);
        Std("Cardboard", new Color(0.42f, 0.33f, 0.22f), null, 0.02f);
        Std("Rug", new Color(0.24f, 0.07f, 0.06f), null, 0.02f);
        Std("TrayWhite", new Color(0.72f, 0.72f, 0.7f), null, 0.6f);
        Std("Chemical", new Color(0.18f, 0.13f, 0.08f), null, 0.9f);
        Std("Bottle", new Color(0.2f, 0.11f, 0.05f), null, 0.85f);
        Std("FigureBody", new Color(0.035f, 0.033f, 0.032f), null, 0.05f);
        Unlit("FigureFace", "Unlit/Color", new Color(0.78f, 0.76f, 0.72f));
        Unlit("Black", "Unlit/Color", Color.black);
        Unlit("EyeSymbol", "Unlit/Transparent", Color.white, ProcTex.EyeSymbol("eye_symbol"));
        Unlit("Footprint", "Unlit/Transparent", Color.white, ProcTex.Footprint("footprint"));
        Emissive("Bulb", new Color(1f, 0.9f, 0.7f), new Color(1.6f, 1.15f, 0.6f));
        Emissive("RedBulb", new Color(0.8f, 0.1f, 0.08f), new Color(2.2f, 0.2f, 0.1f));
        Emissive("Softbox", new Color(0.85f, 0.87f, 0.9f), new Color(0.45f, 0.47f, 0.5f));
        Emissive("Key", new Color(0.78f, 0.6f, 0.25f), new Color(0.25f, 0.18f, 0.05f));

        for (int i = 0; i < 4; i++) Std("Portrait" + i, Color.white, ProcTex.Portrait("portrait_" + i, i), 0.25f);
        for (int i = 0; i < 6; i++) Std("OldPhoto" + i, Color.white, ProcTex.OldPhoto("photo_" + i, i), 0.25f);

        // Chapter 2
        Std("WallpaperBlue", Color.white, ProcTex.Wallpaper("wallpaper_blue", new Color(0.24f, 0.28f, 0.33f), new Color(0.29f, 0.32f, 0.36f), 10, 13, false), 0.08f);
        Std("WallpaperRose", Color.white, ProcTex.Wallpaper("wallpaper_rose", new Color(0.42f, 0.3f, 0.3f), new Color(0.47f, 0.35f, 0.33f), 7, 14, true), 0.08f);
        Std("KitchenTile", Color.white, ProcTex.Tiles("kitchen_tile", new Color(0.62f, 0.62f, 0.58f), new Color(0.3f, 0.32f, 0.3f), 8, 15), 0.45f);
        Std("Carpet", Color.white, ProcTex.Carpet("carpet", new Color(0.27f, 0.12f, 0.1f), 16), 0f);
        Std("ClockFace", Color.white, ProcTex.ClockFace("clock_face"), 0.3f);
        Std("ClockPhotoTrue", Color.white, ProcTex.ClockPhoto("clock_photo_true", 3, 40, 17), 0.25f);
        Std("ClockPhotoFalse", Color.white, ProcTex.ClockPhoto("clock_photo_false", 9, 20, 18), 0.25f);
        Std("FamilyPhoto", Color.white, ProcTex.FamilyPhoto("family_photo"), 0.25f);
        Unlit("FamilyFifth", "Unlit/Transparent", Color.white, ProcTex.FamilyFifth("family_fifth"));
        Unlit("Handprint", "Unlit/Transparent", Color.white, ProcTex.Handprint("handprint"));
        Std("Porcelain", new Color(0.86f, 0.83f, 0.78f), null, 0.75f);
        Std("DollDress", new Color(0.42f, 0.12f, 0.13f), null, 0.1f);
        Std("Appliance", new Color(0.7f, 0.69f, 0.64f), null, 0.5f, 0.1f);
        Std("Glass", new Color(0.05f, 0.06f, 0.07f), null, 0.95f);
        Std("Sheets", new Color(0.55f, 0.53f, 0.5f), null, 0.05f);

        var fx = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/Viewfinder.shader")) { name = "ViewfinderFX" };
        Save(fx);

        var text = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/Shaders/DepthText.shader")) { name = "DepthText" };
        text.mainTexture = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf").material.mainTexture;
        Save(text);
    }

    // ======================================================================
    // Primitive helpers
    // ======================================================================

    static Mesh BoxMesh(Vector3 s, float tile)
    {
        string key = $"{s.x:F3}_{s.y:F3}_{s.z:F3}_{tile:F2}";
        if (boxCache.TryGetValue(key, out var cached)) return cached;

        var verts = new List<Vector3>();
        var norms = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        Vector3[] normals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var n in normals)
        {
            Vector3 up = Mathf.Abs(n.y) > 0.5f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(up, -n);
            float su = Mathf.Abs(Vector3.Dot(s, right)), sv = Mathf.Abs(Vector3.Dot(s, up)), sn = Mathf.Abs(Vector3.Dot(s, n));
            int b = verts.Count;
            for (int k = 0; k < 4; k++)
            {
                float a = (k == 0 || k == 1) ? -1 : 1;
                float c = (k == 1 || k == 2) ? 1 : -1;
                verts.Add(n * sn / 2 + right * (a * su / 2) + up * (c * sv / 2));
                norms.Add(n);
                uvs.Add(new Vector2(a * su / 2 / tile, c * sv / 2 / tile));
            }
            tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
        }
        var mesh = new Mesh { name = "box_" + key };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.AddObjectToAsset(mesh, meshContainer);
        boxCache[key] = mesh;
        return mesh;
    }

    static GameObject Box(string name, Vector3 pos, Vector3 size, string mat, Transform parent = null,
        float tile = 1.5f, bool collider = true, int layer = 0, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent ? parent : env, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size, tile);
        go.AddComponent<MeshRenderer>().sharedMaterial = M[mat];
        if (collider) go.AddComponent<BoxCollider>().size = size;
        go.layer = layer;
        return go;
    }

    static GameObject Prim(PrimitiveType type, string name, Vector3 pos, Vector3 scale, string mat, Transform parent = null,
        Quaternion? rot = null, bool keepCollider = false, int layer = 0)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent ? parent : env, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = M[mat];
        if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.layer = layer;
        return go;
    }

    static GameObject Quad(string name, Vector3 pos, Quaternion rot, Vector2 size, string mat, Transform parent = null, int layer = 0, bool collider = false)
    {
        var go = Prim(PrimitiveType.Quad, name, pos, new Vector3(size.x, size.y, 1), mat, parent, rot, false, layer);
        if (collider) go.AddComponent<BoxCollider>().size = new Vector3(1, 1, 0.05f);
        return go;
    }

    static GameObject Empty(string name, Vector3 pos, Transform parent = null, Quaternion? rot = null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent ? parent : env, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot ?? Quaternion.identity;
        return go;
    }

    static TextMesh Text3D(string name, string text, Vector3 pos, Quaternion rot, float height, Color color, int layer,
        Transform parent = null, FontStyle style = FontStyle.Normal)
    {
        var go = Empty(name, pos, parent, rot);
        var tm = go.AddComponent<TextMesh>();
        tm.text = text;
        tm.font = font;
        tm.fontSize = 96;
        tm.fontStyle = style;
        tm.characterSize = height / 9.6f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = color;
        tm.lineSpacing = 0.9f;
        go.GetComponent<MeshRenderer>().sharedMaterial = M["DepthText"];
        go.AddComponent<DepthText>().template = M["DepthText"];
        go.layer = layer;
        return tm;
    }

    static Light PointLight(string name, Vector3 pos, Color c, float intensity, float range, bool shadows,
        float flicker = 0.05f, bool unstable = false, Renderer bulb = null)
    {
        var go = Empty(name, pos);
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        l.shadowStrength = 0.85f;
        l.shadowBias = 0.03f;
        l.lightmapBakeType = LightmapBakeType.Realtime;
        var f = go.AddComponent<FlickerLight>();
        f.baseIntensity = intensity;
        f.flickerAmount = flicker;
        f.unstable = unstable;
        f.bulb = bulb;
        return l;
    }

    /// Hanging bulb with cord + light. Returns the light's FlickerLight.
    static FlickerLight HangingBulb(string name, Vector3 at, float intensity, float range, bool shadows, bool unstable = false)
    {
        Prim(PrimitiveType.Cylinder, name + "_cord", new Vector3(at.x, (3f + at.y) / 2f + 0.05f, at.z), new Vector3(0.012f, (3f - at.y) / 2f, 0.012f), "Black");
        Prim(PrimitiveType.Cylinder, name + "_socket", at + Vector3.up * 0.07f, new Vector3(0.05f, 0.04f, 0.05f), "DarkMetal");
        var bulb = Prim(PrimitiveType.Sphere, name + "_bulb", at, Vector3.one * 0.09f, "Bulb");
        var l = PointLight(name, at - Vector3.up * 0.08f, new Color(1f, 0.78f, 0.52f), intensity, range, shadows, 0.05f, unstable, bulb.GetComponent<Renderer>());
        return l.GetComponent<FlickerLight>();
    }

    /// Two half-thickness slabs so each side of a wall can have its own finish.
    static void Wall(string name, Vector3 a, Vector3 b, string matNeg, string matPos, float y0 = 0f, float y1 = 3f,
        int layer = 0, Transform parent = null)
    {
        const float t = 0.1f;
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
        Vector3 c = (a + b) / 2f;
        c.y = (y0 + y1) / 2f;
        Vector3 size = alongX ? new Vector3(len + 0.2f, y1 - y0, t) : new Vector3(t, y1 - y0, len + 0.2f);
        Vector3 off = alongX ? new Vector3(0, 0, t / 2) : new Vector3(t / 2, 0, 0);
        Box(name + "_a", c - off, size, matNeg, parent, 1.5f, true, layer);
        Box(name + "_b", c + off, size, matPos, parent, 1.5f, true, layer);
    }

    static void FloorCeiling(string name, Vector3 center, Vector2 size, string floorMat, string ceilMat = "Ceiling")
    {
        Box(name + "_floor", new Vector3(center.x, -0.05f, center.z), new Vector3(size.x + 0.2f, 0.1f, size.y + 0.2f), floorMat, null, 1.6f);
        Box(name + "_ceiling", new Vector3(center.x, 3.05f, center.z), new Vector3(size.x + 0.2f, 0.1f, size.y + 0.2f), ceilMat, null, 2f, false);
    }

    /// Framed picture on a wall. viewDir = direction the viewer looks when facing the wall.
    static GameObject Frame(string name, Vector3 pos, Vector3 viewDir, Vector2 picSize, string picMat, float tiltDeg = 0f, Transform parent = null)
    {
        var rot = Quaternion.LookRotation(viewDir) * Quaternion.Euler(0, 0, tiltDeg);
        var root = Empty(name, pos, parent, rot);
        Box("frame", Vector3.zero, new Vector3(picSize.x + 0.12f, picSize.y + 0.12f, 0.04f), "DarkWood", root.transform, 0.5f, false);
        Quad("picture", new Vector3(0, 0, -0.022f), Quaternion.identity, picSize, picMat, root.transform);
        return root;
    }

    static GameObject Table(string name, Vector3 pos, Vector3 top, float height, string mat = "DarkWood")
    {
        var root = Empty(name, pos);
        Box("top", new Vector3(0, height - top.y / 2, 0), top, mat, root.transform, 0.8f);
        float lx = top.x / 2 - 0.06f, lz = top.z / 2 - 0.06f, lh = height - top.y;
        foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box("leg", new Vector3(s.x * lx, lh / 2, s.y * lz), new Vector3(0.06f, lh, 0.06f), mat, root.transform, 0.8f);
        return root;
    }

    static void Chair(string name, Vector3 pos, float yaw, bool toppled = false)
    {
        var rot = Quaternion.Euler(toppled ? 90 : 0, yaw, toppled ? 15 : 0);
        var root = Empty(name, toppled ? pos + Vector3.up * 0.22f : pos, null, rot);
        Box("seat", new Vector3(0, 0.46f, 0), new Vector3(0.44f, 0.05f, 0.44f), "DarkWood", root.transform, 0.8f);
        Box("back", new Vector3(0, 0.75f, -0.2f), new Vector3(0.44f, 0.55f, 0.04f), "DarkWood", root.transform, 0.8f);
        foreach (var s in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            Box("leg", new Vector3(s.x * 0.19f, 0.22f, s.y * 0.19f), new Vector3(0.04f, 0.44f, 0.04f), "DarkWood", root.transform, 0.8f);
    }

    static void ScatterPhotos(Vector3 center, float radius, int count, int seed)
    {
        var r = new System.Random(seed);
        for (int i = 0; i < count; i++)
        {
            float a = (float)r.NextDouble() * Mathf.PI * 2, d = (float)r.NextDouble() * radius;
            var p = center + new Vector3(Mathf.Cos(a) * d, 0.004f + i * 0.0006f, Mathf.Sin(a) * d);
            Quad("loose_photo", p, Quaternion.Euler(90, (float)r.NextDouble() * 360, 0), new Vector2(0.15f, 0.11f), "OldPhoto" + r.Next(6));
        }
    }

    static GameObject Door(string name, Vector3 hinge, bool alongX, float hingeYaw, string mat, float width = 1.1f)
    {
        var root = Empty(name, hinge, null, Quaternion.Euler(0, hingeYaw, 0));
        Vector3 local = alongX ? new Vector3(width / 2, 1.075f, 0) : new Vector3(0, 1.075f, width / 2);
        Vector3 size = alongX ? new Vector3(width, 2.15f, 0.06f) : new Vector3(0.06f, 2.15f, width);
        Box("panel", local, size, mat, root.transform, 0.8f);
        // raised panels for detail
        for (int i = 0; i < 2; i++)
        {
            float y = 0.6f + i * 0.95f;
            Vector3 inset = alongX ? new Vector3(width / 2, y, 0) : new Vector3(0, y, width / 2);
            Vector3 isz = alongX ? new Vector3(width * 0.7f, 0.75f, 0.075f) : new Vector3(0.075f, 0.75f, width * 0.7f);
            Box("inset", inset, isz, mat, root.transform, 0.8f, false);
        }
        Vector3 handle = alongX ? new Vector3(width - 0.12f, 1.0f, 0) : new Vector3(0, 1.0f, width - 0.12f);
        Prim(PrimitiveType.Sphere, "handle_a", handle + (alongX ? Vector3.back : Vector3.left) * 0.06f, Vector3.one * 0.07f, "Brass", root.transform);
        Prim(PrimitiveType.Sphere, "handle_b", handle + (alongX ? Vector3.forward : Vector3.right) * 0.06f, Vector3.one * 0.07f, "Brass", root.transform);
        return root;
    }

    static void DoorTrim(Vector3 center, bool alongX, float width, float height, int layer = 0, Transform parent = null, float offset = 0f)
    {
        Vector3 o = alongX ? new Vector3(0, 0, offset) : new Vector3(offset, 0, 0);
        Vector3 side = alongX ? Vector3.right : Vector3.forward;
        Vector3 vs = alongX ? new Vector3(0.1f, height, 0.24f) : new Vector3(0.24f, height, 0.1f);
        Box("trim_l", center + o - side * (width / 2 + 0.05f) + Vector3.up * height / 2, vs, "DarkWood", parent, 0.8f, false, layer);
        Box("trim_r", center + o + side * (width / 2 + 0.05f) + Vector3.up * height / 2, vs, "DarkWood", parent, 0.8f, false, layer);
        Vector3 hs = alongX ? new Vector3(width + 0.2f, 0.1f, 0.24f) : new Vector3(0.24f, 0.1f, width + 0.2f);
        Box("trim_t", center + o + Vector3.up * (height + 0.05f), hs, "DarkWood", parent, 0.8f, false, layer);
    }

    static GameObject Figure(string name, Vector3 pos, float yaw, int layer)
    {
        var root = Empty(name, pos, null, Quaternion.Euler(0, yaw, 0));
        var t = root.transform;
        Prim(PrimitiveType.Capsule, "body", new Vector3(0, 1.0f, 0), new Vector3(0.42f, 0.95f, 0.3f), "FigureBody", t);
        Prim(PrimitiveType.Capsule, "arm_l", new Vector3(-0.27f, 1.12f, 0.02f), new Vector3(0.09f, 0.48f, 0.09f), "FigureBody", t, Quaternion.Euler(0, 0, -4));
        Prim(PrimitiveType.Capsule, "arm_r", new Vector3(0.27f, 1.12f, 0.02f), new Vector3(0.09f, 0.48f, 0.09f), "FigureBody", t, Quaternion.Euler(0, 0, 4));
        Prim(PrimitiveType.Sphere, "hand_l", new Vector3(-0.3f, 0.62f, 0.03f), new Vector3(0.07f, 0.12f, 0.06f), "FigureFace", t);
        Prim(PrimitiveType.Sphere, "hand_r", new Vector3(0.3f, 0.62f, 0.03f), new Vector3(0.07f, 0.12f, 0.06f), "FigureFace", t);
        Prim(PrimitiveType.Cylinder, "neck", new Vector3(0, 1.9f, 0), new Vector3(0.09f, 0.08f, 0.09f), "FigureFace", t);
        Prim(PrimitiveType.Sphere, "head", new Vector3(0, 2.07f, 0), new Vector3(0.23f, 0.3f, 0.24f), "FigureFace", t);
        Prim(PrimitiveType.Sphere, "hair", new Vector3(0, 2.13f, -0.045f), new Vector3(0.26f, 0.27f, 0.25f), "FigureBody", t);
        Prim(PrimitiveType.Capsule, "hair_long", new Vector3(0, 1.88f, -0.09f), new Vector3(0.27f, 0.3f, 0.1f), "FigureBody", t);
        Prim(PrimitiveType.Sphere, "eye_l", new Vector3(-0.05f, 2.08f, 0.112f), new Vector3(0.05f, 0.032f, 0.02f), "Black", t);
        Prim(PrimitiveType.Sphere, "eye_r", new Vector3(0.05f, 2.08f, 0.112f), new Vector3(0.05f, 0.032f, 0.02f), "Black", t);
        Box("mouth", new Vector3(0, 1.97f, 0.118f), new Vector3(0.07f, 0.012f, 0.01f), "Black", t, 1f, false);
        Layers.SetRecursive(root, layer);
        return root;
    }

    static TriggerZone Zone(string name, Vector3 center, Vector3 size)
    {
        var z = Empty(name, center).AddComponent<TriggerZone>();
        z.size = size;
        return z;
    }

    static Photographable MakePhotographable(GameObject go, string id, string title, string desc, float maxDist = 8f)
    {
        var p = go.AddComponent<Photographable>();
        p.clueId = id;
        p.title = title;
        p.description = desc;
        p.maxDistance = maxDist;
        return p;
    }

    // ======================================================================
    // Rooms
    // ======================================================================

    static void BuildReception()
    {
        // x 0..6, z 0..6
        FloorCeiling("Reception", new Vector3(3, 0, 3), new Vector2(6, 6), "Floor");
        Wall("Rec_S", new Vector3(0, 0, 0), new Vector3(6, 0, 0), "WallpaperGreen", "WallpaperGreen");
        Wall("Rec_W", new Vector3(0, 0, 0), new Vector3(0, 0, 6), "WallpaperGreen", "WallpaperGreen");
        Wall("Rec_N", new Vector3(0, 0, 6), new Vector3(6, 0, 6), "WallpaperGreen", "WallpaperGreen");
        Wall("Rec_E1", new Vector3(6, 0, 0), new Vector3(6, 0, 3.4f), "WallpaperGreen", "Plaster");
        Wall("Rec_E2", new Vector3(6, 0, 4.6f), new Vector3(6, 0, 6), "WallpaperGreen", "Plaster");
        Wall("Rec_E_lintel", new Vector3(6, 0, 3.4f), new Vector3(6, 0, 4.6f), "WallpaperGreen", "Plaster", 2.2f, 3f);

        HangingBulb("Rec_Light", new Vector3(3, 2.45f, 2.6f), 2.1f, 9f, true);

        // Entrance door (never opens)
        var entrance = Door("EntranceDoor", new Vector3(2.45f, 0, 0.13f), true, 0, "DarkWood");
        var ed = entrance.AddComponent<DoorInteractable>();
        ed.neverOpens = true;
        ed.lockedMessage = "It won't open. It locked behind you when you came in.";
        DoorTrim(new Vector3(3f, 0, 0.1f), true, 1.1f, 2.2f);

        // Desk + note + lamp + chair
        var desk = Empty("Desk", new Vector3(3, 0, 3.7f));
        Box("top", new Vector3(0, 0.76f, 0), new Vector3(1.7f, 0.06f, 0.75f), "DarkWood", desk.transform, 0.8f);
        Box("pedestal_l", new Vector3(-0.6f, 0.365f, 0), new Vector3(0.45f, 0.73f, 0.7f), "DarkWood", desk.transform, 0.8f);
        Box("pedestal_r", new Vector3(0.6f, 0.365f, 0), new Vector3(0.45f, 0.73f, 0.7f), "DarkWood", desk.transform, 0.8f);
        Box("modesty", new Vector3(0, 0.45f, 0.3f), new Vector3(0.8f, 0.5f, 0.03f), "DarkWood", desk.transform, 0.8f);
        Chair("DeskChair", new Vector3(3.0f, 0, 4.45f), 180);

        var note = Quad("ReceptionNote", new Vector3(2.75f, 0.795f, 3.6f), Quaternion.Euler(90, 8, 0), new Vector2(0.26f, 0.34f), "Paper", null, 0, true);
        var n = note.AddComponent<NoteInteractable>();
        n.noteTitle = "A message, addressed to you";
        n.body = "If you are reading this, the camera found its way to you.\n\n" +
                 "Do not trust any photograph taken after midnight.\n\n" +
                 "The studio is through the east wall. The door is still there — you just can't see it. " +
                 "Look through the lens. What the camera sees, it can make real.\n\n— M. Voss";
        n.flagOnRead = "read_intro";
        n.objectiveOnRead = "Find a way into the studio. Look at the east wall through the camera.";
        n.hintOnRead = "[Right Mouse] Raise the camera";

        var lamp = Empty("DeskLamp", new Vector3(3.55f, 0.79f, 3.85f));
        Prim(PrimitiveType.Cylinder, "base", new Vector3(0, 0.01f, 0), new Vector3(0.14f, 0.01f, 0.14f), "Brass", lamp.transform);
        Prim(PrimitiveType.Cylinder, "stem", new Vector3(0, 0.2f, 0), new Vector3(0.02f, 0.2f, 0.02f), "Brass", lamp.transform);
        Prim(PrimitiveType.Cylinder, "shade", new Vector3(0, 0.42f, 0), new Vector3(0.24f, 0.08f, 0.24f), "DarkMetal", lamp.transform);
        var lb = Prim(PrimitiveType.Sphere, "bulb", new Vector3(0, 0.35f, 0), Vector3.one * 0.06f, "Bulb", lamp.transform);
        PointLight("DeskLampLight", new Vector3(3.55f, 1.1f, 3.85f), new Color(1f, 0.72f, 0.45f), 1.1f, 3f, false, 0.08f, false, lb.GetComponent<Renderer>());

        // Bench, coat rack, boxes of photographs
        var bench = Table("Bench", new Vector3(0.45f, 0, 3f), new Vector3(0.45f, 0.06f, 1.8f), 0.45f);
        bench.name = "Bench";
        var rack = Empty("CoatRack", new Vector3(0.55f, 0, 5.4f));
        Prim(PrimitiveType.Cylinder, "pole", new Vector3(0, 0.9f, 0), new Vector3(0.05f, 0.9f, 0.05f), "DarkWood", rack.transform);
        Prim(PrimitiveType.Cylinder, "foot", new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.02f, 0.4f), "DarkWood", rack.transform);
        Prim(PrimitiveType.Capsule, "coat", new Vector3(0.12f, 1.25f, 0), new Vector3(0.35f, 0.45f, 0.22f), "Fabric", rack.transform);
        rack.AddComponent<BoxCollider>().size = new Vector3(0.4f, 3.6f, 0.4f);
        for (int i = 0; i < 4; i++)
            Box("PhotoBox", new Vector3(5.25f + (i % 2) * 0.05f, 0.15f + i * 0.3f, 0.75f), new Vector3(0.55f, 0.3f, 0.42f), "Cardboard", null, 0.6f,
                true, 0, Quaternion.Euler(0, i * 7 - 10, 0));
        ScatterPhotos(new Vector3(2.0f, 0, 2.2f), 1.1f, 9, 11);
        ScatterPhotos(new Vector3(5.0f, 0, 1.4f), 0.6f, 5, 12);

        // Decorative frames
        Frame("RecFrame1", new Vector3(0.12f, 1.7f, 1.6f), Vector3.left, new Vector2(0.5f, 0.38f), "OldPhoto0");
        Frame("RecFrame2", new Vector3(0.12f, 1.65f, 4.4f), Vector3.left, new Vector2(0.4f, 0.3f), "OldPhoto1", 4);
        Frame("RecFrame3", new Vector3(1.1f, 1.7f, 5.88f), Vector3.forward, new Vector2(0.38f, 0.48f), "Portrait1");
        Frame("RecFrame4", new Vector3(4.9f, 1.75f, 5.88f), Vector3.forward, new Vector2(0.5f, 0.38f), "OldPhoto2", -3);
        Frame("RecFrame5", new Vector3(1.3f, 1.7f, 0.12f), Vector3.back, new Vector2(0.45f, 0.34f), "OldPhoto3");
        Frame("RecFrame6", new Vector3(4.7f, 1.7f, 0.12f), Vector3.back, new Vector2(0.45f, 0.34f), "OldPhoto4", 6);

        // --- Camera-only clue: writing on the north wall ---
        var writing = Empty("WallWriting", new Vector3(3f, 1.85f, 5.88f));
        Text3D("text", "PHOTOGRAPHS REMEMBER\nWHAT THE WORLD FORGETS", Vector3.zero, Quaternion.identity, 0.17f, Red, Layers.CameraOnly, writing.transform, FontStyle.Bold);
        writing.layer = Layers.CameraOnly;
        MakePhotographable(writing, "PHOTO_WALL_WRITING", "Writing on the reception wall",
            "Only visible through the lens: PHOTOGRAPHS REMEMBER WHAT THE WORLD FORGETS. Someone painted it, then painted over it. The camera still sees it.", 7f);

        // --- Camera-only clue: footprints leading to the hidden door ---
        var prints = Empty("Footprints", new Vector3(4.75f, 0.01f, 3.3f));
        prints.layer = Layers.CameraOnly;
        Vector2[] path = { new Vector2(4.0f, 2.2f), new Vector2(4.3f, 2.6f), new Vector2(4.6f, 3.0f), new Vector2(4.9f, 3.35f), new Vector2(5.2f, 3.65f), new Vector2(5.5f, 3.9f) };
        for (int i = 0; i < path.Length; i++)
        {
            Vector2 dir = (i < path.Length - 1 ? path[i + 1] - path[i] : path[i] - path[i - 1]).normalized;
            Vector2 perp = new Vector2(-dir.y, dir.x) * (i % 2 == 0 ? 0.09f : -0.09f);
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            Quad("print", new Vector3(path[i].x + perp.x, 0.006f + i * 0.0003f, path[i].y + perp.y), Quaternion.Euler(90, yaw, 0),
                new Vector2(0.11f, 0.26f), "Footprint", null, Layers.CameraOnly).transform.SetParent(prints.transform, true);
        }
        MakePhotographable(prints, "PHOTO_FOOTPRINTS", "Footprints",
            "Bare footprints that only the camera can see. They lead from the desk straight into the east wall — and don't come back.", 6f);

        // --- Puzzle: The Door That Isn't There ---
        var puzzleRoot = Empty("HiddenDoorPuzzle", new Vector3(6, 0, 4));
        var fake = Empty("FakeWall", Vector3.zero, puzzleRoot.transform);
        Wall("FakeWallSlab", new Vector3(6, 0, 3.4f), new Vector3(6, 0, 4.6f), "WallpaperGreen", "Plaster", 0f, 2.2f, Layers.NormalOnly, fake.transform);
        foreach (Transform c in fake.transform)
        {
            // shrink to the opening so the slabs don't poke through the neighbouring wall pieces
            c.localPosition -= puzzleRoot.transform.position;
            var bc = c.GetComponent<BoxCollider>();
            c.GetComponent<MeshFilter>().sharedMesh = BoxMesh(new Vector3(0.1f, 2.2f, 1.2f), 1.5f);
            bc.size = new Vector3(0.1f, 2.2f, 1.2f);
        }

        var trims = Empty("PhantomTrim", Vector3.zero, puzzleRoot.transform);
        DoorTrim(new Vector3(5.86f, 0, 4f), false, 1.2f, 2.2f, Layers.CameraOnly, trims.transform);
        foreach (Transform c in trims.transform) c.localPosition -= puzzleRoot.transform.position;

        var phantom = Empty("PhantomDoor", new Vector3(-0.13f, 0, 0), puzzleRoot.transform);
        Box("panel", new Vector3(0, 1.075f, 0), new Vector3(0.04f, 2.15f, 1.1f), "DarkWood", phantom.transform, 0.8f, false, Layers.CameraOnly);
        Prim(PrimitiveType.Sphere, "handle", new Vector3(-0.04f, 1.0f, 0.4f), Vector3.one * 0.07f, "Brass", phantom.transform, null, false, Layers.CameraOnly);
        var symbolGo = Quad("EyeSymbol", new Vector3(-0.025f, 1.5f, 0), Quaternion.LookRotation(Vector3.right), new Vector2(0.6f, 0.6f), "EyeSymbol", phantom.transform, Layers.CameraOnly);
        var symbol = MakePhotographable(symbolGo, "PHOTO_EYE_SYMBOL", "The eye on the door",
            "Through the lens there is a door in the reception wall, with an eye painted on it. When I photographed it, the wall stopped pretending.", 7f);

        var realDoor = Door("StudioDoor", new Vector3(6f, 0, 3.45f), false, 0, "DarkWood");
        var rd = realDoor.AddComponent<DoorInteractable>();
        rd.openAngle = 95f;
        Quad("eye", new Vector3(-0.035f, 1.5f, 0.55f), Quaternion.LookRotation(Vector3.right), new Vector2(0.6f, 0.6f), "EyeSymbol", realDoor.transform);

        var hp = puzzleRoot.AddComponent<HiddenDoorPuzzle>();
        hp.symbol = symbol;
        hp.fakeWall = fake;
        hp.phantomOnly = new[] { phantom };
        hp.becomeReal = new[] { trims };
        hp.realDoor = realDoor;
    }

    static void BuildStudio()
    {
        // x 6..16, z 0..10
        FloorCeiling("Studio", new Vector3(11, 0, 5), new Vector2(10, 10), "Floor");
        Wall("Stu_S", new Vector3(6, 0, 0), new Vector3(16, 0, 0), "Plaster", "Plaster");
        Wall("Stu_N", new Vector3(6, 0, 10), new Vector3(16, 0, 10), "Plaster", "Plaster");
        Wall("Stu_W", new Vector3(6, 0, 6), new Vector3(6, 0, 10), "Plaster", "Plaster");
        Wall("Stu_E1", new Vector3(16, 0, 0), new Vector3(16, 0, 7.2f), "Plaster", "WallpaperRed");
        Wall("Stu_E2", new Vector3(16, 0, 8.8f), new Vector3(16, 0, 10), "Plaster", "WallpaperRed");
        Wall("Stu_E_lintel", new Vector3(16, 0, 7.2f), new Vector3(16, 0, 8.8f), "Plaster", "WallpaperRed", 2.4f, 3f);
        DoorTrim(new Vector3(15.88f, 0, 8f), false, 1.6f, 2.4f);

        studioLights.Add(HangingBulb("Stu_Light1", new Vector3(9.5f, 2.4f, 3.2f), 1.9f, 8.5f, true));
        studioLights.Add(HangingBulb("Stu_Light2", new Vector3(12.8f, 2.4f, 7.2f), 1.9f, 8.5f, true));

        // Backdrop on the west side
        var bd = Empty("Backdrop", new Vector3(7.3f, 0, 4f));
        Box("paper", new Vector3(0, 1.45f, 0), new Vector3(0.04f, 2.7f, 3.2f), "Backdrop", bd.transform, 2f);
        Box("sweep", new Vector3(0.6f, 0.006f, 0), new Vector3(1.2f, 0.012f, 3.2f), "Backdrop", bd.transform, 2f, false);
        Prim(PrimitiveType.Cylinder, "pole_l", new Vector3(-0.05f, 1.45f, -1.65f), new Vector3(0.04f, 1.45f, 0.04f), "Metal", bd.transform);
        Prim(PrimitiveType.Cylinder, "pole_r", new Vector3(-0.05f, 1.45f, 1.65f), new Vector3(0.04f, 1.45f, 0.04f), "Metal", bd.transform);
        Prim(PrimitiveType.Cylinder, "roll", new Vector3(0, 2.85f, 0), new Vector3(0.12f, 1.7f, 0.12f), "Backdrop", bd.transform, Quaternion.Euler(90, 0, 0));

        var writing = Empty("BackdropWriting", new Vector3(7.345f, 1.65f, 4f), null, Quaternion.LookRotation(Vector3.left));
        Text3D("text", "OLDEST\nFIRST", Vector3.zero, Quaternion.identity, 0.42f, Red, Layers.CameraOnly, writing.transform, FontStyle.Bold);
        writing.layer = Layers.CameraOnly;
        MakePhotographable(writing, "PHOTO_BACKDROP_WRITING", "Message on the backdrop",
            "Across the photographer's backdrop, visible only through the lens: OLDEST FIRST.", 8f);

        // Stool, tripod camera, softboxes
        var stool = Empty("Stool", new Vector3(8.1f, 0, 4f));
        Prim(PrimitiveType.Cylinder, "seat", new Vector3(0, 0.62f, 0), new Vector3(0.36f, 0.03f, 0.36f), "DarkWood", stool.transform);
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Cylinder, "leg", Quaternion.Euler(0, i * 120, 0) * new Vector3(0.12f, 0.31f, 0), new Vector3(0.03f, 0.31f, 0.03f), "DarkWood", stool.transform,
                Quaternion.Euler(0, i * 120, 0) * Quaternion.Euler(0, 0, 8));
        stool.AddComponent<BoxCollider>().size = new Vector3(0.4f, 1.3f, 0.4f);

        var tripod = Empty("TripodCamera", new Vector3(10.2f, 0, 2.5f), null, Quaternion.Euler(0, -110, 0));
        for (int i = 0; i < 3; i++)
            Prim(PrimitiveType.Cylinder, "leg", Quaternion.Euler(0, i * 120, 0) * new Vector3(0.18f, 0.65f, 0), new Vector3(0.025f, 0.67f, 0.025f), "DarkMetal", tripod.transform,
                Quaternion.Euler(0, i * 120, 0) * Quaternion.Euler(0, 0, 15));
        Box("body", new Vector3(0, 1.42f, 0), new Vector3(0.32f, 0.3f, 0.4f), "Fabric", tripod.transform, 1f, false);
        Prim(PrimitiveType.Cylinder, "lens", new Vector3(0, 1.42f, 0.28f), new Vector3(0.16f, 0.1f, 0.16f), "DarkMetal", tripod.transform, Quaternion.Euler(90, 0, 0));
        Box("bellows", new Vector3(0, 1.42f, 0.16f), new Vector3(0.26f, 0.26f, 0.12f), "Fabric", tripod.transform, 1f, false);
        tripod.AddComponent<BoxCollider>().size = new Vector3(0.6f, 3f, 0.6f);

        foreach (float z in new[] { 2.4f, 5.6f })
        {
            var sb = Empty("Softbox", new Vector3(8.6f, 0, z), null, Quaternion.LookRotation(new Vector3(-1, 0, 4f - z).normalized));
            Prim(PrimitiveType.Cylinder, "pole", new Vector3(0, 0.85f, 0), new Vector3(0.03f, 0.85f, 0.03f), "Metal", sb.transform);
            Box("box", new Vector3(0, 1.75f, 0), new Vector3(0.6f, 0.6f, 0.3f), "DarkMetal", sb.transform, 1f, false);
            var face = Box("diffuser", new Vector3(0, 1.75f, 0.16f), new Vector3(0.56f, 0.56f, 0.02f), "Softbox", sb.transform, 1f, false);
            sb.AddComponent<BoxCollider>().size = new Vector3(0.6f, 4f, 0.6f);
            var l = PointLight("SoftboxLight", sb.transform.position + Vector3.up * 1.75f + sb.transform.forward * 0.35f,
                new Color(0.82f, 0.86f, 1f), 0.55f, 3.5f, false, 0.03f, false, face.GetComponent<Renderer>());
            studioLights.Add(l.GetComponent<FlickerLight>());
        }

        // --- Puzzle: The Four Portraits (north wall) ---
        string[] names = { "E. HALE", "R. MOREAU", "A. VOSS", "T. LINDQVIST" };
        int[] years = { 1962, 2004, 1931, 1988 };
        int[] numbers = { 2, 4, 7, 9 };          // oldest first: 1931→7, 1962→2, 1988→9, 2004→4  =>  7294
        int[] texIdx = { 0, 1, 2, 3 };
        for (int i = 0; i < 4; i++)
        {
            float x = 8f + i * 2f;
            var root = Empty("Portrait" + (i + 1), new Vector3(x, 1.75f, 9.86f));
            Box("frame", Vector3.zero, new Vector3(0.95f, 1.2f, 0.06f), "DarkWood", root.transform, 0.5f, false);
            Box("frame_inner", new Vector3(0, 0, -0.02f), new Vector3(0.82f, 1.07f, 0.04f), "Brass", root.transform, 0.5f, false);
            Quad("picture", new Vector3(0, 0, -0.045f), Quaternion.identity, new Vector2(0.74f, 0.96f), "Portrait" + texIdx[i], root.transform);
            Box("plate", new Vector3(0, -0.76f, -0.01f), new Vector3(0.5f, 0.1f, 0.02f), "Brass", root.transform, 0.5f, false);
            Text3D("plate_text", $"{names[i]}  ·  b. {years[i]}", new Vector3(0, -0.76f, -0.022f), Quaternion.identity, 0.045f,
                new Color(0.12f, 0.08f, 0.04f), 0, root.transform, FontStyle.Bold);
            Text3D("hidden_number", numbers[i].ToString(), new Vector3(0.22f, 0.36f, -0.05f), Quaternion.Euler(0, 0, -8 + i * 5), 0.34f,
                Red, Layers.CameraOnly, root.transform, FontStyle.Bold);
            MakePhotographable(root, $"PHOTO_PORTRAIT_0{i + 1}", $"Portrait — {names[i]}, born {years[i]}",
                $"Through the lens, the number {numbers[i]} is scratched across the portrait. The nameplate reads {names[i]}, b. {years[i]}.", 6f);
        }

        // Note on a side table
        var table = Table("NoteTable", new Vector3(13.8f, 0, 3f), new Vector3(1.2f, 0.05f, 0.7f), 0.76f);
        var note = Quad("StudioNote", new Vector3(13.65f, 0.765f, 2.95f), Quaternion.Euler(90, -12, 0), new Vector2(0.26f, 0.34f), "Paper", null, 0, true);
        var n = note.AddComponent<NoteInteractable>();
        n.noteTitle = "In the photographer's handwriting";
        n.body = "My sitters never age in my photographs. Only in this room.\n\n" +
                 "I locked the darkroom key in the cabinet. The faces on the north wall keep the combination — " +
                 "but you won't read it with your eyes. Use the lens.\n\n" +
                 "Take them in the order they were born.\n\n— M.";
        n.flagOnRead = "read_studio";
        n.objectiveOnRead = "Open the cabinet. The portraits hide the combination.";
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Cylinder, "film_canister", new Vector3(14.1f + i * 0.08f, 0.8f, 3.15f - (i % 2) * 0.1f), new Vector3(0.035f, 0.03f, 0.035f), "DarkMetal");
        ScatterPhotos(new Vector3(11.5f, 0, 8.2f), 1.4f, 10, 21);
        ScatterPhotos(new Vector3(14.2f, 0, 5.5f), 0.8f, 5, 22);
        Chair("ToppledChair", new Vector3(14.5f, 0, 6.6f), 30, true);

        // --- Cabinet with combination lock and drawer ---
        var cab = Empty("Cabinet", new Vector3(11f, 0, 0.4f));
        var ct = cab.transform;
        Box("body", new Vector3(0, 0.39f, 0), new Vector3(1.2f, 0.78f, 0.6f), "DarkWood", ct, 0.8f);
        Box("top", new Vector3(0, 1.02f, 0), new Vector3(1.24f, 0.04f, 0.62f), "DarkWood", ct, 0.8f);
        Box("side_l", new Vector3(-0.58f, 0.89f, 0), new Vector3(0.04f, 0.22f, 0.6f), "DarkWood", ct, 0.8f);
        Box("side_r", new Vector3(0.58f, 0.89f, 0), new Vector3(0.04f, 0.22f, 0.6f), "DarkWood", ct, 0.8f);
        Box("back", new Vector3(0, 0.89f, -0.28f), new Vector3(1.2f, 0.22f, 0.04f), "DarkWood", ct, 0.8f);
        Box("lockplate", new Vector3(0, 0.6f, 0.305f), new Vector3(0.66f, 0.22f, 0.02f), "Brass", ct, 0.5f, false);

        var drawer = Empty("Drawer", new Vector3(0, 0.89f, 0.02f), ct);
        Box("tray", Vector3.zero, new Vector3(1.1f, 0.16f, 0.52f), "DarkWood", drawer.transform, 0.8f);
        Box("front", new Vector3(0, 0, 0.275f), new Vector3(1.12f, 0.2f, 0.03f), "DarkWood", drawer.transform, 0.8f);
        Prim(PrimitiveType.Cylinder, "pull", new Vector3(0, 0, 0.31f), new Vector3(0.03f, 0.12f, 0.03f), "Brass", drawer.transform, Quaternion.Euler(0, 0, 90));

        var key = Empty("DarkroomKey", new Vector3(0.12f, 0.095f, 0.14f), drawer.transform, Quaternion.Euler(0, 25, 0));
        Prim(PrimitiveType.Cylinder, "bow", new Vector3(-0.06f, 0, 0), new Vector3(0.05f, 0.006f, 0.05f), "Key", key.transform);
        Box("shaft", new Vector3(0.03f, 0, 0), new Vector3(0.12f, 0.01f, 0.014f), "Key", key.transform, 1f, false);
        Box("teeth", new Vector3(0.075f, 0, 0.012f), new Vector3(0.03f, 0.01f, 0.02f), "Key", key.transform, 1f, false);
        Box("tag", new Vector3(-0.1f, 0, 0.04f), new Vector3(0.05f, 0.004f, 0.08f), "Paper", key.transform, 1f, false);
        key.AddComponent<BoxCollider>().size = new Vector3(0.3f, 0.06f, 0.16f);
        var pick = key.AddComponent<PickupInteractable>();
        pick.flag = "darkroom_key";
        pick.itemName = "the darkroom key";
        pick.message = "Taken: darkroom key. Its tag reads: DO NOT DEVELOP AFTER MIDNIGHT.";
        pick.objectiveOnPickup = "Find the darkroom.";

        var lk = cab.AddComponent<CombinationLock>();
        lk.dials = new DialInteractable[4];
        lk.drawer = drawer.transform;
        lk.reward = pick;
        for (int i = 0; i < 4; i++)
        {
            var dial = Box("Dial" + (i + 1), new Vector3(0.21f - i * 0.14f, 0.6f, 0.335f), new Vector3(0.11f, 0.14f, 0.05f), "DarkMetal", ct, 0.5f);
            var label = Text3D("digit", "0", new Vector3(0, 0, 0.027f), Quaternion.LookRotation(Vector3.back), 0.085f, new Color(0.92f, 0.88f, 0.75f), 0, dial.transform, FontStyle.Bold);
            var d = dial.AddComponent<DialInteractable>();
            d.lockRef = lk;
            d.label = label;
            lk.dials[i] = d;
        }

        studioZone = Zone("StudioZone", new Vector3(11, 1.5f, 5), new Vector3(9.6f, 3, 9.6f));
    }

    static void BuildCorridor()
    {
        // x 16..30, z 7.2..8.8
        FloorCeiling("Corridor", new Vector3(23, 0, 8), new Vector2(14, 1.6f), "Floor");
        Wall("Cor_S", new Vector3(16, 0, 7.2f), new Vector3(30, 0, 7.2f), "WallpaperRed", "WallpaperRed");
        Wall("Cor_N1", new Vector3(16, 0, 8.8f), new Vector3(26, 0, 8.8f), "WallpaperRed", "WallpaperRed");
        Wall("Cor_N2", new Vector3(26, 0, 8.8f), new Vector3(27.4f, 0, 8.8f), "WallpaperRed", "DarkPaint");
        Wall("Cor_N3", new Vector3(28.6f, 0, 8.8f), new Vector3(30, 0, 8.8f), "WallpaperRed", "DarkPaint");
        Wall("Cor_N_lintel", new Vector3(27.4f, 0, 8.8f), new Vector3(28.6f, 0, 8.8f), "WallpaperRed", "DarkPaint", 2.2f, 3f);
        Wall("Cor_E", new Vector3(30, 0, 7.2f), new Vector3(30, 0, 8.8f), "WallpaperRed", "WallpaperRed");
        Box("Runner", new Vector3(23, 0.006f, 8), new Vector3(13.4f, 0.012f, 0.9f), "Rug", null, 1f, false);

        HangingBulb("Cor_Light1", new Vector3(18.5f, 2.55f, 8f), 1.2f, 5.5f, true);
        HangingBulb("Cor_Light2", new Vector3(23f, 2.55f, 8f), 1.1f, 5.5f, false, true);
        HangingBulb("Cor_Light3", new Vector3(27.5f, 2.55f, 8f), 1.1f, 5.5f, true);

        for (int i = 0; i < 5; i++)
        {
            float x = 17.8f + i * 2.4f;
            bool north = i % 2 == 0;
            var pos = new Vector3(x, 1.65f + (i % 3) * 0.08f, north ? 8.68f : 7.32f);
            Frame("CorFrame" + i, pos, north ? Vector3.forward : Vector3.back, new Vector2(0.42f, 0.32f),
                i % 2 == 0 ? "OldPhoto" + (i % 6) : "Portrait" + (i % 4), (i - 2) * 4f);
        }
        var fallen = Frame("FallenFrame", new Vector3(24.2f, 0.03f, 7.55f), Vector3.forward, new Vector2(0.42f, 0.32f), "OldPhoto5");
        fallen.transform.rotation = Quaternion.Euler(90, 20, 0);

        // Darkroom door
        var door = Door("DarkroomDoor", new Vector3(27.45f, 0, 8.8f), true, 0, "DarkWood");
        var dd = door.AddComponent<DoorInteractable>();
        dd.locked = true;
        dd.requiredFlag = "darkroom_key";
        dd.openAngle = -95f;
        dd.lockedMessage = "Locked. A sign on the door reads: DARKROOM — KEEP CLOSED.";
        dd.unlockMessage = "The darkroom key turns.";
        Box("sign", new Vector3(0.55f, 1.68f, -0.04f), new Vector3(0.36f, 0.1f, 0.01f), "Brass", door.transform, 0.5f, false);
        Text3D("sign_text", "DARKROOM", new Vector3(0.55f, 1.68f, -0.047f), Quaternion.identity, 0.055f, new Color(0.1f, 0.06f, 0.03f), 0, door.transform, FontStyle.Bold);
        DoorTrim(new Vector3(28f, 0, 8.68f), true, 1.2f, 2.2f);

        // The figure at the end of the corridor (camera only)
        corridorFigure = Figure("CorridorFigure", new Vector3(29.45f, 0, 8f), -90, Layers.CameraOnly);
        // Faint spill from under the darkroom door so the end of the corridor reads as a silhouette.
        PointLight("DarkroomSpill", new Vector3(28.6f, 0.3f, 8.5f), new Color(1f, 0.2f, 0.12f), 0.9f, 2.6f, false, 0.03f);
        corridorEnd = Empty("CorridorEnd", new Vector3(29.5f, 1.6f, 8f)).transform;
        corridorZone = Zone("CorridorZone", new Vector3(23, 1.5f, 8), new Vector3(13.6f, 3, 1.6f));
    }

    static void BuildDarkroom()
    {
        // x 26..30, z 8.8..13.8
        FloorCeiling("Darkroom", new Vector3(28, 0, 11.3f), new Vector2(4, 5), "Concrete", "DarkPaint");
        Wall("Dark_W", new Vector3(26, 0, 8.8f), new Vector3(26, 0, 13.8f), "DarkPaint", "DarkPaint");
        Wall("Dark_E", new Vector3(30, 0, 8.8f), new Vector3(30, 0, 13.8f), "DarkPaint", "DarkPaint");
        Wall("Dark_N", new Vector3(26, 0, 13.8f), new Vector3(30, 0, 13.8f), "DarkPaint", "DarkPaint");

        var safelight = Prim(PrimitiveType.Sphere, "SafelightBulb", new Vector3(28, 2.7f, 11.3f), new Vector3(0.18f, 0.12f, 0.18f), "RedBulb");
        Prim(PrimitiveType.Cylinder, "SafelightHood", new Vector3(28, 2.82f, 11.3f), new Vector3(0.3f, 0.06f, 0.3f), "DarkMetal");
        PointLight("Safelight", new Vector3(28, 2.55f, 11.3f), new Color(1f, 0.14f, 0.08f), 2.6f, 8f, true, 0.04f, false, safelight.GetComponent<Renderer>());

        var table = Table("DevelopingTable", new Vector3(28, 0, 11.6f), new Vector3(1.8f, 0.06f, 0.85f), 0.9f);
        for (int i = 0; i < 3; i++)
        {
            float x = -0.6f + i * 0.6f;
            Box("tray", new Vector3(x, 0.925f, 0), new Vector3(0.44f, 0.05f, 0.34f), "TrayWhite", table.transform, 0.5f, false);
            Box("liquid", new Vector3(x, 0.947f, 0), new Vector3(0.4f, 0.01f, 0.3f), "Chemical", table.transform, 0.5f, false);
        }
        var print = Quad("DevelopingPrint", new Vector3(28f, 0.955f, 11.58f), Quaternion.Euler(90, 0, 0), new Vector2(0.3f, 0.2f), "OldPhoto2", null, 0, true);
        print.AddComponent<DarkroomPhotoInteractable>();
        Prim(PrimitiveType.Cylinder, "tongs", new Vector3(28.32f, 0.96f, 11.45f), new Vector3(0.015f, 0.12f, 0.015f), "Metal", null, Quaternion.Euler(90, 30, 0));

        // Drying line with prints
        Prim(PrimitiveType.Cylinder, "DryingLine", new Vector3(28, 2.15f, 13.35f), new Vector3(0.008f, 1.85f, 0.008f), "Black", null, Quaternion.Euler(0, 0, 90));
        for (int i = 0; i < 6; i++)
        {
            float x = 26.6f + i * 0.55f;
            Quad("hanging_print", new Vector3(x, 1.95f, 13.34f), Quaternion.Euler(0, 0, (i % 2 == 0 ? 2 : -3)), new Vector2(0.26f, 0.34f),
                i % 2 == 0 ? "Portrait" + (i % 4) : "OldPhoto" + i);
            Box("clip", new Vector3(x, 2.13f, 13.33f), new Vector3(0.03f, 0.05f, 0.02f), "Metal", null, 1f, false);
        }

        // Shelves with bottles
        for (int s = 0; s < 2; s++)
        {
            float y = 1.2f + s * 0.55f;
            Box("shelf", new Vector3(26.28f, y, 11.3f), new Vector3(0.36f, 0.03f, 1.8f), "DarkWood", null, 0.8f);
            for (int b = 0; b < 5; b++)
                Prim(PrimitiveType.Cylinder, "bottle", new Vector3(26.25f, y + 0.12f, 10.6f + b * 0.32f + s * 0.1f),
                    new Vector3(0.09f, 0.1f + (b % 3) * 0.02f, 0.09f), b % 2 == 0 ? "Bottle" : "TrayWhite");
        }

        // Enlarger
        var enl = Empty("Enlarger", new Vector3(29.55f, 0, 12.6f));
        Box("bench", new Vector3(0, 0.45f, 0), new Vector3(0.7f, 0.9f, 1.0f), "DarkWood", enl.transform, 0.8f);
        Prim(PrimitiveType.Cylinder, "column", new Vector3(0.15f, 1.5f, 0), new Vector3(0.05f, 0.6f, 0.05f), "Metal", enl.transform);
        Box("head", new Vector3(0, 1.75f, 0), new Vector3(0.28f, 0.3f, 0.3f), "DarkMetal", enl.transform, 1f, false);
        Prim(PrimitiveType.Cylinder, "lens", new Vector3(0, 1.55f, 0), new Vector3(0.1f, 0.06f, 0.1f), "Black", enl.transform);

        darkroomZone = Zone("DarkroomZone", new Vector3(28, 1.5f, 11.3f), new Vector3(3.8f, 3, 4.8f));

        // Scripted figures (positioned at runtime)
        behindFigure = Figure("BehindFigure", new Vector3(20, 0, 8), 90, Layers.CameraOnly);
        hauntFigure = Figure("HauntFigure", new Vector3(3, 0, 3), 0, Layers.CameraOnly);
        endingFigure = Figure("EndingFigure", new Vector3(28, 0, 12.4f), 180, Layers.CameraOnly);

        var fl = Empty("FlashLight", new Vector3(28, 1.6f, 10.5f)).AddComponent<Light>();
        fl.type = LightType.Point;
        fl.color = new Color(1f, 0.97f, 0.92f);
        fl.range = 9f;
        fl.intensity = 7f;
        fl.shadows = LightShadows.Hard;
        fl.lightmapBakeType = LightmapBakeType.Realtime;
        fl.enabled = false;
        flashLight = fl;
    }

    static GameObject BuildPlayerAndSystems(Vector3 start, float yaw)
    {
        var player = new GameObject("Player");
        player.transform.SetPositionAndRotation(start, Quaternion.Euler(0, yaw, 0));
        player.layer = Layers.PlayerBody; // keeps our own collider out of interaction / line-of-sight raycasts
        var cc = player.AddComponent<CharacterController>();
        cc.height = 1.75f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0, 0.875f, 0);
        cc.stepOffset = 0.3f;
        cc.skinWidth = 0.03f;

        var head = new GameObject("Head");
        head.transform.SetParent(player.transform, false);
        head.transform.localPosition = new Vector3(0, 1.6f, 0);
        head.tag = "MainCamera";
        var cam = head.AddComponent<Camera>();
        cam.nearClipPlane = 0.03f;
        cam.farClipPlane = 60f;
        cam.fieldOfView = 65f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        head.AddComponent<AudioListener>();
        var fx = head.AddComponent<ViewfinderEffect>();
        fx.material = M["ViewfinderFX"];

        // A body that only ever appears in "impossible" photographs.
        var body = Prim(PrimitiveType.Capsule, "Body", new Vector3(0, 0.85f, 0), new Vector3(0.48f, 0.82f, 0.36f), "Fabric", player.transform, null, false, Layers.PlayerBody);
        Prim(PrimitiveType.Sphere, "BodyHead", new Vector3(0, 1.62f, 0), new Vector3(0.22f, 0.26f, 0.24f), "Skin", player.transform, null, false, Layers.PlayerBody);
        Prim(PrimitiveType.Sphere, "BodyHair", new Vector3(0, 1.69f, -0.03f), new Vector3(0.235f, 0.2f, 0.24f), "Fabric", player.transform, null, false, Layers.PlayerBody);
        Box("BodyCamera", new Vector3(0, 1.32f, 0.24f), new Vector3(0.14f, 0.09f, 0.07f), "DarkMetal", player.transform, 1f, false, Layers.PlayerBody);
        body.name = "Body";

        var pc = player.AddComponent<PlayerController>();
        pc.cam = cam;
        pc.head = head.transform;
        var tool = player.AddComponent<CameraTool>();
        tool.cam = cam;
        tool.effect = fx;

        var photoGo = new GameObject("PhotoCamera");
        var pcam = photoGo.AddComponent<Camera>();
        pcam.enabled = false;
        pcam.nearClipPlane = 0.03f;
        pcam.farClipPlane = 60f;
        pcam.clearFlags = CameraClearFlags.SolidColor;
        pcam.backgroundColor = Color.black;
        var pfx = photoGo.AddComponent<ViewfinderEffect>();
        pfx.material = M["ViewfinderFX"];
        pfx.photoMode = true;

        var sys = new GameObject("Systems");
        sys.AddComponent<GameManager>();
        sys.AddComponent<EvidenceManager>();
        sys.AddComponent<AudioManager>();
        sys.AddComponent<UIManager>();
        sys.AddComponent<PhotoRenderer>().photoCam = pcam;
        sys.AddComponent<JumpScare>().face = BuildScareFace(head.transform);
        new GameObject("AutoPlaytest").AddComponent<AutoPlaytest>();
        return sys;
    }

    /// The Figure's face, parented to the camera, used by jump scares.
    static Transform BuildScareFace(Transform cam)
    {
        var root = Empty("ScareFace", new Vector3(0, -0.05f, 0.42f), cam, Quaternion.Euler(0, 180, 0));
        var t = root.transform;
        Prim(PrimitiveType.Sphere, "hair", new Vector3(0, 0.04f, -0.05f), new Vector3(0.42f, 0.5f, 0.3f), "FigureBody", t);
        Prim(PrimitiveType.Sphere, "head", Vector3.zero, new Vector3(0.3f, 0.4f, 0.28f), "FigureFace", t);
        Prim(PrimitiveType.Sphere, "eye_l", new Vector3(-0.065f, 0.03f, 0.125f), new Vector3(0.075f, 0.05f, 0.03f), "Black", t);
        Prim(PrimitiveType.Sphere, "eye_r", new Vector3(0.065f, 0.03f, 0.125f), new Vector3(0.075f, 0.05f, 0.03f), "Black", t);
        Prim(PrimitiveType.Sphere, "mouth", new Vector3(0, -0.11f, 0.12f), new Vector3(0.07f, 0.12f, 0.03f), "Black", t);
        return t;
    }
}
