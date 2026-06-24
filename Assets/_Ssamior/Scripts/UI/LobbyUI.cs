using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;

namespace Game.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _lobbyCode;

        private void OnEnable()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.SessionChanged += RefreshCode;

            // Covers the host, whose session is already set before this scene loads.
            RefreshCode();
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.SessionChanged -= RefreshCode;
        }

        private void RefreshCode()
        {
            ISession session = SessionManager.Instance != null ? SessionManager.Instance.Session : null;
            _lobbyCode.text = session != null ? session.Code : string.Empty;
        }

    }
}
