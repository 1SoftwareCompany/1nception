using System;
using System.Collections.Generic;

namespace One.Inception.MessageProcessing;

public interface ISubscriberFinder<T>
{
    IEnumerable<Type> Find();
}
