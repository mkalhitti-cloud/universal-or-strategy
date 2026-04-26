using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Morpheus.ControlPlane
{
    [ExcludeFromCodeCoverage]
    internal sealed class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("Morpheus Control Plane Initializing...");
            await Task.Delay(100); // Simulate startup async work
        }
    }
}
