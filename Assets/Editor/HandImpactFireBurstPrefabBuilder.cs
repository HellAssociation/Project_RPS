using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class HandImpactFireBurstPrefabBuilder
{
    public const string PrefabPath = "Assets/Resources/VFX/RPS_HandImpact_FireBurst.prefab";
    const string AutoRunAssetPath = "Assets/Editor/HandImpactFireBurstPrefabBuilder.autorun";
    const int BattleSortingLayerIndex = 1;

    const string SwordCrossPath = "Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Fire/CFXR4 Sword Hit FIRE (Cross).prefab";
    const string SwordSlashPath = "Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Sword Trails/Fire/CFXR4 Sword Hit FIRE (Slash).prefab";
    const string EmberPath = "Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Fire/Variants/CFXR3 Flying Ember (Upward Erratic).prefab";
    const string SparksPath = "Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/CFXR Hit Sparks HDR.prefab";
    const string GlowPath = "Assets/Plugins/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/Impacts/Variants/CFXR Impact Glowing HDR (Orange).prefab";

    [InitializeOnLoadMethod]
    static void RebuildWhenRequested()
    {
        string fullPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), AutoRunAssetPath);
        if (!System.IO.File.Exists(fullPath))
            return;

        System.IO.File.Delete(fullPath);
        EditorApplication.delayCall += RebuildAndAssign;
    }

    [MenuItem("Tools/Project RPS/Rebuild Hand Impact Fire Burst Prefab")]
    public static void RebuildAndAssign()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder("Assets/Resources/VFX");

        GameObject root = new("RPS_HandImpact_FireBurst");
        root.transform.localScale = Vector3.one;

        AddLayer(root.transform, GlowPath, "Impact Flash", Vector3.zero, Vector3.zero, new Vector3(1.45f, 1.45f, 1.45f), ParticleProfile.Flash);
        AddLayer(root.transform, SwordCrossPath, "Fire Cross", Vector3.zero, Vector3.zero, new Vector3(1.55f, 1.55f, 1.55f), ParticleProfile.Slash);
        AddLayer(root.transform, SwordSlashPath, "Fire Slash Accent", Vector3.zero, new Vector3(0f, 0f, 28f), new Vector3(1.35f, 1.35f, 1.35f), ParticleProfile.Slash);
        AddLayer(root.transform, SparksPath, "Radial Sparks", new Vector3(0.08f, 0f, 0f), Vector3.zero, new Vector3(1.4f, 1.4f, 1.4f), ParticleProfile.Sparks);
        AddLayer(root.transform, EmberPath, "Directional Ember Burst", new Vector3(0.12f, 0f, 0f), Vector3.zero, new Vector3(1.45f, 1.45f, 1.45f), ParticleProfile.DirectionalEmber);
        AddLayer(root.transform, EmberPath, "Wide Ember Scatter", Vector3.zero, Vector3.zero, new Vector3(1.85f, 1.85f, 1.85f), ParticleProfile.Ember);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        if (prefab == null)
        {
            Debug.LogError($"[HandImpactFireBurst] Failed to create prefab at {PrefabPath}");
            return;
        }

        AssignToSceneSpawners(prefab);
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveOpenScenes();

        Debug.Log($"[HandImpactFireBurst] Built and assigned {PrefabPath}");
    }

    static void AddLayer(Transform parent, string sourcePath, string name, Vector3 localPosition, Vector3 localEuler, Vector3 localScale, ParticleProfile profile)
    {
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        if (source == null)
        {
            Debug.LogError($"[HandImpactFireBurst] Missing source prefab: {sourcePath}");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.name = name;
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        instance.transform.SetParent(parent, false);
        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = Quaternion.Euler(localEuler);
        instance.transform.localScale = localScale;

        ConfigureParticles(instance, profile);
        ConfigureLights(instance, profile);
    }

    static void ConfigureParticles(GameObject root, ParticleProfile profile)
    {
        ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem particleSystem = systems[i];
            ParticleSystem.MainModule main = particleSystem.main;
            ParticleSystem.EmissionModule emission = particleSystem.emission;

            main.loop = false;
            main.playOnAwake = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.duration = profile.Duration;
            main.startLifetime = new ParticleSystem.MinMaxCurve(profile.LifetimeMin, profile.LifetimeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(profile.SpeedMin, profile.SpeedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(profile.SizeMin, profile.SizeMax);
            main.maxParticles = profile.MaxParticles;

            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, profile.BurstMin, profile.BurstMax)
            });

            if (profile.UseLocalDirectionalVelocity)
            {
                ParticleSystem.VelocityOverLifetimeModule velocity = particleSystem.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.Local;
                velocity.x = new ParticleSystem.MinMaxCurve(profile.VelocityXMin, profile.VelocityXMax);
                velocity.y = new ParticleSystem.MinMaxCurve(profile.VelocityYMin, profile.VelocityYMax);
                velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            }

            ParticleSystemRenderer renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                SetBattleSortingLayer(renderer);
                renderer.sortingOrder = 180 + profile.SortingOffset + i;
                renderer.maxParticleSize = Mathf.Max(renderer.maxParticleSize, 0.35f);
            }
        }
    }

    static void SetBattleSortingLayer(ParticleSystemRenderer renderer)
    {
        int battleSortingLayerID = SortingLayer.NameToID("Battle");
        renderer.sortingLayerName = "Battle";
        renderer.sortingLayerID = battleSortingLayerID;

        SerializedObject serialized = new(renderer);
        SerializedProperty sortingLayerID = serialized.FindProperty("m_SortingLayerID");
        SerializedProperty sortingLayer = serialized.FindProperty("m_SortingLayer");

        if (sortingLayerID != null)
            sortingLayerID.intValue = battleSortingLayerID;

        if (sortingLayer != null)
            sortingLayer.intValue = BattleSortingLayerIndex;

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void ConfigureLights(GameObject root, ParticleProfile profile)
    {
        Light[] lights = root.GetComponentsInChildren<Light>(true);
        foreach (Light light in lights)
        {
            light.intensity = profile.LightIntensity;
            light.range = profile.LightRange;
            light.color = profile.LightColor;
        }
    }

    static void AssignToSceneSpawners(GameObject prefab)
    {
        HandImpactEffectSpawner[] spawners = Object.FindObjectsByType<HandImpactEffectSpawner>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (HandImpactEffectSpawner spawner in spawners)
        {
            SerializedObject serialized = new(spawner);
            SerializedProperty property = serialized.FindProperty("impactEffectPrefab");
            if (property == null)
                continue;

            property.objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(spawner);
        }

        if (spawners.Length > 0)
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
        string folder = System.IO.Path.GetFileName(path);

        if (!string.IsNullOrEmpty(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, folder);
    }

    readonly struct ParticleProfile
    {
        public readonly float Duration;
        public readonly float LifetimeMin;
        public readonly float LifetimeMax;
        public readonly float SpeedMin;
        public readonly float SpeedMax;
        public readonly float SizeMin;
        public readonly float SizeMax;
        public readonly short BurstMin;
        public readonly short BurstMax;
        public readonly int MaxParticles;
        public readonly int SortingOffset;
        public readonly float LightIntensity;
        public readonly float LightRange;
        public readonly Color LightColor;
        public readonly bool UseLocalDirectionalVelocity;
        public readonly float VelocityXMin;
        public readonly float VelocityXMax;
        public readonly float VelocityYMin;
        public readonly float VelocityYMax;

        ParticleProfile(
            float duration,
            float lifetimeMin,
            float lifetimeMax,
            float speedMin,
            float speedMax,
            float sizeMin,
            float sizeMax,
            short burstMin,
            short burstMax,
            int maxParticles,
            int sortingOffset,
            float lightIntensity,
            float lightRange,
            Color lightColor,
            bool useLocalDirectionalVelocity = false,
            float velocityXMin = 0f,
            float velocityXMax = 0f,
            float velocityYMin = 0f,
            float velocityYMax = 0f)
        {
            Duration = duration;
            LifetimeMin = lifetimeMin;
            LifetimeMax = lifetimeMax;
            SpeedMin = speedMin;
            SpeedMax = speedMax;
            SizeMin = sizeMin;
            SizeMax = sizeMax;
            BurstMin = burstMin;
            BurstMax = burstMax;
            MaxParticles = maxParticles;
            SortingOffset = sortingOffset;
            LightIntensity = lightIntensity;
            LightRange = lightRange;
            LightColor = lightColor;
            UseLocalDirectionalVelocity = useLocalDirectionalVelocity;
            VelocityXMin = velocityXMin;
            VelocityXMax = velocityXMax;
            VelocityYMin = velocityYMin;
            VelocityYMax = velocityYMax;
        }

        public static ParticleProfile Flash => new(0.45f, 0.18f, 0.38f, 0.05f, 0.3f, 1.6f, 2.7f, 5, 8, 40, 40, 2.8f, 4.2f, new Color(1f, 0.58f, 0.18f));
        public static ParticleProfile Slash => new(0.65f, 0.25f, 0.55f, 0.1f, 0.55f, 1.2f, 2.4f, 8, 14, 70, 30, 2.1f, 3.6f, new Color(1f, 0.35f, 0.08f));
        public static ParticleProfile Sparks => new(0.7f, 0.35f, 0.85f, 2.4f, 5.4f, 0.08f, 0.22f, 32, 54, 90, 70, 1.7f, 3.2f, new Color(1f, 0.72f, 0.25f), true, 1.2f, 2.8f, -0.8f, 0.8f);
        public static ParticleProfile DirectionalEmber => new(1.0f, 0.45f, 0.95f, 2.8f, 5.8f, 0.07f, 0.2f, 52, 86, 140, 60, 1.4f, 2.8f, new Color(1f, 0.34f, 0.05f), true, 2.4f, 5.2f, -1.2f, 1.2f);
        public static ParticleProfile Ember => new(1.15f, 0.75f, 1.15f, 0.8f, 2.4f, 0.08f, 0.18f, 42, 70, 110, 20, 1.1f, 2.5f, new Color(1f, 0.3f, 0.05f));
    }
}
