using UnityEngine;

public class EnergyBall : MonoBehaviour
{
    [Header("Settings")]
    public string treeTag = "Tree";
    
    [Header("Visuals")]
    public GameObject impactEffectPrefab;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(treeTag))
        {
            GrowingTree tree = other.GetComponent<GrowingTree>();
            if (tree != null && !tree.IsGrown)
            {
                tree.StartGrowth();
                
                if (impactEffectPrefab != null)
                {
                    Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
                }
                
                // Destroy the energy ball after it's been used
                Destroy(gameObject);
            }
        }
    }
}
