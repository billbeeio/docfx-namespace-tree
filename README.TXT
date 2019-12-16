# docFx nested namespaces addon

> The standard docFx namespace navigation bar lists nested namespaces flat only.

> This addon makes them nested.


## STATE
> Works, but must be build in an awkward way as described below

## GOAL
> this addon as an npm package

## Install (as long as npm package is not available)
> Note: Open and build DocFx_Example.sln in this folder that shows you how things are set up.
> I assume you already have a running docfx nuget installation using docfx.console v 2.42.3 or higher
  successfully creating some documentation.
> checkout https://github.com/dotnet/docfx
> apply this fix to docfx sources: https://github.com/dotnet/docfx/pull/5366
> apply this feature to docfx sources: https://github.com/billbeeio/docfx/commit/5f05a8228b9f3dce9fddeec253b50dc13da445cd
> To the vs project of the docfx dlls you are about to update, add
  this to the Project->Properties->Post Build Events:
  copy $(TargetDir)$(TargetName).dll $(SolutionDir)..\DocFx_dlls_changed
  copy $(TargetDir)$(TargetName).pdb $(SolutionDir)..\DocFx_dlls_changed
  Namely, you'll need these Microsoft.DocAsCode.*-projects:
  - Build.Common.csproj
  - Build.Engine.csproj
  - Build.ManagedReference.csproj
  - Build.TableOfContents.csproj
  - Plugins.csproj
  - YamlSerialization.csproj
> Build the projects in question. A new dll and pdb file for each project should appear in
  folder /DocFx_Dlls_changed.
> Add the dll and pdb file to your git when you're satisfied with your changes.
> Add NamespacePostProcessor.csproj to your solution that also holds the docfx project
> make your docfx project dependent on the NamespacePostProcessor project so the latter will be build first.
> make sure all file paths for intermediate files are set to 'intermediate_files' and sub folders
  as shown in example_docfx.json file.
> add the post processor to your docfx.json config file as shown in example_docfx.json file.
> check/adapt the pathes in NamespacePostProcessor.proj post build step if something doesn't work.
> make sure you override the default docfx template with the contents from resources\templates\override.
  Simply put the contents of resources\templates to your project. Details are described here: https://dotnet.github.io/docfx/tutorial/howto_create_custom_template.html#merge-template-with-default-template
> Build your documentation solution. Documentation should have nested namespaces now in html namespace nav bar.

## Problems to solve to make this an npm package
> wait for fix https://github.com/dotnet/docfx/pull/5366 to be released for docfx:
> generalize docfx code changes, create pull request & wait for release of docfx with this feature:
  https://github.com/billbeeio/docfx/commit/5f05a8228b9f3dce9fddeec253b50dc13da445cd
> create an npm package from this project
> solve this: This post processor currently assumes the files to be in folder 'intermediate_files'.
  This folder is configurable in the docfx config file. We want to let the postprocessor know this
  folder location automatically. E.g. docFx could add the location to the parameters handed to the postprocessor.
  
