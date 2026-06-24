using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    public class LobbyUI : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _lobbyCode;
        [SerializeField] private Button _copyCodeButton;

        private void OnEnable()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.SessionChanged += RefreshCode;

            
            _copyCodeButton.onClick.AddListener(CopyCode);

            // Covers the host, whose session is already set before this scene loads.
            RefreshCode();
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.SessionChanged -= RefreshCode;

            _copyCodeButton.onClick.RemoveListener(CopyCode);
        }

        private void RefreshCode()
        {
            ISession session = SessionManager.Instance != null ? SessionManager.Instance.Session : null;
            string code = session != null ? session.Code : string.Empty;
            _lobbyCode.text = code;

            // No code yet means nothing to copy.
            _copyCodeButton.interactable = !string.IsNullOrEmpty(code);
        }

        private void CopyCode()
        {
            ISession session = SessionManager.Instance != null ? SessionManager.Instance.Session : null;
            if (session != null && !string.IsNullOrEmpty(session.Code))
                GUIUtility.systemCopyBuffer = session.Code;
        }
    }
}
