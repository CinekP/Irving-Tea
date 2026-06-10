using UnityEngine;

public class EnergyBall : MonoBehaviour
{
    [Header("Settings")]
    public string treeTag = "Tree";
    
    [Header("Visuals")]
    public GameObject impactEffectPrefab;

    private void OnTriggerEnter(Collider other)
    {
        bool isTree = other.CompareTag(treeTag) || 
                      (other.transform.parent != null && other.transform.parent.CompareTag(treeTag));
        if (isTree)
        {
            GrowingTree tree = other.GetComponentInParent<GrowingTree>();
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
