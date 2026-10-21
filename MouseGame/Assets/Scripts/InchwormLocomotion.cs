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
    [SerializeField, Min(0f)] private float edgePadding = 0.6f;
    [SerializeField] private Material inMaterial;
    [SerializeField] private Material damageMaterial;

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
    private float minX;
    private float maxX;
    private float minZ;
    private float maxZ;
    private MeshRenderer[] tiles;
    private bool[] damagedTiles;
    private int currentTile = -1;

    private void OnEnable()
    {
        if (head == null || middleFront == null || middleBack == null || tail == null ||
            directionIndicator == null || gridBoard == null || inMaterial == null ||
            damageMaterial == null || head.parent != transform)
        {
            Debug.LogError("InchwormLocomotion needs the segments, direction indicator, grid board, and tile materials.", this);
            enabled = false;
            return;
        }

        if (!SetBoardLimits())
        {
            enabled = false;
            return;
        }

        Vector3 initialHead = head.position;
        Vector3 initialFront = middleFront.position;
        Vector3 initialBack = middleBack.position;
        Vector3 initialTail = tail.position;

        // Keep the worm root at the head so its position follows each forward stroke.
        transform.position = initialHead;
        head.position = initialHead;
        middleFront.position = initialFront;
        middleBack.position = initialBack;
        tail.position = initialTail;

        if (!FitWormInsideBoard())
        {
            enabled = false;
            return;
        }

        initialHead = head.position;
        initialFront = middleFront.position;
        initialBack = middleBack.position;
        initialTail = tail.position;

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
        damagedTiles = new bool[tiles.Length];
        currentTile = -1;
        UpdateCurrentTile();
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
            UpdateCurrentTile();
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

        // Extend along the selected direction until the head-tail spacing is restored.
        Vector3 fromTail = headStart - tailStart;
        fromTail.y = 0f;
        float alongHeading = Vector3.Dot(fromTail, heading);
        float discriminant = alongHeading * alongHeading + restLength * restLength - fromTail.sqrMagnitude;
        float travel = Mathf.Max(0f, -alongHeading + Mathf.Sqrt(Mathf.Max(0f, discriminant)));
        travel = Mathf.Min(travel, MaxTravelInsideBoard(headStart, heading));
        travel = MaxTravelBeforeDamage(headStart, heading, travel);
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

    private bool SetBoardLimits()
    {
        tiles = gridBoard.GetComponentsInChildren<MeshRenderer>();
        if (tiles.Length == 0)
        {
            Debug.LogError("The grid board has no cube tiles.", this);
            return false;
        }

        Bounds bounds = tiles[0].bounds;
        for (int i = 1; i < tiles.Length; i++)
            bounds.Encapsulate(tiles[i].bounds);

        minX = bounds.min.x + edgePadding;
        maxX = bounds.max.x - edgePadding;
        minZ = bounds.min.z + edgePadding;
        maxZ = bounds.max.z - edgePadding;
        if (minX >= maxX || minZ >= maxZ)
        {
            Debug.LogError("The grid is too small for the worm's edge padding.", this);
            return false;
        }

        return true;
    }

    private bool FitWormInsideBoard()
    {
        float minBodyX = Mathf.Min(head.position.x, tail.position.x);
        float maxBodyX = Mathf.Max(head.position.x, tail.position.x);
        float minBodyZ = Mathf.Min(head.position.z, tail.position.z);
        float maxBodyZ = Mathf.Max(head.position.z, tail.position.z);
        float minShiftX = minX - minBodyX;
        float maxShiftX = maxX - maxBodyX;
        float minShiftZ = minZ - minBodyZ;
        float maxShiftZ = maxZ - maxBodyZ;
        if (minShiftX > maxShiftX || minShiftZ > maxShiftZ)
        {
            Debug.LogError("The worm is too long to fit inside the grid.", this);
            return false;
        }

        transform.position += new Vector3(
            Mathf.Clamp(0f, minShiftX, maxShiftX),
            0f,
            Mathf.Clamp(0f, minShiftZ, maxShiftZ));
        return true;
    }

    private float MaxTravelInsideBoard(Vector3 origin, Vector3 direction)
    {
        float travel = float.PositiveInfinity;
        if (direction.x > 0.000001f)
            travel = Mathf.Min(travel, (maxX - origin.x) / direction.x);
        else if (direction.x < -0.000001f)
            travel = Mathf.Min(travel, (minX - origin.x) / direction.x);

        if (direction.z > 0.000001f)
            travel = Mathf.Min(travel, (maxZ - origin.z) / direction.z);
        else if (direction.z < -0.000001f)
            travel = Mathf.Min(travel, (minZ - origin.z) / direction.z);

        return Mathf.Max(0f, travel);
    }

    private void UpdateCurrentTile()
    {
        int nextTile = FindTile(head.position);
        if (nextTile < 0 || nextTile == currentTile || damagedTiles[nextTile])
            return;

        if (currentTile >= 0)
        {
            damagedTiles[currentTile] = true;
            tiles[currentTile].sharedMaterial = damageMaterial;
        }

        currentTile = nextTile;
        tiles[currentTile].sharedMaterial = inMaterial;
    }

    private int FindTile(Vector3 position)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            Bounds bounds = tiles[i].bounds;
            if (position.x >= bounds.min.x && position.x < bounds.max.x &&
                position.z >= bounds.min.z && position.z < bounds.max.z)
                return i;
        }

        return -1;
    }

    private float MaxTravelBeforeDamage(Vector3 origin, Vector3 direction, float travel)
    {
        for (int i = 0; i < tiles.Length; i++)
        {
            if (!damagedTiles[i] || !TryEnterTile(origin, direction, tiles[i].bounds, out float entry))
                continue;

            if (entry <= travel)
                travel = Mathf.Max(0f, entry - 0.001f);
        }

        return travel;
    }

    private static bool TryEnterTile(Vector3 origin, Vector3 direction, Bounds bounds, out float entry)
    {
        float near = float.NegativeInfinity;
        float far = float.PositiveInfinity;

        if (Mathf.Abs(direction.x) < 0.000001f)
        {
            if (origin.x < bounds.min.x || origin.x >= bounds.max.x)
            {
                entry = 0f;
                return false;
            }
        }
        else
        {
            float a = (bounds.min.x - origin.x) / direction.x;
            float b = (bounds.max.x - origin.x) / direction.x;
            near = Mathf.Max(near, Mathf.Min(a, b));
            far = Mathf.Min(far, Mathf.Max(a, b));
        }

        if (Mathf.Abs(direction.z) < 0.000001f)
        {
            if (origin.z < bounds.min.z || origin.z >= bounds.max.z)
            {
                entry = 0f;
                return false;
            }
        }
        else
        {
            float a = (bounds.min.z - origin.z) / direction.z;
            float b = (bounds.max.z - origin.z) / direction.z;
            near = Mathf.Max(near, Mathf.Min(a, b));
            far = Mathf.Min(far, Mathf.Max(a, b));
        }

        entry = Mathf.Max(0f, near);
        return far > entry + 0.000001f;
    }
}
