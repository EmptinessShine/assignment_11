using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One-time setup tool: imports the art, creates the data assets, all prefabs and the scenes
// Menu and Game. After it has run, everything lives in scenes, prefabs and data assets.
// Data assets (Assets/EnemyGame/Data) are created only if they don't exist yet,
// so running the tool again does not overwrite numbers changed by hand.
public static class EnemyGameBuilder
{
    private const string Root = "Assets/EnemyGame";
    private const string ArtDir = Root + "/Art/";
    private const string AudioDir = Root + "/Audio/";
    private const string PrefabDir = Root + "/Prefabs/";
    private const string SceneDir = Root + "/Scenes/";
    private const string GenDir = Root + "/Generated/";
    private const string DataDir = Root + "/Data/";

    private static Material mat;
    private static Font font;
    private static int wallLayer;
    private static LayerMask wallMask;

    // prefabs
    private static GameObject pSlash, pEnemySlash, pMuzzle, pSpark, pDust, pDeath, pImpact, pPopup;
    private static GameObject pBullet, pWall, pCrate, pBarrel, pExit, pMedkit, pPlayer, pPatroller, pShooter, pCharger;

    // data assets
    private static PlayerStats playerStats;
    private static PatrollerStats patrollerStats;
    private static ShooterStats shooterStats;
    private static ChargerStats chargerStats, bossStats;
    private static ItemData medkitData;
    private static LevelList levelList;

