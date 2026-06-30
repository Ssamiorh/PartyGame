using Unity.Netcode;
using UnityEngine;
using Utils;

namespace Game
{
    /// <summary>
    /// Shared base for minigame player controllers. Owns the lobby color sync and the
    /// MiniGameController hookup so each game's controller only writes its own rules.
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        // Owner-written color index (into PlayerColors), mirrored to every peer to tint
        // this player's sprites with their lobby-picked color.
        private readonly NetworkVariable<int> _colorIndex = new(writePerm: NetworkVariableWritePermission.Owner);

        /// <summary>This player's lobby color index (into PlayerColors), synced to every peer.</summary>
        protected int ColorIndex => _colorIndex.Value;

        private MiniGameController _miniGame;

        public override void OnNetworkSpawn()
        {
            _colorIndex.OnValueChanged += HandleColorChanged;
            // Owner publishes its lobby-picked color; everyone (owner included) applies it.
            if (IsOwner)
            {
                _colorIndex.Value = PlayerColors.Clamp(
                    PlayerPrefs.GetInt(PreferencesManager.playerColor_PlayerPrefKey, 0));
            }       
            ApplyColor(_colorIndex.Value);

            _miniGame = FindAnyObjectByType<MiniGameController>();
            _miniGame.GameFinished += HandleGameFinished;
        }

        public override void OnNetworkDespawn()
        {
            _colorIndex.OnValueChanged -= HandleColorChanged;
            _miniGame.GameFinished -= HandleGameFinished;
        }

        private void HandleColorChanged(int previous, int current) => ApplyColor(current);

        /// <summary>Apply the lobby-picked color to this player's visuals.</summary>
        protected virtual void ApplyColor(int colorIndex) { }

        /// <summary>Reaction when the minigame ends. Runs on every peer.</summary>
        protected virtual void HandleGameFinished() { }
    }
}
