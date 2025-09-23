/// <summary>
/// Author: SmallBurger Inc
/// Date: 2025/09/23
/// Desc:
/// </summary>

namespace SB
{
    public class ExecuteTimeProcessor
    {
        public void Start()
        {
            m_stopWatch.Reset();
            m_stopWatch.Start();
        }

        public long StopGetHours()
        {
            m_stopWatch.Stop();
            return m_stopWatch.Elapsed.Hours;
        }

        public long StopGetMinutes()
        {
            m_stopWatch.Stop();
            return m_stopWatch.Elapsed.Minutes;
        }

        public long StopGetSeconds()
        {
            m_stopWatch.Stop();
            return m_stopWatch.Elapsed.Seconds;
        }

        //ms（毫秒，千分之一秒）
        public long StopGetMS()
        {
            m_stopWatch.Stop();
            return m_stopWatch.ElapsedMilliseconds;
        }

        //μs（微秒，百萬分之一秒）
        public long StopGetUS()
        {
            m_stopWatch.Stop();
            return (long)((m_stopWatch.ElapsedTicks * 1000000) / System.Diagnostics.Stopwatch.Frequency);
        }

        private System.Diagnostics.Stopwatch m_stopWatch =
            new System.Diagnostics.Stopwatch();
    }
}
