using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine.SceneManagement;

namespace Game
{
    /// <summary>
    /// Owns the multiplayer session lifecycle (create / join) on top of the
    /// Unity Multiplayer Services (Sessions) API with a host-based Relay network.
    /// Persists across scenes so the lobby keeps the live session reference.
    /// </summary>
    public class SessionManager : Singleton<SessionManager>
    {
        private const int MaxPlayers = 4;

        /// <summary>Raised whenever the active session reference changes.</summary>
        public event System.Action SessionChanged;

        private ISession _session;
        public ISession Session
        {
            get => _session;
            private set
            {
                _session = value;
                SessionChanged?.Invoke();
            }
        }

        public override void Awake()
        {
            base.Awake();
            if (Instance == this)
                DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Create a host-based session and load the lobby scene over the network.
        /// Returns the join code other players use to connect.
        /// </summary>
        public async Task<string> CreateLobbyAsync()
        {
            await EnsureSignedInAsync();

            var options = new SessionOptions { MaxPlayers = MaxPlayers }.WithRelayNetwork();
            Session = await MultiplayerService.Instance.CreateSessionAsync(options);

            // Host loads the lobby scene through NGO; joining clients sync to it automatically.
            NetworkManager.Singleton.SceneManager.LoadScene(
                GameDataRegistry.LobbySceneName, LoadSceneMode.Single);

            return _session.Code;
        }

        /// <summary>
        /// Join an existing session by its code. The client auto-syncs to the
        /// host's lobby scene via NGO scene management, so no manual load here.
        /// </summary>
        public async Task JoinLobbyByCodeAsync(string code)
        {
            await EnsureSignedInAsync();
            Session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
        }

        /// <summary>
        /// Host-only: load a minigame scene over the network for every player.
        /// </summary>
        public void StartMiniGame(E_MiniGame miniGame)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            NetworkManager.Singleton.SceneManager.LoadScene(
                GameDataRegistry.GetMiniGameSceneName(miniGame), LoadSceneMode.Single);
        }

        /// <summary>
        /// Host-only: bring every player back to the lobby scene over the network.
        /// </summary>
        public void ReturnToLobby()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
                return;

            NetworkManager.Singleton.SceneManager.LoadScene(
                GameDataRegistry.LobbySceneName, LoadSceneMode.Single);
        }

        private static async Task EnsureSignedInAsync()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                await UnityServices.InitializeAsync();

            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }
}
