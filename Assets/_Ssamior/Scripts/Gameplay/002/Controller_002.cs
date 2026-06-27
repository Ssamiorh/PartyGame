using System.Collections.Generic;
using Game.TagPlatformer;
using Unity.Netcode;
using UnityEngine;

namespace Game.TankShooter
{
    public class Controller_002 : MiniGameController<PlayerController_002>
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
