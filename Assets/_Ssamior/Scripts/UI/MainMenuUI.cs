using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _createLobbyButton;
        [SerializeField] private Button _joinLobbyButton;
        [SerializeField] private Button _quitButton;

        [Header("Lobby")]
        [SerializeField] private TMP_InputField _joinCodeInput;

        private bool _busy;

        private void Awake()
        {
            _settingsButton.onClick.AddListener(OnSettingsClicked);
            _createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
            _joinLobbyButton.onClick.AddListener(OnJoinLobbyClicked);
            _quitButton.onClick.AddListener(OnQuitClicked);
        }

        private void OnDestroy()
        {
            _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            _createLobbyButton.onClick.RemoveListener(OnCreateLobbyClicked);
            _joinLobbyButton.onClick.RemoveListener(OnJoinLobbyClicked);
            _quitButton.onClick.RemoveListener(OnQuitClicked);
        }

        private void OnSettingsClicked()
        {
            // TODO: open settings panel
        }

        private async void OnCreateLobbyClicked()
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                string code = await SessionManager.Instance.CreateLobbyAsync();
                Debug.Log($"Lobby created. Join code: {code}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to create lobby.\n{e}");
                SetBusy(false);
            }
        }

        private async void OnJoinLobbyClicked()
        {
            if (_busy) return;

            string code = _joinCodeInput.text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                Debug.LogWarning("No join code entered.");
                return;
            }

            SetBusy(true);
            try
            {
                await SessionManager.Instance.JoinLobbyByCodeAsync(code);
                Debug.Log("Joined lobby.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to join lobby with code '{code}'.\n{e}");
                SetBusy(false);
            }
        }

        /// <summary>
        /// Lock the lobby buttons while a create/join request is in flight.
        /// Not unlocked on success: a successful flow leaves this scene.
        /// </summary>
        private void SetBusy(bool busy)
        {
            _busy = busy;
            _createLobbyButton.interactable = !busy;
            _joinLobbyButton.interactable = !busy;
        }

        /// <summary>
        /// Quit the application.
        /// </summary>
        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
