using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Membuka scene PCG_SpawnArena lalu langsung menekan Play.</summary>
public static class PCGAutoPlay
{
    private const string ScenePath = "Assets/Scenes/PCG_SpawnArena.unity";

    [MenuItem("Tools/GameCerdas P09/Open And Play")]
    public static void OpenAndPlay()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    [MenuItem("Tools/GameCerdas P09/Open And Play (Autopilot)")]
    public static void OpenAndPlayAutopilot()
    {
        PlayerPrefs.SetInt("P09_AutoPilot", 1);
        PlayerPrefs.Save();

        OpenAndPlay();
    }
}
