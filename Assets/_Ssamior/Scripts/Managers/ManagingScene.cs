using Game;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ManagingScene : MonoBehaviour
{
    void Start()
    {
        DontDestroyOnLoad(this);
        SceneManager.LoadSceneAsync(GameDataRegistry.MainMenuSceneName);
    }
}
