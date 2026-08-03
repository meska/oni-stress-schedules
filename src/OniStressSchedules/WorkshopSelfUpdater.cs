/*
 * The direct Steam UGC download approach is derived from Peter Han's Mod Updater.
 * Mod Updater is licensed under the MIT License: https://github.com/peterhaneve/ONIMods
 */

using Ionic.Zip;
using KMod;
using Newtonsoft.Json;
using PeterHan.PLib.Core;
using Steamworks;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OniStressSchedules
{
    internal sealed class WorkshopSelfUpdater
    {
        private const ulong WorkshopId = 3770102539UL;
        private const string ExpectedAssembly = "OniStressSchedules.dll";
        private const string ExpectedStaticId = "OniStressSchedules";
        private const string StateFileName = "meska-workshop-self-updater.json";
        private const string CoordinatorKey = "Meskatech.ONI.WorkshopSelfUpdater.3770102539";
        private static readonly object StateLock = string.Intern("Meskatech.ONI.WorkshopSelfUpdater.State");
        private static readonly Regex VersionLine = new Regex(
            @"^\s*version\s*:\s*[""']?(?<version>\d+(?:\.\d+){1,3})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);
        private static readonly Regex StaticIdLine = new Regex(
            @"^\s*staticID\s*:\s*[""']?(?<id>[A-Za-z0-9_.-]+)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Multiline);

        private static WorkshopSelfUpdater instance;

        private readonly KMod.Mod mod;
        private CallResult<SteamUGCQueryCompleted_t> queryResult;
        private CallResult<RemoteStorageDownloadUGCResult_t> downloadResult;
        private UGCQueryHandle_t queryHandle;
        private uint remoteTimestamp;

        private WorkshopSelfUpdater(KMod.Mod mod)
        {
            this.mod = mod;
            queryHandle = UGCQueryHandle_t.Invalid;
        }

        internal static void Start(KMod.Mod mod)
        {
            if (mod == null || mod.label.distribution_platform != Label.DistributionPlatform.Steam)
                return;

            // Un coordinator per AppDomain evita doppi callback se ONI ricrea la schermata principale.
            if (AppDomain.CurrentDomain.GetData(CoordinatorKey) is bool started && started)
                return;

            AppDomain.CurrentDomain.SetData(CoordinatorKey, true);
            instance = new WorkshopSelfUpdater(mod);
            try
            {
                if (!instance.ReconcilePendingUpdate())
                    instance.QueryLatestPackage();
            }
            catch (Exception exception)
            {
                PUtil.LogWarning("[Stress Schedules] Self-update initialization failed:");
                PUtil.LogExcWarn(exception);
                instance.Finish(null);
            }
        }

        private static string DownloadPath => Path.Combine(Manager.GetDirectory(), WorkshopId + ".self-update.zip");

        private static string StatePath => Path.Combine(Manager.GetDirectory(), StateFileName);

        private void QueryLatestPackage()
        {
            var ids = new[] { new PublishedFileId_t(WorkshopId) };
            queryHandle = SteamUGC.CreateQueryUGCDetailsRequest(ids, 1U);
            if (queryHandle == UGCQueryHandle_t.Invalid)
            {
                Finish("Steam refused the Workshop details request");
                return;
            }

            SteamAPICall_t call = SteamUGC.SendQueryUGCRequest(queryHandle);
            if (call == SteamAPICall_t.Invalid)
            {
                Finish("Steam refused the Workshop query");
                return;
            }

            queryResult = new CallResult<SteamUGCQueryCompleted_t>(OnQueryComplete);
            queryResult.Set(call);
        }

        private void OnQueryComplete(SteamUGCQueryCompleted_t result, bool ioError)
        {
            try
            {
                if (ioError || result.m_eResult != EResult.k_EResultOK || result.m_unNumResultsReturned < 1U)
                {
                    Finish("Steam Workshop details were unavailable");
                    return;
                }

                if (!SteamUGC.GetQueryUGCResult(result.m_handle, 0U, out SteamUGCDetails_t details)
                    || details.m_nPublishedFileId.m_PublishedFileId != WorkshopId
                    || details.m_hFile == UGCHandle_t.Invalid)
                {
                    Finish("Steam returned incomplete Workshop details");
                    return;
                }

                remoteTimestamp = details.m_rtimeUpdated;
                UpdateEntry state = ReadEntry();
                if (!WorkshopUpdatePolicy.ShouldInspect(remoteTimestamp, state.CheckedTimestamp))
                {
                    Finish(null);
                    return;
                }

                // UGCDownloadToLocation usa l'handle appena interrogato e salta il legacy.bin stantio.
                TryDelete(DownloadPath);
                SteamAPICall_t call = SteamRemoteStorage.UGCDownloadToLocation(details.m_hFile, DownloadPath, 0U);
                if (call == SteamAPICall_t.Invalid)
                {
                    Finish("Steam refused the direct Workshop download");
                    return;
                }

                downloadResult = new CallResult<RemoteStorageDownloadUGCResult_t>(OnDownloadComplete);
                downloadResult.Set(call);
            }
            catch (Exception exception)
            {
                PUtil.LogWarning("[Stress Schedules] Workshop query failed:");
                PUtil.LogExcWarn(exception);
                Finish(null);
            }
            finally
            {
                queryResult?.Dispose();
                queryResult = null;
                ReleaseQuery();
            }
        }

        private void OnDownloadComplete(RemoteStorageDownloadUGCResult_t result, bool ioError)
        {
            bool reinstallScheduled = false;
            try
            {
                if (ioError || (result.m_eResult != EResult.k_EResultOK
                    && result.m_eResult != EResult.k_EResultAdministratorOK))
                {
                    TryDelete(DownloadPath);
                    Finish("Steam could not download the current Workshop package");
                    return;
                }

                if (!TryValidatePackage(DownloadPath, out string remoteVersion))
                {
                    TryDelete(DownloadPath);
                    Finish("The downloaded Workshop package was invalid");
                    return;
                }

                string currentVersion = mod.packagedModInfo?.version ?? "0.0.0";
                if (!WorkshopUpdatePolicy.IsNewerVersion(remoteVersion, currentVersion))
                {
                    SaveChecked(remoteTimestamp);
                    TryDelete(DownloadPath);
                    Finish(null);
                    return;
                }

                PreserveConfiguration(DownloadPath);
                if (!TryValidatePackage(DownloadPath, out string preservedVersion)
                    || preservedVersion != remoteVersion)
                    throw new InvalidDataException("Configuration merge produced an invalid Workshop package.");

                KMod.Mod.Status originalStatus = mod.status;
                string originalReinstallPath = mod.reinstall_path;
                mod.status = KMod.Mod.Status.ReinstallPending;
                mod.reinstall_path = DownloadPath;
                try
                {
                    PGameUtils.SaveMods();
                    reinstallScheduled = true;
                }
                catch
                {
                    // Se el save casca, torna subito a uno stato coerente col disco.
                    mod.status = originalStatus;
                    mod.reinstall_path = originalReinstallPath;
                    throw;
                }

                TrySavePending(remoteTimestamp, remoteVersion);
                UnityEngine.Debug.Log("[Stress Schedules] Workshop update {0} downloaded; ONI will install it after restart.".F(remoteVersion));
                Finish(null, retainCoordinator: true);
            }
            catch (Exception exception)
            {
                if (!reinstallScheduled)
                    TryDelete(DownloadPath);
                PUtil.LogWarning("[Stress Schedules] Self-update failed:");
                PUtil.LogExcWarn(exception);
                Finish(null, retainCoordinator: reinstallScheduled);
            }
        }

        private static bool TryValidatePackage(string packagePath, out string version)
        {
            version = null;
            string validationPath = Path.Combine(Manager.GetDirectory(), WorkshopId + ".self-update-validation");
            try
            {
                var packageInfo = new FileInfo(packagePath);
                if (!packageInfo.Exists || packageInfo.Length < 1L || packageInfo.Length > 50L * 1024L * 1024L)
                    return false;

                using (var package = ZipFile.Read(packagePath))
                {
                    ZipEntry metadata = package["mod_info.yaml"];
                    ZipEntry manifest = package["mod.yaml"];
                    ZipEntry assembly = package[ExpectedAssembly];
                    if (metadata == null || manifest == null || assembly == null || assembly.UncompressedSize < 1L
                        || assembly.UncompressedSize > 20L * 1024L * 1024L)
                        return false;

                    long totalSize = 0L;
                    foreach (ZipEntry entry in package)
                    {
                        if (!WorkshopUpdatePolicy.IsSafeArchivePath(entry.FileName)
                            || entry.UncompressedSize < 0L
                            || entry.UncompressedSize > 50L * 1024L * 1024L)
                            return false;
                        totalSize += entry.UncompressedSize;
                        if (totalSize > 100L * 1024L * 1024L)
                            return false;
                    }

                    using (Stream stream = metadata.OpenReader())
                    using (var reader = new StreamReader(stream))
                    {
                        Match match = VersionLine.Match(reader.ReadToEnd());
                        if (match.Success)
                            version = match.Groups["version"].Value;
                    }

                    using (Stream stream = manifest.OpenReader())
                    using (var reader = new StreamReader(stream))
                    {
                        Match match = StaticIdLine.Match(reader.ReadToEnd());
                        if (!match.Success || match.Groups["id"].Value != ExpectedStaticId)
                            return false;
                    }

                    TryDeleteDirectory(validationPath);
                    package.ExtractAll(validationPath, ExtractExistingFileAction.OverwriteSilently);
                }

                if (string.IsNullOrEmpty(version))
                    return false;

                AssemblyName assemblyName = AssemblyName.GetAssemblyName(Path.Combine(validationPath, ExpectedAssembly));
                var expectedVersion = new System.Version(version);
                if (assemblyName.Name != Path.GetFileNameWithoutExtension(ExpectedAssembly)
                    || assemblyName.Version == null
                    || assemblyName.Version.Major != expectedVersion.Major
                    || assemblyName.Version.Minor != expectedVersion.Minor
                    || assemblyName.Version.Build != expectedVersion.Build)
                    return false;
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ZipException
                || exception is BadImageFormatException
                || exception is ArgumentException)
            {
                return false;
            }
            finally
            {
                TryDeleteDirectory(validationPath);
            }

            return true;
        }

        private void PreserveConfiguration(string packagePath)
        {
            if (string.IsNullOrEmpty(mod.label.install_path))
                return;

            string configPath = Path.Combine(mod.label.install_path, "config.json");
            if (!File.Exists(configPath) || new FileInfo(configPath).Length > 100 * 1024L)
                return;

            string temporaryPath = packagePath + ".tmp";
            TryDelete(temporaryPath);
            using (var package = ZipFile.Read(packagePath))
            {
                package.UpdateFile(configPath, string.Empty);
                package.Save(temporaryPath);
            }

            File.Copy(temporaryPath, packagePath, true);
            TryDelete(temporaryPath);
        }

        private static UpdateEntry ReadEntry()
        {
            lock (StateLock)
            {
                StateDocument state = ReadState();
                string id = WorkshopId.ToString();
                return state.Mods.TryGetValue(id, out UpdateEntry entry) && entry != null
                    ? entry
                    : new UpdateEntry();
            }
        }

        private bool ReconcilePendingUpdate()
        {
            UpdateEntry entry;
            try
            {
                entry = ReadEntry();
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException)
            {
                PUtil.LogWarning("[Stress Schedules] Could not read self-update state; retrying from Steam.");
                return false;
            }

            PendingUpdateAction action = WorkshopUpdatePolicy.GetPendingAction(
                entry.PendingTimestamp,
                entry.PendingVersion,
                mod.packagedModInfo?.version ?? "0.0.0",
                mod.status == KMod.Mod.Status.ReinstallPending,
                File.Exists(mod.reinstall_path));
            if (action == PendingUpdateAction.WaitForRestart)
            {
                Finish(null, retainCoordinator: true);
                return true;
            }

            if (action == PendingUpdateAction.MarkApplied)
            {
                entry.CheckedTimestamp = entry.PendingTimestamp;
                ClearPending(entry);
                TrySaveEntry(entry);
                TryDelete(DownloadPath);
            }
            else if (action == PendingUpdateAction.Retry)
            {
                // Reinstall fallita o ZIP sparito: no segnalarlo come visto, va riscaricato.
                ClearPending(entry);
                TrySaveEntry(entry);
                TryDelete(DownloadPath);
            }

            return false;
        }

        private static void ClearPending(UpdateEntry entry)
        {
            entry.PendingTimestamp = 0U;
            entry.PendingVersion = null;
        }

        private static void SaveChecked(uint timestamp)
        {
            UpdateEntry entry = ReadEntry();
            entry.CheckedTimestamp = timestamp;
            ClearPending(entry);
            SaveEntry(entry);
        }

        private static void SavePending(uint timestamp, string version)
        {
            UpdateEntry entry = ReadEntry();
            entry.PendingTimestamp = timestamp;
            entry.PendingVersion = version;
            SaveEntry(entry);
        }

        private static void SaveEntry(UpdateEntry entry)
        {
            lock (StateLock)
            {
                StateDocument state = ReadState();
                state.Mods[WorkshopId.ToString()] = entry;
                WriteState(state);
            }
        }

        private static void TrySavePending(uint timestamp, string version)
        {
            try
            {
                SavePending(timestamp, version);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // L'update resta schedulato; senza stato verrà solo ricontrollato al prossimo avvio.
                PUtil.LogWarning("[Stress Schedules] Could not save pending self-update state.");
            }
        }

        private static void TrySaveEntry(UpdateEntry entry)
        {
            try
            {
                SaveEntry(entry);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                PUtil.LogWarning("[Stress Schedules] Could not reconcile self-update state.");
            }
        }

        private static StateDocument ReadState()
        {
            try
            {
                if (File.Exists(StatePath))
                {
                    StateDocument state = JsonConvert.DeserializeObject<StateDocument>(File.ReadAllText(StatePath))
                        ?? new StateDocument();
                    state.Mods = state.Mods ?? new Dictionary<string, UpdateEntry>();
                    return state;
                }
            }
            catch (Exception exception) when (exception is IOException
                || exception is UnauthorizedAccessException
                || exception is JsonException)
            {
                // Stato rotto? Se riparte da vuoto al massimo ricontrolla un pacchetto, gnente de grave.
            }

            return new StateDocument();
        }

        private static void WriteState(StateDocument state)
        {
            string temporaryPath = StatePath + ".tmp";
            string json = JsonConvert.SerializeObject(state, Formatting.Indented);
            File.WriteAllText(temporaryPath, json);
            File.Copy(temporaryPath, StatePath, true);
            TryDelete(temporaryPath);
        }

        private void ReleaseQuery()
        {
            if (queryHandle != UGCQueryHandle_t.Invalid)
            {
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                queryHandle = UGCQueryHandle_t.Invalid;
            }
        }

        private void Finish(string warning, bool retainCoordinator = false)
        {
            if (!string.IsNullOrEmpty(warning))
                PUtil.LogWarning("[Stress Schedules] Self-update skipped: " + warning + ".");
            queryResult?.Dispose();
            queryResult = null;
            downloadResult?.Dispose();
            downloadResult = null;
            ReleaseQuery();
            if (!retainCoordinator)
                AppDomain.CurrentDomain.SetData(CoordinatorKey, false);
            instance = null;
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                    Directory.Delete(path, true);
            }
            catch (IOException)
            {
                // Directory temporanea: un retry la ripulisce prima di validare.
            }
            catch (UnauthorizedAccessException)
            {
                // Se resta bloccata, la validazione successiva fallirà in sicurezza.
            }
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (IOException)
            {
                // El prossimo giro riprova: no serve far casìn par un file temporaneo.
            }
            catch (UnauthorizedAccessException)
            {
                // Come sora: ONI continua a caricarse anche se la pulizia no riesse.
            }
        }

        private sealed class StateDocument
        {
            public Dictionary<string, UpdateEntry> Mods { get; set; } = new Dictionary<string, UpdateEntry>();
        }

        private sealed class UpdateEntry
        {
            public uint CheckedTimestamp { get; set; }

            public uint PendingTimestamp { get; set; }

            public string PendingVersion { get; set; }
        }
    }
}
