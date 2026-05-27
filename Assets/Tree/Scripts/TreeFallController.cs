using UnityEngine;

namespace VRChopping
{
    public class TreeFallController : MonoBehaviour
    {
        [Header("Tree Parts")]
        [SerializeField] private Transform trunkPart;
        [SerializeField] private Transform topPart;
        [SerializeField] private Transform basePivot;

        [Header("Mass")]
        [SerializeField] private float trunkMass = 220f;
        [SerializeField] private float topMass = 120f;
        [SerializeField] private Vector3 trunkCenterOfMassOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private bool lockTopUntilBreak = true;

        [Header("Pre-Break Tilt")]
        [SerializeField] private float maxHealthTiltDegrees = 8f;
        [SerializeField] private float tiltSmoothing = 9f;

        [Header("Break Impulse")]
        [SerializeField] private float breakImpulseMultiplier = 2.2f;
        [SerializeField] private float minimumBreakImpulse = 20f;
        [SerializeField] private float finalHitImpulseBoost = 1.35f;
        [SerializeField] private float breakUpwardBoost = 0.28f;

        [Header("Ground Safety")]
        [SerializeField] private float groundClearance = 0.03f;
        [SerializeField] private float groundProbeDistance = 3f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float maxGroundCorrection = 0.08f;

        [Header("Spin Control")]
        [SerializeField] private float trunkAngularDragAfterBreak = 4f;
        [SerializeField] private float topAngularDragAfterBreak = 3f;
        [SerializeField] private float maxTrunkAngularSpeed = 4f;
        [SerializeField] private float maxTopAngularSpeed = 6f;

        [Header("Scene Transition")]
        [SerializeField] private TreeFallSceneTransition sceneTransition;

        private Rigidbody _trunkRb;
        private Rigidbody _topRb;
        private bool _isBroken;
        private Vector2 _targetTilt;
        private Vector2 _currentTilt;
        private Vector2 _accumulatedHitDir;
        private Quaternion _initialTrunkLocalRotation;
        private Vector3 _initialTrunkWorldPosition;
        private Quaternion _initialTrunkWorldRotation;
        private Vector3 _initialTopWorldPosition;
        private Quaternion _initialTopWorldRotation;

        private void Reset()
        {
            AutoAssignParts();
            if (basePivot == null)
                basePivot = transform;
        }

        private void Awake()
        {
            AutoAssignParts();

            if (trunkPart == null || topPart == null)
                return;

            _initialTrunkLocalRotation = trunkPart.localRotation;
            _initialTrunkWorldPosition = trunkPart.position;
            _initialTrunkWorldRotation = trunkPart.rotation;
            _initialTopWorldPosition = topPart.position;
            _initialTopWorldRotation = topPart.rotation;

            RemoveLegacyJoints();
            SetupBodies();
            NeutralizeRootRigidbodyIfPresent();
        }

        private void FixedUpdate()
        {
            if (_isBroken)
            {
                ClampPostBreakSpin();
                return;
            }

            _currentTilt.x = Mathf.MoveTowards(_currentTilt.x, _targetTilt.x, tiltSmoothing * Time.fixedDeltaTime);
            _currentTilt.y = Mathf.MoveTowards(_currentTilt.y, _targetTilt.y, tiltSmoothing * Time.fixedDeltaTime);
            ApplyCurrentTilt();
        }

        public void UpdatePreBreakTilt(float healthNormalized, Vector3 hitDirection)
        {
            if (_isBroken || trunkPart == null)
                return;

            var localHit = trunkPart.InverseTransformDirection(-hitDirection.normalized);
            var planar = new Vector2(localHit.x, localHit.z);
            if (planar.sqrMagnitude > 0.0001f)
                _accumulatedHitDir += planar.normalized;

            var damagePercent = Mathf.Clamp01(1f - healthNormalized);
            var tiltMagnitude = damagePercent * maxHealthTiltDegrees;
            var tiltDir = _accumulatedHitDir.sqrMagnitude > 0.0001f ? _accumulatedHitDir.normalized : Vector2.right;

            _targetTilt.x = Mathf.Clamp(tiltDir.y * tiltMagnitude, -maxHealthTiltDegrees, maxHealthTiltDegrees);
            _targetTilt.y = Mathf.Clamp(-tiltDir.x * tiltMagnitude, -maxHealthTiltDegrees, maxHealthTiltDegrees);
        }

