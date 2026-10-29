using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Menyiapkan URP (render pipeline asset, linear color space) dan TextMeshPro essentials.</summary>
public static class PCGRenderSetup
{
    private const string RpAssetPath = "Assets/Settings/PC_RPAsset.asset";

    [MenuItem("Tools/GameCerdas P09/Setup Render Pipeline")]
    public static void Setup()
    {
        UniversalRenderPipelineAsset asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(RpAssetPath);

        if (asset == null)
        {
            Debug.LogError("[Setup] " + RpAssetPath + " tidak ditemukan atau bukan URP asset.");
            return;
        }

        asset.msaaSampleCount = 4;
        asset.supportsHDR = true;
        asset.shadowDistance = 45f;
        asset.supportsCameraOpaqueTexture = false;
        asset.supportsCameraDepthTexture = false;
        EditorUtility.SetDirty(asset);

        GraphicsSettings.defaultRenderPipeline = asset;
        PlayerSettings.colorSpace = ColorSpace.Linear;

        AssetDatabase.SaveAssets();
        Debug.Log("[Setup] URP aktif: " + asset.name);
    }

    /// <summary>Untuk batch mode: impor TMP lalu keluar setelah import benar-benar selesai.</summary>
    public static void ImportTmpEssentialsAndExit()
    {
        AssetDatabase.importPackageCompleted += name =>
        {
            Debug.Log("[Setup] TMP Essentials selesai: " + name);
            AssetDatabase.SaveAssets();
            EditorApplication.Exit(0);
        };

        AssetDatabase.importPackageFailed += (name, error) =>
        {
            Debug.LogError("[Setup] Impor TMP gagal: " + error);
            EditorApplication.Exit(1);
        };

        AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false);
    }

    [MenuItem("Tools/GameCerdas P09/Import TMP Essentials")]
    public static void ImportTmpEssentials()
    {
        AssetDatabase.ImportPackage("Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage", false);
        AssetDatabase.Refresh();
        Debug.Log("[Setup] TMP Essentials diimpor.");
    }
}
