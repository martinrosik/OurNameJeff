using System.Linq;
using NUnit.Framework;
using UnityEditor.Compilation;

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
        public void ProjectAssemblyIsBuiltForThePlayer(string assemblyName)
        {
            bool built = CompilationPipeline.GetAssemblies(AssembliesType.PlayerWithoutTestAssemblies)
                .Any(a => a.name == assemblyName);

            Assert.IsTrue(built, $"Assembly '{assemblyName}' is not part of the player build. Check its .asmdef and that it contains at least one script.");
        }
    }
}
