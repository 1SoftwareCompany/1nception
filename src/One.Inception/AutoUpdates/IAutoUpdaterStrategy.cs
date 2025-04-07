namespace One.Inception.AutoUpdates;

public interface IAutoUpdaterStrategy
{
    IAutoUpdate GetInstanceFor(string name);
}
