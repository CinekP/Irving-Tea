using UnityEngine;

namespace VRChopping
{
    /// <summary>
    /// Keeps distant scene content visible earlier by boosting camera and LOD settings for this scene.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class AfterTreeFallRenderSettings : MonoBehaviour
    {
        [SerializeField] private float farClipPlane = 2500f;
        [SerializeField] private float lodBias = 2.5f;
        [SerializeField] private bool disableOcclusionCulling = true;
        [SerializeField] private bool disableLodGroups = true;

        private float _previousLodBias;

        private void Awake()
        {
            _previousLodBias = QualitySettings.lodBias;
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, lodBias);
            QualitySettings.maximumLODLevel = 0;

            foreach (var camera in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                camera.farClipPlane = farClipPlane;
                if (disableOcclusionCulling)
                    camera.useOcclusionCulling = false;
            }

            if (disableLodGroups)
            {
                foreach (var lodGroup in FindObjectsByType<LODGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    lodGroup.enabled = false;
            }
        }

        private void OnDestroy()
        {
            QualitySettings.lodBias = _previousLodBias;
        }
    }
}
