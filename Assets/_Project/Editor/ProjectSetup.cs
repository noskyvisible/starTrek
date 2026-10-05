using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using ShadowResolution = UnityEngine.Rendering.Universal.ShadowResolution;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// One-click project setup: folder layout (GAME_PROMPT §10), URP quality tiers (Low / High / Ultra)
    /// and the global HDR post-processing profile. Safe to run again; it updates what already exists.
    /// </summary>
    public static class ProjectSetup
    {
        public const string Root = "Assets/_Project";
        public const string RenderingDir = Root + "/Settings/Rendering";
        public const string VolumeProfilePath = RenderingDir + "/VP_Global.asset";

        static readonly string[] Folders =
        {
            "Art/Ships/Federation", "Art/Ships/Klingon", "Art/Interiors", "Art/Characters", "Art/VFX",
            "Audio/Music", "Audio/SFX", "Audio/VO",
            "Scenes/Bootstrap", "Scenes/KobayashiMaru", "Scenes/Ship_Decks", "Scenes/Test",
            "Scripts/Ship", "Scripts/Crew", "Scripts/Combat", "Scripts/Ground", "Scripts/Boarding",
            "Scripts/Turbolift", "Scripts/UI", "Scripts/Audio", "Scripts/Core", "Scripts/Player", "Scripts/Interaction",
            "Prefabs", "Materials", "Settings/Rendering", "Input", "Editor"
        };

        struct Tier
        {
            public string Name;
            public bool Ssao;
            public MsaaQuality Msaa;
            public float RenderScale;
            public ShadowResolution ShadowResolution;
            public int Cascades;
            public float ShadowDistance;
            public bool SoftShadows;
            public bool AdditionalLightShadows;
            public bool DepthTexture;
            public bool OpaqueTexture;
            public ColorGradingMode Grading;
            public int TextureMipLimit;
            public int Anisotropic;
            public float LodBias;
        }

        // Low must hold 30 fps at 720p on the Intel UHD 620 dev laptop (GAME_PROMPT §11).
        static readonly Tier[] Tiers =
        {
            new Tier
            {
                Name = "Low", Ssao = false, Msaa = MsaaQuality.Disabled, RenderScale = 0.75f,
                ShadowResolution = ShadowResolution._1024, Cascades = 1, ShadowDistance = 25f, SoftShadows = false,
                AdditionalLightShadows = false, DepthTexture = false, OpaqueTexture = false,
                Grading = ColorGradingMode.LowDynamicRange, TextureMipLimit = 1, Anisotropic = 0, LodBias = 0.7f
            },
            new Tier
            {
                Name = "High", Ssao = true, Msaa = MsaaQuality._2x, RenderScale = 1f,
                ShadowResolution = ShadowResolution._2048, Cascades = 2, ShadowDistance = 60f, SoftShadows = true,
                AdditionalLightShadows = true, DepthTexture = true, OpaqueTexture = true,
                Grading = ColorGradingMode.HighDynamicRange, TextureMipLimit = 0, Anisotropic = 1, LodBias = 1.5f
            },
            new Tier
            {
                Name = "Ultra", Ssao = true, Msaa = MsaaQuality._4x, RenderScale = 1f,
                ShadowResolution = ShadowResolution._4096, Cascades = 4, ShadowDistance = 120f, SoftShadows = true,
                AdditionalLightShadows = true, DepthTexture = true, OpaqueTexture = true,
                Grading = ColorGradingMode.HighDynamicRange, TextureMipLimit = 0, Anisotropic = 2, LodBias = 2f
            },
        };

        const int EditorTier = 0;   // the Editor previews on Low so the laptop stays responsive
        const int BuildTier = 1;    // standalone builds default to High

        [MenuItem("StarTrek/Setup/Run All")]
        public static void RunAll()
        {
            CreateFolders();
            CreateQualityTiers();
            CreateVolumeProfile();
            Debug.Log("[StarTrek] Project setup complete.");
        }

        [MenuItem("StarTrek/Setup/Create Folders")]
        public static void CreateFolders()
        {
            foreach (string f in Folders)
            {
                string path = Root + "/" + f;
                EnsureFolder(path);
                // Git drops empty folders; a hidden .gitkeep keeps the layout (Unity ignores dot-files).
                string keep = Path.Combine(path, ".gitkeep");
                if (Directory.GetFileSystemEntries(path).Length == 0)
                    File.WriteAllText(keep, string.Empty);
            }
            AssetDatabase.Refresh();
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        [MenuItem("StarTrek/Setup/Create Quality Tiers")]
        public static void CreateQualityTiers()
        {
            EnsureFolder(RenderingDir);
            var full = GetOrCreateRenderer(RenderingDir + "/URP_Renderer_Full.asset", ssao: true);
            var lite = GetOrCreateRenderer(RenderingDir + "/URP_Renderer_Lite.asset", ssao: false);

            var assets = new UniversalRenderPipelineAsset[Tiers.Length];
            for (int i = 0; i < Tiers.Length; i++)
            {
                Tier t = Tiers[i];
                assets[i] = GetOrCreatePipeline(RenderingDir + $"/URP_{t.Name}.asset", t.Ssao ? full : lite);
                Configure(assets[i], t);
            }

            ConfigureQualitySettings(assets);
            GraphicsSettings.defaultRenderPipeline = assets[BuildTier];
            AssetDatabase.SaveAssets();
        }

        static UniversalRendererData GetOrCreateRenderer(string path, bool ssao)
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (data == null)
            {
                // URP's own creation path fills in post-process data and shader resources.
                MethodInfo create = typeof(UniversalRenderPipelineAsset).GetMethod(
                    "CreateRendererAsset", BindingFlags.NonPublic | BindingFlags.Static);
                Type rendererType = create.GetParameters()[1].ParameterType;
                object universal = Enum.Parse(rendererType, "UniversalRenderer");
                data = (UniversalRendererData)create.Invoke(null, new[] { path, universal, false, "Renderer" });
            }

            data.renderingMode = RenderingMode.ForwardPlus;   // no per-object light limit for lit interiors
            bool hasSsao = data.rendererFeatures.Exists(f => f is ScreenSpaceAmbientOcclusion);
            if (ssao && !hasSsao)
            {
                var feature = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                feature.name = "SSAO";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                typeof(ScriptableRendererData)
                    .GetMethod("ValidateRendererFeatures", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(data, null);
            }
            EditorUtility.SetDirty(data);
            return data;
        }

        static UniversalRenderPipelineAsset GetOrCreatePipeline(string path, ScriptableRendererData renderer)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (asset == null)
            {
                asset = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(asset, path);
            }
            var so = new SerializedObject(asset);
            var list = so.FindProperty("m_RendererDataList");
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            so.FindProperty("m_DefaultRendererIndex").intValue = 0;
            so.ApplyModifiedPropertiesWithoutUndo();
            return asset;
        }

        static void Configure(UniversalRenderPipelineAsset asset, Tier t)
        {
            var so = new SerializedObject(asset);
            so.FindProperty("m_SupportsHDR").boolValue = true;
            so.FindProperty("m_HDRColorBufferPrecision").intValue = (int)HDRColorBufferPrecision._32Bits;
            so.FindProperty("m_MSAA").intValue = (int)t.Msaa;
            so.FindProperty("m_RenderScale").floatValue = t.RenderScale;
            so.FindProperty("m_RequireDepthTexture").boolValue = t.DepthTexture;
            so.FindProperty("m_RequireOpaqueTexture").boolValue = t.OpaqueTexture;
            so.FindProperty("m_MainLightShadowsSupported").boolValue = true;
            so.FindProperty("m_MainLightShadowmapResolution").intValue = (int)t.ShadowResolution;
            so.FindProperty("m_AdditionalLightShadowsSupported").boolValue = t.AdditionalLightShadows;
            so.FindProperty("m_ShadowDistance").floatValue = t.ShadowDistance;
            so.FindProperty("m_ShadowCascadeCount").intValue = t.Cascades;
            so.FindProperty("m_SoftShadowsSupported").boolValue = t.SoftShadows;
            so.FindProperty("m_ColorGradingMode").intValue = (int)t.Grading;
            so.FindProperty("m_UseSRPBatcher").boolValue = true;
            so.FindProperty("m_SupportDataDrivenLensFlare").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
        }

        static void ConfigureQualitySettings(UniversalRenderPipelineAsset[] assets)
        {
            var qualityAsset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0];
            var so = new SerializedObject(qualityAsset);
            var levels = so.FindProperty("m_QualitySettings");
            levels.arraySize = Tiers.Length;
            for (int i = 0; i < Tiers.Length; i++)
            {
                Tier t = Tiers[i];
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("name").stringValue = t.Name;
                level.FindPropertyRelative("customRenderPipeline").objectReferenceValue = assets[i];
                level.FindPropertyRelative("vSyncCount").intValue = 1;
                level.FindPropertyRelative("antiAliasing").intValue = 0;   // MSAA is set on the URP asset
                level.FindPropertyRelative("anisotropicTextures").intValue = t.Anisotropic;
                level.FindPropertyRelative("lodBias").floatValue = t.LodBias;
                level.FindPropertyRelative("globalTextureMipmapLimit").intValue = t.TextureMipLimit;
                level.FindPropertyRelative("realtimeReflectionProbes").boolValue = t.Name != "Low";
                level.FindPropertyRelative("softParticles").boolValue = t.DepthTexture;
            }

            so.FindProperty("m_CurrentQuality").intValue = EditorTier;
            var perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            for (int i = 0; i < perPlatform.arraySize; i++)
                perPlatform.GetArrayElementAtIndex(i).FindPropertyRelative("second").intValue = BuildTier;
            so.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(EditorTier, true);
        }

        [MenuItem("StarTrek/Setup/Create Volume Profile")]
        public static void CreateVolumeProfile()
        {
            EnsureFolder(RenderingDir);
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            var bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(false);

            var tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var colour = GetOrAdd<ColorAdjustments>(profile);
            colour.postExposure.Override(0.2f);
            colour.contrast.Override(8f);

            var vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.5f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T existing))
                return existing;
            T component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }
    }
}
