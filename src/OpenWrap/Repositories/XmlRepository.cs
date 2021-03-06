using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenWrap.Exports;
using OpenWrap.Dependencies;
using OpenWrap.Repositories;

namespace OpenWrap.Repositories
{
    public class XmlRepository : IPackageRepository
    {
        public XmlRepository(IHttpNavigator navigator, IEnumerable<IExportBuilder> builders)
        {
            var document = navigator.LoadFileList();

            PackagesByName = (from wrapList in document.Descendants("wrap")
                              let name = wrapList.Attribute("name")
                              let version = wrapList.Attribute("version")
                              let link = (from link in wrapList.Elements("link")
                                          let relAttribute = link.Attribute("rel")
                                          let hrefAttribute = link.Attribute("href")
                                          where hrefAttribute != null && relAttribute != null && relAttribute.Value.Equals("package", StringComparison.OrdinalIgnoreCase)
                                          select hrefAttribute).FirstOrDefault()
                              let baseUri = !string.IsNullOrEmpty(document.BaseUri) ? new Uri(document.BaseUri, UriKind.Absolute) : null
                              let absoluteLink = baseUri == null ? new Uri(link.Value, UriKind.RelativeOrAbsolute) : new Uri(baseUri, new Uri(link.Value, UriKind.RelativeOrAbsolute))
                              where name != null && version != null && link != null
                              let depends = wrapList.Elements("depends").Select(x => x.Value)
                              select new XmlPackageInfo(this, navigator, name.Value, version.Value, absoluteLink, depends, builders))
                .Cast<IPackageInfo>().ToLookup(x => x.Name);
        }
        public ILookup<string, IPackageInfo> PackagesByName { get; private set; }

        public IPackageInfo Find(WrapDependency dependency)
        {
            return PackagesByName.Find(dependency);
        }

        public IPackageInfo Publish(string packageFileName, Stream packageStream)
        {
            throw new NotImplementedException();
        }
    }
}