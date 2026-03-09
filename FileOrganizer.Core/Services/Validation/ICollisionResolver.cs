using System;
using System.Collections.Generic;

namespace FileOrganizer.Core;

public interface ICollisionResolver
{
    string ResolveDestinationPath(
        string proposedDestinationPath,
        IEnumerable<string> reservedDestinationPaths,
        StringComparison pathComparison);
}
