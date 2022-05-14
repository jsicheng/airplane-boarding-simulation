using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using UnityEngine.UI;
using TMPro;
using System;
public class SimulationDirector : MonoBehaviour
{
    public static SimulationDirector instance = null;

    public List<GameObject> goals;
    public List<GameObject> goalsFC;
    public List<Transform> seats;
    public List<Transform> seatsFC;
    //public List<Transform> goals;

    public Transform passengerParent;
    public NavMeshPassenger passengerPrefab;

    public enum BoardingType
    {
        Random,
        FrontToBack,
        BackToFront,
        WindowMidAisle,
        SteffensPerfect,
        SteffensModified,
        All
    }

    public BoardingType boardingType;

    public float minTimeToStow = 0f;
    public float maxTimeToStow = 5f;

    public int numRows;
    public int numSeatsPerRow;

    public float waitTimePerPassenger;
    public int numRowsToBoard;

    [Min(0)]
    public float leaveSeatDuration = 1f;

    [Min(1)]
    public int numberOfTrials = 1;

    string ResultsDirectory { get { return Path.Combine(Application.dataPath, "Results"); } }
    string OutputCSVPath { get { return Path.Combine(ResultsDirectory, "times.csv"); } }

    List<NavMeshPassenger> ActivePassengers { get; set; }

    [Min(0)]
    public int maxActivePassengers = 30;

    public Button startButton;
    public TMP_InputField numberOfTrialsInput;
    public TMP_InputField simulationSpeedInput;
    public TMP_InputField waitTimePerPassengerInput;
    public TMP_InputField numRowsToBoardInput;
    public TMP_InputField minStowageTimeInput;
    public TMP_InputField maxStowageTimeInput;
    public TMP_Dropdown boardingTypeInput;

