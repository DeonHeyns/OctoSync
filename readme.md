# OctoSync

### Why?
Because!

I don't have a on premise build/NuGet server. There are great services out there like MyGet and AppVeyor (I'm coming at this from a .NET point of view) to build and host packages. There is also OctopusDeploy which makes deploying stupidly simple. I wanted automated and rolling deployments using my on premise OctopusDeploy server. For this to work you need to use the built-in OctopusDeploy package server, for the life of me I couldn't find a better way to get the packages up besides using custom code. To be honest I didn't look to hard, OctopusDeploy has an API NuGet is just URLs so this was quick for me.

### How to get going
OctoSync uses Topshelf so to install from a administrator cmd just run OctoSync.exe install to uninstall run OctoSync.exe uninstall. You could also use OctopusDeploy to deploy this as it is just a Windows service. You will need to update some settings in the app.config

| Setting | Explanation |
|---------|-------------|
|PackagePath | Local Path where packages are temporarily stored |
|NuGetUrl | NuGet Base Url - I tested this with MyGet |
| OctopusDeployUrl| Octopus Deploy Server Url |
| OctopusDeployApiKey | Octopus Deploy API Key |

That's it.