using System.Runtime.CompilerServices;

// ResortEmpire.Core: the pure C# game simulation (waves, needs, workers, economy).
// No MonoBehaviours here, so it stays testable in EditMode without a scene.
[assembly: InternalsVisibleTo("ResortEmpire.Tests.EditMode")]
[assembly: InternalsVisibleTo("ResortEmpire.Tests.PlayMode")]
