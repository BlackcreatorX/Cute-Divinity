using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("Patrol")]
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] private float patrolSpeed = 2f;
    [SerializeField] private float waitAtPoint = 1f;
    [SerializeField] private float waypointTolerance = 0.4f;

    [Header("Vision")]
    [SerializeField] private ThirdPersonController player;
    [SerializeField] private float visionDistance = 10f;
    [SerializeField, Range(0f, 360f)] private float visionAngle = 110f;
    [SerializeField] private float eyeHeight = 1.6f;
    [SerializeField] private float playerTargetHeight = 1f;
    [SerializeField] private LayerMask visionBlockers = ~0;

    [Header("Chase")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float catchDistance = 1.2f;
    [SerializeField] private string screamerID = "1";

    private NavMeshAgent agent;
    private int currentPatrolPoint;
    private bool isChasing;
    private bool isWaitingAtPoint;
    private bool hasCaughtPlayer;
    private float waitTimer;
    private float nextChaseDestinationUpdate;
    private bool warnedAboutNavMesh;
    private bool warnedAboutDestination;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        patrolPoints.RemoveAll(point => point == null);

        if (player == null)
        {
            player = FindFirstObjectByType<ThirdPersonController>();
        }
    }

    private void Start()
    {
        agent.speed = patrolSpeed;

        if (player == null)
        {
            Debug.LogWarning("EnemyController could not find a ThirdPersonController.", this);
        }

        if (patrolPoints.Count == 0)
        {
            Debug.LogWarning("EnemyController has no patrol points assigned.", this);
            return;
        }

        if (CanUseAgent())
        {
            SetPatrolDestination();
        }
    }

    private void Update()
    {
        if (!CanUseAgent())
        {
            return;
        }

        if (player != null && CanSeePlayer())
        {
            if (!isChasing)
            {
                nextChaseDestinationUpdate = 0f;
            }

            isChasing = true;
            isWaitingAtPoint = false;
            ChasePlayer();
            return;
        }

        if (isChasing)
        {
            ReturnToPatrol();
        }

        UpdatePatrol();
    }

    private bool CanSeePlayer()
    {
        Vector3 eyePosition = transform.position + Vector3.up * eyeHeight;
        Vector3 targetPosition = player.transform.position + Vector3.up * playerTargetHeight;
        Vector3 direction = targetPosition - eyePosition;
        float distance = direction.magnitude;

        if (distance > visionDistance || distance <= Mathf.Epsilon)
        {
            return false;
        }

        Vector3 flatDirection = Vector3.ProjectOnPlane(direction, Vector3.up);
        if (flatDirection.sqrMagnitude > Mathf.Epsilon &&
            Vector3.Angle(transform.forward, flatDirection) > visionAngle * 0.5f)
        {
            return false;
        }

        if (Physics.Raycast(
            eyePosition,
            direction / distance,
            out RaycastHit hit,
            distance,
            visionBlockers,
            QueryTriggerInteraction.Ignore
        ))
        {
            return hit.transform.root == player.transform.root;
        }

        return true;
    }

    private void ChasePlayer()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.transform.position);
        if (distanceToPlayer <= catchDistance)
        {
            agent.isStopped = true;
            if (!hasCaughtPlayer)
            {
                hasCaughtPlayer = true;
                if (ScreamerManager.Instance != null)
                {
                    ScreamerManager.Instance.PlayScreamer(screamerID);
                }
                else
                {
                    Debug.LogWarning(
                        $"Cannot play screamer '{screamerID}': no ScreamerManager is active.",
                        this
                    );
                }
            }

            return;
        }

        hasCaughtPlayer = false;
        agent.speed = chaseSpeed;
        agent.isStopped = false;
        if (Time.time >= nextChaseDestinationUpdate)
        {
            SetDestination(player.transform.position);
            nextChaseDestinationUpdate = Time.time + 0.2f;
        }
    }

    private void ReturnToPatrol()
    {
        isChasing = false;
        hasCaughtPlayer = false;
        agent.speed = patrolSpeed;
        agent.isStopped = false;

        if (patrolPoints.Count > 0)
        {
            SetPatrolDestination();
        }
    }

    private void UpdatePatrol()
    {
        if (patrolPoints.Count == 0)
        {
            return;
        }

        if (isWaitingAtPoint)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaitingAtPoint = false;
                currentPatrolPoint = (currentPatrolPoint + 1) % patrolPoints.Count;
                SetPatrolDestination();
            }

            return;
        }

        if (!agent.pathPending && agent.remainingDistance <= waypointTolerance)
        {
            isWaitingAtPoint = true;
            waitTimer = waitAtPoint;
            agent.isStopped = true;
        }
    }

    private void SetPatrolDestination()
    {
        agent.isStopped = false;
        SetDestination(patrolPoints[currentPatrolPoint].position);
    }

    private void SetDestination(Vector3 destination)
    {
        if (agent.SetDestination(destination))
        {
            warnedAboutDestination = false;
            return;
        }

        if (!warnedAboutDestination)
        {
            Debug.LogWarning(
                "EnemyController could not set a NavMesh destination. Check the NavMesh and target position.",
                this
            );
            warnedAboutDestination = true;
        }
    }

    private bool CanUseAgent()
    {
        if (agent.enabled && agent.isOnNavMesh)
        {
            return true;
        }

        if (!warnedAboutNavMesh)
        {
            Debug.LogWarning(
                "EnemyController needs an enabled NavMeshAgent placed on a baked NavMesh.",
                this
            );
            warnedAboutNavMesh = true;
        }

        return false;
    }
}
