// ----------------------------------------------------------------------
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

namespace ClrPlus.Scripting.MsBuild.Building {
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using CSharpTest.Net.RpcLibrary;
    using Core.Extensions;
    using Core.Tasks;
    using Languages.PropertySheet;
    using Languages.PropertySheetV3;
    using Languages.PropertySheetV3.Mapping;
    using Languages.PropertySheetV3.RValue;
    using Microsoft.Build.Construction;
    using MsBuild.Utility;
    using Packaging;
    using Platform;
    using Platform.Process;

    public static class MSBuildUtility {
        public static Executable MsbuildExe = new Executable("msbuild.exe", Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));

        static MSBuildUtility() {
            // Ensure that the same path for the MSBuildEXE is in the path
            EnvironmentUtility.EnvironmentPath = EnvironmentUtility.EnvironmentPath.Append(Path.GetDirectoryName(MsbuildExe.Path));

            // as is the .NET framework folder
            EnvironmentUtility.EnvironmentPath = EnvironmentUtility.EnvironmentPath.Append(EnvironmentUtility.DotNetFrameworkFolder);
        }
    }

    public class BuildScript : IDisposable, IProjectOwner {
        private static string[] filterMessages = new[] {
            "due to false condition", "Environment Variables passed to tool"
        };

        private readonly Pivots _pivots;
        private readonly ProjectPlus _project;
        public int MaxThreads = Environment.ProcessorCount;
        private IDictionary<string, string> _macros = new Dictionary<string, string>();
        protected RootPropertySheet _sheet;
        private bool _stop;
        internal IDictionary<string, IValue> productInformation;

        public BuildScript(string filename) {
            try {
                Filename = filename.GetFullPath();
                _project = new ProjectPlus(this, Filename + ".msbuild");
                _project.Xml.AddImport(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "etc", "MSBuild.ExtensionPack.tasks"));

                _sheet = new RootPropertySheet(_project);
                _sheet.ParseFile(Filename);

                _pivots = new Pivots(_sheet.View.configurations);

                _sheet.AddChildRoutes(_project.MemberRoutes);
                // _sheet.AddChildRoutesForType(typeof (ProjectTargetElement), _project.TargetRoutes);

                _sheet.CurrentView.AddMacroHandler((name, context) => _macros.ContainsKey(name.ToLower()) ? _macros[name.ToLower()] : null);
                _sheet.CurrentView.AddMacroHandler((name, context) => Environment.GetEnvironmentVariable(name));
                // convert #product-info into a dictionary.
                productInformation = _sheet.Metadata.Value.Keys.Where(each => each.StartsWith("product-info")).ToXDictionary(each => each.Substring(12), each => _sheet.Metadata.Value[each]);
            } catch(Exception /*e*/) {
                // e.PrintDump();
                Dispose();
            }
        }

        public string Filename {get; set;}

        private static IEnumerable<ToRoute> PtkRoutes {
            get {
                yield return "condition".MapTo<ProjectTargetElement>(tgt => tgt.Condition());
                yield return "*".MapTo<ProjectTargetElement>(tgt => tgt.Condition());

                yield return "$$INDEXED".MapIndexedChildrenTo<ProjectTargetElement>((tgt, child) => tgt.GetTargetItem(child)); // .tasks
            }
        }

        public void Dispose() {
            _sheet = null;
            _project.Dispose();
        }

        public Pivots Pivots {
            get {
                return _pivots;
            }
        }

        public string ProjectName {
            get {
                return Path.GetFileNameWithoutExtension(Filename);
            }
        }

        public string Directory {
            get {
                return Path.GetDirectoryName(Filename);
            }
        }

        public void AddMacro(string key, string value) {
            _macros.AddOrSet(key.ToLower(), value);
        }

        public string EmitScript() {
            _sheet.CopyToModel();
            return Save();
        }

        public string ScriptText() {
            _sheet.CopyToModel();
            var path = Save();
            var result = File.ReadAllText(path);
            path.TryHardToDelete();
            return result;
        }

        public void Stop() {
            _stop = true;
        }

        private static Guid iid = Guid.NewGuid();
        private static string pipeName = @"\pipe\ptk_{0}".format(Process.GetCurrentProcess().Id);
        private static Lazy<RpcServerApi> server;

        static BuildScript() {
            server = new Lazy<RpcServerApi>(() => {
                var result = new RpcServerApi(iid);
                result.AddProtocol(RpcProtseq.ncacn_np, pipeName, 5);
                //Authenticate via WinNT
                // server.AddAuthentication(RpcAuthentication.RPC_C_AUTHN_WINNT);

                //Start receiving calls
                result.StartListening();
                return result;
            });
        }

        public event MSBuildMessage BuildMessage;

        private readonly ConcurrentQueue<BuildMessage> messages = new ConcurrentQueue<BuildMessage>();


        public bool Execute(string[] targets = null) {
            _sheet.CopyToModel();

            targets = targets ?? new string[0];


            var path = Save();
            var result = true;

            try {
                //When a call comes, do the following:
                server.Value.OnExecute += ServerOnOnExecute;

                // Event<Verbose>.Raise("script", "\r\n\r\n{0}\r\n\r\n", File.ReadAllText(path));

                var targs = targets.IsNullOrEmpty() ? string.Empty : targets.Aggregate("/target:", (cur, each) => cur + each + ";").TrimEnd(';');

                var etcPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "etc") + "/";
                Environment.SetEnvironmentVariable("CoAppEtcDirectory", etcPath);

                // remove some variables...
                Environment.SetEnvironmentVariable("BuildingInsideVisualStudio", null);
                Environment.SetEnvironmentVariable("UsePTKFromVisualStudio", null);

                var proc = Process.Start(new ProcessStartInfo(MSBuildUtility.MsbuildExe.Path,
                    @" /nologo /noconsolelogger ""/logger:ClrPlus.Scripting.MsBuild.Building.Logger,{0};{1};{2}"" /m:{6} /p:MaxThreads={6} ""/p:CoAppEtcDirectory={3}"" {4} ""{5}""".format(Assembly.GetExecutingAssembly().Location, pipeName, iid, etcPath, targs,
                        path, MaxThreads > 0 ? MaxThreads : Environment.ProcessorCount)) {
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                        });

                while (!proc.HasExited) {
                    // check our messages -- we need to work on the calling thread.
                    // Thanks powershell, I appreciate working like it's 1989 again.
                    Thread.Sleep(20); // not so tight of loop.

                        while (messages.Any()) {
                        BuildMessage obj;
                        if (messages.TryDequeue(out obj) && obj != null) {
                                if (BuildMessage != null) {
                                    BuildMessage(obj);
                                }

                                Event<MSBuildMessage>.Raise(obj);

                                if (obj.EventType == "BuildError") {
                                    result = false;
                                }
                            }
                    }
                }
                while (!proc.WaitForExit(20)) {
                    if (_stop) {
                        proc.Kill();
                    }
                }

                var stderr = proc.StandardError.ReadToEnd();
                if (stderr.Is()) {
                    Event<Error>.Raise("stderr", stderr);
                }

                var stdout = proc.StandardOutput.ReadToEnd();
                if (stdout.Is()) {
                    Event<Verbose>.Raise("stdout", stdout);
                }

                path.TryHardToDelete();
            }
            catch {
                result = false;
            }
            finally {
                server.Value.OnExecute -= ServerOnOnExecute;
            }
            return result;
        }

        private byte[] ServerOnOnExecute(IRpcClientInfo client, byte[] input) {
                 // messages.Enqueue(JsonSerializer.DeserializeFromString<BuildMessage>(input.ToUtf8String()));
            messages.Enqueue(ClrPlus.Scripting.MsBuild.Building.BuildMessage.DeserializeFromString(input.ToUtf8String()));
            return new byte[0];
        }

        public string Save(string filename = null) {
            filename = filename ?? Filename + ("." + DateTime.Now.Ticks + ".msbuild").MakeSafeFileName(); //  filename ?? "pkt.msbuild".GenerateTemporaryFilename();
            _project.Save(filename);
            return filename;
        }
    }
}