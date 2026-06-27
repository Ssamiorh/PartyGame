using Game.TagPlatformer;
using UnityEngine;

namespace Game
{
    public class Controller_001 : MiniGameController<PlayerController_001>
    {
        protected override void OnGameStarted()
        {
            // Server-only: spawn one player per client, then pick the first one to be "it".
            if (!IsServer)
                return;

            SpawnPlayers();
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
