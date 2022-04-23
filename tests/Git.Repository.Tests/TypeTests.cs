using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using AmpScm.Git;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitRepositoryTests
{
    [TestClass]
    public class TypeTests
    {
        [TestMethod]
        public void VerifyDebuggerDisplayAttribute()
        {
            foreach (var type in typeof(GitRepository).Assembly.GetTypes().Concat(typeof(GitId).Assembly.GetTypes()))
            {
                if (type.GetCustomAttributes(typeof(DebuggerDisplayAttribute), false)?.FirstOrDefault() is DebuggerDisplayAttribute dd)
                {
                    foreach (var q in Regex.Matches(dd.Value, "(?<!\\\\{)(?<={)[^}]+(?=})").Cast<Match>().Select(x => x.Value))
                    {
                        int n = q.IndexOfAny(new[] { ',', ':', '(', '.' });

                        string name = (n >= 0) ? q.Substring(0, n) : q;

                        var f = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

                        if (type.GetField(name, f) is null && type.GetProperty(name, f) is null && name != nameof(ToString))
                            Assert.Fail($"Member {name} referenced in {nameof(DebuggerDisplayAttribute)} on {type.FullName} not found {dd.Value}");
                    }
                }
            }
        }
    }
}
