using Game;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ManagingScene : MonoBehaviour
{
#if UNITY_EDITOR
    [Header("Dev launch (editor only)")]
    [Tooltip("Skip the menu/lobby and host straight into a minigame for testing. " +
             "Hosts locally (no Relay/Sessions) using the real NGO host code path.")]
    [SerializeField] private bool _devLaunchMiniGame;
    [SerializeField] private E_MiniGame _devMiniGame = E_MiniGame.TagPlatformer;
#endif

    void Start()
    {
        DontDestroyOnLoad(this);

#if UNITY_EDITOR
        if (_devLaunchMiniGame)
        {
            DevLaunchMiniGame();
            return;
        }
#endif

        SceneManager.LoadSceneAsync(GameDataRegistry.MainMenuSceneName);
    }

#if UNITY_EDITOR
    // Solo local host straight into a minigame, bypassing the Unity Services lobby.
    private void DevLaunchMiniGame()
    {
        NetworkManager.Singleton.StartHost();
        NetworkManager.Singleton.SceneManager.LoadScene(
            GameDataRegistry.GetMiniGameSceneName(_devMiniGame), LoadSceneMode.Single);
    }
#endif
}
