using UnityEditor;
using UnityEngine;

namespace Aren.EditorTools
{
    /// <summary>
    /// Configura automaticamente os FBX de animação/personagem do Aren:
    /// - Quaternius UAL2 (CC0): Humanoid, clipes in-place (raiz embutida na pose),
    ///   "_Loop" em loop.
    /// Assim a importação é reproduzível (não depende de cliques no Inspector).
    /// </summary>
    public class ArenModelPostprocessor : AssetPostprocessor
    {
        const string UAL2Folder = "Assets/Aren/ThirdParty/Quaternius_UAL2/";

        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith(UAL2Folder)) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith(UAL2Folder)) return;
            var importer = (ModelImporter)assetImporter;
            // Na primeira importação defaultClipAnimations está vazio; o Unity chama de novo
            // depois que o Reimport abaixo roda (ver ArenImportMenu).
            var clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                string n = c.takeName;
                int bar = n.LastIndexOf('|');
                c.name = bar >= 0 ? n.Substring(bar + 1) : n;
                bool loop = c.name.EndsWith("_Loop");
                c.loopTime = loop;
                c.loopPose = loop;
                // In-place: raiz embutida na pose (quem move o Aren é o controller/curvas).
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = true;
                // A raiz "Original" dos clipes da UAL2 aponta para trás em relação aos avatares
                // do projeto: sem o offset o tronco ficava de costas para o transform (Ecos
                // andando/atacando de costas, golpes do Aren começando virados). Medido com
                // Tools/cli/cs/anim_facing_rt.cs: idle passa de -163° para +17° (igual ao DPS).
                c.rotationOffset = 180f;
                clips[i] = c;
            }
            importer.clipAnimations = clips;
        }
    }
}
