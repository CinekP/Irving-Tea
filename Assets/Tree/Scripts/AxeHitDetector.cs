using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRChopping
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class AxeHitDetector : MonoBehaviour
    {
        [Header("Hit Detection")]
        [SerializeField] private string treeTag = "Tree";
        [SerializeField] private float minHitSpeed = 1.5f;
        [SerializeField] private float cooldownBetweenHits = 0.15f;
        [SerializeField] private bool processTriggerHits = true;
        [SerializeField] private bool useChildHitColliders = true;
        [SerializeField] private Collider[] explicitHitColliders;

        [Header("Resistance")]
        [SerializeField] private bool enableResistance = true;
        [SerializeField] private float resistanceStrength = 1.1f;
        [SerializeField] private float minimumResistanceImpulse = 1.5f;
        [SerializeField] private float resistanceDuration = 0.12f;
        [SerializeField] private float heldDragDuringResistance = 8f;
        [SerializeField] private float heldAngularDragDuringResistance = 8f;
        [SerializeField] private float kinematicPushbackDistance = 0.04f;
        [SerializeField] private float postImpactVelocityMultiplier = 0.45f;

        [Header("Haptics")]
        [SerializeField] private float lightHapticAmplitude = 0.3f;
        [SerializeField] private float heavyHapticAmplitude = 0.7f;
        [SerializeField] private float hapticDuration = 0.05f;

        private Rigidbody _rigidbody;
        private XRGrabInteractable _grabInteractable;
        private IXRInteractor _currentInteractor;
        private float _lastHitTime;
        private Vector3 _lastPosition;
        private float _trackedSwingSpeed;
        private float _resistanceEndTime;
        private float _originalDrag;
        private float _originalAngularDrag;
        private readonly HashSet<Collider> _validHitColliders = new HashSet<Collider>();

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _grabInteractable = GetComponent<XRGrabInteractable>();
            _rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            _lastPosition = transform.position;
            _originalDrag = _rigidbody.linearDamping;
            _originalAngularDrag = _rigidbody.angularDamping;
            CacheValidHitColliders();
        }

        private void FixedUpdate()
        {
            var currentPosition = transform.position;
            var dt = Mathf.Max(Time.fixedDeltaTime, 0.0001f);
            _trackedSwingSpeed = (currentPosition - _lastPosition).magnitude / dt;
            _lastPosition = currentPosition;

            if (Time.time >= _resistanceEndTime)
            {
                _rigidbody.linearDamping = _originalDrag;
                _rigidbody.angularDamping = _originalAngularDrag;
            }
        }

        private void OnEnable()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.AddListener(OnSelectEntered);
                _grabInteractable.selectExited.AddListener(OnSelectExited);
            }
        }

        private void OnDisable()
        {
            if (_grabInteractable != null)
            {
                _grabInteractable.selectEntered.RemoveListener(OnSelectEntered);
                _grabInteractable.selectExited.RemoveListener(OnSelectExited);
            }
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            _currentInteractor = args.interactorObject;
        }

        private void OnSelectExited(SelectExitEventArgs args)
        {
            _currentInteractor = null;
        }

        private void OnCollisionEnter(Collision collision)
        {
            var contact = collision.GetContact(0);
            if (!IsValidOwnCollider(contact.thisCollider))
                return;
            ProcessHit(collision.collider, contact.point, contact.normal);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!processTriggerHits)
                return;

            var hitPoint = other.ClosestPoint(transform.position);
            var hitNormal = (transform.position - hitPoint).normalized;
            if (hitNormal.sqrMagnitude < 0.0001f)
                hitNormal = -transform.forward;

            ProcessHit(other, hitPoint, hitNormal);
        }

        private void ProcessHit(Collider targetCollider, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (Time.time - _lastHitTime < cooldownBetweenHits)
                return;

            if (!targetCollider.CompareTag(treeTag))
                return;

            var speed = GetHitSpeed();
            if (speed < minHitSpeed)
                return;

            var hitDirection = -hitNormal;
            var treeHealth = targetCollider.GetComponentInParent<TreeHealth>();
            if (treeHealth != null)
            {
                treeHealth.ApplyHit(hitPoint, hitDirection, speed);
            }

            ApplyResistance(hitNormal, speed);
            SendHitHaptics(speed);
            _lastHitTime = Time.time;
        }

        private float GetHitSpeed()
        {
            return Mathf.Max(_rigidbody.linearVelocity.magnitude, _trackedSwingSpeed);
        }

        private void ApplyResistance(Vector3 surfaceNormal, float speed)
        {
            if (!enableResistance)
                return;

            var normal = surfaceNormal.normalized;
            var impulse = Mathf.Max(minimumResistanceImpulse, speed * resistanceStrength);

            if (_rigidbody.isKinematic)
            {
                transform.position += normal * kinematicPushbackDistance;
            }
            else
            {
                _rigidbody.AddForce(normal * impulse, ForceMode.Impulse);
                _rigidbody.linearVelocity *= Mathf.Clamp01(postImpactVelocityMultiplier);
            }

            if (_grabInteractable.isSelected)
            {
                _rigidbody.linearDamping = heldDragDuringResistance;
                _rigidbody.angularDamping = heldAngularDragDuringResistance;
                _resistanceEndTime = Time.time + resistanceDuration;
            }
        }

        private void SendHitHaptics(float speed)
        {
            var strength = Mathf.InverseLerp(minHitSpeed, minHitSpeed * 3f, speed);
            var amplitude = Mathf.Lerp(lightHapticAmplitude, heavyHapticAmplitude, strength);
            SendHaptics(amplitude);
        }

        private void SendHaptics(float amplitude)
        {
            if (_currentInteractor == null)
                return;

            if (_currentInteractor is XRBaseInputInteractor controllerInteractor &&
                controllerInteractor.xrController != null)
            {
                controllerInteractor.xrController.SendHapticImpulse(amplitude, hapticDuration);
            }
        }

        private void CacheValidHitColliders()
        {
            _validHitColliders.Clear();

            if (explicitHitColliders != null && explicitHitColliders.Length > 0)
            {
                for (var i = 0; i < explicitHitColliders.Length; i++)
                {
                    if (explicitHitColliders[i] != null)
                        _validHitColliders.Add(explicitHitColliders[i]);
                }
            }

            if (!useChildHitColliders)
                return;

            var childColliders = GetComponentsInChildren<Collider>(true);
            for (var i = 0; i < childColliders.Length; i++)
            {
                if (childColliders[i] != null && !childColliders[i].isTrigger)
                    _validHitColliders.Add(childColliders[i]);
            }
        }

        private bool IsValidOwnCollider(Collider ownCollider)
        {
            if (!useChildHitColliders && (explicitHitColliders == null || explicitHitColliders.Length == 0))
                return true;
            if (ownCollider == null)
                return false;
            return _validHitColliders.Contains(ownCollider);
        }
    }
}
