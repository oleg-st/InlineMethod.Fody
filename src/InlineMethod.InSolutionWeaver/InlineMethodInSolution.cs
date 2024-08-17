using System;
using Fody;
using InlineMethod.Fody;
using Mono.Cecil;

namespace InlineMethod.InSolutionWeaver;

public class InlineMethodInSolution : ModuleWeaver
{
    static InlineMethodInSolution()
    {
        GC.KeepAlive(typeof(ModuleDefinition));
        GC.KeepAlive(typeof(WeavingException));
    }
}