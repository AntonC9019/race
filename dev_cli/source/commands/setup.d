module commands.setup;

import jcli;

import commands.context;
import common;

import std.path;
import std.stdio;
import std.process : wait;



@Command("setup", "Sets up all the stuff in the project")
struct SetupCommand
{
    @ParentCommand
    Context* context;

    @("In which mode to build kari.")
    string kariConfiguration = "Debug";

    int onExecute()
    {
        const kariStuffPath = context.projectDirectory.buildPath("kari_stuff");
        const kariPath = kariStuffPath.buildPath("Kari");
        // {
        //     auto pid = spawnProcess2(["dotnet", "tool", "restore"], kariPath);
        //     const status = wait(pid);
        //     if (status != 0)
        //     {
        //         writeln("Kari tool restore failed.");
        //         return status;
        //     }
        // }
        {
            writeln("Building Kari.");
            auto pid = spawnProcess2([
                "dotnet", "build", 
                "--configuration", kariConfiguration,
                "/p:KariBuildPath=" ~ context.buildDirectory ~ `\`],
                kariPath);
            const status = wait(pid);
            if (status != 0)
            {
                writeln("Kari build failed.");
                return status;
            }
        }
        return 0;
    }
}

// TODO: Maybe should build too?
@Command("kari", "Runs Kari on the unity project with all the neeed plugins.")
struct Kari
{
    @ParentCommand
    Context* context;

    @("The configuration in which Kari was built.")
    string configuration = "Debug";

    @("Extra arguments passed to Kari")
    @(ArgRaw)
    string[] rawArgs;

    int onExecute()
    {
        // TODO: this path should be provided by the build system or something
        // msbuild cannot do that afaik, so study the alternatives asap.
        string kariExecutablePath = buildPath(
            context.buildDirectory, "bin", "Kari.Generator", configuration, "net6.0", "Kari.Generator.exe");

        string[] usedKariPlugins = ["DataObject", "Flags", "UnityHelpers"];
        string[] customPlugins;

        string getPluginDllPath(string pluginName, string pluginDllName)
        {
            return buildPath(
                context.buildDirectory, 
                "bin", pluginName, configuration, "net6.0",
                pluginDllName);
        }

        {
            import std.algorithm;
            import std.range;

            // TODO: Improve Kari's argument parsing capabilities, or call it directly
            auto pid = spawnProcess2([
                    kariExecutablePath,
                    "-configurationFile", buildPath(context.projectDirectory, "game", "kari.json"),
                    "-pluginPaths", 
                        chain(
                            usedKariPlugins.map!(p => getPluginDllPath(p, "Kari.Plugins." ~ p ~ ".dll")),
                            customPlugins.map!(p => getPluginDllPath(p, p ~ ".dll")))
                        .join(",")
                ] ~ rawArgs, context.projectDirectory);
            const status = wait(pid);
            if (status != 0)
            {
                writeln("Kari execution failed.");
                return status;
            }
        }

        return 0;
    }
}
