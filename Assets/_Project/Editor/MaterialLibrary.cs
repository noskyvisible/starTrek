using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace StarTrek.EditorTools
{
    /// <summary>
    /// Project-owned URP Lit materials that stand in for the materials glTFast imports from Blender.
    /// glTFast drops Blender's emission strength, so HDR glow is set here by material name.
    /// Existing library materials are left alone, so they can be tuned by hand in Unity.
    /// </summary>
    public static class MaterialLibrary
    {
        public const string Folder = ProjectSetup.Root + "/Materials";

        // HDR emission multipliers (Blender emission strength), keyed by material name.
        static readonly Dictionary<string, float> EmissionStrength = new Dictionary<string, float>
        {
            { "M_Fed_Light_Warm", 6f },
            { "M_Fed_Screen", 0.6f },
            { "M_Fed_Button_Red", 3f },
            { "M_Fed_Panel_Amber", 3f },
            { "M_Fed_Display_Blue", 1.1f },
            { "M_Fed_Display_Green", 1.1f },
            { "M_Fed_Display_Amber", 1.1f },
            { "M_Fed_Display_Red", 1.1f },
            { "M_Kli_Torpedo", 9f },
            { "M_Kli_Impulse", 6f },
            { "M_Kli_Window", 4f },
            { "M_Kli_WarpGlow", 4f },
            { "M_Fre_Strobe", 12f },
        };

        static readonly int BaseColorFactor = Shader.PropertyToID("baseColorFactor");
        static readonly int MetallicFactor = Shader.PropertyToID("metallicFactor");
        static readonly int RoughnessFactor = Shader.PropertyToID("roughnessFactor");
        static readonly int EmissiveFactor = Shader.PropertyToID("emissiveFactor");

        /// <summary>Swap every imported material under <paramref name="root"/> for its library twin.</summary>
        public static void ApplyTo(GameObject root)
        {
            var cache = new Dictionary<Material, Material>();
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Material[] mats = renderer.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null)
                        continue;
                    if (!cache.TryGetValue(mats[i], out Material lib))
                        cache[mats[i]] = lib = GetOrCreate(mats[i]);
                    mats[i] = lib;
                }
                renderer.sharedMaterials = mats;
            }
            AssetDatabase.SaveAssets();
        }

        static Material GetOrCreate(Material source)
        {
            string path = $"{Folder}/{source.name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            ProjectSetup.EnsureFolder(Folder);
            var mat = new Material(GraphicsSettings.currentRenderPipeline.defaultMaterial.shader) { name = source.name };

            Color baseColor = source.HasProperty(BaseColorFactor) ? source.GetColor(BaseColorFactor) : Color.white;
            float metallic = source.HasProperty(MetallicFactor) ? source.GetFloat(MetallicFactor) : 0f;
            float roughness = source.HasProperty(RoughnessFactor) ? source.GetFloat(RoughnessFactor) : 0.5f;
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", 1f - roughness);

            Color emissive = source.HasProperty(EmissiveFactor) ? source.GetColor(EmissiveFactor) : Color.black;
            if (emissive.maxColorComponent > 0.001f)
            {
                float strength = EmissionStrength.TryGetValue(source.name, out float s) ? s : 1f;
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissive * strength);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }
    }
}
