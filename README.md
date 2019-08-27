# TroublemakerProxy
A program that sits between Couchbase Lite and a sync server such as Sync Gateway to cause intentional problems for testing.  Its functionality is driven by plugins.

## Building

The proxy is dependent on a native library called [cblip](https://github.com/couchbaselabs/cblip).  It needs to be built first.  To do so, CMake is required.  Use the following commands from the root of the repo.

Unix:
```sh
mkdir -p cblip/build
cd cblip/build
cmake -DCMAKE_BUILD_TYPE=MinSizeRel ..
make -j8 CBlip
```

Windows:
```powershell
New-Item -Type Directory cblip/build
cd cblip/build

# Replace cmake with path to cmake unless you have an alias
cmake -G "Visual Studio 15 2017 Win64" .. # Drop the Win64 for 32-bit, or change to a different VS version if desired
cmake --build . --target CBlip --config MinSizeRel
```

After that you can simply build the C# projects

## Repo Structure

Here is a layout of the folders in this repo:

- TroublemakerProxy: The main program which is responsible for maintaining the connection properly between the two endpoints, and loading plugins
- TroublemakerInterfaces: The interfaces and needed classes for plugin development
- Replicator: A simple Couchbase Lite program which will perform replication and can interactively add simple documents.  Used for testing.
- \*Plugin: Any folder ending in plugin is a plugin for the proxy

## Plugin Development

All plugins for the troublemaker proxy implement the `ITroublemakerPlugin` interface.  To get some additional benefits, your plugin class should inherit from either `TroublemakerPluginBase` or `TroublemakerPluginBase<T>`.  These are found in the Troublemaker Interfaces project, so reference that when developing.  There are several methods in the interface definition that start with `Handle`.  These methods are called as applicable.  The way to register for these methods is via the `Style` property.  If it contains the `TamperStyle.Network` flag the plugin will receive the `HandleNetworkStage` call, and so on.  If the abstract base class is used, then a `Log` property will also be present which contains a logger that will be included in the Troublemaker Proxy logs.  It will automatically identify the plugin that made the log statement, so don't worry about writing the plugin name in the log output.  Use the various levels (`Verbose`, `Warning`, etc) appropriately.  
