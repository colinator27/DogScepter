using CliFx;
using System;
using System.Threading.Tasks;

namespace DogScepterCLI
{
    public static class Program
    {
        public static async Task<int> Main() =>
            await new CliApplicationBuilder()
                .SetTitle("DogScepter CLI")
                .SetExecutableName("DogScepterCLI")
                .SetVersion("0.0.1")
                .SetDescription("Interface for working with data and project files.")
                .AddCommandsFromThisAssembly()
                .Build()
                .RunAsync();
    }
}
