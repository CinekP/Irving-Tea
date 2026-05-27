using UnityEngine;

namespace VRChopping
{
    public class TreeHealth : MonoBehaviour
    {
        [Header("Health")]
        [Min(1f)]
        [SerializeField] private float maxHealth = 10f;

        [Header("Hit Logic")]
        [SerializeField] private float minDamagePerHit = 0.5f;
        [SerializeField] private float maxDamagePerHit = 3f;
        [SerializeField] private float maxExpectedHitSpeed = 6f;
        [SerializeField] private Transform hitZoneCenter;
        [SerializeField] private float hitZoneRadius = 1f;
        [SerializeField] private float glancingHitMultiplier = 0.25f;
        [SerializeField] private float solidHitDotThreshold = 0.35f;

        [Header("References")]
        [SerializeField] private TreeFallController treeFallController;

        private float _currentHealth;
        private bool _isBroken;

        private void Reset()
        {
            if (treeFallController == null)
            {
                treeFallController = GetComponent<TreeFallController>();
            }
        }

        private void Awake()
        {
            _currentHealth = maxHealth;

            if (treeFallController == null)
            {
                treeFallController = GetComponent<TreeFallController>();
            }
        }

        public void ApplyHit(Vector3 hitPoint, Vector3 hitDirection, float hitSpeed)
        {
            if (_isBroken)
                return;

            if (!IsPointInHitZone(hitPoint))
                return;

            var normalizedSpeed = Mathf.Clamp01(hitSpeed / Mathf.Max(0.01f, maxExpectedHitSpeed));
            var damage = Mathf.Lerp(minDamagePerHit, maxDamagePerHit, normalizedSpeed) * ComputeAngleMultiplier(hitDirection);

            ApplyDamage(damage, hitPoint, hitDirection, hitSpeed);
        }

        private bool IsPointInHitZone(Vector3 point)
        {
            if (hitZoneCenter == null)
                return true;

            var flatPoint = new Vector3(point.x, hitZoneCenter.position.y, point.z);
            var flatCenter = new Vector3(hitZoneCenter.position.x, hitZoneCenter.position.y, hitZoneCenter.position.z);
            var distance = Vector3.Distance(flatPoint, flatCenter);

            return distance <= hitZoneRadius;
        }

        private float ComputeAngleMultiplier(Vector3 hitDirection)
        {
            var local = transform.InverseTransformDirection(hitDirection.normalized);
            var sideAmount = Mathf.Abs(local.x);
            var forwardAmount = Mathf.Abs(local.z);
            var quality = Mathf.Max(sideAmount, forwardAmount);
            return quality >= solidHitDotThreshold ? 1f : glancingHitMultiplier;
        }

        private void ApplyDamage(float amount, Vector3 hitPoint, Vector3 hitDirection, float hitSpeed)
        {
            if (amount <= 0f || _isBroken)
                return;

            var previousHealth = _currentHealth;
            var rawHealth = previousHealth - amount;
            _currentHealth = Mathf.Max(0f, rawHealth);
            var healthNormalized = Mathf.Clamp01(_currentHealth / Mathf.Max(0.01f, maxHealth));

            if (treeFallController != null)
            {
                treeFallController.UpdatePreBreakTilt(healthNormalized, hitDirection);
            }

            if (_currentHealth <= 0f)
            {
                var overkill = Mathf.Max(0f, -rawHealth);
                var overkillBoost = 1f + Mathf.Clamp01(overkill / Mathf.Max(0.01f, maxHealth * 0.25f)) * 0.75f;
                var speedBoost = 1f + Mathf.Clamp01(hitSpeed / Mathf.Max(0.01f, maxExpectedHitSpeed)) * 0.35f;
                BreakTree(hitPoint, hitDirection, hitSpeed, overkillBoost * speedBoost);
            }
        }

        private void BreakTree(Vector3 hitPoint, Vector3 hitDirection, float hitSpeed, float finalHitBoost)
        {
            if (_isBroken)
                return;

            _isBroken = true;

            if (treeFallController != null)
            {
                treeFallController.BreakAndFall(hitPoint, hitDirection, hitSpeed, finalHitBoost);
            }
        }

        public float CurrentHealth => _currentHealth;

        public float MaxHealth => maxHealth;
    }
}
