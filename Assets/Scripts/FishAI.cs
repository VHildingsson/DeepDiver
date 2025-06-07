using UnityEngine;
using System.Collections;

public class FishAI : MonoBehaviour
{
    [Header("Movement Settings")]
    public float swimSpeed = 3f;          // Base swimming speed
    public float turnSpeed = 2f;          // How quickly the fish can turn
    public float minSwimTime = 2f;        // Minimum time between direction changes
    public float maxSwimTime = 5f;        // Maximum time between direction changes
    public float obstacleAvoidDistance = 2f; // Distance to check for obstacles
    public float obstacleAvoidForce = 5f;  // How strongly to avoid obstacles

    [Header("Wandering Settings")]
    public float wanderRadius = 10f;       // Area the fish will stay within
    public float wanderJitter = 1f;       // Randomness in wandering
    public float depthVariation = 3f;      // How much the fish can vary its depth

    private Vector3 spawnPosition;         // Where the fish was spawned
    private Vector3 currentDirection;      // Current swimming direction
    private Vector3 targetDirection;       // Direction the fish wants to go
    private float directionChangeTimer;    // Timer for direction changes
    private float currentSwimTime;         // Current time until next direction change

    private void Start()
    {
        spawnPosition = transform.position;
        currentDirection = transform.forward;
        targetDirection = GetRandomDirection();
        SetNewSwimTime();
    }

    private void Update()
    {
        // Count down to next direction change
        directionChangeTimer -= Time.deltaTime;

        if (directionChangeTimer <= 0)
        {
            // Get a new direction when timer runs out
            targetDirection = GetRandomDirection();
            SetNewSwimTime();
        }

        // Avoid obstacles
        AvoidObstacles();

        // Smoothly turn toward target direction
        currentDirection = Vector3.Slerp(currentDirection, targetDirection, turnSpeed * Time.deltaTime);

        // Move forward
        transform.Translate(Vector3.forward * swimSpeed * Time.deltaTime);

        // Rotate to face direction (with slight tilt for more natural movement)
        Quaternion lookRotation = Quaternion.LookRotation(currentDirection);
        Quaternion tilt = Quaternion.Euler(Random.Range(-5f, 5f), 0, Random.Range(-5f, 5f));
        transform.rotation = Quaternion.Slerp(transform.rotation, lookRotation * tilt, turnSpeed * Time.deltaTime);
    }

    private Vector3 GetRandomDirection()
    {
        // Create a somewhat random direction within the wander radius
        Vector3 randomPoint = spawnPosition + Random.insideUnitSphere * wanderRadius;

        // Add some depth variation
        randomPoint.y = spawnPosition.y + Random.Range(-depthVariation, depthVariation);

        // Add some wandering jitter to current direction
        Vector3 direction = (randomPoint - transform.position).normalized;
        direction += new Vector3(
            Random.Range(-wanderJitter, wanderJitter),
            Random.Range(-wanderJitter / 2f, wanderJitter / 2f),
            Random.Range(-wanderJitter, wanderJitter)
        );

        return direction.normalized;
    }

    private void AvoidObstacles()
    {
        RaycastHit hit;

        // Check for obstacles in front
        if (Physics.Raycast(transform.position, transform.forward, out hit, obstacleAvoidDistance))
        {
            // Calculate avoidance direction
            Vector3 avoidDirection = Vector3.Reflect(transform.forward, hit.normal);
            avoidDirection.y = 0; // Keep mostly horizontal
            avoidDirection.Normalize();

            // Add some randomness
            avoidDirection += new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(-0.2f, 0.5f),
                Random.Range(-0.5f, 0.5f)
            );
            avoidDirection.Normalize();

            // Blend the avoidance direction with current target
            targetDirection = Vector3.Lerp(targetDirection, avoidDirection, obstacleAvoidForce * Time.deltaTime);
            targetDirection.Normalize();

            // Change direction sooner if we hit something
            directionChangeTimer *= 0.5f;
        }
    }

    private void SetNewSwimTime()
    {
        directionChangeTimer = Random.Range(minSwimTime, maxSwimTime);
        currentSwimTime = directionChangeTimer;
    }

    // Visualize wander radius and obstacle detection in editor
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(spawnPosition, wanderRadius);
        }
        else
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, wanderRadius);
        }

        // Draw obstacle detection
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, transform.forward * obstacleAvoidDistance);
    }
}
