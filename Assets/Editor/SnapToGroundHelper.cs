using UnityEngine;
using UnityEditor;

public class SnapToGroundHelper
{
    [MenuItem("Tools/Snap Selected to Ground %g")] // Shortcut: Ctrl + G on Windows
    public static void SnapSelectedToGround()
    {
        Transform[] selectedTransforms = Selection.transforms;
        if (selectedTransforms.Length == 0)
        {
            Debug.LogWarning("No objects selected to snap. Select objects in the Scene or Hierarchy view first.");
            return;
        }

        int snappedCount = 0;
        foreach (var obj in selectedTransforms)
        {
            Undo.RecordObject(obj, "Snap to Ground");

            // Calculate bottom offset of the object based on its collider or renderer bounds
            float bottomOffset = 0f;
            var collider = obj.GetComponentInChildren<Collider>();
            var renderer = obj.GetComponentInChildren<Renderer>();

            if (collider != null)
            {
                bottomOffset = obj.position.y - collider.bounds.min.y;
            }
            else if (renderer != null)
            {
                bottomOffset = obj.position.y - renderer.bounds.min.y;
            }

            // Raycast downwards from slightly above the object to detect colliders under it
            RaycastHit hit;
            Vector3 origin = obj.position + Vector3.up * 5f; // Offset by 5 meters up in case it is already buried
            
            // Ignore the object's own colliders during raycast by disabling them temporarily
            bool wasColliderEnabled = collider != null && collider.enabled;
            if (collider != null) collider.enabled = false;

            if (Physics.Raycast(origin, Vector3.down, out hit, 500f))
            {
                obj.position = new Vector3(obj.position.x, hit.point.y + bottomOffset, obj.position.z);
                snappedCount++;
            }
            else
            {
                Debug.LogWarning($"Could not find any ground/collider under {obj.name} within 500 meters.");
            }

            // Restore collider state
            if (collider != null) collider.enabled = wasColliderEnabled;
        }

        if (snappedCount > 0)
        {
            Debug.Log($"Successfully snapped {snappedCount} objects to the ground.");
        }
    }
}
