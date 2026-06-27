using UnityEngine;

namespace Game.TagPlatformer
{
    /// <summary>
    /// Counts how many colliders on <see cref="_detectionLayer"/> currently overlap this
    /// trigger collider, ignoring the owner's own body. Lives on a dedicated child so the
    /// player controller can tell its foot sensor from its wall sensor even though both
    /// detect the same layer.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class TriggerSensor : MonoBehaviour
    {
        [SerializeField] private LayerMask _detectionLayer;

        private Rigidbody2D _ownBody;
        private int _contactCount;

        public bool IsTouching => _contactCount > 0;

        private void Awake()
        {
            _ownBody = GetComponentInParent<Rigidbody2D>();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (Matches(other))
                _contactCount++;
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (Matches(other))
                _contactCount = Mathf.Max(0, _contactCount - 1);
        }

        // On the detection layer and not part of our own body.
        private bool Matches(Collider2D other)
        {
            if ((_detectionLayer.value & (1 << other.gameObject.layer)) == 0)
                return false;

            return other.attachedRigidbody != _ownBody;
        }
    }
}
