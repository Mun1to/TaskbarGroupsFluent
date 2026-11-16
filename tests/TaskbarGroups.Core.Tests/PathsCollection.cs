using Xunit;

// Paths.ConfigPath is a static; the tests that point it at a temp folder must not
// run beside the ones that don't.
[CollectionDefinition("Paths", DisableParallelization = true)]
public class PathsCollection
{
}
