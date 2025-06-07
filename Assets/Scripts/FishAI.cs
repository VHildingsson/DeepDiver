using UnityEngine;
using System.Collections;

public class CaveFishAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float swimSpeed = 3f;           // Base swimming speed
    public float turnSpeed = 3f;           // How quickly the fish can turn
    public float minSwimTime = 1f;         // Minimum time between direction changes
    public float maxSwimTime = 4f;         // Maximum time between direction changes
    public float idleTime = 0.5f;          // Short pause sometimes at waypoints

    [Header("Obstacle Avoidance")]
    public float lookAheadDistance = 3f;   // How far to check for obstacles
    public float sideCastAngle = 30f;     // Angle for side obstacle checks
    public float avoidanceForce = 5f;      // How strongly to avoid obstacles
    public float minDistanceToWall = 0.5f; // Preferred distance from walls/props

    [Header("Path Following")]
    public float waypointRadius = 5f;      // How close to get to waypoint
    public float depthVariation = 2f;      // How much vertical movement is allowed

    private Vector3 currentDirection;
    private Vector3 targetDirection;
    private Vector3 currentWaypoint;
    private float directionChangeTimer;
    private bool isIdle;
    private float idleTimer;

    void Start()
    {
        currentDirection = transform.forward;
        targetDirection = GetNewDirection();
        currentWaypoint = FindNewWaypoint();
        directionChangeTimer = Random.Range(minSwimTime, maxSwimTime);
    }

    void Update()
    {
        if (isIdle)
        {
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0) isIdle = false;
            return;
        }

        directionChangeTimer -= Time.deltaTime;

        if (directionChangeTimer <= 0 || Vector3.Distance(transform.position, currentWaypoint) < waypointRadius)
        {
            /*if (Random.value < 0.1f) // 10% chance to idle briefly
            {
                isIdle = true;
                idleTimer = idleTime;
            }*/

            currentWaypoint = FindNewWaypoint();
            targetDirection = (currentWaypoint - transform.position).normalized;
            directionChangeTimer = Random.Range(minSwimTime, maxSwimTime);
        }

        // Enhanced obstacle avoidance
        AvoidObstacles();

        // Smooth direction change
        currentDirection = Vector3.Slerp(currentDirection, targetDirection, turnSpeed * Time.deltaTime);

        // Move forward
        transform.position += currentDirection * swimSpeed * Time.deltaTime;

        // Rotate to face direction (with natural-looking slight variation)
        if (currentDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(currentDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                targetRotation * Quaternion.Euler(0, 0, Mathf.Sin(Time.time * 2f) * 5f),
                turnSpeed * Time.deltaTime
            );
        }
    }

    Vector3 FindNewWaypoint()
    {
        // Try to find a direction that follows the cave system
        Vector3 waypointDirection = GetCaveFollowingDirection();

        // Add some randomness but bias toward forward movement
        waypointDirection += new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-0.5f, 0.5f),
            Random.Range(-0.5f, 1f) // Prefer forward movement
        ).normalized * 0.3f;

        waypointDirection.Normalize();

        // Find a point ahead in the cave system
        RaycastHit hit;
        if (Physics.Raycast(transform.position, waypointDirection, out hit, lookAheadDistance * 3f))
        {
            // Aim for a point near the wall but not too close
            return hit.point + hit.normal * minDistanceToWall;
        }

        // Fallback to random point ahead
        return transform.position + waypointDirection * lookAheadDistance * 2f;
    }

    Vector3 GetCaveFollowingDirection()
    {
        // Cast rays in multiple directions to determine cave shape
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;
        Vector3 up = transform.up;

        float[] distances = new float[5];
        Vector3[] directions = new Vector3[5] {
            forward,
            Quaternion.AngleAxis(sideCastAngle, up) * forward,
            Quaternion.AngleAxis(-sideCastAngle, up) * forward,
            Quaternion.AngleAxis(sideCastAngle, right) * forward,
            Quaternion.AngleAxis(-sideCastAngle, right) * forward
        };

        // Find the most open path
        float maxDistance = 0;
        Vector3 bestDirection = forward;

        for (int i = 0; i < directions.Length; i++)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, directions[i], out hit, lookAheadDistance))
            {
                distances[i] = hit.distance;
                // Prefer directions that have more space
                if (hit.distance > maxDistance)
                {
                    maxDistance = hit.distance;
                    bestDirection = directions[i];
                }
            }
            else
            {
                // No hit means open space - prefer this direction
                return directions[i];
            }
        }

        return bestDirection;
    }

    void AvoidObstacles()
    {
        RaycastHit hit;
        Vector3 avoidanceVector = Vector3.zero;
        float weightSum = 0f;

        // Check multiple directions
        Vector3[] checkDirections = new Vector3[] {
            transform.forward,
            transform.forward + transform.right * 0.5f,
            transform.forward - transform.right * 0.5f,
            transform.forward + transform.up * 0.3f,
            transform.forward - transform.up * 0.3f
        };

        foreach (Vector3 dir in checkDirections)
        {
            if (Physics.Raycast(transform.position, dir, out hit, lookAheadDistance))
            {
                float weight = 1f - (hit.distance / lookAheadDistance);
                avoidanceVector += hit.normal * weight;
                weightSum += weight;
            }
        }

        if (weightSum > 0)
        {
            avoidanceVector /= weightSum;
            targetDirection = Vector3.Lerp(
                targetDirection,
                avoidanceVector.normalized,
                avoidanceForce * Time.deltaTime
            ).normalized;
        }
    }

    Vector3 GetNewDirection()
    {
        // Try to follow the cave system
        Vector3 caveDir = GetCaveFollowingDirection();

        // Add some variation
        return (caveDir + Random.insideUnitSphere * 0.3f).normalized;
    }

    void OnDrawGizmosSelected()
    {
        // Draw waypoint
        Gizmos.color = Color.green;
        Gizmos.DrawSphere(currentWaypoint, 0.2f);
        Gizmos.DrawLine(transform.position, currentWaypoint);

        // Draw movement direction
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, currentDirection * 2f);

        // Draw obstacle detection
        Gizmos.color = Color.yellow;
        Vector3[] checkDirections = new Vector3[] {
            transform.forward,
            transform.forward + transform.right * 0.5f,
            transform.forward - transform.right * 0.5f,
            transform.forward + transform.up * 0.3f,
            transform.forward - transform.up * 0.3f
        };

        foreach (Vector3 dir in checkDirections)
        {
            Gizmos.DrawRay(transform.position, dir * lookAheadDistance);
        }
    }
}
