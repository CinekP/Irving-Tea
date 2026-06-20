using System;
using UnityEngine;
using System.Collections;

public class GrowingTree : MonoBehaviour
{
    [Header("Growth Settings")]
    public GameObject smallTreeVisual;
    public GameObject largeTreeVisual;
    public float growthDuration = 2f;
    [Min(0.01f)]
    public float maxGrowthScale = 5f;
    public AnimationCurve growthCurve = AnimationCurve.EaseInOut(0, 0f, 1, 1f);

    [Header("Effects")]
    public ParticleSystem growthParticles;
    public AudioSource growthSound;

    private bool isGrown = false;
    public bool IsGrown => isGrown;
    public event Action TreeGrown;

    private void Start()
    {
        if (smallTreeVisual != null) smallTreeVisual.SetActive(true);
        if (largeTreeVisual != null) largeTreeVisual.SetActive(false);
    }

    public void StartGrowth()
    {
        Debug.Log($"StartGrowth called. isGrown: {isGrown}, smallTree: {smallTreeVisual != null}, largeTree: {largeTreeVisual != null}");
        if (isGrown) return;
        StartCoroutine(GrowthRoutine());
    }

    private IEnumerator GrowthRoutine()
    {
        isGrown = true;
        TreeGrown?.Invoke();
        Debug.Log("GrowthRoutine started");

        if (growthParticles != null) growthParticles.Play();
        if (growthSound != null) growthSound.Play();

        if (smallTreeVisual != null && largeTreeVisual != null)
        {
            var anchorLocalPosition = smallTreeVisual.transform.localPosition;
            var anchorLocalRotation = smallTreeVisual.transform.localRotation;
            var anchorWorldPosition = smallTreeVisual.transform.position;
            var floorY = GetVisualBottomY(smallTreeVisual);

            largeTreeVisual.SetActive(true);
            foreach (Transform child in largeTreeVisual.transform)
                child.gameObject.SetActive(true);

            largeTreeVisual.transform.localPosition = anchorLocalPosition;
            largeTreeVisual.transform.localRotation = anchorLocalRotation;

            var baseLocalScale = largeTreeVisual.transform.localScale;
            var smallHeight = GetVisualHeight(smallTreeVisual);

            largeTreeVisual.transform.localScale = baseLocalScale;
            SnapVisualToAnchor(largeTreeVisual, anchorWorldPosition, floorY);

            var largeHeightAtBaseScale = GetVisualHeight(largeTreeVisual);
            var startScaleMultiplier = largeHeightAtBaseScale > 0.001f
                ? smallHeight / largeHeightAtBaseScale
                : 0.25f;
            var endScaleMultiplier = maxGrowthScale;

            smallTreeVisual.SetActive(false);

            float elapsed = 0f;
            while (elapsed < growthDuration)
            {
                elapsed += Time.deltaTime;
                var progress = growthCurve.Evaluate(Mathf.Clamp01(elapsed / growthDuration));
                var scaleMultiplier = Mathf.Lerp(startScaleMultiplier, endScaleMultiplier, progress);

                largeTreeVisual.transform.localScale = baseLocalScale * scaleMultiplier;
                SnapVisualToAnchor(largeTreeVisual, anchorWorldPosition, floorY);

                yield return null;
            }

            largeTreeVisual.transform.localScale = baseLocalScale * endScaleMultiplier;
            SnapVisualToAnchor(largeTreeVisual, anchorWorldPosition, floorY);

            Debug.Log("Large tree growth finished using small tree base");
        }
    }

    private static float GetVisualBottomY(GameObject visual)
    {
        var renderer = visual.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.min.y : visual.transform.position.y;
    }

    private static float GetVisualHeight(GameObject visual)
    {
        var renderer = visual.GetComponentInChildren<Renderer>();
        return renderer != null ? renderer.bounds.size.y : 0f;
    }

    private static void SnapVisualToAnchor(GameObject visual, Vector3 anchorWorldPosition, float floorY)
    {
        var renderer = visual.GetComponentInChildren<Renderer>();
        if (renderer == null)
        {
            visual.transform.position = new Vector3(
                anchorWorldPosition.x,
                anchorWorldPosition.y,
                anchorWorldPosition.z);
            return;
        }

        var deltaY = floorY - renderer.bounds.min.y;
        var position = visual.transform.position;
        visual.transform.position = new Vector3(
            anchorWorldPosition.x,
            position.y + deltaY,
            anchorWorldPosition.z);
    }
}
