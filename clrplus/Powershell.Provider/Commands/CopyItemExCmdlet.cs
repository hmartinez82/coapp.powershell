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

namespace ClrPlus.Powershell.Provider.Commands {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using System.Threading;
    using System.Threading.Tasks;
    using Base;
    using Core.Exceptions;
    using Core.Extensions;
    using Filesystem;
    using Microsoft.PowerShell.Commands;
    using Utility;

    [Cmdlet("Copy", "ItemEx", DefaultParameterSetName = "Selector", SupportsShouldProcess = true, SupportsTransactions = false)]
    public class CopyItemExCmdlet : CopyItemCommand {
        public static ILocationResolver GetLocationResolver(ProviderInfo providerInfo) {
            var result = providerInfo as ILocationResolver;
            if (result == null) {
                if (providerInfo.Name == "FileSystem") {
                    return new FilesystemLocationProvider(providerInfo);
                }
            }
            if (result == null) {
                throw new ClrPlusException("Unable to create location resolver for {0}".format(providerInfo.Name));
            }
            return result;
        }

        private readonly CancellationTokenSource _cancellationToken = new CancellationTokenSource();


        protected override void BeginProcessing() {
           // Console.WriteLine("===BeginProcessing()===");
            base.BeginProcessing();
        }

        protected override void EndProcessing() {
            //Console.WriteLine("===EndProcessing()===");
            base.EndProcessing();
        }

        protected virtual void Process(ProviderInfo sourceProvider, IEnumerable<string> sourcePaths, ProviderInfo destinationProvider, string destinationPath) {
        }

        protected override void ProcessRecord() {
            ProviderInfo destinationProviderInfo;




            var destinationLocation = ResolveDestinationLocation(out destinationProviderInfo);




            var sources = Path.Select(each => {
                ProviderInfo spi;
                var sourceFiles = SessionState.Path.GetResolvedProviderPathFromPSPath(each, out spi);
                return new SourceSet {
                    ProviderInfo = spi,
                    SourcePaths = sourceFiles.ToArray(),
                };
            }).ToArray();






            var providerInfos = sources.Select(each => each.ProviderInfo).Distinct().ToArray();
            if (providerInfos.Length == 1 && providerInfos[0] == destinationProviderInfo) {
                WriteVerbose("Using regular copy-item");
                base.ProcessRecord();
                return;
            }

            bool force = Force;








            var copyOperations = ResolveSourceLocations(sources, destinationLocation).ToArray();

            if (copyOperations.Length > 1 && destinationLocation.IsFile) {
                // source can only be a single file.
                ThrowTerminatingError(new ErrorRecord(new DirectoryNotFoundException(), "0", ErrorCategory.InvalidArgument, null));
                //WriteError(new ErrorRecord(new ClrPlusException("Destination file exists--multiple source files specified."), "ErrorId", ErrorCategory.InvalidArgument, null));
                return;
            }


            var s = new Stopwatch();
            s.Start();
            for  (var i = 0; i < copyOperations.Length;  i++) {
                var operation = copyOperations[i];
                WriteProgress(CreateProgressRecord(1, "Copy", "Copying item {0} of {1}".format(i, copyOperations.Length), 100 * (double)i/copyOperations.Length));

                //Console.WriteLine("COPY '{0}' to '{1}'", operation.Source.AbsolutePath, operation.Destination.AbsolutePath);
                if (!force) {
                    if (operation.Destination.Exists) {
                        ThrowTerminatingError(new ErrorRecord(new ClrPlusException("Destination file '{0}' exists. Must use -force to override".format(operation.Destination.AbsolutePath)), "ErrorId", ErrorCategory.ResourceExists, null));
                        return;
                    }
                }



                using (var inputStream = new ProgressStream(operation.Source.Open(FileMode.Open))) {
                    using (var outputStream = new ProgressStream(operation.Destination.Open(FileMode.Create))) {

                        var inputLength = inputStream.Length;

                        inputStream.BytesRead += (sender, args) => {};
                        CopyOperation operation1 = operation;
                        outputStream.BytesWritten += (sender, args) => WriteProgress(CreateProgressRecord(2, "Copy",
                            "Copying '{0}' to '{1}'".format(operation1.Source.AbsolutePath, operation1.Destination.AbsolutePath), 100*(double)args.StreamPosition/inputLength, 1));

                        inputStream.CopyTo(outputStream, 32768);

                        /*
                        Task t = inputStream.CopyToAsync(outputStream, _cancellationToken.Token, false);
                        try {
                            t.RunSynchronously();
                        } catch (TaskCanceledException e) {
                            return;
                        }
                        */
                    }
                }

                WriteProgress(CreateCompletedProgressRecord(2, "Copy",
                            "Copying '{0}' to '{1}'".format(operation.Source.AbsolutePath, operation.Destination.AbsolutePath), 1));

               // WriteVerbose("Copy from {0} to {1}".format(operation.Source.AbsolutePath, operation.Destination.AbsolutePath));
            }
            WriteProgress(CreateCompletedProgressRecord(1, "Copy", "Copy finished"));
            s.Stop();
            WriteVerbose("Completed in {0}".format(s.Elapsed));

        }




