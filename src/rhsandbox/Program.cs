using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using robinhood;

namespace rhsandbox
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var repsA = 10000;
            var repsB = 100;
            var times_RH = RunBenchmark(() => RunRH(repsA), repsB).ToList();
            var times_dict = RunBenchmark(() => RunDict(repsA), repsB).ToList();

            WriteTimes("Dictionary", times_dict);
            WriteTimes("RobinHood", times_RH);
            Console.ReadKey();
        }

        private static void WriteTimes(string name, IEnumerable<TimeSpan> times)
        {
            Console.WriteLine($"[{name}] Mean {times.Average(ts => ts.TotalMilliseconds)} Max {times.Max(ts => ts.TotalMilliseconds)} Min => {times.Min(ts => ts.TotalMilliseconds)}");
        }

        private static TimeSpan RunRH(int repetitions)
        {
            var dict = new RobinHoodDictionary<string, string>();
            string s = "", s2;

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < repetitions; i++)
            {
                s2 = s;
                var c = (char)(i % char.MaxValue);
                s += c;

                dict.Add(s, s2);
                //Console.WriteLine($"Added {c}, Count = {dict.Count}");
            }
            sw.Stop();

            return sw.Elapsed;
        }

        private static TimeSpan RunDict(int repetitions)
        {
            var dict = new Dictionary<string, string>();
            string s = "", s2;

            var sw = Stopwatch.StartNew();
            for (var i = 0; i < repetitions; i++)
            {
                s2 = s;
                var c = (char)(i % char.MaxValue);
                s += c;

                dict.Add(s, s2);
                //Console.WriteLine($"Added {c}, Count = {dict.Count}");
            }
            sw.Stop();

            return sw.Elapsed;
        }

        private static IEnumerable<TimeSpan> RunBenchmark(Func<TimeSpan> method, int repetitions)
        {
            for (int i = 0; i < repetitions; i++)
            {
                method();
                yield return method();
                method();
            }
        }
    }
}