using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace ResortEmpire.Tests
{
    /// <summary>
    /// Project rules §6: the word "idle" is banned in all project materials, code included.
    /// Resort Empire is not an idle game. The resting state between waves is AWAITING_WAVE.
    /// </summary>
    public class TerminologyTests
    {
        // Catches "Idle", "IDLE", "IdleState", "isIdle", "_idle" and a lowercase "idle" at a word start,
        // but not unrelated words such as "bridle" or "sidle".
        private static readonly Regex BannedWord = new Regex(@"Idle|IDLE|(?<![A-Za-z])idle", RegexOptions.Compiled);

        [Test]
        public void ScriptsDoNotUseTheWordIdle()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            Assume.That(Directory.Exists(scriptsRoot), $"Scripts folder not found: {scriptsRoot}");

            var offenders = Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories)
                .SelectMany(file => File.ReadLines(file).Select((line, index) => (file, line, number: index + 1)))
                .Where(entry => BannedWord.IsMatch(entry.line))
                .Select(entry => $"{Path.GetFileName(entry.file)}:{entry.number}: {entry.line.Trim()}")
                .ToList();

            Assert.IsEmpty(offenders,
                "The word 'idle' is banned (project rules §6). Use AWAITING_WAVE / AwaitingWave for the resting state.\n"
                + string.Join("\n", offenders));
        }
    }
}
