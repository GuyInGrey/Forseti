using System.Collections.Generic;
using System.Reflection;
using System.Timers;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace ForsetiFramework
{
    public static class ClockworkManager
    {
        public static List<(Timer, MethodInfo)> Running;

        public static void Init()
        {
            Running = new List<(Timer, MethodInfo)>();

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            {
                RuntimeHelpers.RunClassConstructor(t.TypeHandle);

                foreach (var m in t.GetMethods())
                {
                    foreach (var a in m.GetCustomAttributes<ClockworkAttribute>())
                    {
                        var timer = new Timer(a.TimeBetweenRunsMS);
                        timer.Elapsed += (s, e) => 
                        { 
                            var o = m?.Invoke(null, null); 
                            if (o is Task task)
                            {
                                task.GetAwaiter().GetResult();
                            }
                        };
                        timer.AutoReset = true;
                        timer.Enabled = true;
                        Running.Add((timer, m));
                    }
                }
            }
        }
    }
}
