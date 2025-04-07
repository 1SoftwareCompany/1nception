using One.Inception.Workflow;

namespace One.Inception.MessageProcessing;

public interface ISubscriberWorkflowFactory<T>
{
    IWorkflow GetWorkflow();
}
