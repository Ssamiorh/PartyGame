using Unity.Netcode.Components;

namespace Game.TagPlatformer
{
    /// <summary>
    /// NetworkAnimator that lets the owning client (not the server) drive the Animator,
    /// matching this player's owner-authoritative movement. Animator parameters set on the
    /// owner are replicated to every other peer so remote players animate too.
    /// </summary>
    public class OwnerNetworkAnimator : NetworkAnimator
    {
        protected override bool OnIsServerAuthoritative() => false;
    }
}
