using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game
{
    public enum E_MiniGameState : byte
    {
        WaitingForPlayers,
        Playing,
        Finished,
        ReturningToLobby,
    }

    /// <summary>
    /// Base class for every minigame manager. Owns the common, server-authoritative
    /// flow: wait for all players to load in, run an optional countdown, finish the
    /// game (on timeout or custom logic), then return everyone to the lobby.
    /// Subclasses implement the game-specific wrap-up in <see cref="OnGameFinished"/>.
    /// </summary>
    public abstract class MiniGameController : NetworkBehaviour
    {
        [Header("Cursor")]
        [Tooltip("Whether the hardware cursor is visible during this minigame.")]
        [SerializeField] protected bool _showCursor = true;

        [Tooltip("Cursor texture to use when the cursor is shown. Leave empty to use the project's default cursor.")]
        [SerializeField] protected Texture2D _cursorTexture;

        [Header("Game parameters")]
        [Tooltip("Game length in seconds. Set to 0 (or less) for no timer: the game then ends only via FinishGame().")]
        [SerializeField] protected float _gameDuration = 60f;

        [Tooltip("Delay in seconds between the game finishing and returning to the lobby.")]
        [SerializeField] protected float _returnToLobbyDelay = 5f;


        // Server-authoritative game state, mirrored to every client.
        private readonly NetworkVariable<E_MiniGameState> _state = new(E_MiniGameState.WaitingForPlayers);

        // ServerTime at which the timer expires. Clients derive the remaining time
        // locally from this instead of syncing the countdown every frame.
        private readonly NetworkVariable<double> _gameEndTime = new();

        // ServerTime at which players are sent back to the lobby. Set once the game
        // finishes so clients can display the return countdown.
        private readonly NetworkVariable<double> _returnToLobbyTime = new();

        /// <summary>Current game state (synced to all peers).</summary>
        public E_MiniGameState State => _state.Value;

        public bool HasTimer => _gameDuration > 0f;

        /// <summary>Seconds left before the timer expires; 0 when there is no timer or the game is not playing.</summary>
        public float TimeRemaining
        {
            get
            {
                if (!HasTimer || _state.Value != E_MiniGameState.Playing)
                    return 0f;
                return Mathf.Max(0f, (float)(_gameEndTime.Value - NetworkManager.ServerTime.Time));
            }
        }

        /// <summary>Seconds left before returning to the lobby; 0 unless the game is finished.</summary>
        public float ReturnCountdownRemaining
        {
            get
            {
                if (_state.Value != E_MiniGameState.Finished)
                    return 0f;
                return Mathf.Max(0f, (float)(_returnToLobbyTime.Value - NetworkManager.ServerTime.Time));
            }
        }

        /// <summary>Raised on every peer when the game transitions to Playing (all players loaded).</summary>
        public event Action GameStarted;

        /// <summary>Raised on every peer when the game transitions to Finished.</summary>
        public event Action GameFinished;

        public override void OnNetworkSpawn()
        {
            ApplyCursor();

            _state.OnValueChanged += HandleStateChanged;

            if (IsServer)
                NetworkManager.SceneManager.OnLoadEventCompleted += HandleLoadEventCompleted;
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= HandleStateChanged;

            if (IsServer && NetworkManager != null && NetworkManager.SceneManager != null)
                NetworkManager.SceneManager.OnLoadEventCompleted -= HandleLoadEventCompleted;
        }

        // Local-only: apply this minigame's cursor settings on every peer. A null
        // texture reverts to the project's default cursor (Player Settings).
        private void ApplyCursor()
        {
            Cursor.visible = _showCursor;

            if (_showCursor)
                Cursor.SetCursor(_cursorTexture, Vector2.zero, CursorMode.Auto);
        }

        // --- Server flow -------------------------------------------------------

        // Fires on the server once every client has finished loading a networked scene.
        private void HandleLoadEventCompleted(string sceneName, LoadSceneMode mode,
            List<ulong> clientsCompleted, List<ulong> clientsTimedOut)
        {
            // React only to this controller's own scene, and only on the initial load.
            if (_state.Value != E_MiniGameState.WaitingForPlayers || sceneName != gameObject.scene.name)
                return;

            StartGame();
        }

        private void StartGame()
        {
            if (HasTimer)
                _gameEndTime.Value = NetworkManager.ServerTime.Time + _gameDuration;

            _state.Value = E_MiniGameState.Playing;
        }

        /// <summary>
        /// Server-only: end the game now via custom logic (e.g. last player standing).
        /// No-op unless the game is currently playing, so it is safe to call repeatedly.
        /// </summary>
        protected void FinishGame()
        {
            if (!IsServer || _state.Value != E_MiniGameState.Playing)
                return;

            _state.Value = E_MiniGameState.Finished;
        }

        private void Update()
        {
            if (!IsServer || !HasTimer || _state.Value != E_MiniGameState.Playing)
                return;

            if (NetworkManager.ServerTime.Time >= _gameEndTime.Value)
                FinishGame();
        }

        // --- State transitions (run on every peer) -----------------------------

        private void HandleStateChanged(E_MiniGameState previous, E_MiniGameState current)
        {
            Debug.Log($"[MiniGame] {(IsServer ? "Server" : "Client")} state: {previous} -> {current}", this);

            switch (current)
            {
                case E_MiniGameState.Playing:
                    OnGameStarted();
                    GameStarted?.Invoke();
                    break;

                case E_MiniGameState.Finished:
                    OnGameFinished();
                    GameFinished?.Invoke();
                    if (IsServer)
                        StartCoroutine(ReturnToLobbyAfterDelay());
                    break;
            }
        }

        private IEnumerator ReturnToLobbyAfterDelay()
        {
            _returnToLobbyTime.Value = NetworkManager.ServerTime.Time + _returnToLobbyDelay;

            yield return new WaitForSeconds(_returnToLobbyDelay);

            _state.Value = E_MiniGameState.ReturningToLobby;
            SessionManager.Instance.ReturnToLobby();
        }

        // --- Subclass hooks ----------------------------------------------------

        /// <summary>Called on every peer when the game starts (all players loaded). Optional.</summary>
        protected virtual void OnGameStarted() { }

        /// <summary>
        /// Called on every peer when the game finishes (timer expired or FinishGame()).
        /// Implement the game-specific wrap-up here (freeze players, show results, ...).
        /// </summary>
        protected abstract void OnGameFinished();
    }

    /// <summary>
    /// Minigame manager that spawns and tracks a roster of <typeparamref name="T"/> players.
    /// Owns the player prefab, spawn points, and the spawned-player list so each game only
    /// writes its own rules on top of <see cref="SpawnPlayers"/>.
    /// </summary>
    public abstract class MiniGameController<T> : MiniGameController where T : PlayerController
    {
        [SerializeField] protected NetworkObject _playerPrefab;
        [SerializeField] protected Transform[] _spawnPoints;

        // Server-only: every spawned player.
        protected readonly List<T> _players = new();

        /// <summary>
        /// Server-only: spawn one player per connected client at the configured spawn points,
        /// owned by that client, and collect them into <see cref="_players"/>.
        /// </summary>
        protected void SpawnPlayers()
        {
            if (!IsServer)
                return;

            if (_playerPrefab == null || _spawnPoints == null || _spawnPoints.Length == 0)
            {
                Debug.LogError("Player prefab or spawn points are not assigned.", this);
                return;
            }

            _players.Clear();

            int index = 0;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                Transform spawn = _spawnPoints[index % _spawnPoints.Length];
                NetworkObject instantiatedPlayer = NetworkManager.SpawnManager.InstantiateAndSpawn(
                    _playerPrefab, clientId, destroyWithScene: true,
                    position: spawn.position, rotation: spawn.rotation);

                if (instantiatedPlayer.TryGetComponent(out T player))
                    _players.Add(player);

                index++;
            }
        }
    }
}
