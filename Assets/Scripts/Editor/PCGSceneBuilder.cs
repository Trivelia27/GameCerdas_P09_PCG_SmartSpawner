using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Membangun seluruh scene PCG_SpawnArena (Tools > GameCerdas P09 > Build Scene).</summary>
public static class PCGSceneBuilder
{
    private const string ScenePath = "Assets/Scenes/PCG_SpawnArena.unity";

    private static Shader litShader;
    private static Shader additiveShader;

    private static Texture2D softDot, ringTex, ringDashTex, beamTex, arrowTex, gridTex, edgeTex, crossTex, whiteTex;

    private static readonly Color CyanNeon = new Color(0.25f, 0.9f, 1f);
    private static readonly Color MagentaNeon = new Color(1f, 0.3f, 0.75f);

    // ------------------------------------------------------------------ entry point

    [MenuItem("Tools/GameCerdas P09/Build Scene")]
    public static void Build()
    {
        EnsureFolders();

        litShader = Shader.Find("Universal Render Pipeline/Lit");
        additiveShader = Shader.Find("Legacy Shaders/Particles/Additive");

        if (litShader == null || additiveShader == null)
        {
            Debug.LogError($"[Builder] Shader tidak ditemukan. Lit: {litShader}, Additive: {additiveShader}");
            return;
        }

        int playerLayer = EnsureLayer("Player");
        int obstacleLayer = EnsureLayer("Obstacle");

        CleanOldAssets();
        BuildTextures();

        Material glowRes = Additive("Assets/Resources/Fx/FxGlow.mat", softDot, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        Additive("Assets/Resources/Fx/FxRing.mat", ringTex, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        Additive("Assets/Resources/Fx/FxRingDash.mat", ringDashTex, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        Additive("Assets/Resources/Fx/FxLine.mat", whiteTex, new Color(0.5f, 0.5f, 0.5f, 0.8f));
        Additive("Assets/Resources/Fx/FxX.mat", crossTex, new Color(0.5f, 0.5f, 0.5f, 0.9f));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        ConfigureLighting();

        // ----- Environment
        GameObject environment = new GameObject("Environment");
        BuildEnvironment(environment.transform, obstacleLayer);

        // ----- Player
        GameObject player = BuildPlayer(playerLayer, glowRes);

        // ----- Prefabs
        GameObject red = BuildEnemy("Enemy_Red", new Color(1f, 0.2f, 0.25f), EnemyBehavior.Chase, 2.4f, 1, 1, 10, 9f, 70f, BodyGrunt);
        GameObject blue = BuildEnemy("Enemy_Blue", new Color(0.2f, 0.5f, 1f), EnemyBehavior.Pursuit, 2.9f, 1, 1, 20, 10f, 90f, BodyHunter);
        GameObject yellow = BuildEnemy("Enemy_Yellow", new Color(1f, 0.85f, 0.15f), EnemyBehavior.Charger, 3.1f, 1, 1, 35, 12f, 0f, BodyStriker);
        GameObject green = BuildEnemy("Enemy_Green", new Color(0.2f, 1f, 0.45f), EnemyBehavior.Brute, 1.9f, 2, 2, 60, 14f, 35f, BodyBrute);

        GameObject coin = BuildItem("Item_Coin", "Coin", new Color(1f, 0.8f, 0.2f), 1, 0, ItemRarity.Common, false, 1.5f, BodyCoin);
        GameObject health = BuildItem("Item_Health", "Health Potion", new Color(1f, 0.3f, 0.5f), 3, 2, ItemRarity.Uncommon, false, 1.8f, BodyHealth);
        GameObject rare = BuildItem("Item_Rare", "Rare Crystal", new Color(0.3f, 0.92f, 1f), 5, 0, ItemRarity.Rare, true, 2.1f, BodyCrystal);
        GameObject legendary = BuildItem("Item_Legendary", "Legendary Gem", new Color(0.8f, 0.35f, 1f), 15, 1, ItemRarity.Legendary, true, 2.6f, BodyLegendary);

        GameObject exitPrefab = BuildExit();
        GameObject obstaclePrefab = BuildObstaclePrefab(obstacleLayer);

        // ----- Generated root + manager
        GameObject generated = new GameObject("GeneratedObjects");

        GameObject manager = new GameObject("PCGManager");
        PCGSpawner spawner = manager.AddComponent<PCGSpawner>();

        SerializedObject so = new SerializedObject(spawner);
        so.FindProperty("player").objectReferenceValue = player.transform;
        so.FindProperty("generatedRoot").objectReferenceValue = generated.transform;
        so.FindProperty("obstacleMask").intValue = 1 << obstacleLayer;
        so.FindProperty("exitPrefab").objectReferenceValue = exitPrefab;
        so.FindProperty("obstaclePrefab").objectReferenceValue = obstaclePrefab;

        SerializedProperty fixedObstacles = so.FindProperty("fixedObstacles");
        fixedObstacles.arraySize = 4;

        for (int i = 0; i < 4; i++)
        {
            fixedObstacles.GetArrayElementAtIndex(i).objectReferenceValue = environment.transform.Find($"Obstacle_0{i + 1}").gameObject;
        }

        SetEntries(so.FindProperty("enemyEntries"), new[]
        {
            Entry("Common Red", red, 60, new Color(1f, 0.25f, 0.3f)),
            Entry("Uncommon Blue", blue, 30, new Color(0.25f, 0.55f, 1f)),
            Entry("Rare Yellow", yellow, 10, new Color(1f, 0.85f, 0.2f)),
            Entry("Elite Green", green, 5, new Color(0.25f, 1f, 0.5f)),
        });

        SetEntries(so.FindProperty("itemEntries"), new[]
        {
            Entry("Common Coin", coin, 70, new Color(1f, 0.8f, 0.25f)),
            Entry("Health Potion", health, 20, new Color(1f, 0.35f, 0.55f)),
            Entry("Rare Crystal", rare, 8, new Color(0.35f, 0.92f, 1f)),
            Entry("Legendary Gem", legendary, 2, new Color(0.8f, 0.4f, 1f)),
        });

        so.ApplyModifiedPropertiesWithoutUndo();

        GameObject overlayObject = new GameObject("PCGOverlay");
        PCGOverlay overlay = overlayObject.AddComponent<PCGOverlay>();
        SetRef(overlay, "spawner", spawner);
        SetRef(overlay, "player", player.transform);

        // ----- Game systems
        GameObject systems = new GameObject("GameManager");
        AutoPilot autoPilot = systems.AddComponent<AutoPilot>();
        systems.AddComponent<AudioManager>();
        systems.AddComponent<PostFxController>();
        GameManager gm = systems.AddComponent<GameManager>();
        systems.AddComponent<GameHUD>();
        systems.AddComponent<PCGDebugPanel>();

        SerializedObject gmSo = new SerializedObject(gm);
        gmSo.FindProperty("spawner").objectReferenceValue = spawner;
        gmSo.FindProperty("player").objectReferenceValue = player.transform;
        gmSo.FindProperty("autoPilot").objectReferenceValue = autoPilot;
        gmSo.ApplyModifiedPropertiesWithoutUndo();

        BuildGuideArrow(player.transform);
        BuildPostProcessing();
        ConfigureCamera(player.transform);

        // ----- Sanity check (dry run, tidak membuat object)
        Physics.SyncTransforms();
        PCGSpawner.GenerationResult r = spawner.Generate(false);
        Debug.Log($"[Builder] Dry run OK | seed {r.seed} | enemy {r.enemySpawned}/{r.enemyRequested} | item {r.itemSpawned}/{r.itemRequested} | rejected {r.rejected}");

        EditorSceneManager.SaveScene(scene, ScenePath);

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[Builder] Scene PCG_SpawnArena selesai dibuat.");
    }

    // ------------------------------------------------------------------ folder, layer, cleanup

    private static void EnsureFolders()
    {
        foreach (string folder in new[]
        {
            "Assets/Materials", "Assets/Materials/FX", "Assets/Prefabs", "Assets/Prefabs/Enemies",
            "Assets/Prefabs/Items", "Assets/Scenes", "Assets/Settings", "Assets/Textures",
            "Assets/Resources", "Assets/Resources/Fx"
        })
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
            }
        }
    }

    private static int EnsureLayer(string layerName)
    {
        int existing = LayerMask.NameToLayer(layerName);
        if (existing >= 0)
        {
            return existing;
        }

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 6; i < 32; i++)
        {
            SerializedProperty slot = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(slot.stringValue))
            {
                slot.stringValue = layerName;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }

        Debug.LogError("Tidak ada slot layer kosong untuk " + layerName);
        return 0;
    }