        private ILocation ResolveDestinationLocation(out ProviderInfo destinationProviderInfo) {

            try {
                //if Destination doesn't exist, this will throw
                var destination = SessionState.Path.GetResolvedProviderPathFromPSPath(Destination, out destinationProviderInfo);
                var path = destination[0];

                return GetLocationResolver(destinationProviderInfo).GetLocation(path);

            } catch (Exception) {
                //the destination didn't exist, probably a file
                var lastSlash = Destination.LastIndexOf('\\');
                var hasASlash = lastSlash >= 0;
                var probablyDirectoryDestination = hasASlash ? Destination.Substring(0, lastSlash) : ".";
                //if this throws not even the directory exists
                var destination = SessionState.Path.GetResolvedProviderPathFromPSPath(probablyDirectoryDestination, out destinationProviderInfo);

                var path = destination[0];
                path += hasASlash ? Destination.Substring(lastSlash) : @"\" + Destination;

                return GetLocationResolver(destinationProviderInfo).GetLocation(path);
            }
        }







        private ProgressRecord CreateProgressRecord(int activityId, string activity, string statusDescription, double percentComplete, int parentActivityId = 0) {
            return new ProgressRecord(activityId, activity, statusDescription) {
                                                                                      PercentComplete = (int)percentComplete,
                                                                                      ParentActivityId = parentActivityId
                                                                                  };

        }

        private ProgressRecord CreateCompletedProgressRecord(int activityId, string activity, string statusDescription, int parentActivityId = 0) {
            return new ProgressRecord(activityId, activity, statusDescription) {
                                                              RecordType = ProgressRecordType.Completed,
                                                              ParentActivityId = parentActivityId
                                                          };
        }

        internal virtual IEnumerable<CopyOperation> ResolveSourceLocations(SourceSet[] sourceSet, ILocation destinationLocation) {
            bool copyContainer = this.Container;

            foreach (var src in sourceSet) {
                var resolver = GetLocationResolver(src.ProviderInfo);
                foreach (var path in src.SourcePaths) {
                    var location = resolver.GetLocation(path);
                    var absolutePath = location.AbsolutePath;

                    if (!location.IsFile) {
                        // if this is not a file, then it should be a container.
                        if (!location.IsItemContainer) {
                            throw new ClrPlusException("Unable to resolve path '{0}' to a file or folder.".format(path));
                        }

                        // if it's a container, get all the files in the container
                        var files = location.GetFiles(Recurse);
                        foreach (var f in files) {
                            var relativePath = (copyContainer ? location.Name + @"\\" : "") + absolutePath.GetRelativePath(f.AbsolutePath);
                            yield return new CopyOperation {
                                Destination = destinationLocation.IsFileContainer ? destinationLocation.GetChildLocation(relativePath) : destinationLocation,
                                Source = f
                            };
                        }
                        continue;
                    }

                    yield return new CopyOperation {
                        Destination = destinationLocation.IsFileContainer ?  destinationLocation.GetChildLocation(location.Name) : destinationLocation,
                        Source = location
                    };
                }
            }
        }

        protected override void StopProcessing() {

            base.StopProcessing();
            _cancellationToken.Cancel();

        }
    }
}