        public void BreakAndFall(Vector3 hitPoint, Vector3 hitDirection, float hitSpeed, float finalHitBoost = 1f)
        {
            if (_isBroken || _trunkRb == null)
                return;

            _currentTilt = _targetTilt;
            ApplyCurrentTilt();
            _isBroken = true;

            var trunkPos = trunkPart.position;
            var trunkRot = trunkPart.rotation;
            var topPos = topPart != null ? topPart.position : Vector3.zero;
            var topRot = topPart != null ? topPart.rotation : Quaternion.identity;

            DetachTreePartsForPhysics();

            trunkPart.position = trunkPos;
            trunkPart.rotation = trunkRot;
            if (topPart != null)
            {
                topPart.position = topPos;
                topPart.rotation = topRot;
            }

            _trunkRb.position = trunkPos;
            _trunkRb.rotation = trunkRot;
            _trunkRb.linearVelocity = Vector3.zero;
            _trunkRb.angularVelocity = Vector3.zero;
            _trunkRb.isKinematic = false;
            _trunkRb.useGravity = true;
            _trunkRb.constraints = RigidbodyConstraints.None;
            _trunkRb.angularDamping = trunkAngularDragAfterBreak;

            if (_topRb != null)
            {
                _topRb.position = topPos;
                _topRb.rotation = topRot;
                _topRb.linearVelocity = Vector3.zero;
                _topRb.angularVelocity = Vector3.zero;
                _topRb.isKinematic = false;
                _topRb.useGravity = true;
                _topRb.constraints = RigidbodyConstraints.None;
                _topRb.angularDamping = topAngularDragAfterBreak;
                _topRb.WakeUp();
            }

            NudgePartsAboveGroundIfNeeded();
            ApplyBreakImpulse(hitPoint, hitDirection, hitSpeed, finalHitBoost);
            ScheduleSceneTransition();
        }

        private void ScheduleSceneTransition()
        {
            if (sceneTransition == null)
                sceneTransition = GetComponent<TreeFallSceneTransition>();

            sceneTransition?.ScheduleTransitionAfterFall();
        }

        private void AutoAssignParts()
        {
            if (trunkPart == null && transform.childCount > 0)
                trunkPart = transform.GetChild(0);
            if (topPart == null && transform.childCount > 1)
                topPart = transform.GetChild(1);
        }

        private void SetupBodies()
        {
            _trunkRb = EnsureRigidbody(trunkPart.gameObject);
            _trunkRb.mass = Mathf.Max(1f, trunkMass);
            _trunkRb.useGravity = false;
            _trunkRb.isKinematic = true;
            _trunkRb.constraints = RigidbodyConstraints.None;
            _trunkRb.interpolation = RigidbodyInterpolation.Interpolate;
            _trunkRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _trunkRb.centerOfMass = trunkCenterOfMassOffset;
            _trunkRb.linearVelocity = Vector3.zero;
            _trunkRb.angularVelocity = Vector3.zero;

            _topRb = EnsureRigidbody(topPart.gameObject);
            _topRb.mass = Mathf.Max(1f, topMass);
            _topRb.useGravity = false;
            _topRb.isKinematic = lockTopUntilBreak;
            _topRb.constraints = RigidbodyConstraints.None;
            _topRb.interpolation = RigidbodyInterpolation.Interpolate;
            _topRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _topRb.linearVelocity = Vector3.zero;
            _topRb.angularVelocity = Vector3.zero;

            SetAllCollidersSolid(trunkPart);
            SetAllCollidersSolid(topPart);
        }

        private void ApplyCurrentTilt()
        {
            if (trunkPart == null || basePivot == null)
                return;

            var targetRotation =
                Quaternion.AngleAxis(_currentTilt.x, Vector3.right) *
                Quaternion.AngleAxis(_currentTilt.y, Vector3.forward) *
                _initialTrunkLocalRotation;

            var smoothedRotation = Quaternion.Slerp(
                trunkPart.localRotation,
                targetRotation,
                Mathf.Clamp01(tiltSmoothing * Time.fixedDeltaTime));

            var worldTiltDelta = trunkPart.parent != null
                ? trunkPart.parent.rotation * (smoothedRotation * Quaternion.Inverse(_initialTrunkLocalRotation))
                : smoothedRotation * Quaternion.Inverse(_initialTrunkLocalRotation);

            var fromPivot = _initialTrunkWorldPosition - basePivot.position;
            var tiltedPosition = basePivot.position + (worldTiltDelta * fromPivot);

            trunkPart.rotation = worldTiltDelta * _initialTrunkWorldRotation;
            trunkPart.position = tiltedPosition;

            if (topPart != null && topPart.parent != trunkPart)
            {
                var topFromPivot = _initialTopWorldPosition - basePivot.position;
                topPart.rotation = worldTiltDelta * _initialTopWorldRotation;
                topPart.position = basePivot.position + (worldTiltDelta * topFromPivot);
            }
        }

