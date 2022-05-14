using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent)), RequireComponent(typeof(NavMeshObstacle))]
public class NavMeshPassenger : MonoBehaviour
{
    [SerializeField]
    NavMeshAgent m_Agent;
    [SerializeField]
    NavMeshObstacle m_Obstacle;

    public float TimeToStow { get; set; } = 1f;
    public bool IsStowing { get; private set; } = false;
    public bool HasStowedOnce { get; private set; } = false;

    [SerializeField]
    Transform m_Goal;
    public string GoalName { get; private set; } = "";
    public string RowName { get; private set; } = "";

    public MeshRenderer meshRenderer = null;
    Material m_OriginalMaterial = null;
    public Material stowingMaterial = null;
    public float QueuingDistance;

    //NavMeshPassenger[] activePassengers;
    public bool InQueue { get; set; } = false;
    public bool ShouldBeQueued { get; set; } = false;
    public Vector3 backOfPlanePosition;
    public float nearbyRadius = 1.5f;
    public bool HasLeftSeat { get; set; } = false;

    public bool PathCompleted
    {
        get
        {
            if (IsStowing || !m_Agent.enabled || m_Goal == null)
                return false;

            // Reference: http://answers.unity.com/answers/746157/view.html
            if (!m_Agent.pathPending)
            {
                if (m_Agent.remainingDistance <= m_Agent.stoppingDistance)
                {
                    if (!m_Agent.hasPath || m_Agent.velocity.sqrMagnitude == 0f)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }

    void Awake()
    {
        m_Agent = GetComponent<NavMeshAgent>();
        m_Obstacle = GetComponent<NavMeshObstacle>();
        m_Obstacle.enabled = false;

        if (meshRenderer != null)
        {
            m_OriginalMaterial = meshRenderer.material;
        }
    }

    public void DisableNavMeshAgent()
    {
        if (!IsStowing)
            m_Agent.enabled = false;
    }

    public void EnableNavMeshAgent()
    {
        if (!IsStowing)
            m_Agent.enabled = true;
    }

    public void SetGoal(string postFix)
    {
        GoalName = "Goal" + postFix;
        RowName = "Row" + postFix;
        RowName = RowName.Substring(0, RowName.Length - 1); // Strip the trailing letter

        GameObject goal = GameObject.Find(GoalName);
        if (goal == null)
        {
            Debug.LogError("Goal named \"" + GoalName + "\" not found");
            return;
        }

        m_Goal = goal.transform;
        NavMeshPath path = new NavMeshPath();
        NavMesh.CalculatePath(transform.position, m_Goal.position, NavMesh.AllAreas, path);
        m_Agent.SetPath(path);
    }

    public void SetGoal(Transform goal)
    {
        m_Goal = goal;
        GoalName = m_Goal.name;
        RowName = "Row" + goal.name.Substring("Goal".Length);
        RowName = RowName.Substring(0, RowName.Length - 1); // Strip the trailing letter
        
        NavMeshPath path = new NavMeshPath();
        NavMesh.CalculatePath(transform.position, m_Goal.position, NavMesh.AllAreas, path);
        m_Agent.SetPath(path);
    }

    void StowItems()
    {
        if (!HasStowedOnce && !IsStowing)
            StartCoroutine(StowItemsCoroutine());
    }

    IEnumerator StowItemsCoroutine()
    {
        HasStowedOnce = true;

        IsStowing = true;
        m_Agent.enabled = false;
        m_Obstacle.enabled = true;
        if (meshRenderer != null && stowingMaterial != null)
            meshRenderer.material = stowingMaterial;

        yield return new WaitForSeconds(TimeToStow);

        m_Obstacle.enabled = false;
        m_Agent.enabled = true;
        if (m_Goal != null)
        {
            NavMeshPath path = new NavMeshPath();
            NavMesh.CalculatePath(transform.position, m_Goal.position, NavMesh.AllAreas, path);
            m_Agent.SetPath(path);
        }
        if (meshRenderer != null && m_OriginalMaterial != null)
            meshRenderer.material = m_OriginalMaterial;
        IsStowing = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!HasStowedOnce && other.name == RowName)
        {
            StowItems();
        }
    }

    //private float getDistToGoal(NavMeshPassenger passenger)
    //{
    //    NavMeshPath path = new NavMeshPath();
    //    bool pathValid = NavMesh.CalculatePath(passenger.transform.position, m_Goal.position, NavMesh.AllAreas, path);
    //    if (pathValid)
    //    {
    //        float dist = 0f;
    //        for (int i = 0; i < path.corners.Length - 1; ++i)
    //        {
    //            dist += Vector3.Distance(path.corners[i], path.corners[i + 1]);
    //        }
    //        return dist;
    //    }
    //    return float.PositiveInfinity;
    //}

    public float DistanceToBackOfPlane
    {
        get
        {
            NavMeshPath path = new NavMeshPath();
            bool pathValid = NavMesh.CalculatePath(transform.position, backOfPlanePosition, NavMesh.AllAreas, path);
            if (pathValid)
            {
                float dist = 0f;
                for (int i = 0; i < path.corners.Length - 1; ++i)
                {
                    dist += Vector3.Distance(path.corners[i], path.corners[i + 1]);
                }
                return dist;
            }
            return float.PositiveInfinity;
        }
    }

    public IEnumerable<NavMeshPassenger> NearbyPassengers
    {
        get
        {
            return Physics.OverlapSphere(transform.position, nearbyRadius).ToList()
                .Where(col => col.GetComponent<NavMeshPassenger>() != null && col.GetComponent<NavMeshPassenger>() != this)
                .Select(col => col.GetComponent<NavMeshPassenger>());
        }
    }

    public void Enqueue()
    {
        InQueue = true;
        m_Agent.enabled = false;
        m_Obstacle.enabled = true;
    }

    public void Dequeue()
    {
        InQueue = false;
        m_Obstacle.enabled = false;
        m_Agent.enabled = true;
        if (m_Goal != null)
        {
            NavMeshPath path = new NavMeshPath();
            NavMesh.CalculatePath(transform.position, m_Goal.position, NavMesh.AllAreas, path);
            m_Agent.SetPath(path);
        }
    }

    //void Update()
    //{
    //    if (m_Goal != null)
    //    {
    //        float selfDist = getDistToGoal(this);
    //        if (float.IsInfinity(selfDist))
    //        {
    //            selfDist = 0f;
    //        }
    //        activePassengers = UnityEngine.Object.FindObjectsOfType<NavMeshPassenger>().Where(p => p.m_Goal != null && !p.PathCompleted && (p.IsStowing || p.InQueue)).ToArray();
    //        bool stopped = false;
    //        foreach (NavMeshPassenger passenger in activePassengers)
    //        {
    //            if (passenger.Equals(this))
    //            {
    //                continue;
    //            }

    //            if (Vector3.Distance(this.transform.position, passenger.transform.position) < QueuingDistance)
    //            {
    //                float dist = getDistToGoal(passenger);
    //                if (dist < selfDist && m_Agent.enabled)
    //                {
    //                    m_Agent.enabled = false;
    //                    m_Obstacle.enabled = true;
    //                    stopped = true;
    //                    InQueue = true;
    //                    break;
    //                }
    //            }
    //        }
    //        if (!stopped && InQueue && !IsStowing)
    //        {
    //            m_Obstacle.enabled = false;
    //            m_Agent.enabled = true;
    //            m_Agent.destination = m_Goal.position;
    //            InQueue = false;
    //        }
    //    }
    //}

    //void OnDrawGizmos() 
    //{
    //    GUIStyle style = new GUIStyle();
    //    style.normal.textColor = Color.white;
    //    style.alignment = TextAnchor.LowerCenter;
        
    //    if (PathCompleted)
    //    {
    //        style.normal.textColor = Color.green;
    //        Handles.Label(transform.position + Vector3.up * 2f, "Finished", style);
    //    }
    //    else
    //    {
    //        Handles.Label(transform.position + Vector3.up * 2f, DistanceToBackOfPlane.ToString(), style);
            
    //        if (InQueue)
    //        {
    //            Gizmos.color = Color.red;
    //            Gizmos.DrawWireSphere(transform.position, 1f);
    //        }
    //    }
    //}
}
