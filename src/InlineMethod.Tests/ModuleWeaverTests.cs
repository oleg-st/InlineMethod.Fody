using System;
using Fody;
using InlineMethod.Fody;
using Xunit;

namespace InlineMethod.Tests
{
    public class ModuleWeaverTests
    {
        private static readonly TestResult TestResult;

        static ModuleWeaverTests()
        {
            var weavingTask = new ModuleWeaver();
            TestResult = weavingTask.ExecuteTestRun("InlineMethod.Tests.AssemblyToProcess.dll");
        }
    }
}
