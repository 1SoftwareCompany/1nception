//using Machine.Specifications;
//using One.Inception.AtomicAction;
//using One.Inception.AtomicAction.InMemory;
//using System;

//namespace One.Inception.Tests.InMemoryEventStoreSuite
//{
//    [Subject("InMemoryLock")]
//    public class When_checking_a_locked_resource
//    {
//        Establish context = () =>
//        {
//            @lock = new InMemoryLock();
//            resource = "locked";
//            @lock.Lock(resource, TimeSpan.FromMinutes(1));
//        };

//        Because of = () => locked = @lock.IsLocked(resource);

//        It should_be_locked = () => locked.ShouldBeTrue();

//        static ILock @lock;
//        static string resource;
//        static bool locked;
//    }
//}
