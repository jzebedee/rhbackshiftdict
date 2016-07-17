using System;
using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using rhbackshiftdict;
using Xunit;

namespace DictionaryLoadTests
{
    public class LoadTests
    {
        public static IEnumerable<object> LoadDictionaryData
        {
            get
            {
                yield return new[] { IntDictionary };
                yield return new[] { IntRHDictionary };
            }
        }

        public static IDictionary<int, int> IntDictionary
        {
            get
            {
                var dict = new Dictionary<int, int>();

                return dict;
            }
        }

        public static IDictionary<int, int> IntRHDictionary
        {
            get
            {
                var dict = new RobinHoodDictionary<int, int>();

                return dict;
            }
        }

        [Theory]
        [MemberData(nameof(LoadDictionaryData))]
        [Benchmark]
        public void LoadDictionary(IDictionary<int, int> dict)
        {
            var x = new Random();

            for(int i = 0; i < (1 << 18); i++)
                dict.Add(i, x.Next(int.MinValue, int.MaxValue));
        }
    }
}