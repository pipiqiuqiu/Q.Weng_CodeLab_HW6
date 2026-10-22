using System.Collections.Generic;
using UnityEngine;

public sealed class GridBoardRandomizer : MonoBehaviour
{
    [SerializeField, Min(0)] private int floorsToRemove = 5;
    [SerializeField] private Transform head;
    [SerializeField] private Transform middleFront;
    [SerializeField] private Transform middleBack;
    [SerializeField] private Transform tail;

    public bool RemoveRandomFloors()
    {
        if (head == null || middleFront == null || middleBack == null || tail == null)
        {
            Debug.LogError("GridBoardRandomizer needs all four worm segments.", this);
            return false;
        }

        var candidates = new List<GameObject>();
        FloorBreak[] floorBreaks = GetComponentsInChildren<FloorBreak>();
        foreach (FloorBreak floorBreak in floorBreaks)
        {
            BoxCollider collider = floorBreak.GetComponentInParent<BoxCollider>();
            if (collider == null)
            {
                Debug.LogError("A grid floor is missing its BoxCollider.", floorBreak);
                return false;
            }

            Bounds bounds = collider.bounds;
            if (ContainsXZ(bounds, head.position) ||
                ContainsXZ(bounds, middleFront.position) ||
                ContainsXZ(bounds, middleBack.position) ||
                ContainsXZ(bounds, tail.position))
                continue;

            candidates.Add(collider.gameObject);
        }

        int count = Mathf.Min(floorsToRemove, candidates.Count);
        for (int i = 0; i < count; i++)
        {
            int index = Random.Range(0, candidates.Count);
            GameObject floor = candidates[index];
            candidates[index] = candidates[candidates.Count - 1];
            candidates.RemoveAt(candidates.Count - 1);

            // Disable its collider immediately; Destroy completes at the end of the frame.
            floor.SetActive(false);
            Destroy(floor);
        }

        return true;
    }

    private static bool ContainsXZ(Bounds bounds, Vector3 point)
    {
        return point.x >= bounds.min.x && point.x < bounds.max.x &&
               point.z >= bounds.min.z && point.z < bounds.max.z;
    }
}
