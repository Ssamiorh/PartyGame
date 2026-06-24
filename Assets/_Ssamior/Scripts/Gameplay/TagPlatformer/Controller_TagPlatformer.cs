using Unity.Netcode;
using UnityEngine;

namespace Game
{
    public class Controller_TagPlatformer : MiniGameController
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private Transform _playersParent;
        [SerializeField] private Transform[] _spawnPoints;

        protected override void OnGameStarted()
        {
            // Server-only: spawn one player per client, owned by that client. Every other
            // peer receives it as a network-synced visual representation.
            if (!IsServer)
                return;

            if (_playerPrefab == null || _spawnPoints == null || _spawnPoints.Length == 0)
            {
                Debug.LogError("[TagPlatformer] Player prefab or spawn points are not assigned.", this);
                return;
            }

            int index = 0;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                Transform spawn = _spawnPoints[index % _spawnPoints.Length];
                NetworkObject instantiatedPlayer = NetworkManager.SpawnManager.InstantiateAndSpawn(
                    _playerPrefab, clientId, destroyWithScene: true,
                    position: spawn.position, rotation: spawn.rotation);
                instantiatedPlayer.TrySetParent(_playersParent);
                index++;
            }
        }

        protected override void OnGameFinished()
        {
            // TODO: TagPlatformer-specific wrap-up (freeze players, reveal winner, ...).
        }
    }
}