    public Camera allCam;
    public Camera terminalCam;
    public Camera planeCam;
    public TMP_Dropdown camInput;
    //public TMP_Dropdown camInput2;
    //public TMP_Dropdown camInput3;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(this.gameObject);
    }

    void Start()
    {
        Button startBtn = startButton.GetComponent<Button>();
        startBtn.onClick.AddListener(BeginSimulation);

        string[] boardingTypes = Enum.GetNames(typeof(BoardingType));
        List<string> boardingTypesList = new List<string>(boardingTypes);
        boardingTypeInput.ClearOptions();
        boardingTypeInput.AddOptions(boardingTypesList);

        List<string> camList = new List<string>() { "View All", "View Terminal", "View Plane" };
        terminalCam.enabled = false;
        planeCam.enabled = false;
        camInput.ClearOptions();
        camInput.AddOptions(camList);
        camInput.onValueChanged.AddListener(delegate { switchCam(camInput.value); });
    }

    void Update()
    {
        ActivePassengers = FindObjectsOfType<NavMeshPassenger>().ToList().Where(p => !p.PathCompleted && !p.HasStowedOnce && p.HasLeftSeat).OrderBy(p => p.DistanceToBackOfPlane).ToList();

        foreach (NavMeshPassenger p in ActivePassengers)
        {
            bool shouldBeQueued = p.NearbyPassengers.Where(np => !np.PathCompleted).Any(np => (np.InQueue || np.IsStowing) && np.DistanceToBackOfPlane < p.DistanceToBackOfPlane);

            if (!p.InQueue && shouldBeQueued)
                p.Enqueue();
            else if (p.InQueue && !shouldBeQueued)
                p.Dequeue();
        }
    }

    void BeginSimulation()
    {
        if (!int.TryParse(numberOfTrialsInput.text, out numberOfTrials))
        {
            numberOfTrials = 1;
        }
        numberOfTrials = Mathf.Clamp(numberOfTrials, 1, int.MaxValue);

        float timeScale = 1f;
        Time.timeScale = timeScale;
        if (float.TryParse(simulationSpeedInput.text, out timeScale))
        {
            timeScale = Mathf.Clamp(timeScale, 0.01f, 20f);
            Time.timeScale = timeScale;
        }

        if (!float.TryParse(waitTimePerPassengerInput.text, out waitTimePerPassenger))
        {
            waitTimePerPassenger = 1;
        }
        waitTimePerPassenger = Mathf.Clamp(waitTimePerPassenger, 0.01f, 100f);

        if (!int.TryParse(numRowsToBoardInput.text, out numRowsToBoard))
        {
            numRowsToBoard = 6;
        }
        numRowsToBoard = Mathf.Clamp(numRowsToBoard, 1, 24);

        if (!float.TryParse(maxStowageTimeInput.text, out maxTimeToStow))
        {
            maxTimeToStow = 30;
        }
        maxTimeToStow = Mathf.Clamp(maxTimeToStow, 0, int.MaxValue);

        if (!float.TryParse(minStowageTimeInput.text, out minTimeToStow))
        {
            minTimeToStow = 5;
        }
        minTimeToStow = Mathf.Clamp(minTimeToStow, 0, int.MaxValue);
        if (minTimeToStow > maxTimeToStow)
        {
            maxTimeToStow = minTimeToStow;
        }
        boardingType = (BoardingType)boardingTypeInput.value;

        if (!Directory.Exists(ResultsDirectory))
        {
            Directory.CreateDirectory(ResultsDirectory);
            if (!File.Exists(OutputCSVPath))
            {
                string header = "boarding type,time";
                File.WriteAllLines(OutputCSVPath, new string[] { header });
            }
        }

        if (boardingType == BoardingType.All)
            StartCoroutine(RunAllBoardingTypes(numberOfTrials));
        else
            StartCoroutine(RunSimulations(numberOfTrials));
    }

    IEnumerator RunAllBoardingTypes(int n)
    {
        foreach (BoardingType bt in System.Enum.GetValues(typeof(BoardingType)))
        {
            if (bt == BoardingType.All)
                continue;

            SimulationStatistics statistics = new SimulationStatistics();

            for (int i = 0; i < n; i++)
            {
                ResetSimulation();

                float startTime = Time.time;
                switch (bt)
                {
                    case BoardingType.BackToFront:
                        yield return StartCoroutine(BackToFrontSeating(numRowsToBoard, waitTimePerPassenger * numSeatsPerRow * numRowsToBoard));
                        break;
                    case BoardingType.WindowMidAisle:
                        yield return StartCoroutine(OutsideInsideSeating(waitTimePerPassenger * numRows * 2));
                        break;
                    case BoardingType.FrontToBack:
                        yield return StartCoroutine(FrontToBackSeating(numRowsToBoard, waitTimePerPassenger * numSeatsPerRow * numRowsToBoard));
                        break;
                    case BoardingType.SteffensPerfect:
                        yield return StartCoroutine(SteffensPerfectSeating(waitTimePerPassenger));
                        break;
                    case BoardingType.SteffensModified:
                        yield return StartCoroutine(SteffensModifiedSeating(waitTimePerPassenger * (numSeatsPerRow / 2) * (numRows / 2)));
                        break;
                    case BoardingType.Random:
                    default:
                        yield return StartCoroutine(RandomSeating(waitTimePerPassenger));
                        break;
                }
                float endTime = Time.time;
                float elapsedTime = endTime - startTime;
                statistics.AddTime(elapsedTime);

                Debug.Log(bt.ToString() + " Run " + i + ": " + (endTime - startTime));

                if (File.Exists(OutputCSVPath))
                {
                    string entry = bt.ToString() + "," + elapsedTime.ToString();
                    File.AppendAllLines(OutputCSVPath, new string[] { entry });
                }
            }

            Debug.Log(bt.ToString() + " Mean: " + statistics.GetTimeMean());
            Debug.Log(bt.ToString() + " Standard Deviation: " + statistics.GetTimeStDev());
        }
    }

    IEnumerator RunSimulations(int n)
    {
        SimulationStatistics statistics = new SimulationStatistics();

        for (int i = 0; i < n; i++)
        {
            ResetSimulation();

            float startTime = Time.time;
            switch (boardingType)
            {
                case BoardingType.BackToFront:
                    yield return StartCoroutine(BackToFrontSeating(numRowsToBoard, waitTimePerPassenger * numSeatsPerRow * numRowsToBoard));
                    break;
                case BoardingType.WindowMidAisle:
                    yield return StartCoroutine(OutsideInsideSeating(waitTimePerPassenger * numRows * 2));
                    break;
                case BoardingType.FrontToBack:
                    yield return StartCoroutine(FrontToBackSeating(numRowsToBoard, waitTimePerPassenger * numSeatsPerRow * numRowsToBoard));
                    break;
                case BoardingType.SteffensPerfect:
                    yield return StartCoroutine(SteffensPerfectSeating(waitTimePerPassenger));
                    break;
                case BoardingType.SteffensModified:
                    yield return StartCoroutine(SteffensModifiedSeating(waitTimePerPassenger * (numSeatsPerRow / 2) * (numRows / 2)));
                    break;
                case BoardingType.Random:
                default:
                    yield return StartCoroutine(RandomSeating(waitTimePerPassenger));
                    break;
            }
            float endTime = Time.time;
            float elapsedTime = endTime - startTime;
            statistics.AddTime(elapsedTime);

            Debug.Log("Run " + i + ": " + (endTime - startTime));

            if (File.Exists(OutputCSVPath))
            {
                string entry = boardingType.ToString() + "," + elapsedTime.ToString();
                File.AppendAllLines(OutputCSVPath, new string[] { entry });
            }
        }

        Debug.Log("Mean: " + statistics.GetTimeMean());
        Debug.Log("Standard Deviation: " + statistics.GetTimeStDev());
    }

    void ResetSimulation()
    {
        foreach (Transform child in passengerParent)
            Destroy(child.gameObject);
    }

    void switchCam(int targetCam)
    {
        Debug.Log("Camera: " + targetCam.ToString());
        switch (targetCam)
        {
            case 1:
                allCam.enabled = false;
                terminalCam.enabled = true;
                planeCam.enabled = false;
                break;
            case 2:
                allCam.enabled = false;
                terminalCam.enabled = false;
                planeCam.enabled = true;
                break;
            case 0:
            default:
                allCam.enabled = true;
                terminalCam.enabled = false;
                planeCam.enabled = false;
                break;
        }
    }

    IEnumerator RandomSeating(float timeBetweenBoarding)
    {
        List<Transform> seatsOpen = seats.ToList();
        List<Transform> goalsOpen = goals.Select(goal => goal.transform).ToList();
        List<NavMeshPassenger> seatedPassengers = new List<NavMeshPassenger>();
        List<NavMeshPassenger> allPassengers = new List<NavMeshPassenger>();

        while (seatsOpen.Count > 0)
        {
            Transform randomSeat = seatsOpen[UnityEngine.Random.Range(0, seatsOpen.Count)];
            seatsOpen.Remove(randomSeat);
            NavMeshPassenger passenger = Instantiate(passengerPrefab, randomSeat.position + Vector3.up, randomSeat.rotation, passengerParent);
            seatedPassengers.Add(passenger);
            allPassengers.Add(passenger);
        }

        seatedPassengers.ForEach(p =>
        {
            p.DisableNavMeshAgent();
            p.TimeToStow = UnityEngine.Random.Range(minTimeToStow, maxTimeToStow);
        });

        yield return StartCoroutine(BoardFirstClass());

        while (seatedPassengers.Count > 0)
        {
            Transform randomGoal = goalsOpen[UnityEngine.Random.Range(0, goalsOpen.Count)];
            goalsOpen.Remove(randomGoal);
            NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
            seatedPassengers.Remove(passenger);

            yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
            StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal.transform, leaveSeatDuration));

            yield return new WaitForSeconds(timeBetweenBoarding);
        }

        yield return new WaitUntil(() => allPassengers.All(p => p.PathCompleted));
    }

    IEnumerator BackToFrontSeating(int numBoardingRows, float timeBetweenBoarding)
    {
        List<Transform> seatsOpen = seats.ToList();
        List<GameObject> goalsAll = goals.ToList();
        List<NavMeshPassenger> seatedPassengers = new List<NavMeshPassenger>();
        List<NavMeshPassenger> allPassengers = new List<NavMeshPassenger>();

        while (seatsOpen.Count > 0)
        {
            Transform randomSeat = seatsOpen[UnityEngine.Random.Range(0, seatsOpen.Count)];
            seatsOpen.Remove(randomSeat);
            NavMeshPassenger passenger = Instantiate(passengerPrefab, randomSeat.position + Vector3.up, randomSeat.rotation, passengerParent);
            seatedPassengers.Add(passenger);
            allPassengers.Add(passenger);
        }

        seatedPassengers.ForEach(p =>
        {
            p.DisableNavMeshAgent();
            p.TimeToStow = UnityEngine.Random.Range(minTimeToStow, maxTimeToStow);
        });

        yield return StartCoroutine(BoardFirstClass());

        int numRowsLeft = numRows;
        while (numRowsLeft > 0)
        {
            for (int i = 0; i < numBoardingRows; i++)
            {
                List<Transform> goalsOpen = goalsAll.Where(goal => int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) == numRowsLeft - i).Select(goal => goal.transform).ToList();
                while (goalsOpen.Count > 0)
                {
                    Transform randomGoal = goalsOpen[UnityEngine.Random.Range(0, goalsOpen.Count)];
                    goalsOpen.Remove(randomGoal);

                    NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
                    seatedPassengers.Remove(passenger);

                    yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
                    StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
                }
            }
            yield return new WaitForSeconds(timeBetweenBoarding);
            numRowsLeft -= numBoardingRows;
        }

        yield return new WaitUntil(() => allPassengers.All(p => p.PathCompleted));
    }

    IEnumerator OutsideInsideSeating(float timeBetweenBoarding)
    {
        List<Transform> seatsOpen = seats.ToList();
        List<GameObject> goalsAll = goals.ToList();
        List<NavMeshPassenger> seatedPassengers = new List<NavMeshPassenger>();
        List<NavMeshPassenger> allPassengers = new List<NavMeshPassenger>();
        List<string> columnNames = new List<string>();

        for (int i = 0; i < numSeatsPerRow; i++)
        {
            columnNames.Add(((char)('A' + i)).ToString());
        }

        while (seatsOpen.Count > 0)
        {
            Transform randomSeat = seatsOpen[UnityEngine.Random.Range(0, seatsOpen.Count)];
            seatsOpen.Remove(randomSeat);
            NavMeshPassenger passenger = Instantiate(passengerPrefab, randomSeat.position + Vector3.up, randomSeat.rotation, passengerParent);
            seatedPassengers.Add(passenger);
            allPassengers.Add(passenger);
        }

        seatedPassengers.ForEach(p =>
        {
            p.DisableNavMeshAgent();
            p.TimeToStow = UnityEngine.Random.Range(minTimeToStow, maxTimeToStow);
        });

        yield return StartCoroutine(BoardFirstClass());

        for (int c = 0; c < numSeatsPerRow / 2; c++)
        {
            string columnNameL = columnNames[c];
            string columnNameR = columnNames[numSeatsPerRow - c - 1];
            List<Transform> goalsOpen = goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameL).Select(goal => goal.transform).ToList();
            goalsOpen.AddRange(goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameR).Select(goal => goal.transform).ToList());
            while (goalsOpen.Count > 0)
            {
                Transform randomGoal = goalsOpen[UnityEngine.Random.Range(0, goalsOpen.Count)];
                goalsOpen.Remove(randomGoal);

                NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
                seatedPassengers.Remove(passenger);

                yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
                StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
            }
            yield return new WaitForSeconds(timeBetweenBoarding);
        }

        yield return new WaitUntil(() => allPassengers.All(p => p.PathCompleted));
    }

    IEnumerator FrontToBackSeating(int numBoardingRows, float timeBetweenBoarding)
    {
        List<Transform> seatsOpen = seats.ToList();
        List<GameObject> goalsAll = goals.ToList();
        List<NavMeshPassenger> seatedPassengers = new List<NavMeshPassenger>();
        List<NavMeshPassenger> allPassengers = new List<NavMeshPassenger>();

        while (seatsOpen.Count > 0)
        {
            Transform randomSeat = seatsOpen[UnityEngine.Random.Range(0, seatsOpen.Count)];
            seatsOpen.Remove(randomSeat);
            NavMeshPassenger passenger = Instantiate(passengerPrefab, randomSeat.position + Vector3.up, randomSeat.rotation, passengerParent);
            seatedPassengers.Add(passenger);
            allPassengers.Add(passenger);
        }

        seatedPassengers.ForEach(p =>
        {
            p.DisableNavMeshAgent();
            p.TimeToStow = UnityEngine.Random.Range(minTimeToStow, maxTimeToStow);
        });

        yield return StartCoroutine(BoardFirstClass());

        int numRowsLeft = numRows;
        int numRowsBoarded = 0;
        while (numRowsBoarded < numRows)
        {
            for (int i = 0; i < numBoardingRows; i++)
            {
                List<Transform> goalsOpen = goalsAll.Where(goal => int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) == numRowsBoarded + 1).Select(goal => goal.transform).ToList();
                while (goalsOpen.Count > 0)
                {

                    Transform randomGoal = goalsOpen[UnityEngine.Random.Range(0, goalsOpen.Count)];
                    goalsOpen.Remove(randomGoal);

                    NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
                    seatedPassengers.Remove(passenger);

                    yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
                    StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
                }
                numRowsBoarded++;
            }
            yield return new WaitForSeconds(timeBetweenBoarding);
        }

        yield return new WaitUntil(() => allPassengers.All(p => p.PathCompleted));
    }

    IEnumerator SteffensPerfectSeating(float timeBetweenBoarding)
    {
        List<Transform> seatsOpen = seats.ToList();
        List<GameObject> goalsAll = goals.ToList();
        List<NavMeshPassenger> seatedPassengers = new List<NavMeshPassenger>();
        List<NavMeshPassenger> allPassengers = new List<NavMeshPassenger>();
        List<string> columnNames = new List<string>();

        for (int i = 0; i < numSeatsPerRow; i++)
        {
            columnNames.Add(((char)('A' + i)).ToString());
        }

        while (seatsOpen.Count > 0)
        {
            Transform randomSeat = seatsOpen[UnityEngine.Random.Range(0, seatsOpen.Count)];
            seatsOpen.Remove(randomSeat);
            NavMeshPassenger passenger = Instantiate(passengerPrefab, randomSeat.position + Vector3.up, randomSeat.rotation, passengerParent);
            seatedPassengers.Add(passenger);
            allPassengers.Add(passenger);
        }

        seatedPassengers.ForEach(p =>
        {
            p.DisableNavMeshAgent();
            p.TimeToStow = UnityEngine.Random.Range(minTimeToStow, maxTimeToStow);
        });

        yield return StartCoroutine(BoardFirstClass());

        for (int c = 0; c < numSeatsPerRow / 2; c++)
        {
            string columnNameL = columnNames[c];
            for (int i = numRows; i > 0; i -= 2)
            {
                List<Transform> goalsOpenL = goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameL && int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) == i).Select(goal => goal.transform).ToList();
                Transform randomGoal = goalsOpenL[0];

                NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
                seatedPassengers.Remove(passenger);

                yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
                StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
                yield return new WaitForSeconds(timeBetweenBoarding);
            }

            string columnNameR = columnNames[numSeatsPerRow - c - 1];
            for (int i = numRows; i > 0; i -= 2)
            {
                List<Transform> goalsOpenR = goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameR && int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) == i).Select(goal => goal.transform).ToList();
                Transform randomGoal = goalsOpenR[0];

                NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
                seatedPassengers.Remove(passenger);

                yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
                StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
                yield return new WaitForSeconds(timeBetweenBoarding);
            }

            for (int i = numRows - 1; i > 0; i -= 2)
            {
                List<Transform> goalsOpenL = goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameL && int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) == i).Select(goal => goal.transform).ToList();
                Transform randomGoal = goalsOpenL[0];

                NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
                seatedPassengers.Remove(passenger);

                yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
                StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
                yield return new WaitForSeconds(timeBetweenBoarding);
            }

            for (int i = numRows - 1; i > 0; i -= 2)
            {
                List<Transform> goalsOpenR = goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameR && int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) == i).Select(goal => goal.transform).ToList();
                Transform randomGoal = goalsOpenR[0];

                NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
                seatedPassengers.Remove(passenger);

                yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
                StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
                yield return new WaitForSeconds(timeBetweenBoarding);
            }
        }

        yield return new WaitUntil(() => allPassengers.All(p => p.PathCompleted));
    }

    IEnumerator SteffensModifiedSeating(float timeBetweenBoarding)
    {
        List<Transform> seatsOpen = seats.ToList();
        List<GameObject> goalsAll = goals.ToList();
        List<NavMeshPassenger> seatedPassengers = new List<NavMeshPassenger>();
        List<NavMeshPassenger> allPassengers = new List<NavMeshPassenger>();
        List<string> columnNames = new List<string>();

        for (int i = 0; i < numSeatsPerRow; i++)
        {
            columnNames.Add(((char)('A' + i)).ToString());
        }

        while (seatsOpen.Count > 0)
        {
            Transform randomSeat = seatsOpen[UnityEngine.Random.Range(0, seatsOpen.Count)];
            seatsOpen.Remove(randomSeat);
            NavMeshPassenger passenger = Instantiate(passengerPrefab, randomSeat.position + Vector3.up, randomSeat.rotation, passengerParent);
            seatedPassengers.Add(passenger);
            allPassengers.Add(passenger);
        }

        seatedPassengers.ForEach(p =>
        {
            p.DisableNavMeshAgent();
            p.TimeToStow = UnityEngine.Random.Range(minTimeToStow, maxTimeToStow);
        });

        yield return StartCoroutine(BoardFirstClass());

        List<Transform> goalsOpenL = new List<Transform>();
        List<Transform> goalsOpenR = new List<Transform>();
        for (int c = 0; c < numSeatsPerRow / 2; c++)
        {
            string columnNameL = columnNames[c];
            string columnNameR = columnNames[numSeatsPerRow - c - 1];
            goalsOpenL.AddRange(goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameL).Select(goal => goal.transform).ToList());
            goalsOpenR.AddRange(goalsAll.Where(goal => goal.name.Substring(goal.name.Length - 1) == columnNameR).Select(goal => goal.transform).ToList());
        }
        List<Transform> goalsOpenLEven = goalsOpenL.Where(goal => int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) % 2 == 0).Select(goal => goal.transform).ToList();
        List<Transform> goalsOpenREven = goalsOpenR.Where(goal => int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) % 2 == 0).Select(goal => goal.transform).ToList();
        List<Transform> goalsOpenLOdd = goalsOpenL.Where(goal => int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) % 2 == 1).Select(goal => goal.transform).ToList();
        List<Transform> goalsOpenROdd = goalsOpenR.Where(goal => int.Parse(goal.name.Substring("Goal".Length).Substring(0, goal.name.Substring("Goal".Length).Length - 1)) % 2 == 1).Select(goal => goal.transform).ToList();
        while (goalsOpenLEven.Count > 0)
        {
            Transform randomGoal = goalsOpenLEven[UnityEngine.Random.Range(0, goalsOpenLEven.Count)];
            goalsOpenLEven.Remove(randomGoal);

            NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
            seatedPassengers.Remove(passenger);

            yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
            StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
        }
        yield return new WaitForSeconds(timeBetweenBoarding);
        while (goalsOpenREven.Count > 0)
        {
            Transform randomGoal = goalsOpenREven[UnityEngine.Random.Range(0, goalsOpenREven.Count)];
            goalsOpenREven.Remove(randomGoal);

            NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
            seatedPassengers.Remove(passenger);

            yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
            StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
        }
        yield return new WaitForSeconds(timeBetweenBoarding);
        while (goalsOpenLOdd.Count > 0)
        {
            Transform randomGoal = goalsOpenLOdd[UnityEngine.Random.Range(0, goalsOpenLOdd.Count)];
            goalsOpenLOdd.Remove(randomGoal);

            NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
            seatedPassengers.Remove(passenger);

            yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
            StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
        }
        yield return new WaitForSeconds(timeBetweenBoarding);
        while (goalsOpenROdd.Count > 0)
        {
            Transform randomGoal = goalsOpenROdd[UnityEngine.Random.Range(0, goalsOpenROdd.Count)];
            goalsOpenROdd.Remove(randomGoal);

            NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
            seatedPassengers.Remove(passenger);

            yield return new WaitUntil(() => ActivePassengers.Count < maxActivePassengers);
            StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal, leaveSeatDuration));
        }

        yield return new WaitUntil(() => allPassengers.All(p => p.PathCompleted));
    }

    IEnumerator BoardFirstClass()
    {
        List<Transform> seatsFCOpen = seatsFC.ToList();
        List<Transform> goalsFCAll = goalsFC.Select(goalFC => goalFC.transform).ToList();
        List<NavMeshPassenger> seatedPassengers = new List<NavMeshPassenger>();
        List<NavMeshPassenger> allPassengers = new List<NavMeshPassenger>();

        while (seatsFCOpen.Count > 0)
        {
            Transform randomSeat = seatsFCOpen[UnityEngine.Random.Range(0, seatsFCOpen.Count)];
            seatsFCOpen.Remove(randomSeat);
            NavMeshPassenger passenger = Instantiate(passengerPrefab, randomSeat.position + Vector3.up, randomSeat.rotation, passengerParent);
            seatedPassengers.Add(passenger);
            allPassengers.Add(passenger);
        }

        seatedPassengers.ForEach(p =>
        {
            p.DisableNavMeshAgent();
            p.TimeToStow = UnityEngine.Random.Range(minTimeToStow, maxTimeToStow);
        });

        while (seatedPassengers.Count > 0)
        {
            Transform randomGoal = goalsFCAll[UnityEngine.Random.Range(0, goalsFCAll.Count)];
            goalsFCAll.Remove(randomGoal);

            NavMeshPassenger passenger = seatedPassengers[UnityEngine.Random.Range(0, seatedPassengers.Count)];
            seatedPassengers.Remove(passenger);

            StartCoroutine(PassengerLeaveSeatCoroutine(passenger, randomGoal.transform, leaveSeatDuration));
        }

        yield return new WaitUntil(() => allPassengers.All(p => p.PathCompleted));
    }

    IEnumerator PassengerLeaveSeatCoroutine(NavMeshPassenger passenger, Transform goal, float duration)
    {
        Vector3 originalPos = passenger.transform.position;
        Vector3 endPos = originalPos + passenger.transform.forward + passenger.transform.up * -1f;

        float startTime = Time.time;
        float currentTime = 0f;

        while (currentTime < duration)
        {
            currentTime = Time.time - startTime;
            passenger.transform.position = Vector3.Lerp(originalPos, endPos, currentTime / duration);
            yield return null;
        }

        passenger.transform.position = endPos;
        passenger.EnableNavMeshAgent();
        passenger.SetGoal(goal);

        passenger.HasLeftSeat = true;
    }


}
