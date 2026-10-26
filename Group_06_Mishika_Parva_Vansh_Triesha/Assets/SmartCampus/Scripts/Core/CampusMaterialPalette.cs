using System.Collections.Generic;
using UnityEngine;

namespace SmartCampus.Core
{
    public class CampusMaterialPalette
    {
        private static readonly Dictionary<string, Material> cachedMaterials = new Dictionary<string, Material>();

        public static Material GetMaterial(string name, Color baseColor, float smoothness = 0.2f, float metallic = 0.0f, Color? emission = null)
        {
            if (cachedMaterials.TryGetValue(name, out var existing) && existing != null)
            {
                return existing;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                         ?? Shader.Find("Standard")
                         ?? Shader.Find("Diffuse");

            var mat = new Material(shader)
            {
                name = name
            };

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);

            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);

            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);

            if (emission.HasValue && emission.Value != Color.black)
            {
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission.Value);
                }
            }

            cachedMaterials[name] = mat;
            return mat;
        }

        public static Material PlazaStone => GetMaterial("M_PlazaStone", new Color(0.78f, 0.77f, 0.74f), 0.15f, 0.02f);
        public static Material LawnGrass => GetMaterial("M_LawnGrass", new Color(0.24f, 0.48f, 0.22f), 0.05f, 0.0f);
        public static Material FloorTile => GetMaterial("M_FloorTile", new Color(0.92f, 0.91f, 0.89f), 0.65f, 0.05f);
        public static Material InteriorWall => GetMaterial("M_InteriorWall", new Color(0.94f, 0.94f, 0.92f), 0.15f, 0.0f);
        public static Material WallAccentTeal => GetMaterial("M_WallAccentTeal", new Color(0.12f, 0.32f, 0.42f), 0.25f, 0.05f);
        public static Material OakWood => GetMaterial("M_OakWood", new Color(0.55f, 0.38f, 0.24f), 0.35f, 0.0f);
        public static Material DarkMetal => GetMaterial("M_DarkMetal", new Color(0.2f, 0.22f, 0.24f), 0.5f, 0.75f);
        public static Material DoorLeafWood => GetMaterial("M_DoorLeafWood", new Color(0.48f, 0.32f, 0.18f), 0.4f, 0.0f);
        public static Material Whiteboard => GetMaterial("M_Whiteboard", new Color(0.98f, 0.98f, 0.98f), 0.85f, 0.05f);
        public static Material ComputerScreen => GetMaterial("M_ComputerScreen", new Color(0.08f, 0.12f, 0.2f), 0.7f, 0.1f, new Color(0.1f, 0.25f, 0.45f));
        public static Material BookBlock => GetMaterial("M_BookBlock", new Color(0.65f, 0.25f, 0.2f), 0.2f, 0.0f);
        public static Material CeilingTile => GetMaterial("M_CeilingTile", new Color(0.96f, 0.96f, 0.96f), 0.1f, 0.0f);
        public static Material SignBoard => GetMaterial("M_SignBoard", new Color(0.08f, 0.25f, 0.45f), 0.5f, 0.2f, new Color(0.05f, 0.3f, 0.6f));
        public static Material ArchConcrete => GetMaterial("M_ArchConcrete", new Color(0.85f, 0.84f, 0.82f), 0.2f, 0.05f);
    }
}
