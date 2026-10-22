using System;
using UnityEngine;

public sealed class BallLocomotion : MonoBehaviour
{
    public event Action BlockedTooLong;

    [SerializeField] private Transform ball;
    [SerializeField] private HeadDirectionSweep directionIndicator;
    [SerializeField] private Transform gridBoard;
    [SerializeField, Min(0f)] private float moveSpeed = 2.5f;
    [SerializeField, Min(0.1f)] private float blockedDuration = 5f;

    private SphereCollider ballCollider;
    private BoxCollider[] floorColliders;
    private FloorBreak[] floorBreaks;
    private FloorBreak currentFloor;
    private float blockedSeconds;

    private void OnEnable()
    {
        if (ball == null || ball.parent != transform ||
            directionIndicator == null || gridBoard == null)
        {
            Debug.LogError("BallLocomotion needs the ball, direction indicator, and grid board.", this);
            enabled = false;
            return;
        }

        ballCollider = ball.GetComponent<SphereCollider>();
        if (ballCollider == null || !CacheFloors())
        {
            Debug.LogError("The ball needs a SphereCollider and each floor needs a BoxCollider and FloorBreak.", this);
            enabled = false;
            return;
        }

        blockedSeconds = 0f;
        currentFloor = null;
        currentFloor = FindFloorAt(ball.position);
        if (currentFloor == null)
        {
            Debug.LogError("The ball must start on an active floor.", this);
            enabled = false;
            return;
        }

        currentFloor.PlayBreak();
    }

    private void Update()
    {
        Vector3 direction = directionIndicator.CurrentDirection;
        direction.y = 0f;
        bool moved = false;
        if (direction.sqrMagnitude >= 0.000001f)
        {
            direction.Normalize();
            float remaining = moveSpeed * Time.deltaTime;
            float step = Mathf.Max(0.02f, ballCollider.bounds.extents.x * 0.25f);

            while (remaining > 0f)
            {
                float distance = Mathf.Min(step, remaining);
                Vector3 nextPosition = ball.position + direction * distance;
                FloorBreak nextFloor = FindFloorAt(nextPosition);
                if (nextFloor == null)
                    break;

                transform.position += direction * distance;
                moved = true;
                if (nextFloor != currentFloor)
                {
                    currentFloor = nextFloor;
                    currentFloor.PlayBreak();
                }

                remaining -= distance;
            }

            // Turning in place lets the ball steer away from a blocked edge.
            ball.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        blockedSeconds = moved ? 0f : blockedSeconds + Time.deltaTime;
        if (blockedSeconds < blockedDuration)
            return;

        enabled = false;
        BlockedTooLong?.Invoke();
    }

    private bool CacheFloors()
    {
        floorBreaks = gridBoard.GetComponentsInChildren<FloorBreak>();
        if (floorBreaks.Length == 0)
            return false;

        floorColliders = new BoxCollider[floorBreaks.Length];
        for (int i = 0; i < floorBreaks.Length; i++)
        {
            floorColliders[i] = floorBreaks[i].GetComponentInParent<BoxCollider>();
            if (floorColliders[i] == null)
                return false;
        }

        return true;
    }

    private FloorBreak FindFloorAt(Vector3 position)
    {
        FloorBreak nearest = null;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < floorColliders.Length; i++)
        {
            BoxCollider floor = floorColliders[i];
            FloorBreak floorBreak = floorBreaks[i];
            if (floor == null || !floor.enabled || !floor.gameObject.activeInHierarchy ||
                floorBreak == null || (floorBreak.IsBreaking && floorBreak != currentFloor))
                continue;

            Bounds bounds = floor.bounds;
            if (position.x < bounds.min.x || position.x >= bounds.max.x ||
                position.z < bounds.min.z || position.z >= bounds.max.z)
                continue;

            if (!Physics.ComputePenetration(
                    ballCollider, position, ball.rotation,
                    floor, floor.transform.position, floor.transform.rotation,
                    out _, out _))
                continue;

            float dx = position.x - bounds.center.x;
            float dz = position.z - bounds.center.z;
            float distance = dx * dx + dz * dz;
            if (distance >= nearestDistance)
                continue;

            nearest = floorBreak;
            nearestDistance = distance;
        }

        return nearest;
    }
}
