using UnityEngine;

public sealed class InchwormLocomotion : MonoBehaviour
{
    [Header("Segments")]
    [SerializeField] private Transform head;
    [SerializeField] private Transform middleFront;
    [SerializeField] private Transform middleBack;
    [SerializeField] private Transform tail;
    [SerializeField] private HeadDirectionSweep directionIndicator;

    [Header("Board")]
    [SerializeField] private Transform gridBoard;

    [Header("Motion")]
    [SerializeField, Min(0.05f)] private float gatherDuration = 0.6f;
    [SerializeField, Min(0.05f)] private float stretchDuration = 0.8f;
    [SerializeField, Range(0.1f, 0.95f)] private float compressedLengthFraction = 0.7f;
    [SerializeField, Min(0f)] private float archHeight = 0.35f;

    private enum Phase { Gather, Stretch }

    private Phase phase;
    private float phaseTime;
    private float restLength;
    private float frontFraction;
    private float backFraction;
    private Vector3 headStart;
    private Vector3 headTarget;
    private Vector3 tailStart;
    private Vector3 tailTarget;
    private Quaternion headRotationStart;
    private Quaternion headRotationTarget;
    private SphereCollider headCollider;
    private BoxCollider[] floorColliders;
    private FloorBreak[] floorBreaks;
    private FloorBreak currentFloor;

    private void OnEnable()
    {
        if (head == null || middleFront == null || middleBack == null || tail == null ||
            directionIndicator == null || gridBoard == null || head.parent != transform)
        {
            Debug.LogError("InchwormLocomotion needs the segments, direction indicator, and grid board.", this);
            enabled = false;
            return;
        }

        headCollider = head.GetComponent<SphereCollider>();
        if (headCollider == null || !CacheFloors())
        {
            Debug.LogError("The head needs a SphereCollider and each floor needs a BoxCollider and FloorBreak.", this);
            enabled = false;
            return;
        }

        Vector3 initialHead = head.position;
        Vector3 initialFront = middleFront.position;
        Vector3 initialBack = middleBack.position;
        Vector3 initialTail = tail.position;

        transform.position = initialHead;
        head.position = initialHead;
        middleFront.position = initialFront;
        middleBack.position = initialBack;
        tail.position = initialTail;

        Vector3 body = initialTail - initialHead;
        body.y = 0f;
        restLength = body.magnitude;
        if (restLength < 0.001f)
        {
            Debug.LogError("The inchworm's head and tail must start apart.", this);
            enabled = false;
            return;
        }

        float lengthSquared = restLength * restLength;
        frontFraction = Mathf.Clamp01(Vector3.Dot(initialFront - initialHead, body) / lengthSquared);
        backFraction = Mathf.Clamp01(Vector3.Dot(initialBack - initialHead, body) / lengthSquared);
        currentFloor = null;
        currentFloor = FindFloorAt(head.position);
        if (currentFloor == null)
        {
            Debug.LogError("The inchworm's head must start on an active floor.", this);
            enabled = false;
            return;
        }

        currentFloor.PlayBreak();
        BeginGather();
    }

    private void LateUpdate()
    {
        float duration = Mathf.Max(0.05f, phase == Phase.Gather ? gatherDuration : stretchDuration);
        phaseTime = Mathf.Min(phaseTime + Time.deltaTime, duration);
        float t = phaseTime / duration;
        float smoothT = t * t * (3f - 2f * t);

        if (phase == Phase.Gather)
        {
            transform.position = headStart;
            tail.position = Vector3.Lerp(tailStart, tailTarget, smoothT);
            PositionMiddleSegments(smoothT);
        }
        else
        {
            transform.position = Vector3.Lerp(headStart, headTarget, smoothT);
            tail.position = tailStart;
            head.rotation = Quaternion.Slerp(headRotationStart, headRotationTarget, smoothT);
            PositionMiddleSegments(1f - smoothT);
            UpdateCurrentFloor();
        }

        if (phaseTime >= duration)
        {
            if (phase == Phase.Gather)
                BeginStretch();
            else
                BeginGather();
        }
    }

    private void BeginGather()
    {
        phase = Phase.Gather;
        phaseTime = 0f;
        headStart = head.position;
        tailStart = tail.position;

        Vector3 towardHead = headStart - tailStart;
        towardHead.y = 0f;
        float currentLength = towardHead.magnitude;
        float travel = Mathf.Max(0f, currentLength - restLength * compressedLengthFraction);
        tailTarget = tailStart + towardHead.normalized * travel;
    }

    private void BeginStretch()
    {
        phase = Phase.Stretch;
        phaseTime = 0f;
        headStart = head.position;
        tailStart = tail.position;

        Vector3 heading = directionIndicator.CurrentDirection;
        heading.y = 0f;
        if (heading.sqrMagnitude < 0.000001f)
            heading = headStart - tailStart;
        heading.Normalize();

        Vector3 fromTail = headStart - tailStart;
        fromTail.y = 0f;
        float alongHeading = Vector3.Dot(fromTail, heading);
        float discriminant = alongHeading * alongHeading + restLength * restLength - fromTail.sqrMagnitude;
        float travel = Mathf.Max(0f, -alongHeading + Mathf.Sqrt(Mathf.Max(0f, discriminant)));
        travel = MaxTravelOnFloors(headStart, heading, travel);
        headTarget = headStart + heading * travel;

        headRotationStart = head.rotation;
        Vector3 finalBodyDirection = headTarget - tailStart;
        finalBodyDirection.y = 0f;
        headRotationTarget = Quaternion.LookRotation(finalBodyDirection.normalized, Vector3.up);
    }

    private void PositionMiddleSegments(float archAmount)
    {
        Vector3 body = tail.position - head.position;
        middleFront.position = head.position + body * frontFraction +
            Vector3.up * (Mathf.Sin(Mathf.PI * frontFraction) * archHeight * archAmount);
        middleBack.position = head.position + body * backFraction +
            Vector3.up * (Mathf.Sin(Mathf.PI * backFraction) * archHeight * archAmount);
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

    private float MaxTravelOnFloors(Vector3 origin, Vector3 direction, float travel)
    {
        if (travel <= 0f)
            return 0f;

        // Check the whole route so a single stretch cannot jump over an empty floor.
        float step = Mathf.Max(0.02f, headCollider.bounds.extents.x * 0.25f);
        float lastSafe = 0f;
        for (float distance = step; distance < travel; distance += step)
        {
            if (FindFloorAt(origin + direction * distance) == null)
                return lastSafe;
            lastSafe = distance;
        }

        return FindFloorAt(origin + direction * travel) == null ? lastSafe : travel;
    }

    private void UpdateCurrentFloor()
    {
        FloorBreak nextFloor = FindFloorAt(head.position);
        if (nextFloor == null || nextFloor == currentFloor)
            return;

        currentFloor = nextFloor;
        currentFloor.PlayBreak();
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
                    headCollider, position, head.rotation,
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
