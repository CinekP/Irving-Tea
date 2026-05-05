using UnityEngine;
using System.Collections;

public class GrowingTree : MonoBehaviour
{
    [Header("Growth Settings")]
    public GameObject smallTreeVisual;
    public GameObject largeTreeVisual;
    public float growthDuration = 2f;
    public AnimationCurve growthCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Effects")]
    public ParticleSystem growthParticles;
    public AudioSource growthSound;

    private bool isGrown = false;
    public bool IsGrown => isGrown;

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
        Debug.Log("GrowthRoutine started");

        if (growthParticles != null) growthParticles.Play();
        if (growthSound != null) growthSound.Play();

        if (smallTreeVisual != null && largeTreeVisual != null)
        {
            // 1. Calculate the FLOOR position based on the SMALL tree
            // We assume the small tree is already sitting correctly on the ground.
            Vector3 smallWorldPos = smallTreeVisual.transform.position;
            Vector3 smallWorldScale = smallTreeVisual.transform.lossyScale;
            float floorY = smallWorldPos.y - (smallWorldScale.y * 0.5f);
            
            // 2. Prepare the LARGE tree
            largeTreeVisual.SetActive(true);
            foreach (Transform child in largeTreeVisual.transform) child.gameObject.SetActive(true);
            
            Vector3 finalLocalScale = largeTreeVisual.transform.localScale;
            Vector3 finalWorldPos = largeTreeVisual.transform.position; // Keep original X/Z

            // Deactivate small tree now that we have its ground position
            smallTreeVisual.SetActive(false);

            // 3. Setup the growth loop
            float elapsed = 0f;
            while (elapsed < growthDuration)
            {
                elapsed += Time.deltaTime;
                float t = growthCurve.Evaluate(elapsed / growthDuration);
                
                // Scale proportionally
                largeTreeVisual.transform.localScale = finalLocalScale * t;

                // Position so the bottom stays at the small tree's floorY
                float currentWorldHeight = largeTreeVisual.transform.lossyScale.y;
                float newWorldY = floorY + (currentWorldHeight * 0.5f);
                
                largeTreeVisual.transform.position = new Vector3(
                    finalWorldPos.x,
                    newWorldY,
                    finalWorldPos.z
                );
                
                yield return null;
            }

            // 4. Final Snap
            float endT = growthCurve.Evaluate(1f);
            largeTreeVisual.transform.localScale = finalLocalScale * endT;
            float finalHeight = largeTreeVisual.transform.lossyScale.y;
            largeTreeVisual.transform.position = new Vector3(
                finalWorldPos.x,
                floorY + (finalHeight * 0.5f),
                finalWorldPos.z
            );
            
            Debug.Log("Large tree growth finished using small tree base");
        }
    }
}
