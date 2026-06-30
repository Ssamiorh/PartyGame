using Unity.Netcode;
using UnityEngine;

namespace Game.OfficeGame
{
    /// <summary>
    /// A pushable, networked office prop. To make pushing resolve with real physics (mass,
    /// friction, damping), the prop is "claimed" by the client of whoever pushes it, so it
    /// simulates on the same machine as that player. Claims propagate along a pushed line of
    /// props, and a prop frees itself once it comes to rest so another player can claim it.
    /// </summary>
    public class Prop003_Generic : NetworkBehaviour
    {
        [SerializeField] private E_PropClass _propClass;
        [SerializeField] private E_PropType _propType;

        [SerializeField] private Rigidbody2D _rb;
        [Tooltip("Speed below which the prop counts as 'at rest' and starts its release timer.")]
        [SerializeField] private float _restSpeed = 0.1f;
        [Tooltip("Seconds at rest before the prop frees itself so another player can claim it.")]
        [SerializeField] private float _releaseDelay = 0.5f;

        // Sentinel meaning "claimed by nobody, free to take".
        private const ulong Free = ulong.MaxValue;

        // Which client's push-cluster currently owns this prop. Drives the tiebreak (nobody else
        // can take it until it frees) and disambiguates a host-claimed prop from a truly free one,
        // which otherwise share the same OwnerClientId. Server-written, readable everywhere.
        private readonly NetworkVariable<ulong> _claimedBy = new(Free);

        private float _restTimer;

        private bool IsFree => _claimedBy.Value == Free;

        /// <summary>Client-side: ask the server to claim this prop for the local client, if it's free.</summary>
        public void RequestClaim()
        {
            if (!IsSpawned || !IsFree)
                return;

            ClaimRpc();
        }

        // Anyone — a colliding player, or an already-claimed prop pulling the next one into the
        // cluster — may request a claim. The server uses the caller's id and enforces the tiebreak.
        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        private void ClaimRpc(RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;

            // Tiebreak: a prop held by another client can't be taken until it frees itself.
            if (_claimedBy.Value != Free && _claimedBy.Value != clientId)
                return;

            _claimedBy.Value = clientId;
            if (OwnerClientId != clientId)
                NetworkObject.ChangeOwnership(clientId);
        }

        // Owner-only: pull any prop we bump into our cluster, so a pushed line stays in one
        // simulation and resolves with real physics instead of jamming at the first un-owned box.
        private void OnCollisionStay2D(Collision2D collision)
        {
            if (!IsOwner || IsFree)
                return;

            Prop003_Generic other = collision.collider.GetComponentInParent<Prop003_Generic>();
            if (other != null)
                other.RequestClaim();
        }

        // The current owner frees the prop once it settles, so others can claim it again. Runs on
        // whoever owns it now — the claiming client, or the server after that client disconnects.
        private void FixedUpdate()
        {
            if (!IsOwner || IsFree)
                return;

            if (_rb.linearVelocity.sqrMagnitude > _restSpeed * _restSpeed)
            {
                _restTimer = 0f;
                return;
            }

            _restTimer += Time.fixedDeltaTime;
            if (_restTimer >= _releaseDelay)
            {
                _restTimer = 0f;
                ReleaseServerRpc();
            }
        }

        [ServerRpc]
        private void ReleaseServerRpc()
        {
            _claimedBy.Value = Free;
            if (OwnerClientId != NetworkManager.ServerClientId)
                NetworkObject.ChangeOwnership(NetworkManager.ServerClientId);
        }
    }

    public enum E_PropClass
    {
        None = 0,
        Cardbox = 1,
        Chair = 2,
    }

    public enum E_PropType
    {
        None = 0,

        CardboxBig = 1,
        CardboxMedium = 2,
        CardboxSmall = 3,


        Chair1_E = 100,
        Chair1_E_var = 101,
        Chair1_N = 102,
        Chair1_S = 103,
        Chair2_E = 104,
        Chair2_E_var = 105,
        Chair2_N = 106,
        Chair2_S = 107,
        Chair3_E = 108,
        Chair3_E_var = 109,
        Chair3_N = 110,
        Chair3_S = 111,
        Chair4_E = 112,
        Chair4_E_var = 113,
        Chair4_N = 114,
        Chair4_S = 115,
        Chair5_E = 116,
        Chair5_E_var = 117,
        Chair5_N = 118,
        Chair5_S = 119,

    }
}