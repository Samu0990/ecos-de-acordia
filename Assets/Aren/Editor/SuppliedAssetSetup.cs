using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>Importacao reproduzivel dos arquivos entregues pelo autor nesta etapa.</summary>
    public static class SuppliedAssetSetup
    {
        const string FluteDir = "Assets/Aren/Resources/Character/MagicFlute/";
        const string FluteModel = FluteDir + "MagicFlute.fbx";
        const string FluteMaterial = FluteDir + "MagicFlute.mat";
        const string Music = "Assets/Aren/Resources/Audio/Music/BardOfBrokenBells.mp3";

        [MenuItem("Aren/Setup/Assets fornecidos - flauta e musica")]
        public static string All()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var log = new System.Text.StringBuilder();
            log.Append(ConfigureTextures());
            log.Append(ConfigureFlute());
            log.Append(ConfigureMusic());
            AssetDatabase.SaveAssets();
            return log.ToString();
        }

        static string ConfigureTextures()
        {
            ConfigureTexture("Textures/MagicFlute_BaseColor.png", false, true);
            ConfigureTexture("Textures/MagicFlute_Normal.png", true, false);
            ConfigureTexture("Textures/MagicFlute_MetallicSmoothness.png", false, false);
            return "MagicFlute: texturas 1024 comprimidas\n";
        }

        static void ConfigureTexture(string relative, bool normal, bool srgb)
        {
            var importer = AssetImporter.GetAtPath(FluteDir + relative) as TextureImporter;
            if (importer == null) return;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = srgb && !normal;
            importer.maxTextureSize = 1024;
            importer.mipmapEnabled = true;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }

        static string ConfigureFlute()
        {
            var importer = AssetImporter.GetAtPath(FluteModel) as ModelImporter;
            if (importer == null) return "ERRO: MagicFlute.fbx nao importado\n";
            importer.importAnimation = false;
            importer.importBlendShapes = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.SaveAndReimport();

            var material = AssetDatabase.LoadAssetAtPath<Material>(FluteMaterial);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, FluteMaterial);
            }
            material.shader = Shader.Find("Standard");
            material.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(FluteDir + "Textures/MagicFlute_BaseColor.png"));
            material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FluteDir + "Textures/MagicFlute_Normal.png"));
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(FluteDir + "Textures/MagicFlute_MetallicSmoothness.png"));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_GlossMapScale", 0.88f);
            material.EnableKeyword("_METALLICGLOSSMAP");
            material.SetColor("_Color", Color.white);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);

            // O nome do material dentro de modelos gerados pode variar. Remapeia todos os
            // slots encontrados no prefab importado para o PBR local e verificavel.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(FluteModel);
            var names = new HashSet<string>();
            if (model != null)
                foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
                    foreach (var shared in renderer.sharedMaterials)
                        if (shared != null) names.Add(shared.name);
            foreach (string name in names)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), name), material);
            importer.SaveAndReimport();

            model = AssetDatabase.LoadAssetAtPath<GameObject>(FluteModel);
            int triangles = 0;
            if (model != null)
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                    if (filter.sharedMesh != null) triangles += filter.sharedMesh.triangles.Length / 3;
            return "MagicFlute: " + triangles + " triangulos, material PBR remapeado\n";
        }

        static string ConfigureMusic()
        {
            var importer = AssetImporter.GetAtPath(Music) as AudioImporter;
            if (importer == null) return "ERRO: BardOfBrokenBells.mp3 nao importado\n";
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.72f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.forceToMono = false;
            importer.loadInBackground = true;
            importer.SaveAndReimport();
            return "Bard of Broken Bells: streaming Vorbis, carregamento em segundo plano\n";
        }
    }
}