        private void ApplyBreakImpulse(Vector3 hitPoint, Vector3 hitDirection, float hitSpeed, float finalHitBoost)
        {
            var pushDirection = -hitDirection;
            pushDirection.y = Mathf.Max(breakUpwardBoost, pushDirection.y);
            if (pushDirection.sqrMagnitude < 0.0001f)
                pushDirection = trunkPart.forward;
            pushDirection.Normalize();

            var boosted = Mathf.Max(1f, finalHitBoost) * Mathf.Max(1f, finalHitImpulseBoost);
            var trunkImpulse = Mathf.Max(minimumBreakImpulse, hitSpeed * breakImpulseMultiplier * _trunkRb.mass * 0.02f * boosted);
            _trunkRb.AddForceAtPosition(pushDirection * trunkImpulse, hitPoint, ForceMode.Impulse);

            if (_topRb != null)
            {
                _topRb.AddForce(pushDirection * (trunkImpulse * 0.65f), ForceMode.Impulse);
            }
        }

        private void DetachTreePartsForPhysics()
        {
            if (trunkPart != null)
            {
                RemoveAllJointsInHierarchy(trunkPart);
                trunkPart.SetParent(null, true);
            }

            if (topPart != null)
            {
                RemoveAllJointsInHierarchy(topPart);
                topPart.SetParent(null, true);
            }
        }

        private void ClampPostBreakSpin()
        {
            if (_trunkRb != null && _trunkRb.angularVelocity.magnitude > maxTrunkAngularSpeed)
            {
                _trunkRb.angularVelocity = _trunkRb.angularVelocity.normalized * maxTrunkAngularSpeed;
            }

            if (_topRb != null && _topRb.angularVelocity.magnitude > maxTopAngularSpeed)
            {
                _topRb.angularVelocity = _topRb.angularVelocity.normalized * maxTopAngularSpeed;
            }
        }

        private void NudgePartsAboveGroundIfNeeded()
        {
            var groundY = ResolveGroundY();
            var minAllowedY = groundY + groundClearance;
            var lowest = Mathf.Min(GetLowestColliderY(trunkPart), GetLowestColliderY(topPart));
            if (lowest >= minAllowedY)
                return;

            var correction = minAllowedY - lowest;
            if (correction > maxGroundCorrection)
                return;

            if (trunkPart != null)
            {
                var p = trunkPart.position;
                p.y += correction;
                trunkPart.position = p;
                if (_trunkRb != null)
                    _trunkRb.position = p;
            }

            if (topPart != null)
            {
                var p = topPart.position;
                p.y += correction;
                topPart.position = p;
                if (_topRb != null)
                    _topRb.position = p;
            }
        }

        private float ResolveGroundY()
        {
            if (basePivot == null)
                return 0f;

            var origin = basePivot.position + Vector3.up * groundProbeDistance;
            if (Physics.Raycast(origin, Vector3.down, out var hit, groundProbeDistance * 2f, groundMask, QueryTriggerInteraction.Ignore))
                return hit.point.y;

            return basePivot.position.y;
        }

        private static float GetLowestColliderY(Transform part)
        {
            if (part == null)
                return float.PositiveInfinity;

            var colliders = part.GetComponentsInChildren<Collider>(true);
            var found = false;
            var minY = float.PositiveInfinity;
            for (var i = 0; i < colliders.Length; i++)
            {
                var col = colliders[i];
                if (col == null || col.isTrigger)
                    continue;
                found = true;
                minY = Mathf.Min(minY, col.bounds.min.y);
            }

            return found ? minY : part.position.y;
        }

        private void NeutralizeRootRigidbodyIfPresent()
        {
            var rootRb = GetComponent<Rigidbody>();
            if (rootRb == null)
                return;

            if (trunkPart != transform)
            {
                rootRb.isKinematic = true;
                rootRb.useGravity = false;
                rootRb.linearVelocity = Vector3.zero;
                rootRb.angularVelocity = Vector3.zero;
                rootRb.constraints = RigidbodyConstraints.FreezeAll;
            }
        }

        private static Rigidbody EnsureRigidbody(GameObject obj)
        {
            var rb = obj.GetComponent<Rigidbody>();
            if (rb == null)
                rb = obj.AddComponent<Rigidbody>();
            return rb;
        }

        private static void SetAllCollidersSolid(Transform root)
        {
            var colliders = root.GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null)
                    colliders[i].isTrigger = false;
            }
        }

        private void RemoveLegacyJoints()
        {
            if (trunkPart != null)
                RemoveAllJointsInHierarchy(trunkPart);
            if (topPart != null)
                RemoveAllJointsInHierarchy(topPart);
        }

        private static void RemoveAllJointsInHierarchy(Transform root)
        {
            var joints = root.GetComponentsInChildren<Joint>(true);
            for (var i = 0; i < joints.Length; i++)
            {
                if (joints[i] != null)
                {
                    if (Application.isPlaying)
                        Destroy(joints[i]);
                    else
                        DestroyImmediate(joints[i]);
                }
            }
        }
    }
}
