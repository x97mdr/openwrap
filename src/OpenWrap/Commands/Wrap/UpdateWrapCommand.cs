﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenWrap.Build.Services;
using OpenWrap.Commands.Core;
using OpenWrap.Dependencies;
using OpenWrap.Repositories;

namespace OpenWrap.Commands.Wrap
{
    [Command(Verb = "update", Noun = "wrap")]
    public class UpdateWrapCommand : ICommand
    {

        protected IEnvironment Environment
        {
            get { return WrapServices.GetService<IEnvironment>(); }
        }

        protected IPackageManager PackageManager
        {
            get { return WrapServices.GetService<IPackageManager>(); }
        }

        public IEnumerable<ICommandResult> Execute()
        {
            if (Environment.ProjectRepository != null)
                return UpdateProjectPackages();
            return UpdateUserPackages();
        }

        IEnumerable<ICommandResult> UpdateUserPackages()
        {
            var installedPackages = Environment.UserRepository.PackagesByName.Select(x => x.Key);

            var packagesToSearch = new WrapDescriptor
            {
                Dependencies = (from package in installedPackages
                               let maxVersion = Environment.UserRepository.PackagesByName[package]
                                .OrderByDescending(x=>x.Version)
                                .Select(x=>x.Version)
                                .First()
                                select new WrapDependency
                {
                    Name = package,
                    VersionVertices = { new GreaterThenVersionVertice(maxVersion) }
                }).ToList()
            };
            yield return new Result("Searching for updated packages");
            var resolveResult = PackageManager.TryResolveDependencies(packagesToSearch, null, null, Environment.RemoteRepositories);

            
            foreach(var packageToUpdate in resolveResult.Dependencies)
            {
                yield return new Result("Copying {0} to user repository.", packageToUpdate.Package.FullName);
                using (var stream = packageToUpdate.Package.Load().OpenStream())
                    Environment.UserRepository.Publish(packageToUpdate.Package.FullName, stream);
            }
            
        }

        IEnumerable<ICommandResult> UpdateProjectPackages()
        {
            var packagesToCopy = from dependency in Environment.Descriptor.Dependencies
                                 let remotePackage = Environment.RemoteRepositories
                                     .Select(x => x.Find(dependency)).FirstOrDefault()
                                 where remotePackage != null
                                 select remotePackage;
            foreach(var packageToCopy in packagesToCopy)
            {
                if (!Environment.UserRepository.HasDependency(packageToCopy.Name, packageToCopy.Version))
                {
                    yield return new Result("Copying {0} to user repository", packageToCopy.FullName);
                    using(var stream = packageToCopy.Load().OpenStream())
                        Environment.UserRepository.Publish(packageToCopy.FullName, stream);
                }
                if (!Environment.ProjectRepository.HasDependency(packageToCopy.Name, packageToCopy.Version))
                {
                    yield return new Result("Copying {0} to project repository", packageToCopy.FullName);
                    using (var stream = packageToCopy.Load().OpenStream())
                        Environment.ProjectRepository.Publish(packageToCopy.FullName, stream);                    
                }
            }
        }
    }
    public static class PackageRepositoryExtensions
    {
        public static bool HasDependency(this IPackageRepository packageRepository, string name, Version version)
        {
            return packageRepository.Find(new WrapDependency { Name = name, VersionVertices = { new ExactVersionVertice(version) } }) != null;
        }
    }
}
