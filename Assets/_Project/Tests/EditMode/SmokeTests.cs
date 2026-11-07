using System;
using System.Linq;
using NUnit.Framework;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Proves the assembly layout compiles and the test runner works.
    /// </summary>
    public class SmokeTests
    {
        [TestCase("ResortEmpire.Core")]
        [TestCase("ResortEmpire.Game")]
        [TestCase("ResortEmpire.UI")]
        public void ProjectAssemblyIsLoaded(string assemblyName)
        {
            bool loaded = AppDomain.CurrentDomain.GetAssemblies()
                .Any(a => a.GetName().Name == assemblyName);

            Assert.IsTrue(loaded, $"Assembly '{assemblyName}' is not loaded. Check its .asmdef and that it contains at least one script.");
        }
    }
}
