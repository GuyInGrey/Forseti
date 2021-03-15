using System.Collections.Generic;
using System.Reflection;
using System.Timers;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;
using System.Linq;
using System;

namespace ForsetiFramework
{
    public static class ClockworkManager
    {
        public static List<(Timer, MethodInfo)> Running;

        [Event(Events.Ready)]
        public static void Init()
        {
            Running = new List<(Timer, MethodInfo)>();

            foreach (var t in Assembly.GetExecutingAssembly().GetTypes())
            {
                RuntimeHelpers.RunClassConstructor(t.TypeHandle);
            }

            var toRun = Assembly.GetExecutingAssembly().GetTypes().SelectMany(t => t.GetMethods())
                .Where(m => m.IsStatic && !(m.GetCustomAttribute<ClockworkAttribute>() is null))
                .Select(m => (m, m.GetCustomAttribute<ClockworkAttribute>())).ToList();

            toRun.ForEach((p) =>
            {
                var m = p.m; 
                var a = p.Item2;
                var timer = new Timer(a.TimeBetweenRunsMS);
                timer.Elapsed += async (s, e) =>
                {
                    try
                    {
                        var o = m?.Invoke(null, null);
                        if (o is Task task)
                        {
                            task.GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        await Log.FromException(ex).Post();
                    }
                };
                timer.AutoReset = true;
                timer.Enabled = true;
                Running.Add((timer, m));
            });
        }
    }
}
