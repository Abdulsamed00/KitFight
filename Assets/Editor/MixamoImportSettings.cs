using UnityEditor;

namespace KitFight.EditorTools
{
    /// <summary>
    /// Mixamo FBX files in Assets/Mixamo are imported as Humanoid animations (retargeted onto our rig).
    /// Root height and rotation are baked into the pose so falls/jumps stay visible with root motion off,
    /// while horizontal root motion is dropped (movement is done in code).
    /// </summary>
    public class MixamoImportSettings : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            if (!assetPath.StartsWith("Assets/Mixamo/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importAnimation = true;
            mi.materialImportMode = ModelImporterMaterialImportMode.None;
        }

        void OnPreprocessAnimation()
        {
            if (!assetPath.StartsWith("Assets/Mixamo/")) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                c.lockRootRotation = true;
                c.keepOriginalOrientation = true;
                c.lockRootHeightY = true;
                c.keepOriginalPositionY = true;
                c.lockRootPositionXZ = false;
                c.loopTime = false;
            }
            mi.clipAnimations = clips;
        }
    }
}
