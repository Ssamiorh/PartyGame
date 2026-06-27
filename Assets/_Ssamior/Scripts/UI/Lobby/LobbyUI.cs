using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace Game.UI
{
    public class LobbyUI : MonoBehaviour
    {
        // Session player property key carrying the chosen color index.
        private const string ColorPropertyKey = "color";

        [SerializeField] private TextMeshProUGUI _lobbyCode;
        [SerializeField] private Button _copyCodeButton;

        [Header("Player list")]
        [SerializeField] private LobbyPlayerRow _playerRowPrefab;
        [SerializeField] private Transform _playerListContainer;

        private ISession _subscribedSession;

        private void OnEnable()
        {
            // Minigames may swap or hide the cursor; restore the default on every lobby load.
            Cursor.visible = true;
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);

            if (SessionManager.Instance != null)
                SessionManager.Instance.SessionChanged += HandleSessionChanged;

            _copyCodeButton.onClick.AddListener(CopyCode);

            // Covers the host, whose session is already set before this scene loads.
            HandleSessionChanged();
        }

        private void OnDisable()
        {
            if (SessionManager.Instance != null)
                SessionManager.Instance.SessionChanged -= HandleSessionChanged;

            UnsubscribeFromSession();
            _copyCodeButton.onClick.RemoveListener(CopyCode);
        }

        private void HandleSessionChanged()
        {
            ISession session = SessionManager.Instance != null ? SessionManager.Instance.Session : null;

            if (session != _subscribedSession)
            {
                UnsubscribeFromSession();
                _subscribedSession = session;

                if (_subscribedSession != null)
                {
                    _subscribedSession.PlayerJoined += HandlePlayerListChanged;
                    _subscribedSession.PlayerHasLeft += HandlePlayerListChanged;
                    _subscribedSession.PlayerPropertiesChanged += RefreshPlayers;

                    // Push our saved color into the session so others see it.
                    PublishLocalColor();
                }
            }

            RefreshCode();
            RefreshPlayers();
        }

        private void UnsubscribeFromSession()
        {
            if (_subscribedSession == null)
                return;

            _subscribedSession.PlayerJoined -= HandlePlayerListChanged;
            _subscribedSession.PlayerHasLeft -= HandlePlayerListChanged;
            _subscribedSession.PlayerPropertiesChanged -= RefreshPlayers;
            _subscribedSession = null;
        }

        private void HandlePlayerListChanged(string playerId) => RefreshPlayers();

        private void RefreshCode()
        {
            string code = _subscribedSession != null ? _subscribedSession.Code : string.Empty;
            _lobbyCode.text = code;

            // No code yet means nothing to copy.
            _copyCodeButton.interactable = !string.IsNullOrEmpty(code);
        }

        private void RefreshPlayers()
        {
            foreach (Transform child in _playerListContainer)
                Destroy(child.gameObject);

            if (_subscribedSession == null)
                return;

            string localId = _subscribedSession.CurrentPlayer != null
                ? _subscribedSession.CurrentPlayer.Id
                : null;

            // Number players by join order, which is consistent across clients.
            List<IReadOnlyPlayer> ordered = new(_subscribedSession.Players);
            ordered.Sort((a, b) => a.Joined.CompareTo(b.Joined));

            for (int i = 0; i < ordered.Count; i++)
            {
                IReadOnlyPlayer player = ordered[i];
                bool isLocal = player.Id == localId;

                LobbyPlayerRow row = Instantiate(_playerRowPrefab, _playerListContainer);
                row.Setup($"Player {i + 1}", ReadColorIndex(player), isLocal, HandleLocalColorSelected);
            }
        }

        private async void HandleLocalColorSelected(int colorIndex)
        {
            colorIndex = PlayerColors.Clamp(colorIndex);

            // Persist locally (kept across games) ...
            PlayerPrefs.SetInt(PreferencesManager.playerColor_PlayerPrefKey, colorIndex);
            PlayerPrefs.Save();

            // ... and sync to the session so the change reaches everyone.
            await ApplyColorToSession(colorIndex);

            // Setting our own property does not raise PlayerPropertiesChanged locally,
            // so refresh manually to update our own swatch.
            RefreshPlayers();
        }

        private async void PublishLocalColor()
        {
            int colorIndex;
            if (PlayerPrefs.HasKey(PreferencesManager.playerColor_PlayerPrefKey))
            {
                colorIndex = PlayerColors.Clamp(
                    PlayerPrefs.GetInt(PreferencesManager.playerColor_PlayerPrefKey, 0));
            }
            else
            {
                // First time in: grab a color nobody else is using, and keep it.
                colorIndex = PickUnusedColorIndex();
                PlayerPrefs.SetInt(PreferencesManager.playerColor_PlayerPrefKey, colorIndex);
                PlayerPrefs.Save();
            }

            await ApplyColorToSession(colorIndex);
        }

        // Best-effort: first palette color not already used by another player in the
        // lobby. Duplicates are still allowed, so if everything is taken (or there is
        // no session) we fall back to the first color.
        private int PickUnusedColorIndex()
        {
            if (_subscribedSession == null)
                return 0;

            string localId = _subscribedSession.CurrentPlayer != null
                ? _subscribedSession.CurrentPlayer.Id
                : null;

            HashSet<int> taken = new();
            foreach (IReadOnlyPlayer player in _subscribedSession.Players)
            {
                if (player.Id == localId)
                    continue;

                // Only count players who actually picked a color.
                if (player.Properties != null && player.Properties.ContainsKey(ColorPropertyKey))
                    taken.Add(ReadColorIndex(player));
            }

            for (int i = 0; i < PlayerColors.Count; i++)
            {
                if (!taken.Contains(i))
                    return i;
            }

            return 0;
        }

        private async Task ApplyColorToSession(int colorIndex)
        {
            if (_subscribedSession == null || _subscribedSession.CurrentPlayer == null)
                return;

            _subscribedSession.CurrentPlayer.SetProperty(
                ColorPropertyKey, new PlayerProperty(colorIndex.ToString()));

            try
            {
                await _subscribedSession.SaveCurrentPlayerDataAsync();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to sync player color: {e.Message}");
            }
        }

        private static int ReadColorIndex(IReadOnlyPlayer player)
        {
            if (player.Properties != null
                && player.Properties.TryGetValue(ColorPropertyKey, out PlayerProperty property)
                && int.TryParse(property.Value, out int index))
                return PlayerColors.Clamp(index);

            return 0;
        }

        private void CopyCode()
        {
            if (_subscribedSession != null && !string.IsNullOrEmpty(_subscribedSession.Code))
                GUIUtility.systemCopyBuffer = _subscribedSession.Code;
        }
    }
}
