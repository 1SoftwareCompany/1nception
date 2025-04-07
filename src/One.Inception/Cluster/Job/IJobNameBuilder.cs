namespace One.Inception.Cluster.Job;

public interface IJobNameBuilder
{
    string GetJobName(string defaultName);
}
