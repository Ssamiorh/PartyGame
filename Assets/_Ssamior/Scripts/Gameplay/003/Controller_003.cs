using UnityEngine;

namespace Game.OfficeGame
{
    public class Controller_003 : MiniGameController<PlayerController_003>
    {
        protected override void OnGameStarted()
        {
            // Server-only: spawn one player per client, then pick the first one to be "it".
            if (!IsServer)
                return;

            SpawnPlayers();
        }

        protected override void OnGameFinished()
        {
            // TODO: TagPlatformer-specific wrap-up (freeze players, reveal winner, ...).
        }
    }
}