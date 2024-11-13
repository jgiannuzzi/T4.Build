## T4.Build

T4.Build is a tool to automatically transform T4 templates during build.

It is similar to the functionality provided by Visual Studio, except it works anywhere where you have a .NET (Core) runtime installed.

It is fully integrated with MSBuild and supports transforming the templates during the build process, and cleaning them during the clean process.

T4.Build aims for performance by transforming only the templates that have changed, and doing so in parallel (so more CPU cores => faster).

## Usage

Just add these lines to your project file:

```xml
<ItemGroup>
    <PackageReference Include="T4.Build" Version="0.2.5" PrivateAssets="All" />
</ItemGroup>
```

That's it!

Now the next time you run `dotnet build`, your templates will be transformed before compilation happens 🚀

If you want to clean all the generated files, just do a regular `dotnet clean` and they will be gone 🧹

This also works with the 'Build/Rebuild/Clean' buttons in Visual Studio. No need to click 'Transform All T4 Templates' anymore 💪🏽

The following properties can be defined in your project file to further control how T4.Build works:
* `TextTemplateTransformSkipUpToDate` (default: `true`): Indicates whether only out of date templates should be transformed, or whether all templates should be transformed
* `TextTemplateTransformParallel` (default: `true`): Indicates whether templates should be transformed in parallel, or whether they should be transformed sequentially
* `TextTemplateTransformAll` (default: `true`): Indicates whether all *.tt files should be automatically transformed. Otherwise T4.Build will only look for `None` items whose `Generator` is set to `TextTemplatingFileGenerator` (that's the default when creating a new text template in Visual Studio).
* `TextTemplateTransformOnBuild` (default: `true`): Indicates whether template transform/clean should happen automatically during build/clean

You can specify the list of text templates to transform manually via the `TextTemplateTransformFiles` item:

```xml
<ItemGroup>
    <TextTemplateTransformFiles Include="MyTemplate.custom;AnotherOne.custom" />
</ItemGroup>
```

If you disable `TextTemplateTransformOnBuild`, you can run the tasks manually via MSBuild:

```sh
dotnet msbuild -t:TextTemplateTransform
dotnet msbuild -t:TextTemplateClean
```