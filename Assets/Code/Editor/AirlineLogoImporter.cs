using UnityEditor;

// UI wordmarks must retain their source aspect ratio; default NPOT resampling distorts them.
public sealed class AirlineLogoImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Resources/AirlineLogos/") || !assetPath.EndsWith(".png"))return;
        var importer=(TextureImporter)assetImporter;
        importer.npotScale=TextureImporterNPOTScale.None;importer.mipmapEnabled=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency=true;importer.wrapMode=UnityEngine.TextureWrapMode.Clamp;
        importer.filterMode=UnityEngine.FilterMode.Trilinear;importer.maxTextureSize=2048;
    }
}