    private static void CleanOldAssets()
    {
        // Material Standard dari versi pertama praktikum (diganti material URP).
        foreach (string guid in AssetDatabase.FindAssets("M_ t:Material", new[] { "Assets/Materials" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            if (Path.GetFileName(path).StartsWith("M_"))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    // ------------------------------------------------------------------ tekstur prosedural

    private static void BuildTextures()
    {
        softDot = SaveTexture("T_SoftDot", Generate(128, 128, (u, v) =>
        {
            float d = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f);
            float a = Mathf.Pow(1f - d, 2.2f);
            return new Color(1f, 1f, 1f, a);
        }), TextureWrapMode.Clamp);

        ringTex = SaveTexture("T_Ring", Generate(256, 256, (u, v) =>
        {
            float r = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
            float a = Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.035f, 2f)) + 0.25f * Mathf.Exp(-Mathf.Pow((r - 0.86f) / 0.12f, 2f));
            a *= Mathf.Clamp01((1f - r) / 0.08f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }), TextureWrapMode.Clamp);

        ringDashTex = SaveTexture("T_RingDash", Generate(256, 256, (u, v) =>
        {
            Vector2 p = new Vector2(u - 0.5f, v - 0.5f);
            float r = p.magnitude * 2f;
            float angle = Mathf.Atan2(p.y, p.x);
            float mask = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Sin(angle * 14f) + 0.3f) * 3f));

            float outer = Mathf.Exp(-Mathf.Pow((r - 0.88f) / 0.04f, 2f)) * mask;
            float inner = 0.55f * Mathf.Exp(-Mathf.Pow((r - 0.58f) / 0.02f, 2f));
            float glow = 0.18f * Mathf.Exp(-Mathf.Pow((r - 0.88f) / 0.14f, 2f));

            float a = (outer + inner + glow) * Mathf.Clamp01((1f - r) / 0.06f);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(a));
        }), TextureWrapMode.Clamp);

        beamTex = SaveTexture("T_Beam", Generate(64, 256, (u, v) =>
        {
            float a = Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.14f, 2f)) * Mathf.Pow(1f - v, 1.6f);
            return new Color(1f, 1f, 1f, a);
        }), TextureWrapMode.Clamp);

        arrowTex = SaveTexture("T_Arrow", Generate(128, 128, (u, v) =>
        {
            int hits = 0;

            for (int sy = 0; sy < 3; sy++)
            {
                for (int sx = 0; sx < 3; sx++)
                {
                    Vector2 p = new Vector2(u + (sx - 1) / 384f, v + (sy - 1) / 384f);

                    bool inside = InTriangle(p, new Vector2(0.5f, 0.92f), new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.12f)) &&
                                  !InTriangle(p, new Vector2(0.5f, 0.4f), new Vector2(0.3f, 0.1f), new Vector2(0.7f, 0.1f));

                    if (inside)
                    {
                        hits++;
                    }
                }
            }

            return new Color(1f, 1f, 1f, hits / 9f);
        }), TextureWrapMode.Clamp);

        gridTex = SaveTexture("T_Grid", Generate(512, 512, (u, v) =>
        {
            float cx = u * 5f;
            float cy = v * 5f;

            float minor = Mathf.Min(Frac(cx), Frac(cy));
            float border = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v)) * 5f;

            float lines = 0.4f * Mathf.Exp(-Mathf.Pow(minor / 0.022f, 2f));
            float edge = Mathf.Exp(-Mathf.Pow(border / 0.035f, 2f));
            float i = Mathf.Clamp01(lines + edge);

            return new Color(i, i, i, 1f);
        }), TextureWrapMode.Repeat, 8);

        whiteTex = SaveTexture("T_White", Generate(8, 8, (u, v) => Color.white), TextureWrapMode.Clamp);

        crossTex = SaveTexture("T_Cross", Generate(64, 64, (u, v) =>
        {
            float d1 = Mathf.Abs(u - v) * 0.7071f;
            float d2 = Mathf.Abs(u + v - 1f) * 0.7071f;
            float d = Mathf.Min(d1, d2);
            float within = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(u - 0.5f), Mathf.Abs(v - 0.5f)) * 2f / 0.78f);
            float a = Mathf.Clamp01(1.2f - d / 0.055f) * Mathf.SmoothStep(0f, 1f, within * 6f);
            return new Color(1f, 1f, 1f, a);
        }), TextureWrapMode.Clamp);

        edgeTex = SaveTexture("T_Edge", Generate(128, 128, (u, v) =>
        {
            float d = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float i = Mathf.Exp(-Mathf.Pow(d / 0.035f, 2f)) + 0.12f * Mathf.Exp(-Mathf.Pow(d / 0.14f, 2f));
            i = Mathf.Clamp01(i);
            return new Color(i, i, i, 1f);
        }), TextureWrapMode.Clamp);
    }

    private static float Frac(float x)
    {
        float f = x - Mathf.Floor(x);
        return Mathf.Min(f, 1f - f);
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool negative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool positive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(negative && positive);
    }

    private static float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }

    private static Texture2D Generate(int width, int height, Func<float, float, Color> pixel)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] colors = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                colors[y * width + x] = pixel((x + 0.5f) / width, (y + 0.5f) / height);
            }
        }

        texture.SetPixels(colors);
        texture.Apply();
        return texture;
    }

    private static Texture2D SaveTexture(string name, Texture2D texture, TextureWrapMode wrap, int aniso = 1)
    {
        string path = $"Assets/Textures/{name}.png";
        string fullPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);

        File.WriteAllBytes(fullPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.wrapMode = wrap;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.anisoLevel = aniso;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ------------------------------------------------------------------ material

    private static Material Lit(string name, Color baseColor, Color emission, float metallic = 0.3f, float smoothness = 0.55f,
        Texture emissionMap = null, Vector2? tiling = null)
    {
        string path = $"Assets/Materials/{name}.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(litShader);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = litShader;
        }

        m.SetColor("_BaseColor", baseColor);
        m.SetFloat("_Metallic", metallic);
        m.SetFloat("_Smoothness", smoothness);

        if (emission.maxColorComponent > 0.001f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", emission);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else
        {
            m.DisableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", Color.black);
        }

        if (emissionMap != null)
        {
            m.SetTexture("_EmissionMap", emissionMap);
        }

        m.mainTextureScale = tiling ?? Vector2.one;

        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material Additive(string path, Texture texture, Color tint)
    {
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (m == null)
        {
            m = new Material(additiveShader);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = additiveShader;
        }

        m.SetTexture("_MainTex", texture);
        m.SetColor("_TintColor", tint);

        EditorUtility.SetDirty(m);
        return m;
    }

    private static Material GlowFor(string name, Color color, float strength = 0.55f)
    {
        return Additive($"Assets/Materials/FX/Glow_{name}.mat", softDot, new Color(color.r, color.g, color.b, strength));
    }

    // ------------------------------------------------------------------ helper geometri

    private static GameObject Part(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, Vector3 euler = default)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        go.transform.localScale = scale;
        go.transform.localEulerAngles = euler;

        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.GetComponent<Renderer>().sharedMaterial = material;
        return go;
    }

    private static GameObject FxQuad(Transform parent, string name, Vector3 position, Vector3 euler, Vector3 scale, Material material)
    {
        GameObject go = Part(parent, PrimitiveType.Quad, name, position, scale, material, euler);

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go;
    }

    private static void SetRef(Object target, string field, Object value)
    {
        SerializedObject so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ------------------------------------------------------------------ lingkungan

    private static void ConfigureLighting()
    {
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.12f, 0.15f, 0.26f);
        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.02f, 0.03f, 0.07f);
        RenderSettings.fogDensity = 0.011f;

        Light sun = Object.FindFirstObjectByType<Light>();

        if (sun != null)
        {
            sun.type = LightType.Directional;
            sun.color = new Color(0.72f, 0.82f, 1f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.85f;
            sun.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
        }
    }

    private static void BuildEnvironment(Transform environment, int obstacleLayer)
    {
        Material floorMat = Lit("Floor", new Color(0.035f, 0.045f, 0.08f), CyanNeon * 1.5f, 0.35f, 0.82f, gridTex, new Vector2(6f, 6f));
        Material voidMat = Lit("Void", new Color(0.012f, 0.016f, 0.03f), Color.black, 0.5f, 0.9f);
        Material metalMat = Lit("Obstacle", new Color(0.09f, 0.1f, 0.15f), CyanNeon * 1.9f, 0.7f, 0.5f, edgeTex);
        Material wallMat = Lit("Wall", new Color(0.06f, 0.07f, 0.11f), MagentaNeon * 1.6f, 0.7f, 0.45f, edgeTex);
        Material pylonMat = Lit("Pylon", new Color(0.05f, 0.06f, 0.09f), Color.black, 0.8f, 0.6f);

        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.SetParent(environment, false);
        ground.transform.localScale = new Vector3(3f, 1f, 3f);
        ground.GetComponent<Renderer>().sharedMaterial = floorMat;

        GameObject outer = GameObject.CreatePrimitive(PrimitiveType.Plane);
        outer.name = "VoidFloor";
        outer.transform.SetParent(environment, false);
        outer.transform.position = new Vector3(0f, -0.04f, 0f);
        outer.transform.localScale = new Vector3(12f, 1f, 12f);
        outer.GetComponent<Renderer>().sharedMaterial = voidMat;
        Object.DestroyImmediate(outer.GetComponent<Collider>());

        CreateObstacle(environment, "Obstacle_01", new Vector3(5, 1, 5), new Vector3(3, 2, 1), obstacleLayer, metalMat);
        CreateObstacle(environment, "Obstacle_02", new Vector3(-6, 1, 4), new Vector3(2, 2, 4), obstacleLayer, metalMat);
        CreateObstacle(environment, "Obstacle_03", new Vector3(5, 1, -6), new Vector3(4, 2, 2), obstacleLayer, metalMat);
        CreateObstacle(environment, "Obstacle_04", new Vector3(-4, 1, -5), new Vector3(2, 2, 2), obstacleLayer, metalMat);

        // Dinding perimeter (layer Default, tidak ikut validasi PCG).
        CreateWall(environment, "Wall_N", new Vector3(0f, 0.6f, 15.3f), new Vector3(31f, 1.2f, 0.6f), wallMat);
        CreateWall(environment, "Wall_S", new Vector3(0f, 0.6f, -15.3f), new Vector3(31f, 1.2f, 0.6f), wallMat);
        CreateWall(environment, "Wall_E", new Vector3(15.3f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 31f), wallMat);
        CreateWall(environment, "Wall_W", new Vector3(-15.3f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 31f), wallMat);

        // Pylon sudut dengan lampu neon.
        Color[] pylonColors = { CyanNeon, MagentaNeon, MagentaNeon, CyanNeon };
        Vector2[] corners = { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };

        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 pos = new Vector3(corners[i].x * 16.2f, 0f, corners[i].y * 16.2f);
            Color c = pylonColors[i];

            GameObject pylon = new GameObject($"Pylon_{i}");
            pylon.transform.SetParent(environment, false);
            pylon.transform.position = pos;

            Part(pylon.transform, PrimitiveType.Cylinder, "Column", new Vector3(0f, 1.6f, 0f), new Vector3(0.55f, 1.6f, 0.55f), pylonMat);
            Material capMat = Lit($"PylonCap_{i}", c * 0.3f, c * 3f, 0.2f, 0.7f);
            Part(pylon.transform, PrimitiveType.Sphere, "Cap", new Vector3(0f, 3.4f, 0f), new Vector3(0.8f, 0.8f, 0.8f), capMat);
            FxQuad(pylon.transform, "Halo", new Vector3(0f, 0.05f, 0f), new Vector3(90f, 0f, 0f), new Vector3(5f, 5f, 1f), GlowFor($"Pylon{i}", c, 0.45f));

            Light light = new GameObject("Light").AddComponent<Light>();
            light.transform.SetParent(pylon.transform, false);
            light.transform.localPosition = new Vector3(0f, 3.2f, 0f);
            light.type = LightType.Point;
            light.color = c;
            light.range = 16f;
            light.intensity = 3.2f;
        }

        GameObject motes = new GameObject("AmbientMotes");
        motes.transform.SetParent(environment, false);
        motes.AddComponent<AmbientMotes>();
    }

    private static void CreateObstacle(Transform parent, string name, Vector3 position, Vector3 scale, int layer, Material material)
    {
        GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.name = name;
        obstacle.transform.SetParent(parent, false);
        obstacle.transform.position = position;
        obstacle.transform.localScale = scale;
        obstacle.layer = layer;
        obstacle.GetComponent<Renderer>().sharedMaterial = material;
    }

    private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent, false);
        wall.transform.position = position;
        wall.transform.localScale = scale;
        wall.GetComponent<Renderer>().sharedMaterial = material;
    }

    // ------------------------------------------------------------------ player

    private static GameObject BuildPlayer(int playerLayer, Material glowResource)
    {
        Material body = Lit("Player_Body", new Color(0.85f, 0.9f, 1f), CyanNeon * 0.25f, 0.5f, 0.7f);
        Material visor = Lit("Player_Visor", new Color(0.02f, 0.1f, 0.12f), CyanNeon * 4f, 0f, 0.9f);

        GameObject player = new GameObject("Player");
        player.layer = playerLayer;
        player.transform.position = new Vector3(0f, 1f, 0f);

        CharacterController controller = player.AddComponent<CharacterController>();
        controller.height = 1.9f;
        controller.radius = 0.45f;

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(player.transform, false);

        Part(visual.transform, PrimitiveType.Capsule, "Body", Vector3.zero, new Vector3(0.9f, 0.92f, 0.9f), body);
        Part(visual.transform, PrimitiveType.Cube, "Visor", new Vector3(0f, 0.38f, 0.33f), new Vector3(0.52f, 0.17f, 0.3f), visor);
        Part(visual.transform, PrimitiveType.Sphere, "Core", new Vector3(0f, -0.05f, -0.4f), new Vector3(0.28f, 0.28f, 0.28f), visor);

        FxQuad(player.transform, "Glow", new Vector3(0f, -0.94f, 0f), new Vector3(90f, 0f, 0f), new Vector3(3.2f, 3.2f, 1f), GlowFor("Player", CyanNeon, 0.6f));

        Light light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(player.transform, false);
        light.transform.localPosition = new Vector3(0f, 1.6f, 0f);
        light.type = LightType.Point;
        light.color = CyanNeon;
        light.range = 8f;
        light.intensity = 2.6f;

        GameObject trailObject = new GameObject("Trail");
        trailObject.transform.SetParent(player.transform, false);
        trailObject.transform.localPosition = new Vector3(0f, -0.35f, 0f);

        TrailRenderer trail = trailObject.AddComponent<TrailRenderer>();
        trail.time = 0.35f;
        trail.startWidth = 0.55f;
        trail.endWidth = 0f;
        trail.minVertexDistance = 0.08f;
        trail.emitting = false;
        trail.sharedMaterial = glowResource;
        trail.shadowCastingMode = ShadowCastingMode.Off;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(CyanNeon, 0f), new GradientColorKey(new Color(0.2f, 0.4f, 1f), 1f) },
            new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        trail.colorGradient = gradient;

        SimplePlayerController pc = player.AddComponent<SimplePlayerController>();
        SetRef(pc, "visual", visual.transform);
        SetRef(pc, "trail", trail);

        return player;
    }

    private static void BuildGuideArrow(Transform player)
    {
        Material arrowMat = Additive("Assets/Materials/FX/Arrow.mat", arrowTex, new Color(CyanNeon.r, CyanNeon.g, CyanNeon.b, 0.75f));

        GameObject arrow = FxQuad(null, "GuideArrow", new Vector3(0f, 0.06f, 0f), new Vector3(90f, 0f, 0f), new Vector3(1.1f, 1.1f, 1f), arrowMat);

        GuideArrow guide = arrow.AddComponent<GuideArrow>();
        SetRef(guide, "player", player);
        SetRef(guide, "arrowRenderer", arrow.GetComponent<Renderer>());
    }

    // ------------------------------------------------------------------ enemy

    private delegate void BodyBuilder(Transform visual, Material body, Material core, Color color);

    private static GameObject BuildEnemy(string name, Color color, EnemyBehavior behavior, float speed, int damage, int hp, int score,
        float aggro, float spin, BodyBuilder builder)
    {
        Material body = Lit($"{name}_Body", color * 0.35f, color * 2.6f, 0.4f, 0.65f);
        Material core = Lit($"{name}_Core", Color.white * 0.8f, Color.white * 3f, 0f, 0.9f);

        GameObject root = new GameObject(name);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.95f, 0f);

        builder(visual.transform, body, core, color);

        FxQuad(root.transform, "Glow", new Vector3(0f, 0.04f, 0f), new Vector3(90f, 0f, 0f), new Vector3(2.6f, 2.6f, 1f), GlowFor(name, color, 0.55f));

        EnemyChaser chaser = root.AddComponent<EnemyChaser>();
        chaser.Configure(behavior, speed, damage, hp, score, aggro, color, spin);
        SetRef(chaser, "visual", visual.transform);

        root.AddComponent<SpawnPop>().Configure(color, 1.5f);

        return SavePrefab(root, $"Assets/Prefabs/Enemies/{name}.prefab");
    }

    private static void BodyGrunt(Transform visual, Material body, Material core, Color color)
    {
        Part(visual, PrimitiveType.Cube, "Body", Vector3.zero, Vector3.one * 0.85f, body, new Vector3(45f, 0f, 35f));
        Part(visual, PrimitiveType.Cube, "Core", Vector3.zero, Vector3.one * 0.38f, core, new Vector3(45f, 0f, 35f));
    }

    private static void BodyHunter(Transform visual, Material body, Material core, Color color)
    {
        Part(visual, PrimitiveType.Sphere, "Body", Vector3.zero, Vector3.one * 0.78f, body);
        Part(visual, PrimitiveType.Cylinder, "Ring", Vector3.zero, new Vector3(1.35f, 0.025f, 1.35f), core, new Vector3(68f, 0f, 0f));
        Part(visual, PrimitiveType.Sphere, "Eye", new Vector3(0f, 0f, 0.36f), Vector3.one * 0.26f, core);
    }

    private static void BodyStriker(Transform visual, Material body, Material core, Color color)
    {
        Part(visual, PrimitiveType.Capsule, "Body", Vector3.zero, new Vector3(0.48f, 0.62f, 0.48f), body, new Vector3(90f, 0f, 0f));
        Part(visual, PrimitiveType.Cube, "Wings", new Vector3(0f, 0f, -0.18f), new Vector3(1.25f, 0.06f, 0.38f), body);
        Part(visual, PrimitiveType.Sphere, "Nose", new Vector3(0f, 0f, 0.6f), Vector3.one * 0.22f, core);
    }

    private static void BodyBrute(Transform visual, Material body, Material core, Color color)
    {
        visual.localPosition = new Vector3(0f, 1.05f, 0f);
        Part(visual, PrimitiveType.Sphere, "Body", Vector3.zero, Vector3.one * 1.3f, body);
        Part(visual, PrimitiveType.Sphere, "Core", Vector3.zero, Vector3.one * 0.55f, core);

        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f * Mathf.Deg2Rad;
            Part(visual, PrimitiveType.Sphere, $"Satellite_{i}", new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 1.05f, Vector3.one * 0.3f, core);
        }
    }

    // ------------------------------------------------------------------ item

    private static GameObject BuildItem(string name, string display, Color color, int points, int heal, ItemRarity rarity, bool beam, float glowSize,
        BodyBuilder builder)
    {
        Material body = Lit($"{name}_Body", color * 0.35f, color * 2.8f, 0.5f, 0.7f);
        Material core = Lit($"{name}_Core", Color.white * 0.85f, Color.white * 3.2f, 0f, 0.9f);

        GameObject root = new GameObject(name);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localPosition = new Vector3(0f, 0.85f, 0f);

        builder(visual.transform, body, core, color);

        FxQuad(root.transform, "Glow", new Vector3(0f, 0.04f, 0f), new Vector3(90f, 0f, 0f), new Vector3(glowSize * 1.6f, glowSize * 1.6f, 1f), GlowFor(name, color, 0.6f));

        if (beam)
        {
            Material beamMat = Additive($"Assets/Materials/FX/Beam_{name}.mat", beamTex, new Color(color.r, color.g, color.b, 0.5f));
            FxQuad(root.transform, "Beam_A", new Vector3(0f, 2.6f, 0f), Vector3.zero, new Vector3(1.2f, 5.2f, 1f), beamMat);
            FxQuad(root.transform, "Beam_B", new Vector3(0f, 2.6f, 0f), new Vector3(0f, 90f, 0f), new Vector3(1.2f, 5.2f, 1f), beamMat);
        }

        CollectibleItem item = root.AddComponent<CollectibleItem>();
        item.Configure(display, points, heal, rarity, color);
        SetRef(item, "visual", visual.transform);

        root.AddComponent<SpawnPop>().Configure(color, 1.1f);

        return SavePrefab(root, $"Assets/Prefabs/Items/{name}.prefab");
    }

    private static void BodyCoin(Transform visual, Material body, Material core, Color color)
    {
        Part(visual, PrimitiveType.Cylinder, "Disc", Vector3.zero, new Vector3(0.62f, 0.045f, 0.62f), body, new Vector3(90f, 0f, 0f));
        Part(visual, PrimitiveType.Cylinder, "Inner", Vector3.zero, new Vector3(0.36f, 0.06f, 0.36f), core, new Vector3(90f, 0f, 0f));
    }

    private static void BodyHealth(Transform visual, Material body, Material core, Color color)
    {
        Part(visual, PrimitiveType.Cube, "Body", Vector3.zero, Vector3.one * 0.52f, body);

        foreach (float z in new[] { 0.27f, -0.27f })
        {
            Part(visual, PrimitiveType.Cube, "CrossH", new Vector3(0f, 0f, z), new Vector3(0.36f, 0.1f, 0.05f), core);
            Part(visual, PrimitiveType.Cube, "CrossV", new Vector3(0f, 0f, z), new Vector3(0.1f, 0.36f, 0.05f), core);
        }
    }

    private static void BodyCrystal(Transform visual, Material body, Material core, Color color)
    {
        Part(visual, PrimitiveType.Cube, "Crystal", Vector3.zero, new Vector3(0.5f, 0.5f, 0.5f), body, new Vector3(45f, 0f, 45f));
        Part(visual, PrimitiveType.Cube, "Core", Vector3.zero, Vector3.one * 0.22f, core, new Vector3(45f, 0f, 45f));
    }

    private static void BodyLegendary(Transform visual, Material body, Material core, Color color)
    {
        Part(visual, PrimitiveType.Cube, "Gem", Vector3.zero, Vector3.one * 0.62f, body, new Vector3(45f, 0f, 45f));
        Part(visual, PrimitiveType.Cube, "Core", Vector3.zero, Vector3.one * 0.28f, core, new Vector3(45f, 0f, 45f));

        for (int i = 0; i < 3; i++)
        {
            float angle = i * 120f * Mathf.Deg2Rad;
            Part(visual, PrimitiveType.Cube, $"Orbit_{i}", new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 0.72f, Vector3.one * 0.15f, core, new Vector3(45f, 45f, 0f));
        }
    }

    // ------------------------------------------------------------------ exit

    private static GameObject BuildExit()
    {
        Color locked = new Color(1f, 0.2f, 0.25f);

        Material padMat = Lit("Exit_Pad", locked * 0.4f, locked * 2.2f, 0.5f, 0.7f);

        GameObject root = new GameObject("Exit");

        Part(root.transform, PrimitiveType.Cylinder, "Pad", new Vector3(0f, 0.03f, 0f), new Vector3(3f, 0.03f, 3f), padMat);

        Material ringMat = Additive("Assets/Materials/FX/Exit_Ring.mat", ringDashTex, new Color(locked.r, locked.g, locked.b, 0.5f));
        GameObject ring = FxQuad(root.transform, "Ring", new Vector3(0f, 0.08f, 0f), new Vector3(90f, 0f, 0f), new Vector3(5.2f, 5.2f, 1f), ringMat);

        Material beamMat = Additive("Assets/Materials/FX/Exit_Beam.mat", beamTex, new Color(locked.r, locked.g, locked.b, 0.5f));
        FxQuad(root.transform, "Beam_A", new Vector3(0f, 3.5f, 0f), Vector3.zero, new Vector3(2.2f, 7f, 1f), beamMat);
        FxQuad(root.transform, "Beam_B", new Vector3(0f, 3.5f, 0f), new Vector3(0f, 90f, 0f), new Vector3(2.2f, 7f, 1f), beamMat);

        Light light = new GameObject("Light").AddComponent<Light>();
        light.transform.SetParent(root.transform, false);
        light.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        light.type = LightType.Point;
        light.color = locked;
        light.range = 10f;
        light.intensity = 3f;

        ExitPoint exit = root.AddComponent<ExitPoint>();
        SetRef(exit, "ring", ring.transform);
        SetRef(exit, "pointLight", light);

        root.AddComponent<SpawnPop>().Configure(new Color(1f, 0.4f, 0.4f), 3f);

        return SavePrefab(root, "Assets/Prefabs/Exit.prefab");
    }

    private static GameObject BuildObstaclePrefab(int obstacleLayer)
    {
        Material metal = Lit("Obstacle", new Color(0.09f, 0.1f, 0.15f), CyanNeon * 1.9f, 0.7f, 0.5f, edgeTex);

        // Collider dipertahankan: Physics.CheckSphere harus mendeteksi obstacle hasil generate.
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Obstacle";
        cube.layer = obstacleLayer;
        cube.GetComponent<Renderer>().sharedMaterial = metal;

        return SavePrefab(cube, "Assets/Prefabs/Obstacle.prefab");
    }

    private static GameObject SavePrefab(GameObject root, string path)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // ------------------------------------------------------------------ kamera, post FX

    private static void ConfigureCamera(Transform player)
    {
        Camera cam = Camera.main;

        if (cam == null)
        {
            return;
        }

        cam.transform.position = new Vector3(0f, 21f, -13f);
        cam.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.012f, 0.016f, 0.035f);
        cam.fieldOfView = 52f;
        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 140f;
        cam.allowHDR = true;

        UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

        CameraRig rig = cam.gameObject.AddComponent<CameraRig>();
        SetRef(rig, "target", player);
    }

    private static void BuildPostProcessing()
    {
        const string path = "Assets/Settings/PCG_PostFX.asset";

        AssetDatabase.DeleteAsset(path);

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);

        Bloom bloom = profile.Add<Bloom>(true);
        bloom.threshold.Override(0.85f);
        bloom.intensity.Override(1.35f);
        bloom.scatter.Override(0.72f);
        bloom.highQualityFiltering.Override(true);

        Vignette vignette = profile.Add<Vignette>(true);
        vignette.intensity.Override(0.34f);
        vignette.smoothness.Override(0.5f);

        ColorAdjustments colorAdjustments = profile.Add<ColorAdjustments>(true);
        colorAdjustments.postExposure.Override(0.15f);
        colorAdjustments.contrast.Override(14f);
        colorAdjustments.saturation.Override(12f);

        Tonemapping tonemapping = profile.Add<Tonemapping>(true);
        tonemapping.mode.Override(TonemappingMode.ACES);

        ChromaticAberration chromatic = profile.Add<ChromaticAberration>(true);
        chromatic.intensity.Override(0.06f);

        foreach (VolumeComponent component in profile.components)
        {
            AssetDatabase.AddObjectToAsset(component, profile);
        }

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();

        GameObject volumeObject = new GameObject("Global Volume");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.sharedProfile = profile;
    }

    // ------------------------------------------------------------------ tabel spawn

    private struct EntryData
    {
        public string name;
        public GameObject prefab;
        public int weight;
        public Color color;
    }

    private static EntryData Entry(string name, GameObject prefab, int weight, Color color)
    {
        return new EntryData { name = name, prefab = prefab, weight = weight, color = color };
    }

    private static void SetEntries(SerializedProperty array, EntryData[] data)
    {
        array.arraySize = data.Length;

        for (int i = 0; i < data.Length; i++)
        {
            SerializedProperty e = array.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("name").stringValue = data[i].name;
            e.FindPropertyRelative("prefab").objectReferenceValue = data[i].prefab;
            e.FindPropertyRelative("weight").intValue = data[i].weight;
            e.FindPropertyRelative("yOffset").floatValue = 0f;
            e.FindPropertyRelative("color").colorValue = data[i].color;
        }
    }
}
