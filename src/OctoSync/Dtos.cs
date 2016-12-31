using System;
using System.Linq;
using ServiceStack;

namespace OctoSync
{
    [Route("/api/v2/feed-state")]
    public class FeedState : IReturn<FeedStateResponse>
    {
    }

    public class FeedStateResponse
    {
        // ReSharper disable once InconsistentNaming
        public long _Date { get; set; }
        public Package[] Packages { get; set; }
        public DateTime Date => new DateTime(_Date);
    }

    public class Package
    {
        public string PackageType { get; set; }
        public string Id { get; set; }
        public string[] Versions { get; set; }
        public long[] Dates { get; set; }
        public DateTime[] PublishedDates => Dates.Select(t => new DateTime(t)).ToArray();
    }

    [Route("/api/v2/package/{PackageName}/{Version}")]
    public class PackageDownload
    {
        public string PackageName { get; set; }
        public string Version { get; set; }
    }
}