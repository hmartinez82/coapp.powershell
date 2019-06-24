//-----------------------------------------------------------------------
// <copyright company="CoApp Project">
//     Copyright (c) 2010-2012 Garrett Serack and CoApp Contributors.
//     Contributors can be discovered using the 'git log' command.
//     All rights reserved.
// </copyright>
// <license>
//     The software is licensed under the Apache 2.0 License (the "License")
//     You may not use the software except in compliance with the License.
// </license>
//-----------------------------------------------------------------------

namespace CoApp.Powershell.Commands {
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using ClrPlus.Core.Extensions;
    using ClrPlus.Core.Utility;
    using ClrPlus.Platform;
    using ClrPlus.Powershell.Core;
    using ClrPlus.Scripting.Languages.PropertySheet;
    using ClrPlus.Scripting.Languages.PropertySheetV3;
    using ClrPlus.Scripting.MsBuild.Packaging;

    [Cmdlet(AllVerbs.Write, "NuGetPackage")]
    public class WriteNuGetPackage : BaseCmdlet {

        static WriteNuGetPackage() {
            // ensure that the etc folder is added to the path.
            var x = CmdletUtility.EtcPath;
        }


        [Parameter(HelpMessage = "Autopackage script file (.autopkg)", Mandatory = true, Position = 0)]
        public string Package {get; set;}

        [Parameter(HelpMessage = "Don't clean up intermediate files")]
        public SwitchParameter NoClean {get; set;}

        [Parameter(HelpMessage = "Don't acutally package the files up (just generate the intemediate files)")]
        public SwitchParameter GenerateOnly { get; set; }

        [Parameter(HelpMessage = "Directory where dependent packages are found ")]
        public string PackageDirectory {get; set;}

        [Parameter(HelpMessage = "Size (in megabytes) after which pivot combinations split into satellite packages. Defaults to 10")]
        public long SplitThreshold { get; set; }

        [Parameter(HelpMessage = "Never Split the package into overlays.")]
        public SwitchParameter NoSplit { get; set; }


        [Parameter]
        public string[] Define {
            get;
            set;
        }

        [Parameter]
        public string[] Defines {
            get;
            set;
        }

        public WriteNuGetPackage() {
            SplitThreshold = 10;
        }

        protected override void ProcessRecord() {
#if USING_RESTABLE_CMDLET
            if (Remote) {
                ProcessRecordViaRest();
                return;
            }
#endif

            ProviderInfo packagePathProviderInfo;
            var pkgPath = SessionState.Path.GetResolvedProviderPathFromPSPath(Package, out packagePathProviderInfo);

            using (var local = LocalEventSource) {
                local.Events += new SourceError((code, location, message, objects) => {
                    location = location ?? SourceLocation.Unknowns;
                    Host.UI.WriteErrorLine("{0}:Error {1}:{2}".format(location.FirstOrDefault(), code, message.format(objects)));
                    return true;
                });

                if (!NoWarnings) {
                    local.Events += new SourceWarning((code, location, message, objects) => {
                        WriteWarning("{0}:Warning {1}:{2}".format((location ?? SourceLocation.Unknowns).FirstOrDefault(), message.format(objects)));
                        return false;
                    });
                }

                local.Events += new SourceDebug((code, location, message, objects) => {
                    WriteVerbose("{0}:DebugMessage {1}:{2}".format((location ?? SourceLocation.Unknowns).FirstOrDefault(), code, message.format(objects)));
                    return false;
                });

                using (var script = new PackageScript(pkgPath.FirstOrDefault())) {
                    script.SplitThreshold = SplitThreshold * 1024 * 1024;
                    if (NoSplit.IsPresent) {
                        script.NoSplit = true;
                    }

                    if (PackageDirectory.Is()) {
                        script.AddNuGetPackageDirectory(PackageDirectory.GetFullPath());
                    }
                    if (Defines != null) {
                        foreach (var i in Defines) {
                            var p = i.IndexOf("=");
                            var k = p > -1 ? i.Substring(0, p) : i;
                            var v = p > -1 ? i.Substring(p + 1) : "";
                            script.AddMacro(k, v);
                        }
                    }
                    if (Define != null) {
                        foreach (var i in Define) {
                            var p = i.IndexOf("=");
                            var k = p > -1 ? i.Substring(0, p) : i;
                            var v = p > -1 ? i.Substring(p + 1) : "";
                            script.AddMacro(k, v);
                        }
                    }
                    IEnumerable<string> overlayPackages;
                    var pkgFile = script.Save(PackageTypes.NuGet, !NoClean, GenerateOnly, out overlayPackages);
                }
            }
        }
    }
}