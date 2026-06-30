using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace Game.TankShooter
{
    /// <summary>
    /// Object pool for tank shells, wired into Netcode via <see cref="INetworkPrefabInstanceHandler"/>.
    /// Registering the handler makes NGO pull bullets from this pool (instead of Instantiate) and
    /// return them to it (instead of Destroy) on every peer, so spawning/despawning a shell never
    /// allocates after the initial prewarm.
    ///
    /// One instance lives in the minigame scene. The bullet prefab must also be registered in the
    /// NGO network prefabs list so peers can resolve it by hash.
    /// </summary>
    public class BulletPooling_002 : MonoBehaviour
    {
        public static BulletPooling_002 Instance { get; private set; }

        [SerializeField] private NetworkObject _bulletPrefab;
        [Tooltip("How many bullets to instantiate up front so play never allocates.")]
        [SerializeField] private int _prewarmCount = 20;

        private readonly Queue<NetworkObject> _pool = new();
        private PrefabHandler _handler;

        private void Awake() => Instance = this;

        private void Start()
        {
            for (int i = 0; i < _prewarmCount; i++)
                _pool.Enqueue(CreatePooled());

            _handler = new PrefabHandler(this);
            NetworkManager.Singleton.PrefabHandler.AddHandler(_bulletPrefab, _handler);
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.PrefabHandler.RemoveHandler(_bulletPrefab);

            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Server-only: take a bullet from the pool, spawn it across the network (which makes every
        /// client pull its own pooled instance), then aim it toward <paramref name="direction"/>.
        /// </summary>
        public void Spawn(Vector3 position, Vector2 direction, int colorIndex, ulong shooterId)
        {
            if (!NetworkManager.Singleton.IsServer)
                return;

            Quaternion rotation = Quaternion.Euler(0f, 0f, Vector2.SignedAngle(Vector2.up, direction));
            NetworkObject netObj = GetFromPool(position, rotation);
            Bullet_002 bullet = netObj.GetComponent<Bullet_002>();

            // Set the tint before spawning so it ships in the spawn snapshot (no first-frame flicker).
            bullet.SetColor(colorIndex);
            netObj.Spawn();
            bullet.Launch(direction, shooterId);
        }

        // --- Pool plumbing -----------------------------------------------------

        private NetworkObject CreatePooled()
        {
            NetworkObject obj = Instantiate(_bulletPrefab);
            obj.gameObject.SetActive(false);
            return obj;
        }

        private NetworkObject GetFromPool(Vector3 position, Quaternion rotation)
        {
            NetworkObject obj = _pool.Count > 0 ? _pool.Dequeue() : CreatePooled();
            obj.transform.SetPositionAndRotation(position, rotation);
            obj.gameObject.SetActive(true);
            return obj;
        }

        private void ReturnToPool(NetworkObject obj)
        {
            obj.gameObject.SetActive(false);
            _pool.Enqueue(obj);
        }

        // Bridges NGO's spawn/despawn to the pool. Kept separate from the MonoBehaviour so the
        // INetworkPrefabInstanceHandler.Instantiate method does not shadow Object.Instantiate.
        private class PrefabHandler : INetworkPrefabInstanceHandler
        {
            private readonly BulletPooling_002 _pool;

            public PrefabHandler(BulletPooling_002 pool) => _pool = pool;

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
                => _pool.GetFromPool(position, rotation);

            // Play the explosion first; the bullet returns itself to the pool once the blast ends.
            public void Destroy(NetworkObject networkObject)
                => networkObject.GetComponent<Bullet_002>().Detonate(() => _pool.ReturnToPool(networkObject));
        }
    }
}
