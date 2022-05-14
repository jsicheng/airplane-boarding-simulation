using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SimulationStatistics
{
    List<float> m_Times;

    public SimulationStatistics()
    {
        m_Times = new List<float>();
    }

    public void AddTime(float time)
    {
        m_Times.Add(time);
    }

    public float GetTimeMean()
    {
        if (m_Times.Count == 0)
            return 0f;

        return m_Times.Average();
    }

    public float GetTimeStDev()
    {
        if (m_Times.Count == 0)
            return 0f;

        int n = m_Times.Count;
        float mean = m_Times.Average();
        float ss = m_Times.Sum(t => Mathf.Pow(t - mean, 2));

        return Mathf.Sqrt(ss / n);
    }
}
