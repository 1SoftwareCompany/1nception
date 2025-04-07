using One.Inception.Projections.Versioning;

namespace One.Inception.EventStore.Index;

public class EventStoreIndexManagerState : AggregateRootState<EventStoreIndexManager, EventStoreIndexManagerId>
{
    public override EventStoreIndexManagerId Id { get; set; }

    public VersionRequestTimebox LastVersionRequestTimebox { get; set; }

    public bool IsBuilding { get; set; }

    public bool IndexExists { get; set; }

    public void When(EventStoreIndexRequested e)
    {
        Id = e.Id;
        LastVersionRequestTimebox = e.Timebox;
        IsBuilding = true;
    }

    public void When(EventStoreIndexIsNowPresent e)
    {
        Id = e.Id;
        IsBuilding = false;
        IndexExists = true;
    }
}
