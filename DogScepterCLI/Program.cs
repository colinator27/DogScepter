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
                .SetDescription("DogScepter command line interface for project files.")
                .AddCommandsFromThisAssembly()
                .Build()
                .RunAsync();
    }
}
