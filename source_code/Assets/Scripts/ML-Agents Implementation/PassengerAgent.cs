using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class PassengerAgent : Agent
{
    PassengerSettings m_PassengerSettings;
    Rigidbody m_AgentRb;

    public GameObject ground;
    Bounds m_areaBounds;


    public GameObject plane;
    Bounds planeBounds;
    public GameObject planeEntrance;

    private string seat;
    private int seatRow;
    private int currentRow;
    private bool seatCalled = true; //set to false later when we implement seat calling

    void Awake()
    {
        m_PassengerSettings = FindObjectOfType<PassengerSettings>();
    }

    public override void Initialize()
    {
        m_AgentRb = GetComponent<Rigidbody>();
        m_areaBounds = ground.GetComponent<Collider>().bounds;
        planeBounds = plane.GetComponent<Collider>().bounds;
        seat = name.Substring("PassengerAgent".Length);
        seatRow = System.Int32.Parse(seat.Substring(0, seat.Length - 1));
    }

    public override void OnEpisodeBegin()
    {
        transform.position = GetRandomSeat();
        //transform.position = GetRandomSpawnPos();
        transform.rotation = GetRandomSpawnRot();
        m_AgentRb.velocity = Vector3.zero;
        m_AgentRb.angularVelocity = Vector3.zero;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(seatRow - currentRow);
        sensor.AddObservation(transform.position);
        sensor.AddObservation(m_AgentRb.velocity.normalized);
    }

    public override void OnActionReceived(ActionBuffers actionBuffers)
    {
        if (seatCalled)
        {
            MoveAgent(actionBuffers.DiscreteActions);
            // Penalty given each step to encourage agent to finish task quickly.
            AddReward(-1f / MaxStep);

            if (!planeBounds.Contains(transform.position - new Vector3(0, 1f, 0)))
            {
                AddReward((planeEntrance.transform.position - transform.position).magnitude * -.001f);
            }
        }
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var discreteActionsOut = actionsOut.DiscreteActions;
        if (Input.GetKey(KeyCode.W))
        {
            discreteActionsOut[0] = 1;
        }
        else if (Input.GetKey(KeyCode.A))
        {
            discreteActionsOut[0] = 2;
        }
        else if (Input.GetKey(KeyCode.S))
        {
            discreteActionsOut[0] = 3;
        }
        else if (Input.GetKey(KeyCode.D))
        {
            discreteActionsOut[0] = 4;
        }

        if (Input.GetKey(KeyCode.Mouse0))
        {
            discreteActionsOut[1] = 1;
        }
        else if (Input.GetKey(KeyCode.Mouse1))
        {
            discreteActionsOut[1] = 2;
        }
    }

    /// <summary>
    /// Use the ground's bounds to pick a random spawn position.
    /// </summary>
    //public Vector3 GetRandomSpawnPos()
    //{
    //    var foundNewSpawnLocation = false;
    //    var randomSpawnPos = Vector3.zero;
    //    while (foundNewSpawnLocation == false)
    //    {
    //        var randomPosX = Random.Range(-m_areaBounds.extents.x * m_PassengerSettings.spawnAreaMarginMultiplier,
    //            m_areaBounds.extents.x * m_PassengerSettings.spawnAreaMarginMultiplier);

    //        var randomPosZ = Random.Range(-m_areaBounds.extents.z * m_PassengerSettings.spawnAreaMarginMultiplier,
    //            m_areaBounds.extents.z * m_PassengerSettings.spawnAreaMarginMultiplier);
    //        randomSpawnPos = ground.transform.position + new Vector3(randomPosX, 1f, randomPosZ);
    //        if (Physics.CheckBox(randomSpawnPos, new Vector3(2.5f, 0.01f, 2.5f)) == false)
    //        {
    //            foundNewSpawnLocation = true;
    //        }
    //    }
    //    return randomSpawnPos;
    //}

    public Vector3 GetRandomSeat()
    {
        GameObject[] seatList = GameObject.FindGameObjectsWithTag("seat");
        var foundNewSpawnLocation = false;
        var randomSpawnSeat = Vector3.zero;
        while (foundNewSpawnLocation == false)
        {
            var seatNum = Random.Range(0, seatList.Length - 1);
            randomSpawnSeat = seatList[seatNum].transform.position;
            randomSpawnSeat.y = ground.transform.position.y + 1f;
            if (Physics.CheckBox(randomSpawnSeat, new Vector3(2.5f, 0.01f, 2.5f)) == false)
            {
                foundNewSpawnLocation = true;
            }
        }
        return randomSpawnSeat;

    }

    public Quaternion GetRandomSpawnRot()
    {
        return Quaternion.Euler(0f, Random.Range(-180f, 180f), 0f);
    }

    /// <summary>
    /// Moves the agent according to the selected action.
    /// </summary>
    public void MoveAgent(ActionSegment<int> act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        var move = act[0];
        var rotate = act[1];

        switch (move)
        {
            case 1:
                dirToGo = transform.forward * m_PassengerSettings.forwardMovementScale;
                break;
            case 2:
                dirToGo = transform.right * -m_PassengerSettings.sidewaysMovementScale;
                break;
            case 3:
                dirToGo = transform.forward * -m_PassengerSettings.backwardMovementScale;
                break;
            case 4:
                dirToGo = transform.right * m_PassengerSettings.sidewaysMovementScale;
                break;
        }

        switch (rotate)
        {
            case 1:
                rotateDir = transform.up * -1f;
                break;
            case 2:
                rotateDir = transform.up * 1f;
                break;
        }

        transform.Rotate(rotateDir, Time.fixedDeltaTime * m_PassengerSettings.agentRotationSpeed);
        m_AgentRb.AddForce(dirToGo * m_PassengerSettings.agentRunSpeed,
            ForceMode.VelocityChange);
    }

    void OnTriggerEnter(Collider col)
    {
        // Touched goal.
        if (col.gameObject.CompareTag("goal") && col.gameObject.name.Substring("Goal".Length).Equals(seat))
        {
            AddReward(100f);
            EndEpisode();
        }

        else if (col.gameObject.CompareTag("row"))
        {
            currentRow = System.Int32.Parse(col.gameObject.name.Substring("Row".Length));
        }

        else if (col.gameObject.CompareTag("wall"))
        {
            AddReward(-1f);
        }
    }

    void CallSeat(GameObject goal)
    {
        if (goal.name.Substring("Goal".Length).Equals(seat))
        {
            seatCalled = true;
        }
    }
}
