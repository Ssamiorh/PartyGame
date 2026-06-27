using System.Collections.Generic;
using Game.TagPlatformer;
using Unity.Netcode;
using UnityEngine;

namespace Game
{
    public class Controller_TagPlatformer : MiniGameController
    {
        [SerializeField] private NetworkObject _playerPrefab;
        [SerializeField] private Transform[] _spawnPoints;

        // Server-only: every spawned player, used to pick the first one to be "it".
        private readonly List<PlayerController_001> _players = new();

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

            _players.Clear();

            int index = 0;
            foreach (ulong clientId in NetworkManager.ConnectedClientsIds)
            {
                Transform spawn = _spawnPoints[index % _spawnPoints.Length];
                NetworkObject instantiatedPlayer = NetworkManager.SpawnManager.InstantiateAndSpawn(
                    _playerPrefab, clientId, destroyWithScene: true,
                    position: spawn.position, rotation: spawn.rotation);

                if (instantiatedPlayer.TryGetComponent(out PlayerController_001 player))
                    _players.Add(player);

                index++;
            }

            TagRandomPlayer();
        }

        // Pick one random player to start the game as "it".
        private void TagRandomPlayer()
        {
            if (_players.Count == 0)
                return;

            _players[Random.Range(0, _players.Count)].SetTagged(true);
        }

        protected override void OnGameFinished()
        {
            // TODO: TagPlatformer-specific wrap-up (freeze players, reveal winner, ...).
        }
    }
}
