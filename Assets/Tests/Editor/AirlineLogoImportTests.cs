using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class AirlineLogoImportTests
{
    [TestCase("AA")][TestCase("DL")][TestCase("UA")][TestCase("B6")]
    public void LocalLogoRetainsSourceDimensionsAndUncompressedQuality(string code)
    {
        string path="Assets/Resources/AirlineLogos/"+code+".png";
        byte[] png=File.ReadAllBytes(path);
        int Width(int offset)=>(png[offset]<<24)|(png[offset+1]<<16)|(png[offset+2]<<8)|png[offset+3];
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        Assert.IsNotNull(texture);Assert.AreEqual(Width(16),texture.width);Assert.AreEqual(Width(20),texture.height);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        Assert.AreEqual(TextureImporterNPOTScale.None,importer.npotScale);
        Assert.AreEqual(TextureImporterCompression.Uncompressed,importer.textureCompression);
    }
}
