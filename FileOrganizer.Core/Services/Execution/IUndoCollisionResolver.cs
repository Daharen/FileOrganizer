namespace FileOrganizer.Core;

public interface IUndoCollisionResolver
{
    string ResolvePreservedUndoPath(string restoreTargetPath, string currentPath);
}
