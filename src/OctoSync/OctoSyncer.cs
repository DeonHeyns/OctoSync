using System;
using System.IO;
using System.Linq;
using Octopus.Client;
using Serilog;
using ServiceStack;

namespace OctoSync
{
    public class OctoSyncer
    {
        private readonly string _packagePath;
        private readonly string _nugetUrl;
        private readonly string _octopusDeployUrl;
        private readonly string _octopusDeployApiKey;
        private readonly ILogger _log;

        public OctoSyncer(string packagePath, string nugetUrl, 
            string octopusDeployUrl, string octopusDeployApiKey, ILogger log)
        {
            _packagePath = packagePath;
            _nugetUrl = nugetUrl;
            _octopusDeployUrl = octopusDeployUrl;
            _octopusDeployApiKey = octopusDeployApiKey;
            _log = log;
        }

        public void Sync()
        {
            _log.Information("Starting Sync processing...");
            var response = GetPublishedPackages();
            var octo = 
                CreateOctopusRepo();

            foreach (var package in response.Packages)
            {
                _log.Information("Processing Package: {Package}", package.Id);
                var latestPublish = package.PublishedDates.Max();
                var index = Array.IndexOf(package.PublishedDates, latestPublish);
                var latestVersion = package.Versions[index];

                var listPackages = octo.BuiltInPackageRepository.ListPackages(package.Id);
                if (listPackages.TotalResults == 0)
                {
                    _log.Information("{Package} was not uploaded to OctopusDeploy before, preparing to upload", package.Id);
                    SaveAndUploadPackage(package, latestVersion, octo);
                    continue;
                }

                var lastUploadedVersion = listPackages.Items.First(t => t.LastModifiedOn == listPackages.Items.Max(s => s.LastModifiedOn));
                if (lastUploadedVersion.Version.Equals(latestVersion)) continue;

                SaveAndUploadPackage(package, latestVersion, octo);
            }
            _log.Information("Syncing stopping...");
        }

        private void SaveAndUploadPackage(Package package, string latestVersion, OctopusRepository octo)
        {
            if (package == null) throw new ArgumentNullException(nameof(package));
            if (latestVersion == null) throw new ArgumentNullException(nameof(latestVersion));
            if (octo == null) throw new ArgumentNullException(nameof(octo));

            var packagePath = SavePackage(package.Id, latestVersion);

            UploadPackage(octo, packagePath, package.Id, latestVersion);
            _log.Information(
                "Deleting {Package} {Version} from {PackagePath}",
                package.Id, latestVersion, packagePath);
            File.Delete(packagePath);
        }

        private  string SavePackage(string packageName, string version)
        {
            if (packageName == null) throw new ArgumentNullException(nameof(packageName));
            if (version == null) throw new ArgumentNullException(nameof(version));

            var request = new PackageDownload
            {
                PackageName = packageName,
                Version = version
            };
            
            var url = _nugetUrl + request.ToGetUrl();

            _log.Information(
                "Downloading {Package} {Version} from {PackageUrl}", 
                packageName, version, url);

            var bytes = url.GetBytesFromUrl();
            _log.Information(
                "Total size of {Package} {Version} is {Bytes} bytes", 
                packageName, version, bytes.Length);

            Directory.CreateDirectory(_packagePath);
            var path = CreatePackagePath(packageName, version);

            _log.Information(
                "Saving {Package} {Version} to {Path}",
                packageName, version, path);

            File.WriteAllBytes(path, bytes);
            return path;
        }

        private string CreatePackagePath(string packageName, string version)
        {
            return Path.Combine(_packagePath,
                packageName + version + ".nupkg");
        }

        private void UploadPackage(OctopusRepository octoRepo, string packagePath, string packageName, string version)
        {
            if (octoRepo == null) throw new ArgumentNullException(nameof(octoRepo));
            if (packagePath == null) throw new ArgumentNullException(nameof(packagePath));
            if (packageName == null) throw new ArgumentNullException(nameof(packageName));
            if (version == null) throw new ArgumentNullException(nameof(version));

            _log.Information(
                "Uploading {Package} {Version} to OctopusDeploy. " +
                "File is saved on disk at {PackagePath}", 
                packageName, version, packagePath);

            using (var stream = File.Open(packagePath, FileMode.Open))
            {
                octoRepo.BuiltInPackageRepository
                    .PushPackage(packagePath, stream);
            }

            _log.Information(
                "Successfully uploaded {Package} {Version} to OctopusDeploy",
                packageName, version);
        }


        private FeedStateResponse GetPublishedPackages()
        {
            _log.Information(
                "Retrieving NuGet package info from {NuGetUrl}", _nugetUrl);
            var client = new JsonServiceClient(_nugetUrl);
            var request = new FeedState();
            var response = client.Get(request);
            _log.Information(
                "Retrieved {PackageCount} packages from {NuGetUrl}", 
                response.Packages.Length, _nugetUrl);
            return response;
        }
        private OctopusRepository CreateOctopusRepo()
        {
            _log.Information("Creating OctopusRepository");
            var endpoint = new OctopusServerEndpoint(_octopusDeployUrl, _octopusDeployApiKey);
            var repository = new OctopusRepository(endpoint);
            return repository;
        }
    }
}