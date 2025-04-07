using One.Inception.Discoveries;
using System;
using System.Collections.Generic;
using System.Linq;

namespace One.Inception;

public interface IAssemblyScanner
{
    IEnumerable<Type> Scan();
}

public class DefaulAssemblyScanner : IAssemblyScanner
{
    public IEnumerable<Type> Scan()
    {
        return AssemblyLoader.Assemblies.SelectMany(asm => asm.Value.GetLoadableTypes());
    }
}
