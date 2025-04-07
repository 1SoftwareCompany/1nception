//using Machine.Specifications;
//using One.Inception.AtomicAction;
//using One.Inception.AtomicAction.InMemory;
//using System;

//namespace One.Inception.Tests.InMemoryEventStoreSuite
//{
//    [Subject("InMemoryLock")]
//    public class When_unlocking_a_locked_resource
//    {
//        Establish context = () =>
//        {
//            @lock = new InMemoryLock();
//            resource = "locked";
//            @lock.Lock(resource, TimeSpan.FromMinutes(1));
//        };

//        Because of = () => @lock.Unlock(resource);

//        It should_be_unlocked = () => @lock.IsLocked(resource).ShouldBeFalse();

//        static ILock @lock;
//        static string resource;
//    }
//}
