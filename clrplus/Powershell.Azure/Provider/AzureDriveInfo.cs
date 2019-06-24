//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2013 Garrett Serack and CoApp Contributors.
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License.
// </license>
//-----------------------------------------------------------------------

namespace ClrPlus.Powershell.Azure.Provider {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Management.Automation;
    using ClrPlus.Core.Collections;
    using ClrPlus.Core.Exceptions;
    using ClrPlus.Core.Extensions;
    using Microsoft.WindowsAzure.Storage;
    using Microsoft.WindowsAzure.Storage.Auth;
    using Microsoft.WindowsAzure.Storage.Blob;
    using Powershell.Provider.Utility;
    using Scripting.Languages.PropertySheet;

    public class AzureDriveInfo : PSDriveInfo {

        public const string SAS_GUID = "C28BE884-16CF-4401-8B30-18217CF8FF0D";

        internal const string ProviderScheme = "azure";
        internal const string ProviderDescription = "azure blob storage";

        internal Path Path;
        internal string Secret;
        private CloudStorageAccount _account;
        private CloudBlobClient _blobStore;
        private readonly IDictionary<string, CloudBlobContainer> _containerCache = new XDictionary<string, CloudBlobContainer>();

        private Uri _baseUri;
        private string _accountName;

        internal string HostAndPort {
            get {
                return Path.HostAndPort;
            }
        }

        internal string ContainerName {
            get {
                return Path.Container;
            }
        }

        private bool _isSas;

        internal string RootPath {
            get {
                return Path.SubPath;
            }
        }

        internal CloudBlobClient CloudFileSystem {
            get {
                if (_blobStore == null) {
                    // is the secret really a SAS token?
                    // Eric : this is the spot to use the token!
                    if (_isSas) {
                        _account = new CloudStorageAccount(new StorageCredentials(Secret), _baseUri.ToString(), null, true);

                        _blobStore = _account.CreateCloudBlobClient();

                        //get it to the right container and stuff
                      /*if (_blobStore == null)
                            throw new ClrPlusException("Couldn't get a CloudBlobClient for SasAccount {0} and SasContainer {1}".format(SasAccountUri, SasContainer));*/
                    } else {
                        _account = new CloudStorageAccount(new StorageCredentials(_accountName, Secret), _baseUri.ToString(), null, true);

                        _blobStore = _account.CreateCloudBlobClient();
                       /* if (_blobStore == null)
                            throw new ClrPlusException("Couldn't get a CloudBlobClient for SasAccount {0} and SasContainer {1}".format(SasAccountUri, SasContainer));*/
                    }

                }
                return _blobStore;
            }
        }


        internal CloudBlobContainer GetContainer(string containerName) {
            if (_containerCache.ContainsKey(containerName)) {
                return _containerCache[containerName];
            }
            var container = CloudFileSystem.GetContainerReference(containerName);

            if (_isSas) {
                try {
                    container.ListBlobs("$$$JUSTCHECKINGIFTHISCONTAINEREVENEXISTS$$$");
                    _containerCache.Add(containerName, container);
                } catch {
                    return null;
                }
                return container;
            } else {
                if (container.Exists()) {
                    _containerCache.Add(containerName, container);
                    return container;
                }
            }
            return null;
        }

        public AzureDriveInfo(Rule aliasRule, ProviderInfo providerInfo, PSCredential psCredential = null)
            : this(GetDriveInfo(aliasRule, providerInfo, psCredential)) {

            // continues where the GetDriveInfo left off.

            /*
            Path = new Path {
                HostAndPort = aliasRule.HasProperty("key") ? aliasRule["key"].Value : aliasRule.Parameter,
                Container = aliasRule.HasProperty("container") ? aliasRule["container"].Value : "",
                SubPath = aliasRule.HasProperty("root") ? aliasRule["root"].Value.Replace('/', '\\').Replace("\\\\", "\\").Trim('\\') : "",
            };
            Path.Validate();
            Secret = aliasRule.HasProperty("secret") ? aliasRule["secret"].Value : psCredential != null ? psCredential.Password.ToUnsecureString() : null;
             * */
            // Path.Validate();
            // Secret = aliasRule.HasProperty("secret") ? aliasRule["secret"].Value : psCredential != null ? psCredential.Password.ToUnsecureString() : null;
        }

