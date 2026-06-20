using Unity.XR.CoreUtils;
using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SceneAudioController : MonoBehaviour
{
    [Header("Ambience")]
    [SerializeField] private AudioClip forestAmbience;
    [SerializeField, Range(0f, 1f)] private float ambienceVolume = 0.35f;

    [Header("Footsteps")]
    [SerializeField] private AudioClip footstepClip;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.55f;
    [SerializeField] private float minMoveSpeed = 0.08f;
    [SerializeField] private float stepDistance = 0.65f;
    [SerializeField] private Vector2 pitchRange = new Vector2(0.92f, 1.08f);

    private AudioSource _ambienceSource;
    private AudioSource _footstepSource;
    private Transform _trackedTransform;
    private Vector3 _lastTrackedPosition;
    private float _distanceSinceLastStep;

    private void Awake()
    {
        _ambienceSource = GetComponent<AudioSource>();
        _ambienceSource.playOnAwake = false;
        _ambienceSource.loop = true;
        _ambienceSource.spatialBlend = 0f;

        if (forestAmbience != null)
        {
            _ambienceSource.clip = forestAmbience;
            _ambienceSource.volume = ambienceVolume;
            _ambienceSource.Play();
        }

        _footstepSource = gameObject.AddComponent<AudioSource>();
        _footstepSource.playOnAwake = false;
        _footstepSource.loop = false;
        _footstepSource.spatialBlend = 1f;
        _footstepSource.minDistance = 0.5f;
        _footstepSource.maxDistance = 12f;
    }

    private void Start()
    {
        var origin = FindAnyObjectByType<XROrigin>();
        _trackedTransform = origin != null ? origin.transform : Camera.main?.transform;
        if (_trackedTransform != null)
            _lastTrackedPosition = _trackedTransform.position;
    }

    private void Update()
    {
        if (footstepClip == null || _trackedTransform == null)
            return;

        var currentPosition = _trackedTransform.position;
        var delta = currentPosition - _lastTrackedPosition;
        delta.y = 0f;

        var speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
        if (speed >= minMoveSpeed)
            _distanceSinceLastStep += delta.magnitude;

        if (_distanceSinceLastStep >= stepDistance)
        {
            _footstepSource.pitch = Random.Range(pitchRange.x, pitchRange.y);
            _footstepSource.PlayOneShot(footstepClip, footstepVolume);
            _distanceSinceLastStep = 0f;
        }

        _lastTrackedPosition = currentPosition;
    }
}
