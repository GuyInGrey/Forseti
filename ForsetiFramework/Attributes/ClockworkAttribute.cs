using System;

namespace ForsetiFramework
{
    [AttributeUsage(AttributeTargets.Method)]
    public class ClockworkAttribute : Attribute
    {
        public double TimeBetweenRunsMS;

        /// <summary>
        /// Must be placed on a `public static` method (can be async). Will run first time after <paramref name="timeBetweenCallsMS"/> passes one time.
        /// </summary>
        /// <param name="timeBetweenCallsMS">Time in between each call in milliseconds.</param>
        public ClockworkAttribute(double timeBetweenCallsMS)
        {
            TimeBetweenRunsMS = timeBetweenCallsMS;
        }
    }
}