    [MenuItem("Tools/Enemy Game/Build Everything")]
    public static void BuildAll()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogError("EnemyGameBuilder: Unity is still compiling scripts. Wait until it finishes and run again.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Directory.CreateDirectory(PrefabDir);
        Directory.CreateDirectory(PrefabDir + "FX");
        Directory.CreateDirectory(PrefabDir + "Levels");
        Directory.CreateDirectory(SceneDir);
        Directory.CreateDirectory(GenDir);
        Directory.CreateDirectory(DataDir);
        Directory.CreateDirectory(DataDir + "Levels");
        AssetDatabase.Refresh();

        wallLayer = EnsureLayer("Walls");
        wallMask = 1 << wallLayer;
        ImportArt();
        mat = MakeMaterial();
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        BuildStatsAssets();
        BuildFxPrefabs();
        BuildWorldPrefabs();
        BuildCharacterPrefabs();
        BuildLevels();

        BuildMenuScene();
        BuildGameScene();
        RemoveOldScenes();

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(SceneDir + "Menu.unity", true),
            new EditorBuildSettingsScene(SceneDir + "Game.unity", true),
        };
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(SceneDir + "Menu.unity");
        Debug.Log("EnemyGameBuilder: done. Scenes are in " + SceneDir + ", data assets are in " + DataDir);
    }

    // builds Builds/assignment_11.apk with the Menu and Game scenes
    [MenuItem("Tools/Enemy Game/Build APK")]
    public static void BuildApk()
    {
        if (EditorApplication.isCompiling)
        {
            Debug.LogError("EnemyGameBuilder: Unity is still compiling scripts. Wait until it finishes and run again.");
            return;
        }
        if (!File.Exists(SceneDir + "Game.unity"))
        {
            Debug.LogError("EnemyGameBuilder: run Tools/Enemy Game/Build Everything first.");
            return;
        }

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.buildAppBundle = false; // .apk, not .aab

        var options = new BuildPlayerOptions();
        options.scenes = new[] { SceneDir + "Menu.unity", SceneDir + "Game.unity" };
        options.locationPathName = "Builds/assignment_11.apk";
        options.target = BuildTarget.Android;

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result == BuildResult.Succeeded)
        {
            Debug.Log("EnemyGameBuilder: APK is ready: " + Path.GetFullPath(options.locationPathName));
            EditorUtility.RevealInFinder(options.locationPathName);
        }
        else
        {
            Debug.LogError("EnemyGameBuilder: build failed (" + report.summary.result + "). See the errors above.");
        }
    }

    // opens the folder with save.json (for the broken file test)
    [MenuItem("Tools/Enemy Game/Open Save Folder")]
    public static void OpenSaveFolder()
    {
        string file = Path.Combine(Application.persistentDataPath, "save.json");
        if (File.Exists(file)) EditorUtility.RevealInFinder(file);
        else EditorUtility.RevealInFinder(Application.persistentDataPath);
    }

    // Level1, Level2 and Random from the last assignment are replaced by Game + LevelData
    private static void RemoveOldScenes()
    {
        AssetDatabase.DeleteAsset(SceneDir + "Level1.unity");
        AssetDatabase.DeleteAsset(SceneDir + "Level2.unity");
        AssetDatabase.DeleteAsset(SceneDir + "Random.unity");
        AssetDatabase.DeleteAsset(PrefabDir + "Rooms");
    }

    // =====================================================================  ART
    private static void ImportArt()
    {
        foreach (string file in Directory.GetFiles(ArtDir, "*.png", SearchOption.AllDirectories))
        {
            string path = file.Replace('\\', '/');
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            if (imp == null) continue;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 32f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            imp.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spritePixelsPerUnit = 32f;
            if (path.Contains("/UI/")) settings.spriteBorder = path.EndsWith("panel.png") ? new Vector4(10, 10, 10, 10) : Vector4.zero;
            imp.SetTextureSettings(settings);
            imp.wrapMode = path.Contains("/Tiles/") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            imp.SaveAndReimport();
        }
    }

    private static Sprite S(string rel) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtDir + rel + ".png");
    private static Sprite[] Frames(string rel, int count)
    {
        var list = new Sprite[count];
        for (int i = 0; i < count; i++) list[i] = S(rel + "_" + i);
        return list;
    }
    private static AudioClip A(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>(AudioDir + name + ".wav");

    private static Material MakeMaterial()
    {
        string path = GenDir + "SpriteUnlit.mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        m = new Material(sh);
        AssetDatabase.CreateAsset(m, path);
        return m;
    }

    // =====================================================================  PREFABS
    private static void BuildFxPrefabs()
    {
        pSlash = FxPrefab("Slash", Frames("FX/slash", 4), 22f, 1f);
        pEnemySlash = FxPrefab("EnemySlash", Frames("FX/enemyslash", 4), 22f, 1.1f);
        pMuzzle = FxPrefab("MuzzleFlash", Frames("FX/muzzle", 3), 24f, 1.2f);
        pSpark = FxPrefab("HitSpark", Frames("FX/spark", 4), 20f, 1.4f);
        pImpact = FxPrefab("Impact", Frames("FX/spark", 4), 24f, 0.8f);
        pDust = FxPrefab("Dust", Frames("FX/dust", 3), 12f, 1f, 2);
        pDeath = FxPrefab("DeathPuff", Frames("FX/dust", 3), 8f, 2.5f);

        var go = new GameObject("Popup");
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 48;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = Color.yellow;
        tm.text = "В спину!";
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 60;
        var popup = go.AddComponent<FloatingPopup>();
        Set(popup, "text", tm);
        pPopup = SavePrefab(go, "FX/Popup");
    }

    private static GameObject FxPrefab(string name, Sprite[] frames, float fps, float scale, int order = 15)
    {
        var go = new GameObject(name);
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.sharedMaterial = mat;
        sr.sortingOrder = order;
        var anim = go.AddComponent<SpriteFrameAnimation>();
        Set(anim, "spriteRenderer", sr);
        Set(anim, "frames", frames);
        Set(anim, "fps", fps);
        return SavePrefab(go, "FX/" + name);
    }

    private static void BuildWorldPrefabs()
    {
        // Bullet
        var b = new GameObject("Bullet");
        var rb = b.AddComponent<Rigidbody2D>();
        SetupBody(rb, 0.1f);
        var bc = b.AddComponent<CircleCollider2D>();
        bc.radius = 0.12f;
        bc.isTrigger = true;
        Child(b.transform, "Sprite", S("FX/bullet"), Vector2.zero, 1.6f, 12);
        var proj = b.AddComponent<Projectile>();
        Set(proj, "wallMask", wallMask);
        Set(proj, "impactEffect", pImpact);
        pBullet = SavePrefab(b, "Bullet");

        // Wall (tiled - stretch it with Size in the SpriteRenderer)
        var w = new GameObject("Wall");
        w.layer = wallLayer;
        var wsr = w.AddComponent<SpriteRenderer>();
        wsr.sprite = S("Tiles/wall");
        wsr.drawMode = SpriteDrawMode.Tiled;
        wsr.size = Vector2.one;
        wsr.sharedMaterial = mat;
        wsr.sortingOrder = 0;
        w.AddComponent<BoxCollider2D>().size = Vector2.one;
        pWall = SavePrefab(w, "Wall");

        // Cover props
        pCrate = Prop("Crate", S("Props/crate"), false);
        pBarrel = Prop("Barrel", S("Props/barrel"), true);

        // Exit
        var e = new GameObject("Exit");
        var ec = e.AddComponent<BoxCollider2D>();
        ec.isTrigger = true;
        ec.size = new Vector2(1.4f, 1.4f);
        Child(e.transform, "Visual", S("Props/exit"), Vector2.zero, 1f, -10);
        e.AddComponent<ExitZone>();
        pExit = SavePrefab(e, "Exit");
    }

    private static GameObject Prop(string name, Sprite sprite, bool round)
    {
        var go = new GameObject(name);
        go.layer = wallLayer;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.sortingOrder = 1;
        if (round) go.AddComponent<CircleCollider2D>().radius = 0.45f;
        else go.AddComponent<BoxCollider2D>().size = new Vector2(0.9f, 0.9f);
        return SavePrefab(go, name);
    }

    private static void BuildMedkit()
    {
        var go = new GameObject("Medkit");
        var col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.45f;
        var glow = Child(go.transform, "Glow", S("FX/glow"), Vector2.zero, 1.3f, 0);
        glow.GetComponent<SpriteRenderer>().color = new Color(0.3f, 1f, 0.4f, 0.5f);
        var dot = Child(go.transform, "Icon", S("FX/circle"), Vector2.zero, 0.6f, 1);
        dot.GetComponent<SpriteRenderer>().color = new Color(0.3f, 0.9f, 0.4f, 1f);

        var plus = new GameObject("Plus");
        plus.transform.SetParent(go.transform, false);
        var tm = plus.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 48;
        tm.characterSize = 0.1f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
        tm.color = Color.white;
        tm.text = "+";
        var mr = plus.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 2;

        var pickup = go.AddComponent<HealthPickup>();
        Set(pickup, "item", medkitData);
        pMedkit = SavePrefab(go, "Medkit");
    }

    private static void BuildCharacterPrefabs()
    {
        BuildMedkit();

        // ---------- Player ----------
        var go = new GameObject("Player");
        go.tag = "Player";
        var rb = go.AddComponent<Rigidbody2D>();
        SetupBody(rb, 1f);
        go.AddComponent<CircleCollider2D>().radius = 0.35f;
        Child(go.transform, "Shadow", S("Characters/shadow"), new Vector2(0, -0.2f), 1f, -5);
        var body = Child(go.transform, "Body", S("Characters/player_0"), Vector2.zero, 1f, 6);

        var health = go.AddComponent<Health>();
        Set(health, "body", body.GetComponent<SpriteRenderer>());
        Set(health, "hitEffect", pSpark);
        Set(health, "hurtSound", A("hurt"));

        var anim = go.AddComponent<CharacterVisual>();
        SetupAnimator(anim, body, "player");

        var pc = go.AddComponent<PlayerController>();
        Set(pc, "stats", playerStats);
        Set(pc, "visual", anim);
        Set(pc, "body", body.GetComponent<SpriteRenderer>());
        Set(pc, "slashEffect", pSlash);
        Set(pc, "popupPrefab", pPopup);
        Set(pc, "slashSound", A("slash"));
        Set(pc, "hitSound", A("hit"));
        Set(pc, "backstabSound", A("backstab"));
        pPlayer = SavePrefab(go, "Player");

        // ---------- Enemies ----------
        var pat = Enemy<PatrollerEnemy>("Patroller", "patroller", 0.38f, 1f, patrollerStats);
        Set(pat, "slashEffect", pEnemySlash);
        Set(pat, "slashSound", A("slash"));
        pPatroller = SavePrefab(pat.gameObject, "Patroller");

        var sh = Enemy<ShooterEnemy>("Shooter", "shooter", 0.36f, 1f, shooterStats);
        Set(sh, "bulletPrefab", pBullet.GetComponent<Projectile>());
        Set(sh, "aimLine", Line(sh.transform, new Color(1f, 0.15f, 0.15f, 1f), 0.06f, 2));
        Set(sh, "wallMask", wallMask);
        Set(sh, "muzzleFlash", pMuzzle);
        Set(sh, "shotSound", A("shot"));
        pShooter = SavePrefab(sh.gameObject, "Shooter");

        var ch = Enemy<ChargerEnemy>("Charger", "charger", 0.5f, 1.3f, chargerStats);
        Set(ch, "pathLine", Line(ch.transform, new Color(1f, 0.15f, 0.15f, 0.35f), 0.7f, 2));
        Set(ch, "wallMask", wallMask);
        Set(ch, "dustEffect", pDust);
        Set(ch, "chargeSound", A("charge"));
        Set(ch, "wallHitSound", A("hit"));
        pCharger = SavePrefab(ch.gameObject, "Charger");
    }

    private static T Enemy<T>(string name, string art, float radius, float scale, EnemyStats stats) where T : EnemyBase
    {
        var go = new GameObject(name);
        var rb = go.AddComponent<Rigidbody2D>();
        SetupBody(rb, 5f);
        go.AddComponent<CircleCollider2D>().radius = radius;

        var sight = Line(go.transform, new Color(1f, 0.9f, 0.2f, 0.45f), 0.04f, 3);
        sight.enabled = true;
        sight.sortingOrder = -4;

        Child(go.transform, "Shadow", S("Characters/shadow"), new Vector2(0, -0.2f), scale, -5);
        var glow = Child(go.transform, "TelegraphGlow", S("FX/glow"), Vector2.zero, 2f * scale, 4);
        var body = Child(go.transform, "Body", S("Characters/" + art + "_0"), Vector2.zero, scale, 5);
        var icon = Child(go.transform, "StatusIcon", null, new Vector2(0, 0.75f * scale + 0.2f), 1.2f, 20);
        var label = Label(go.transform, new Vector2(0, -0.6f * scale - 0.15f));

        var health = go.AddComponent<Health>();
        Set(health, "body", body.GetComponent<SpriteRenderer>());
        Set(health, "hitEffect", pSpark);
        Set(health, "deathEffect", pDeath);
        Set(health, "hurtSound", A("hit"));
        Set(health, "deathSound", A("enemy_die"));

        var vision = go.AddComponent<VisionSensor>();
        Set(vision, "wallMask", wallMask);
        Set(vision, "sightLine", sight);

        var tel = go.AddComponent<Telegraph>();
        Set(tel, "glow", glow.GetComponent<SpriteRenderer>());
        Set(tel, "windUpSound", A("windup"));

        var anim = go.AddComponent<CharacterVisual>();
        SetupAnimator(anim, body, art);

        var enemy = go.AddComponent<T>();
        Set(enemy, "stats", stats);
        Set(enemy, "visual", anim);
        Set(enemy, "icon", icon.GetComponent<SpriteRenderer>());
        Set(enemy, "alertIcon", S("FX/icon_alert"));
        Set(enemy, "searchIcon", S("FX/icon_search"));
        Set(enemy, "stunIcon", S("FX/icon_stun"));
        Set(enemy, "stateLabel", label);
        return enemy;
    }

    private static void SetupAnimator(CharacterVisual anim, GameObject body, string art)
    {
        Set(anim, "body", body.transform);
        Set(anim, "bodyRenderer", body.GetComponent<SpriteRenderer>());
        Set(anim, "walkFrames", new Object[] { S("Characters/" + art + "_0"), S("Characters/" + art + "_1"), S("Characters/" + art + "_0"), S("Characters/" + art + "_2") });
    }

    // =====================================================================  DATA ASSETS
    // returns the existing asset or creates a new one. isNew = true only for a new asset.
    private static T DataAsset<T>(string name, out bool isNew) where T : ScriptableObject
    {
        string path = DataDir + name + ".asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        isNew = asset == null;
        if (isNew)
        {
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
        }
        return asset;
    }

    private static void Common(EnemyStats s, int hp, float speed, int damage, float size, int score, float viewDistance, float viewAngle)
    {
        s.maxHealth = hp;
        s.invincibleTime = 0.15f;
        s.moveSpeed = speed;
        s.damage = damage;
        s.size = size;
        s.scoreValue = score;
        s.viewDistance = viewDistance;
        s.viewAngle = viewAngle;
        s.lookAroundSpeed = 150f;
        s.backstabAngle = 110f;
    }

    private static void BuildStatsAssets()
    {
        bool isNew;

        playerStats = DataAsset<PlayerStats>("PlayerStats", out isNew);
        if (isNew)
        {
            playerStats.moveSpeed = 4.5f;
            playerStats.maxHealth = 3;
            playerStats.invincibleTime = 1f;
            playerStats.attackRadius = 1.3f;
            playerStats.attackDamage = 1;
            playerStats.attackCooldown = 0.35f;
            playerStats.backstabDamage = 10;
            EditorUtility.SetDirty(playerStats);
        }

        patrollerStats = DataAsset<PatrollerStats>("PatrollerStats", out isNew);
        if (isNew)
        {
            Common(patrollerStats, 2, 2f, 1, 1f, 100, 6f, 100f);
            patrollerStats.chaseSpeed = 3.2f;
            patrollerStats.attackRange = 1.2f;
            patrollerStats.hitRange = 1.4f;
            patrollerStats.windUpTime = 0.55f;   // after the playtest (was 0.45)
            patrollerStats.cooldownTime = 0.9f;
            patrollerStats.searchTime = 3f;
            EditorUtility.SetDirty(patrollerStats);
        }

        shooterStats = DataAsset<ShooterStats>("ShooterStats", out isNew);
        if (isNew)
        {
            Common(shooterStats, 1, 1.6f, 1, 1f, 150, 7f, 70f);
            shooterStats.scanSpeed = 50f;
            shooterStats.scanTime = 2f;
            shooterStats.aimTime = 0.7f;         // after the playtest (was 0.6)
            shooterStats.aimLockTime = 0.2f;
            shooterStats.cooldownTime = 1.2f;
            shooterStats.bulletSpeed = 7f;
            shooterStats.bulletLifetime = 3f;
            shooterStats.investigateTime = 6f;
            EditorUtility.SetDirty(shooterStats);
        }

        chargerStats = DataAsset<ChargerStats>("ChargerStats", out isNew);
        if (isNew)
        {
            Common(chargerStats, 3, 1.6f, 1, 1f, 200, 6f, 120f);
            chargerStats.triggerRange = 5.5f;
            chargerStats.windUpTime = 0.7f;
            chargerStats.chargeSpeed = 11f;
            chargerStats.chargeTime = 0.6f;
            chargerStats.hitDistance = 0.9f;
            chargerStats.stunTime = 1.3f;
            chargerStats.investigateTime = 6f;
            chargerStats.investigateSpeed = 3.2f;
            EditorUtility.SetDirty(chargerStats);
        }

        // the boss is a charger prefab with its own numbers
        bossStats = DataAsset<ChargerStats>("BossStats", out isNew);
        if (isNew)
        {
            Common(bossStats, 20, 1.4f, 2, 1.6f, 1000, 8f, 140f);
            bossStats.triggerRange = 7f;
            bossStats.windUpTime = 0.8f;
            bossStats.chargeSpeed = 12f;
            bossStats.chargeTime = 0.8f;
            bossStats.hitDistance = 1.3f;
            bossStats.stunTime = 2f;
            bossStats.investigateTime = 8f;
            bossStats.investigateSpeed = 3f;
            EditorUtility.SetDirty(bossStats);
        }

        medkitData = DataAsset<ItemData>("Medkit", out isNew);
        if (isNew)
        {
            medkitData.itemName = "Аптечка";
            medkitData.healAmount = 1;
            EditorUtility.SetDirty(medkitData);
        }
    }

    private static EnemySpawn Sp(EnemyType type, Vector2 pos, Vector2 look, params Vector2[] route)
    {
        var s = new EnemySpawn();
        s.type = type;
        s.position = pos;
        s.lookDirection = look;
        s.route = route;
        return s;
    }

    private static LevelData Level(string file, string title, GameObject layout, Vector2 start, LevelGoal goal,
        float time, float alertRadius, int helpers, int pointsPerSecond, EnemySpawn[] enemies)
    {
        bool isNew;
        var level = DataAsset<LevelData>("Levels/" + file, out isNew);
        level.layout = layout; // the layout prefab is rebuilt every time, the link is always set
        if (isNew)
        {
            level.levelName = title;
            level.playerStart = start;
            level.goal = goal;
            level.timeLimit = time;
            level.alertRadius = alertRadius;
            level.helpersCount = helpers;
            level.pointsPerSecondLeft = pointsPerSecond;
            level.enemies = enemies;
        }
        EditorUtility.SetDirty(level);
        return level;
    }

    private static void BuildLevels()
    {
        var up = new Vector2(0, 1);
        var right = new Vector2(1, 0);

        // ---------- Level 1: one of each + a second patroller ----------
        var l1 = Level("Level_01", "Уровень 1", Layout1(), new Vector2(-17, -7), LevelGoal.ReachExit, 90f, 10f, 1, 10, new[]
        {
            Sp(EnemyType.Patroller, new Vector2(-8, -6), up, new Vector2(-8, -7), new Vector2(-8, 7)),
            Sp(EnemyType.Patroller, new Vector2(7, 3), right, new Vector2(7, 3), new Vector2(13, 3), new Vector2(13, -3), new Vector2(7, -3)),
            Sp(EnemyType.Shooter, new Vector2(17, 4), new Vector2(-1, -0.3f)),
            Sp(EnemyType.Charger, new Vector2(1, -7), right, new Vector2(1, -7), new Vector2(9, -7)),
        });

        // ---------- Level 2: bigger, 7 enemies, three rooms ----------
        var l2 = Level("Level_02", "Уровень 2", Layout2(), new Vector2(-20, 0), LevelGoal.ReachExit, 120f, 12f, 2, 10, new[]
        {
            Sp(EnemyType.Patroller, new Vector2(-9, 9), right, new Vector2(-9, 9), new Vector2(3, 9), new Vector2(3, 4), new Vector2(-9, 4)),
            Sp(EnemyType.Patroller, new Vector2(-9, -4), right, new Vector2(-9, -4), new Vector2(0, -4), new Vector2(0, -10), new Vector2(-9, -10)),
            Sp(EnemyType.Shooter, new Vector2(4, -11), new Vector2(-1, 0.5f)),
            Sp(EnemyType.Charger, new Vector2(9, 8), new Vector2(0, -1), new Vector2(9, 8), new Vector2(9, -8)),
            Sp(EnemyType.Shooter, new Vector2(22, 8), new Vector2(-1, -0.3f)),
            Sp(EnemyType.Charger, new Vector2(14, -9), right, new Vector2(14, -9), new Vector2(20, -6)),
            Sp(EnemyType.Patroller, new Vector2(15, 4), right, new Vector2(15, 4), new Vector2(21, 4), new Vector2(21, -4), new Vector2(15, -4)),
        });

        // ---------- Level 3: arena with the boss, you have to defeat everyone ----------
        var boss = Sp(EnemyType.Charger, new Vector2(8, 0), new Vector2(-1, 0), new Vector2(4, 0), new Vector2(11, 0));
        boss.statsOverride = bossStats;
        var l3 = Level("Level_03", "Уровень 3. Босс", Layout3(), new Vector2(-15, 0), LevelGoal.KillAll, 150f, 14f, 3, 15, new[]
        {
            boss,
            Sp(EnemyType.Shooter, new Vector2(15, 8), new Vector2(-1, -0.5f)),
            Sp(EnemyType.Shooter, new Vector2(15, -8), new Vector2(-1, 0.5f)),
            Sp(EnemyType.Patroller, new Vector2(-8, 8), right, new Vector2(-8, 8), new Vector2(8, 8)),
        });

        bool isNew;
        levelList = DataAsset<LevelList>("LevelList", out isNew);
        if (isNew)
        {
            levelList.levels = new[] { l1, l2, l3 };
            EditorUtility.SetDirty(levelList);
        }
    }

    // =====================================================================  LEVEL LAYOUTS (prefabs)
    // floor, walls, boxes, medkits and the exit. enemies are not here, they are in LevelData.
    private static GameObject NewLayout(string name, Vector2 floorSize)
    {
        var root = new GameObject(name);
        var floor = new GameObject("Floor").AddComponent<SpriteRenderer>();
        floor.transform.SetParent(root.transform, false);
        floor.sprite = S("Tiles/floor");
        floor.drawMode = SpriteDrawMode.Tiled;
        floor.size = floorSize;
        floor.sharedMaterial = mat;
        floor.sortingOrder = -20;
        return root;
    }

    private static GameObject Layout1()
    {
        var root = NewLayout("Layout_Level1", new Vector2(40, 20));
        var L = root.transform;

        Wall(L, 0, 10, 41, 1); Wall(L, 0, -10, 41, 1); Wall(L, -20, 0, 1, 21); Wall(L, 20, 0, 1, 21);
        Wall(L, -12, 3, 1, 14);   // gap at the bottom
        Wall(L, -4, -3, 1, 14);   // gap at the top
        Wall(L, 4, 3, 2, 2); Wall(L, 4, -3, 2, 2); Wall(L, 10, 0, 2, 2); Wall(L, 15, -4, 1, 4);

        Place(pCrate, new Vector2(-16, 4), L); Place(pBarrel, new Vector2(-17, 7), L);
        Place(pCrate, new Vector2(0, 5), L); Place(pBarrel, new Vector2(14, 6), L);
        Place(pMedkit, new Vector2(-15, 8), L);
        Place(pExit, new Vector2(18, -8), L);
        return SavePrefab(root, "Levels/Layout_Level1");
    }

    private static GameObject Layout2()
    {
        var root = NewLayout("Layout_Level2", new Vector2(48, 26));
        var L = root.transform;

        Wall(L, 0, 13, 49, 1); Wall(L, 0, -13, 49, 1); Wall(L, -24, 0, 1, 27); Wall(L, 24, 0, 1, 27);
        // wall A at x=-12 with doors at y -9..-6 and 5..8
        Wall(L, -12, -11, 1, 4); Wall(L, -12, -0.5f, 1, 11); Wall(L, -12, 10.5f, 1, 5);
        // wall B at x=6 with a door at y -2..2
        Wall(L, 6, -7.5f, 1, 11); Wall(L, 6, 7.5f, 1, 11);
        // middle cover
        Wall(L, -3, 0, 10, 1);
        // right room pillars
        Wall(L, 12, 5, 2, 2); Wall(L, 12, -5, 2, 2); Wall(L, 18, 0, 2, 4);

        Place(pBarrel, new Vector2(-18, 8), L); Place(pBarrel, new Vector2(-18, -8), L); Place(pCrate, new Vector2(-17, -9), L);
        Place(pCrate, new Vector2(-3, 6.5f), L); Place(pCrate, new Vector2(-3, -6.5f), L);
        Place(pCrate, new Vector2(11, 1), L); Place(pBarrel, new Vector2(22, 11), L);
        Place(pMedkit, new Vector2(-6, -11), L); Place(pMedkit, new Vector2(15, 11), L);
        Place(pExit, new Vector2(21, -10), L);
        return SavePrefab(root, "Levels/Layout_Level2");
    }

    private static GameObject Layout3()
    {
        var root = NewLayout("Layout_Level3", new Vector2(36, 22));
        var L = root.transform;

        Wall(L, 0, 11, 37, 1); Wall(L, 0, -11, 37, 1); Wall(L, -18, 0, 1, 23); Wall(L, 18, 0, 1, 23);
        // pillars: lure the boss into them, it gets stunned
        Wall(L, -6, 4, 2, 2); Wall(L, -6, -4, 2, 2); Wall(L, 4, 5, 2, 2); Wall(L, 4, -5, 2, 2);
        // cover in front of the shooters
        Place(pCrate, new Vector2(12, 6), L); Place(pCrate, new Vector2(12, -6), L);
        Place(pBarrel, new Vector2(-14, 8), L); Place(pCrate, new Vector2(-14, -8), L);
        Place(pMedkit, new Vector2(-16, 9), L); Place(pMedkit, new Vector2(0, -9), L);
        return SavePrefab(root, "Levels/Layout_Level3");
    }

    // =====================================================================  SCENES
    private static void SaveScene(string name)
    {
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SceneDir + name + ".unity");
    }

    private static Camera NewCamera(Vector2 pos)
    {
        var camGo = new GameObject("Main Camera");
        camGo.tag = "MainCamera";
        camGo.transform.position = new Vector3(pos.x, pos.y, -10);
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 6f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.07f, 0.07f, 0.1f);
        camGo.AddComponent<AudioListener>();
        return cam;
    }

    // empty full-screen object to hold one menu screen
    private static GameObject Panel(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return go;
    }

    // ---------- Game: one scene for all levels ----------
    private static void BuildGameScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var player = Place(pPlayer, Vector2.zero, null);
        var cam = NewCamera(Vector2.zero);
        Set(cam.gameObject.AddComponent<CameraFollow>(), "target", player.transform);

        // Alerts (siren + nearest enemies run to you). radius and helpers come from LevelData
        var alert = new GameObject("AlertSystem").AddComponent<AlertSystem>();
        Set(alert, "sirenSound", A("siren"));
        Set(alert, "sirenVolume", 0.25f);
        Set(alert, "sirenCooldown", 5f);

        var gm = new GameObject("GameManager").AddComponent<GameManager>();
        Set(gm, "levelList", levelList);
        Set(gm, "patrollerPrefab", pPatroller);
        Set(gm, "shooterPrefab", pShooter);
        Set(gm, "chargerPrefab", pCharger);
        Set(gm, "restartDelay", 1.2f);
        Set(gm, "winSound", A("win"));
        Set(gm, "loseSound", A("lose"));
        BuildHud(gm, player);
        SaveScene("Game");
    }

    private static void BuildHud(GameManager gm, GameObject player)
    {
        Canvas canvas = NewCanvas();
        Transform c = canvas.transform;

        var joyBase = UIImage(c, "Joystick", S("UI/joy_base"), Color.white, new Vector2(0, 0), new Vector2(250, 250), new Vector2(320, 320));
        var knob = UIImage(joyBase.transform, "Knob", S("UI/joy_knob"), Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(140, 140));
        knob.GetComponent<Image>().raycastTarget = false;
        var joy = joyBase.AddComponent<VirtualJoystick>();
        Set(joy, "background", joyBase.GetComponent<RectTransform>());
        Set(joy, "handle", knob.GetComponent<RectTransform>());
        Set(player.GetComponent<PlayerController>(), "joystick", joy);

        var hit = UIImage(c, "HitButton", S("UI/btn_attack"), Color.white, new Vector2(1, 0), new Vector2(-240, 240), new Vector2(250, 250));
        hit.AddComponent<AttackButton>();

        Set(gm, "levelText", UIText(c, "LevelName", "Уровень 1", 44, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(260, -50), new Vector2(460, 70)));
        Set(gm, "hpText", UIText(c, "HP", "Жизни 3/3", 44, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(260, -112), new Vector2(460, 70)));
        Set(gm, "triesText", UIText(c, "Tries", "Попытка 1", 34, TextAnchor.UpperLeft, new Vector2(0, 1), new Vector2(260, -170), new Vector2(460, 60)));
        Set(gm, "timerText", UIText(c, "Timer", "90 с", 56, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -50), new Vector2(400, 80)));
        Set(gm, "goalText", UIText(c, "Goal", "Цель", 34, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -125), new Vector2(900, 50)));

        var message = UIText(c, "Message", "", 80, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 120), new Vector2(1400, 300));
        message.raycastTarget = false;
        Set(gm, "messageText", message);

        Button menu = UIButton(c, "Меню", new Vector2(1, 1), new Vector2(-200, -60), new Vector2(340, 90), 34);
        UnityEventTools.AddPersistentListener(menu.onClick, gm.LoadMenu);

        // Level clear panel
        var panel = UIImage(c, "ClearPanel", S("UI/panel"), new Color(0.05f, 0.06f, 0.1f, 0.92f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 700));
        panel.GetComponent<Image>().type = Image.Type.Sliced;
        Set(gm, "winPanel", panel);
        Set(gm, "winText", UIText(panel.transform, "ClearText", "Уровень пройден", 46, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -170), new Vector2(860, 300)));
        Button next = UIButton(panel.transform, "Дальше", new Vector2(0.5f, 0), new Vector2(-280, 90), new Vector2(240, 100), 40);
        UnityEventTools.AddPersistentListener(next.onClick, gm.LoadNext);
        Set(gm, "nextButton", next.gameObject);
        Button retry = UIButton(panel.transform, "Заново", new Vector2(0.5f, 0), new Vector2(0, 90), new Vector2(240, 100), 40);
        UnityEventTools.AddPersistentListener(retry.onClick, gm.Restart);
        Button menu2 = UIButton(panel.transform, "Меню", new Vector2(0.5f, 0), new Vector2(280, 90), new Vector2(240, 100), 40);
        UnityEventTools.AddPersistentListener(menu2.onClick, gm.LoadMenu);
        panel.SetActive(false);
    }

    // ---------- Menu: main screen, level select, settings, reset confirm ----------
    private static void BuildMenuScene()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        NewCamera(Vector2.zero);

        var floor = new GameObject("Floor").AddComponent<SpriteRenderer>();
        floor.sprite = S("Tiles/floor");
        floor.drawMode = SpriteDrawMode.Tiled;
        floor.size = new Vector2(30, 14);
        floor.sharedMaterial = mat;
        floor.color = new Color(0.6f, 0.6f, 0.7f);
        floor.sortingOrder = -20;

        Canvas canvas = NewCanvas();
        Transform c = canvas.transform;
        var menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        Set(menu, "levelList", levelList);

        UIText(c, "Title", "Три врага", 110, TextAnchor.MiddleCenter, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(1600, 160));
        Set(menu, "infoText", UIText(c, "Info", "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0), new Vector2(0, 170), new Vector2(1400, 60)));

        // --- main panel ---
        var main = Panel(c, "MainPanel");
        string[] names = { "patroller", "shooter", "charger" };
        string[] titles = { "Патрульный", "Стрелок", "Таран" };
        for (int i = 0; i < 3; i++)
        {
            var img = UIImage(main.transform, titles[i], S("Characters/" + names[i] + "_0"), Color.white, new Vector2(0.5f, 0.5f), new Vector2(-520 + i * 520, 120), new Vector2(160, 160));
            img.GetComponent<Image>().preserveAspect = true;
            img.transform.localRotation = Quaternion.Euler(0, 0, 90);
            UIText(main.transform, titles[i] + "Label", titles[i], 36, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(-520 + i * 520, 10), new Vector2(400, 60));
        }
        Button play = UIButton(main.transform, "Играть", new Vector2(0.5f, 0.5f), new Vector2(-220, -180), new Vector2(380, 120), 48);
        UnityEventTools.AddPersistentListener(play.onClick, menu.ShowLevels);
        Button settings = UIButton(main.transform, "Настройки", new Vector2(0.5f, 0.5f), new Vector2(220, -180), new Vector2(380, 120), 48);
        UnityEventTools.AddPersistentListener(settings.onClick, menu.ShowSettings);
        Button reset = UIButton(main.transform, "Сбросить прогресс", new Vector2(1, 0), new Vector2(-250, 70), new Vector2(440, 90), 32);
        UnityEventTools.AddPersistentListener(reset.onClick, menu.ShowConfirmReset);
        Set(menu, "mainPanel", main);

        // --- level select ---
        var levels = Panel(c, "LevelsPanel");
        UIText(levels.transform, "Header", "Выбор уровня", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(900, 90));
        var buttons = new Object[levelList.levels.Length];
        for (int i = 0; i < levelList.levels.Length; i++)
        {
            float x = (i - (levelList.levels.Length - 1) / 2f) * 460;
            buttons[i] = LevelButtonUI(levels.transform, i, new Vector2(x, -20), menu);
        }
        Set(menu, "levelButtons", buttons);
        Button back1 = UIButton(levels.transform, "Назад", new Vector2(0.5f, 0.5f), new Vector2(0, -280), new Vector2(300, 100), 40);
        UnityEventTools.AddPersistentListener(back1.onClick, menu.ShowMain);
        Set(menu, "levelsPanel", levels);

        // --- settings ---
        var set = Panel(c, "SettingsPanel");
        UIText(set.transform, "Header", "Настройки", 56, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 200), new Vector2(900, 90));
        UIText(set.transform, "VolumeLabel", "Громкость", 40, TextAnchor.MiddleRight, new Vector2(0.5f, 0.5f), new Vector2(-380, 60), new Vector2(360, 60));
        Slider slider = MakeSlider(set.transform, new Vector2(140, 60));
        UnityEventTools.AddPersistentListener(slider.onValueChanged, menu.OnVolumeChanged);
        Set(menu, "volumeSlider", slider);
        Toggle toggle = MakeToggle(set.transform, "Подписи над врагами", new Vector2(-250, -60));
        UnityEventTools.AddPersistentListener(toggle.onValueChanged, menu.OnLabelsChanged);
        Set(menu, "labelsToggle", toggle);
        Button back2 = UIButton(set.transform, "Назад", new Vector2(0.5f, 0.5f), new Vector2(0, -280), new Vector2(300, 100), 40);
        UnityEventTools.AddPersistentListener(back2.onClick, menu.ShowMain);
        Set(menu, "settingsPanel", set);

        // --- reset confirm (on top of the main panel) ---
        var confirm = Panel(c, "ConfirmPanel");
        var shade = confirm.AddComponent<Image>();
        shade.color = new Color(0, 0, 0, 0.6f); // also blocks clicks on the buttons behind it
        var box = UIImage(confirm.transform, "Box", S("UI/panel"), new Color(0.05f, 0.06f, 0.1f, 0.95f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 480));
        box.GetComponent<Image>().type = Image.Type.Sliced;
        UIText(box.transform, "Question", "Сбросить весь прогресс?\n\nОстанется открыт только первый уровень,\nрекорды удалятся. Настройки не сбросятся.", 38, TextAnchor.UpperCenter, new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(940, 200));
        Button yes = UIButton(box.transform, "Да, сбросить", new Vector2(0.5f, 0), new Vector2(-220, 90), new Vector2(380, 100), 38);
        yes.GetComponent<Image>().color = new Color(0.6f, 0.2f, 0.2f, 0.95f);
        UnityEventTools.AddPersistentListener(yes.onClick, menu.ConfirmReset);
        Button no = UIButton(box.transform, "Отмена", new Vector2(0.5f, 0), new Vector2(220, 90), new Vector2(380, 100), 38);
        UnityEventTools.AddPersistentListener(no.onClick, menu.HideConfirmReset);
        Set(menu, "confirmPanel", confirm);

        levels.SetActive(false);
        set.SetActive(false);
        confirm.SetActive(false);
        SaveScene("Menu");
    }

    private static LevelButton LevelButtonUI(Transform parent, int index, Vector2 pos, MainMenu menu)
    {
        var go = UIImage(parent, "Level_" + (index + 1), S("UI/panel"), new Color(0.25f, 0.3f, 0.45f, 0.9f), new Vector2(0.5f, 0.5f), pos, new Vector2(400, 260));
        go.GetComponent<Image>().type = Image.Type.Sliced;
        var button = go.AddComponent<Button>();
        UnityEventTools.AddIntPersistentListener(button.onClick, menu.PlayLevel, index);

        var nameText = UIText(go.transform, "Name", "Уровень " + (index + 1), 42, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, 45), new Vector2(380, 70));
        var infoText = UIText(go.transform, "Info", "", 32, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(380, 60));
        infoText.color = new Color(1f, 0.85f, 0.5f);

        var lb = go.AddComponent<LevelButton>();
        Set(lb, "levelIndex", index);
        Set(lb, "button", button);
        Set(lb, "nameText", nameText);
        Set(lb, "infoText", infoText);
        return lb;
    }

    private static Slider MakeSlider(Transform parent, Vector2 pos)
    {
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(560, 40);

        go.transform.Find("Background").GetComponent<Image>().color = new Color(0.2f, 0.22f, 0.3f);
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = new Color(1f, 0.55f, 0.2f);
        var handle = go.transform.Find("Handle Slide Area/Handle").GetComponent<RectTransform>();
        handle.sizeDelta = new Vector2(50, 0);

        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 1f;
        return slider;
    }

    private static Toggle MakeToggle(Transform parent, string label, Vector2 pos)
    {
        var go = DefaultControls.CreateToggle(new DefaultControls.Resources());
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        go.transform.localScale = Vector3.one * 2.5f; // the default toggle is very small for a phone

        go.transform.Find("Background/Checkmark").GetComponent<Image>().color = new Color(1f, 0.55f, 0.2f);
        var text = go.transform.Find("Label").GetComponent<Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 16;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow; // the default label is lower than the text

        var toggle = go.GetComponent<Toggle>();
        toggle.isOn = true;
        return toggle;
    }

    // =====================================================================  HELPERS
    private static GameObject Place(GameObject prefab, Vector2 pos, Transform parent)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        if (parent != null) go.transform.SetParent(parent, false);
        go.transform.position = pos;
        return go;
    }

    private static void Wall(Transform parent, float x, float y, float w, float h)
    {
        var go = Place(pWall, new Vector2(x, y), parent);
        var sr = go.GetComponent<SpriteRenderer>();
        sr.size = new Vector2(w, h);
        go.GetComponent<BoxCollider2D>().size = new Vector2(w, h);
        go.isStatic = true;
    }

    private static GameObject SavePrefab(GameObject go, string name)
    {
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, PrefabDir + name + ".prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    private static void SetupBody(Rigidbody2D rb, float mass)
    {
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.mass = mass;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private static GameObject Child(Transform parent, string name, Sprite sprite, Vector2 pos, float scale, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = Vector3.one * scale;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sharedMaterial = mat;
        sr.sortingOrder = order;
        return go;
    }

    private static TextMesh Label(Transform parent, Vector2 pos)
    {
        var go = new GameObject("StateLabel");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        tm.fontSize = 40;
        tm.characterSize = 0.06f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;
        tm.color = new Color(1, 1, 1, 0.8f);
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 50;
        return tm;
    }

    private static LineRenderer Line(Transform parent, Color color, float width, int points)
    {
        var go = new GameObject(points == 3 ? "SightLine" : "TelegraphLine");
        go.transform.SetParent(parent, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = points;
        lr.useWorldSpace = true;
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = color;
        lr.sharedMaterial = mat;
        lr.sortingOrder = 3;
        lr.numCapVertices = 4;
        lr.enabled = false;
        return lr;
    }

    private static Canvas NewCanvas()
    {
        var go = new GameObject("UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM
        es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
        es.AddComponent<StandaloneInputModule>();
#endif
        return canvas;
    }

    private static GameObject UIImage(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.color = color;
        return go;
    }

    private static Text UIText(Transform parent, string name, string text, int size, TextAnchor align, Vector2 anchor, Vector2 pos, Vector2 box)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = box;
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = font;
        t.fontSize = size;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0, 0, 0, 0.8f);
        shadow.effectDistance = new Vector2(3, -3);
        return t;
    }

    private static Button UIButton(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize)
    {
        var go = UIImage(parent, "Button_" + label, S("UI/panel"), new Color(0.25f, 0.3f, 0.45f, 0.9f), anchor, pos, size);
        go.GetComponent<Image>().type = Image.Type.Sliced;
        var b = go.AddComponent<Button>();
        UIText(go.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        return b;
    }

    // Sets a private [SerializeField] so it is saved with the scene / prefab.
    private static void Set(Object target, string field, object value)
    {
        var so = new SerializedObject(target);
        var p = so.FindProperty(field);
        if (p == null) { Debug.LogError("EnemyGameBuilder: no field '" + field + "' on " + target); return; }

        switch (value)
        {
            case Object[] arr:
                p.arraySize = arr.Length;
                for (int i = 0; i < arr.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = arr[i];
                break;
            case Object o: p.objectReferenceValue = o; break;
            case float f: p.floatValue = f; break;
            case int i2: p.intValue = i2; break;
            case bool b: p.boolValue = b; break;
            case string s: p.stringValue = s; break;
            case Color c: p.colorValue = c; break;
            case Vector2 v: p.vector2Value = v; break;
            case LayerMask m: p.intValue = m.value; break;
            default: Debug.LogError("EnemyGameBuilder: unsupported type for " + field); break;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tm.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            var p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = name;
                tm.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }
        return 0;
    }
}
