using System;
using Unity.Netcode;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Host-only lobby panel: builds one button per available minigame and
    /// triggers a networked scene load when one is clicked. Hidden for clients.
    /// </summary>
    public class LobbyHostPanel : MonoBehaviour
    {
        [SerializeField] private LobbyButton _lobbyButtonPrefab;
        [SerializeField] private Transform _buttonContainer;

        private void Start()
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;

            CleanPanel();

            if (isHost)
                BuildButtons();
        }

        private void CleanPanel()
        {
            foreach(Transform child in _buttonContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private void BuildButtons()
        {
            foreach (E_MiniGame miniGame in Enum.GetValues(typeof(E_MiniGame)))
            {
                LobbyButton button = Instantiate(_lobbyButtonPrefab, _buttonContainer);
                button.Setup(miniGame, SessionManager.Instance.StartMiniGame);
            }
        }
    }
}
