using UnityEditor;

/// <summary>
/// Keeps the cursor art importable as a cursor. GameCursor reads the pixels of every image in
/// Assets/Resources/Cursor/ to scale them up, which needs them readable, uncompressed and without
/// mipmaps. Enforced here so an edited or replaced PNG can never silently lose those settings
/// (the symptom would be the plain system cursor and one warning in the console).
/// </summary>
public class CursorTextureImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        if (!assetPath.Replace('\\', '/').StartsWith("Assets/Resources/Cursor/")) return;

        TextureImporter ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Default;
        ti.isReadable = true;
        ti.mipmapEnabled = false;
        ti.filterMode = UnityEngine.FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.alphaIsTransparency = true;
        ti.npotScale = TextureImporterNPOTScale.None;
        ti.wrapMode = UnityEngine.TextureWrapMode.Clamp;
    }
}
