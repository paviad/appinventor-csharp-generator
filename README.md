# AiaGenerator

This project is a source generator. Add an `.aia` file to your project, mark it
as `C# analyzer additional file` and also as `Copy always`, and the generator will
generate a `.aia.cs` file with public methods that correspond to *Procedures* blocks
in your AppInventor project.

## Installation

To use this generator in your project add this to your `csproj` file:

```
    <ItemGroup>
        <ProjectReference Include="..\AiaGenerator\AiaGenerator.csproj" OutputItemType="Analyzer" ReferenceOutputAssembly="false" />
    </ItemGroup>
```

Adjust paths as required.

You might also want to add this to be able to observe generated files:

```
    <PropertyGroup>
        <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
        <CompilerGeneratedFilesOutputPath>..\tmp</CompilerGeneratedFilesOutputPath>
    </PropertyGroup>
```

See the `Integration.Test.csproj` file for an example.

## Usage

Go into *AppInventor*, create a block diagram with *Procedures*, export it - it will save an `.aia` file.
Add that file to your project, and mark it as `C# analyzer additional file` and also as `Copy always`, and
the generator will generate a `.aia.cs` file with public methods that correspond to *Procedures* blocks
in your AppInventory project.

Any procedure with a name that start with `Invoke` is not emitted, it is assumed to be part of your own
partial class implementation. See the example `HelloPurr.aia` file (import it into AppInventor).

## Example

This diagram:

![image](https://github.com/user-attachments/assets/1729ac45-2255-4eee-b0f3-80be22955cc0)

Gets converted to this code:

```csharp
namespace Testa {
    [Aia]
    public partial class HelloPurr {
        public async Task<bool> MAIN() {
            async Task<bool> LocalFunction2() {
                bool Success = false;
                bool Failure = false;
                int Count = 1;
                async Task<bool> LocalFunction1() {
                    while ((!(Success) && !(Failure) && (Count != 3))) {
                        if ((await Detect("Select All") && await Detect("Unload"))) {
                            await Click("Select All");
                            await Click("Unload");
                            Success = true;
                        }
                        else {
                            if (await Detect("Taskbar Icon")) {
                                await Click("Taskbar Icon");
                            }
                            else {
                                Failure = true;
                            }
                        }
                        Count = (Count) + (1);
                    }
                    return Success;
                }
                return await LocalFunction1();
            }
            return await LocalFunction2();
        }
        
        public async Task<bool> Detect(string Template) {
            return await Invoke_Detect(Template);
        }
        
        public async Task<bool> Testa() {
            async Task<bool> LocalFunction5() {
                await Click("Taskbar Icon");
                return true;
            }
            async Task<bool> LocalFunction4() {
                await Click("Taskbar Icon");
                return true;
            }
            async Task<bool> LocalFunction3() {
                await Click("Taskbar Icon");
                return true;
            }
            return await LocalFunction3() ? await LocalFunction4() : await LocalFunction5();
        }
        
        public async Task Move(int x, int y) {
            await Invoke_Move(x, y);
        }
        
        public async Task Click(string Location) {
            await Invoke_Click(Location);
        }
        
    }
}
```

## TODO

This is a very incomplete project, but it is a good start.

Things to work on:

* The namespace used is `Testa` - make it a bit more relevant to the project somehow.
* The generator only picks the "first" block diagram it finds in the `.aia` file.
* Not all block types are supported, I only implemented the ones I needed thus far.
* `Invoke` methods are hard coded into the generator. In the future I want to use reflection to insert them appropriately.