        private static PSDriveInfo GetDriveInfo(Rule aliasRule, ProviderInfo providerInfo, PSCredential psCredential) {
            var name = aliasRule.Parameter;
            var account = aliasRule.HasProperty("key") ? aliasRule["key"].Value : name;
            var container = aliasRule.HasProperty("container") ? aliasRule["container"].Value : "";

            if(psCredential == null || (psCredential.UserName == null && psCredential.Password == null)) {
                psCredential = new PSCredential(account, aliasRule.HasProperty("secret") ? aliasRule["secret"].Value.ToSecureString() : null);
            }

            if (string.IsNullOrEmpty(container)) {
                return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\".format(ProviderScheme, account), ProviderDescription, psCredential);
            }

            var root = aliasRule.HasProperty("root") ? aliasRule["root"].Value.Replace('/', '\\').Replace("\\\\", "\\").Trim('\\') : "";

            if (string.IsNullOrEmpty(root)) {
                return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\{2}\".format(ProviderScheme, account, container), ProviderDescription, psCredential);
            }

            return new PSDriveInfo(name, providerInfo, @"{0}:\{1}\{2}\{3}\".format(ProviderScheme, account, container, root), ProviderDescription, psCredential);
        }

        public AzureDriveInfo(PSDriveInfo driveInfo)
            : base(driveInfo) {
            Init(driveInfo.Provider, driveInfo.Root, driveInfo.Credential);
        }

        public AzureDriveInfo(string name, ProviderInfo provider, string root, string description, PSCredential credential)
            : base(name, provider, root, description, credential) {
            Init(provider, root, credential);
        }
        /*
        public static string SetRoot(string root, PSCredential credential) {
            if (credential != null && credential.UserName.Contains(" "))
            {
                var sasUsernamePieces = credential.UserName.Split(' ');
                if (sasUsernamePieces.Length != 2)
                {
                    throw new ClrPlusException("Wrong number of SASUsername pieces, should be 2");

                }

                if (!sasUsernamePieces[1].IsWebUri())
                    throw new ClrPlusException("Second part of SASUsername must be a valid Azure Uri");

                var containerUri = new Uri(sasUsernamePieces[1]);

                //TODO Do I actually need to flip the slashes here? I'll do it to be safe for now
                root = @"azure:\\{0}\{1}".format(sasUsernamePieces[0], containerUri.AbsolutePath.Replace('/', '\\'));

                SasAccountUri = "https://" + containerUri.Host;
                SasContainer = containerUri.AbsolutePath;




                //it's a SASToken!
            }

            return root;
        }*/

        private void Init(ProviderInfo provider, string root, PSCredential credential) {


            // first, try to get the account from the credential
            // if that fails, attempt to get it from the root.
                // http://account.blob.core.windows.net ... guess the account, have the base uri
                // https://account.blob.core.windows.net ... guess the account, have the base uri

                // azure://coapp ... -> guess the account, generate the baseuri

                // http://downloads.coapp.org  user must supply account, have the base uri
                // https://downloads.coapp.org user must supply account, have the base uri
                // http://127.0.0.1:10000/     user must supply account, have the base uri

            var parsedPath = Path.ParseWithContainer(root);


            //check if Credential is Sas


            if (credential != null && credential.UserName != null && credential.Password != null) {

                if (credential.UserName.Contains(SAS_GUID)) {
                    _accountName = credential.UserName.Split(new[] {
                                                                       SAS_GUID
                                                                   }, StringSplitOptions.RemoveEmptyEntries)[0];
                    _isSas = true;
                } else
                    _accountName = credential.UserName;



                if(parsedPath.Scheme == ProviderScheme) {
                    // generate the baseuri like they do
                    _baseUri = new Uri("https://{0}.blob.core.windows.net/".format(_accountName));
                } else {
                    _baseUri = new Uri("{0}://{1}/".format(parsedPath.Scheme, parsedPath.HostAndPort));
                }

                Secret = credential.Password.ToUnsecureString();

            } else {
                if(parsedPath.Scheme == ProviderScheme) {
                    _accountName = parsedPath.HostName;
                    _baseUri = new Uri("https://{0}.blob.core.windows.net/".format(_accountName));
                }
                else if (parsedPath.HostName.ToLower().EndsWith(".blob.core.windows.net")) {
                    _accountName = parsedPath.HostName.Substring(0, parsedPath.HostName.IndexOf('.'));
                    _baseUri = new Uri("{0}://{1}/".format(parsedPath.Scheme, parsedPath.HostAndPort));
                } else {
                    // throw xxx
                }


            }

            Path = parsedPath;




            if (string.IsNullOrEmpty(parsedPath.HostAndPort) || string.IsNullOrEmpty(parsedPath.Scheme)) {
                Path = parsedPath;
                return; // this is the root azure namespace.
            }

            var pi = provider as AzureProviderInfo;
            if (pi == null) {
                throw new ClrPlusException("Invalid ProviderInfo");
            }


            var alldrives = (pi.AddingDrives.Union(pi.Drives)).Select(each => each as AzureDriveInfo).ToArray();

            if (parsedPath.Scheme == ProviderScheme) {
                // it's being passed a full url to a blob storage
                Path = parsedPath;

                if (credential == null || credential.Password == null) {
                    // look for another mount off the same account and container for the credential
                    foreach (var d in alldrives.Where(d => d.HostAndPort == HostAndPort && d.ContainerName == ContainerName)) {
                        Secret = d.Secret;
                        return;
                    }
                    // now look for another mount off just the same account for the credential
                    foreach(var d in alldrives.Where(d => d.HostAndPort == HostAndPort)) {
                        Secret = d.Secret;
                        return;
                    }
                    throw new ClrPlusException("Missing credential information for {0} mount '{1}'".format(ProviderScheme, root));
                }

                Secret = credential.Password.ToUnsecureString();
                return;
            }

            // otherwise, it's an sub-folder off of another mount.
            foreach (var d in alldrives.Where(d => d.Name == parsedPath.Scheme)) {
                Path = new Path {
                    HostAndPort = d.HostAndPort,
                    Container = string.IsNullOrEmpty(d.ContainerName) ? parsedPath.HostAndPort : d.ContainerName,
                    SubPath = string.IsNullOrEmpty(d.RootPath) ? parsedPath.SubPath : d.RootPath + '\\' + parsedPath.SubPath
                };
                Path.Validate();
                Secret = d.Secret;
                return;
            }

        }

        internal string ActualHostAndPort
        {
            get
            {
                return _baseUri == null ? "" : (((_baseUri.Scheme == "https" && _baseUri.Port == 443) || (_baseUri.Scheme == "http" && _baseUri.Port == 80) || _baseUri.Port == 0) ? _baseUri.Host : _baseUri.Host + ":" + _baseUri.Port);
            }
        }
    }
    /*
    internal class RelativeBlobDirectoryUri
    {
        internal string Container { get; private set; }
        internal IEnumerable<string> VirtualDirectories { get; private set; }

        internal RelativeBlobDirectoryUri(string relativeBlobDirectoryUri)
        {
            var splitString = relativeBlobDirectoryUri.Split('/');
            //if (splitString.Length ==0) bad!!!
            if (splitString.Length >= 1)
            {
                Container = splitString[0];

            }
            if (splitString.Length >= 2)
            {
                VirtualDirectories = splitString.Skip(1).ToList();
            }

        }
    }*/

}