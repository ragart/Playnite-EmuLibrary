using EmuLibrary.RomTypes;
using EmuLibrary.RomTypes.M3uPlaylist;
using EmuLibrary.RomTypes.MultiFile;
using EmuLibrary.RomTypes.SingleFile;
using EmuLibrary.Settings;
using EmuLibrary.Util.FileCopier;
using Playnite.SDK;
using Playnite.SDK.Events;
using Playnite.SDK.Models;
using Playnite.SDK.Plugins;
using ProtoBuf.Meta;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace EmuLibrary
{
    public class EmuLibrary : LibraryPlugin, IEmuLibrary
    {
        // LibraryPlugin fields
        public override Guid Id { get; } = PluginId;
        public override string Name => s_pluginName;
        public override string LibraryIcon => Icon;

        // IEmuLibrary fields
        public ILogger Logger => LogManager.GetLogger();
        public IPlayniteAPI Playnite { get; private set; }
        public Settings.Settings Settings { get; private set; }
        RomTypeScanner IEmuLibrary.GetScanner(RomType romType) => _scanners[romType];

        private const string s_pluginName = "EmuLibrary";

        internal static readonly string Icon = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), @"icon.png");
        internal static readonly Guid PluginId = Guid.Parse("41e49490-0583-4148-94d2-940c7c74f1d9");
        internal static readonly MetadataNameProperty SourceName = new MetadataNameProperty(s_pluginName);

        private readonly Dictionary<RomType, RomTypeScanner> _scanners = new Dictionary<RomType, RomTypeScanner>();
        private int _backgroundMaintenanceRunning;

        public EmuLibrary(IPlayniteAPI api) : base(api)
        {
            Playnite = api;

            // This must occur before we instantiate the Settings class
            InitializeRomTypeScanners();

            Settings = new Settings.Settings(this, this);
        }

        private void InitializeRomTypeScanners()
        {
            var romTypes = Enum.GetValues(typeof(RomType)).Cast<RomType>();
            foreach (var rt in romTypes)
            {
                var fieldInfo = rt.GetType().GetField(rt.ToString());
                if (fieldInfo == null)
                {
                    Logger.Warn($"Failed to find FieldInfo for RomType {rt}. Skipping...");
                    continue;
                }

                var romInfo = fieldInfo.GetCustomAttributes(false).OfType<RomTypeInfoAttribute>().FirstOrDefault();
                if (romInfo == null)
                {
                    Logger.Warn($"Failed to find {nameof(RomTypeInfoAttribute)} for RomType {rt}. Skipping...");
                    continue;
                }

                if (romInfo.GameInfoType == null || !typeof(ELGameInfo).IsAssignableFrom(romInfo.GameInfoType))
                {
                    Logger.Error($"Invalid GameInfoType '{romInfo.GameInfoType}' for RomType {rt}. Skipping...");
                    continue;
                }

                if (romInfo.ScannerType == null || !typeof(RomTypeScanner).IsAssignableFrom(romInfo.ScannerType))
                {
                    Logger.Error($"Invalid ScannerType '{romInfo.ScannerType}' for RomType {rt}. Skipping...");
                    continue;
                }

                // Hook up ProtoInclude on ELGameInfo for each RomType
                // Starts at field number 10 to not conflict with ELGameInfo's fields
                try
                {
                    RuntimeTypeModel.Default[typeof(ELGameInfo)].AddSubType((int)rt + 10, romInfo.GameInfoType);
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to register protobuf subtype for RomType {rt} (GameInfoType {romInfo.GameInfoType}). {ex}");
                    continue;
                }

                var scannerConstructor = romInfo.ScannerType.GetConstructor(new[] { typeof(IEmuLibrary) });
                if (scannerConstructor == null)
                {
                    Logger.Error($"Failed to find constructor scanner(IEmuLibrary) for RomType {rt} (using {romInfo.ScannerType}).");
                    continue;
                }

                RomTypeScanner scanner;
                try
                {
                    scanner = scannerConstructor.Invoke(new object[] { this }) as RomTypeScanner;
                }
                catch (Exception ex)
                {
                    Logger.Error($"Failed to instantiate scanner for RomType {rt} (using {romInfo.ScannerType}). {ex}");
                    continue;
                }

                if (scanner == null)
                {
                    Logger.Error($"Scanner instance for RomType {rt} resolved to null (using {romInfo.ScannerType}).");
                    continue;
                }

                if (_scanners.ContainsKey(rt))
                {
                    Logger.Warn($"Scanner for RomType {rt} is already initialized. Skipping duplicate registration.");
                    continue;
                }

                _scanners.Add(rt, scanner);
            }
        }

        public override IEnumerable<GameMetadata> GetGames(LibraryGetGamesArgs args)
        {
            if (Playnite.ApplicationInfo.Mode == ApplicationMode.Fullscreen && !Settings.ScanGamesInFullScreen)
            {
                yield break;
            }

            foreach (var mapping in Settings.Mappings?.Where(m => m.Enabled))
            {
                if (args.CancelToken.IsCancellationRequested)
                    yield break;

                if (mapping.Emulator == null)
                {
                    Logger.Warn($"Emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }

                if (mapping.EmulatorProfile == null)
                {
                    Logger.Warn($"Emulator profile {mapping.EmulatorProfileId} for emulator {mapping.EmulatorId} not found, skipping.");
                    continue;
                }

                if (mapping.Platform == null)
                {
                    Logger.Warn($"Platform {mapping.PlatformId} not found, skipping.");
                    continue;
                }

                if (!_scanners.TryGetValue(mapping.RomType, out RomTypeScanner scanner))
                {
                    Logger.Warn($"Rom type {mapping.RomType} not supported, skipping.");
                    continue;
                }

                foreach (var g in scanner.GetGames(mapping, args))
                {
                    yield return g;
                }
            }

            QueueBackgroundMaintenance();
        }

        public override ISettings GetSettings(bool firstRunSettings) => Settings;
        public override UserControl GetSettingsView(bool firstRunSettings) => new SettingsView();

        public override IEnumerable<InstallController> GetInstallActions(GetInstallActionsArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                yield return args.Game.GetELGameInfo().GetInstallController(args.Game, this);
            }
        }

        public override IEnumerable<UninstallController> GetUninstallActions(GetUninstallActionsArgs args)
        {
            if (args.Game.PluginId == Id)
            {
                yield return args.Game.GetELGameInfo().GetUninstallController(args.Game, this);
            }
        }

        public override void OnGameInstalled(OnGameInstalledEventArgs args)
        {
            base.OnGameInstalled(args);

            if (args.Game.PluginId == PluginId && Settings.NotifyOnInstallComplete)
            {
                Playnite.Notifications.Add(args.Game.GameId, $"Installation of \"{args.Game.Name}\" has completed", NotificationType.Info);
            }
        }

        public override void OnGameStarting(OnGameStartingEventArgs args)
        {
            var game = args.Game;
            if (game.PluginId != Id)
                return;

            var info = game.GetELGameInfo();
            if (info == null)
                return;

            if (info.CheckSourceExists())
                return;

            args.CancelStartup = true;
            Playnite.Dialogs.ShowErrorMessage($"Game source for \"{game.Name}\" is missing. Cannot start the game.", "Startup Error");
            //info.HandleMissingSource(game, this);
        }

        public override void OnGameStopped(OnGameStoppedEventArgs args)
        {
            Playnite.Dialogs.ShowMessage($"Game \"{args.Game.Name}\" has stopped.", "Game Stopped", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            yield return new MainMenuItem()
            {
                Action = (arags) => RemoveGamesMissingSourceFiles(true, default),
                Description = "Remove games with missing source files...",
                MenuSection = "EmuLibrary"
            };

            yield return new MainMenuItem()
            {
                Action = (arags) => DryRunRemoveGamesMissingSourceFiles(default),
                Description = "Dry-run remove games with missing source files...",
                MenuSection = "EmuLibrary"
            };

            yield return new MainMenuItem()
            {
                Action = (arags) => ConvertInstalledGamesToCurrentInstallMethod(true, default),
                Description = "Convert installed games to selected install method...",
                MenuSection = "EmuLibrary"
            };

            yield return new MainMenuItem()
            {
                Action = (arags) => DryRunConvertInstalledGamesToCurrentInstallMethod(default),
                Description = "Dry-run conversion to selected install method...",
                MenuSection = "EmuLibrary"
            };
        }

        public override IEnumerable<GameMenuItem> GetGameMenuItems(GetGameMenuItemsArgs args)
        {
            var ourGameInfos = args.Games.Select(game =>
            {
                if (game.PluginId != Id)
                    return (null, null);

                ELGameInfo gameInfo;
                try
                {
                    gameInfo = game.GetELGameInfo();
                }
                catch
                {
                    return (null, null);
                }

                return (game, gameInfo);
            }).Where(ggi => ggi.game != null);

            if (ourGameInfos.Any())
            {
                yield return new GameMenuItem()
                {
                    Action = (arags) =>
                    {
                        ourGameInfos.ForEach(ggi => ggi.gameInfo.BrowseToSource());
                    },
                    Description = "Browse to Source...",
                    MenuSection = "EmuLibrary"
                };
                yield return new GameMenuItem()
                {
                    Action = (arags) =>
                    {
                        var text = ourGameInfos.Select(ggi => ggi.gameInfo.ToDescriptiveString(ggi.game))
                            .Aggregate((a, b) => $"{a}\n--------------------------------------------------------------------\n{b}");
                        Playnite.Dialogs.ShowSelectableString("Decoded GameId info for each selected game is shown below. This information can be useful for troubleshooting.", "EmuLibrary Game Info", text);
                    },
                    Description = "Show Debug Info...",
                    MenuSection = "EmuLibrary"
                };

                if (ourGameInfos.Any(ggi => ggi.game.IsInstalled))
                {
                    yield return new GameMenuItem()
                    {
                        Action = (arags) =>
                        {
                            ConvertGamesToCurrentInstallMethod(ourGameInfos.Select(ggi => ggi.game), true, true, default);
                        },
                        Description = "Convert to selected install method...",
                        MenuSection = "EmuLibrary"
                    };
                }
            }
        }

        private void RemoveGamesMissingSourceFiles(bool promptUser, CancellationToken ct)
        {
            RemoveGamesMissingSourceFiles(promptUser, ct, useProgressForInteractive: true, skipDeleteConfirmation: false);
        }

        private void RemoveGamesMissingSourceFiles(bool promptUser, CancellationToken ct, bool useProgressForInteractive, bool skipDeleteConfirmation)
        {
            var removeInstalled = Settings.AutoRemoveInstalledGamesMissingFromSource;
            var removeNotInstalled = Settings.AutoRemoveNonInstalledGamesMissingFromSource;

            if (!removeInstalled && !removeNotInstalled)
            {
                if (promptUser)
                {
                    Playnite.Dialogs.ShowMessage("Nothing to do.");
                }
                return;
            }

            List<Game> toRemove;
            if (promptUser && useProgressForInteractive)
            {
                toRemove = null;
                var analysisResult = Playnite.Dialogs.ActivateGlobalProgress(
                    (progressArgs) =>
                    {
                        progressArgs.IsIndeterminate = true;
                        progressArgs.Text = "Analyzing games with missing source files...";

                        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, progressArgs.CancelToken))
                        {
                            toRemove = GetGamesMissingSourceFilesToRemove(removeInstalled, removeNotInstalled, linkedCts.Token);
                        }
                    },
                    new GlobalProgressOptions("Preparing remove operation...")
                    {
                        Cancelable = true,
                        IsIndeterminate = true
                    }
                );

                if (analysisResult?.Error != null)
                {
                    Logger.Error($"Remove analysis failed. {analysisResult.Error}");
                    Playnite.Dialogs.ShowMessage("Remove analysis failed. Please check logs for details.", "EmuLibrary");
                    return;
                }

                if (analysisResult?.Canceled == true)
                {
                    Playnite.Dialogs.ShowMessage("Remove operation canceled.", "EmuLibrary");
                    return;
                }

                if (toRemove == null)
                {
                    Playnite.Dialogs.ShowMessage("Remove analysis produced no result.", "EmuLibrary");
                    return;
                }
            }
            else
            {
                toRemove = GetGamesMissingSourceFilesToRemove(removeInstalled, removeNotInstalled, ct);
            }

            if (toRemove.Count != 0)
            {
                System.Windows.MessageBoxResult res;
                if (skipDeleteConfirmation)
                {
                    res = System.Windows.MessageBoxResult.Yes;
                }
                else if (promptUser)
                    res = Playnite.Dialogs.ShowMessage($"Delete {toRemove.Count} library entries?\n\n(This may take a while, during while Playnite will seem frozen.)", "Confirm deletion", System.Windows.MessageBoxButton.YesNo);
                else
                    res = System.Windows.MessageBoxResult.Yes;

                if (res == System.Windows.MessageBoxResult.Yes)
                {
                    var gameIds = toRemove.Select(g => g.Id).ToList();

                    Action<CancellationToken, GlobalProgressActionArgs> removeAction = (effectiveToken, progressArgs) =>
                    {
                        for (var index = 0; index < gameIds.Count; index++)
                        {
                            if (effectiveToken.IsCancellationRequested)
                                break;

                            var game = Playnite.Database.Games.Get(gameIds[index]);
                            if (game == null)
                                continue;

                            if (progressArgs != null)
                            {
                                progressArgs.CurrentProgressValue = index + 1;
                                progressArgs.Text = $"Removing {index + 1}/{gameIds.Count}: {game.Name}";
                            }

                            try
                            {
                                if (game.IsInstalled)
                                    Playnite.UninstallGame(game.Id);
                                else
                                    Playnite.Database.Games.Remove(game);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"Failed removing game '{game.Name}'. {ex}");
                            }
                        }
                    };

                    if (promptUser)
                    {
                        Playnite.Dialogs.ActivateGlobalProgress(
                            (progressArgs) =>
                            {
                                progressArgs.IsIndeterminate = false;
                                progressArgs.ProgressMaxValue = gameIds.Count;

                                using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, progressArgs.CancelToken))
                                {
                                    removeAction(linkedCts.Token, progressArgs);
                                }
                            },
                            new GlobalProgressOptions("Removing games with missing source files...")
                            {
                                Cancelable = true,
                                IsIndeterminate = false
                            }
                        );
                    }
                    else
                    {
                        removeAction(ct, null);
                    }

                    if (promptUser)
                    {
                        Playnite.Dialogs.ShowMessage($"Removed up to {toRemove.Count} library entries.", "EmuLibrary");
                    }
                }
            }
            else if (promptUser)
            {
                Playnite.Dialogs.ShowMessage("Nothing to do.");
            }
        }

        private void DryRunRemoveGamesMissingSourceFiles(CancellationToken ct)
        {
            var removeInstalled = Settings.AutoRemoveInstalledGamesMissingFromSource;
            var removeNotInstalled = Settings.AutoRemoveNonInstalledGamesMissingFromSource;

            if (!removeInstalled && !removeNotInstalled)
            {
                Playnite.Dialogs.ShowMessage(
                    "Nothing to do. Enable at least one remove option (installed/non-installed) in Settings first.",
                    "EmuLibrary Dry-run");
                return;
            }

            List<Game> toRemove = null;

            var progressResult = Playnite.Dialogs.ActivateGlobalProgress(
                (progressArgs) =>
                {
                    progressArgs.IsIndeterminate = true;
                    progressArgs.Text = "Analyzing games with missing source files...";

                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, progressArgs.CancelToken))
                    {
                        toRemove = GetGamesMissingSourceFilesToRemove(removeInstalled, removeNotInstalled, linkedCts.Token);
                    }
                },
                new GlobalProgressOptions("Dry-run remove analysis...")
                {
                    Cancelable = true,
                    IsIndeterminate = true
                }
            );

            if (progressResult?.Error != null)
            {
                Logger.Error($"Dry-run remove analysis failed. {progressResult.Error}");
                Playnite.Dialogs.ShowMessage("Dry-run failed. Please check logs for details.", "EmuLibrary Dry-run");
                return;
            }

            if (progressResult?.Canceled == true)
            {
                Playnite.Dialogs.ShowMessage("Dry-run canceled.", "EmuLibrary Dry-run");
                return;
            }

            if (toRemove == null)
            {
                Playnite.Dialogs.ShowMessage("Dry-run produced no result.", "EmuLibrary Dry-run");
                return;
            }

            if (toRemove.Count == 0)
            {
                Playnite.Dialogs.ShowMessage("Dry run: no games would be removed.", "EmuLibrary Dry-run");
                return;
            }

            var names = toRemove.Select(g => g.Name)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .Take(30)
                .ToList();

            var details =
                $"Dry run only. No games were removed.\n\n" +
                $"Games that would be removed: {toRemove.Count}\n\n" +
                "Preview:\n" +
                string.Join("\n", names.Select(name => $"- {name}"));

            if (toRemove.Count > names.Count)
            {
                details += $"\n... and {toRemove.Count - names.Count} more.";
            }

            Playnite.Dialogs.ShowMessage(details, "EmuLibrary Dry-run");

            var removeNowResult = Playnite.Dialogs.ShowMessage(
                "Remove these games now?",
                "EmuLibrary Dry-run",
                System.Windows.MessageBoxButton.YesNo);

            if (removeNowResult == System.Windows.MessageBoxResult.Yes)
            {
                RemoveGamesMissingSourceFiles(true, CancellationToken.None, useProgressForInteractive: true, skipDeleteConfirmation: true);
            }
        }

        private List<Game> GetGamesMissingSourceFilesToRemove(bool removeInstalled, bool removeNotInstalled, CancellationToken ct)
        {
            var toRemove = new List<Game>();
            var candidateGames = Playnite.Database.Games.Where(g => g.PluginId == Id).ToList();

            foreach (var game in candidateGames)
            {
                if (ct.IsCancellationRequested)
                    break;

                if ((game.IsInstalled && !removeInstalled) || (!game.IsInstalled && !removeNotInstalled))
                    continue;

                ELGameInfo info;
                try
                {
                    info = game.GetELGameInfo();
                }
                catch
                {
                    continue;
                }

                if (info?.Mapping == null || !info.Mapping.Enabled)
                    continue;

                if (info.CheckSourceExists())
                    continue;

                toRemove.Add(game);
            }

            return toRemove;
        }

        private void QueueBackgroundMaintenance()
        {
            if (Interlocked.Exchange(ref _backgroundMaintenanceRunning, 1) == 1)
            {
                return;
            }

            Task.Run(() =>
            {
                try
                {
                    RemoveGamesMissingSourceFiles(false, CancellationToken.None);

                    if (Settings.AutoConvertInstalledGamesToSelectedInstallMethod)
                    {
                        ConvertInstalledGamesToCurrentInstallMethod(false, false, CancellationToken.None);
                    }
                }
                catch (Exception ex)
                {
                    Logger.Error($"Background maintenance failed. {ex}");
                }
                finally
                {
                    Interlocked.Exchange(ref _backgroundMaintenanceRunning, 0);
                }
            });
        }

        public void ConvertInstalledGamesToCurrentInstallMethod(bool promptUser, CancellationToken ct)
        {
            ConvertInstalledGamesToCurrentInstallMethod(promptUser, true, ct);
        }

        private void ConvertInstalledGamesToCurrentInstallMethod(bool promptUser, bool showCompletionDialog, CancellationToken ct)
        {
            var installedGames = Playnite.Database.Games
                .Where(g => g.PluginId == Id && g.IsInstalled)
                .ToList();

            ConvertGamesToCurrentInstallMethod(installedGames, promptUser, showCompletionDialog, ct);
        }

        public void DryRunConvertInstalledGamesToCurrentInstallMethod(CancellationToken ct)
        {
            List<Game> toConvert = null;
            var skipped = 0;
            var method = Settings.InstallMethod;

            var progressResult = Playnite.Dialogs.ActivateGlobalProgress(
                (progressArgs) =>
                {
                    progressArgs.IsIndeterminate = true;
                    progressArgs.Text = "Analyzing installed games for dry-run...";

                    using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, progressArgs.CancelToken))
                    {
                        toConvert = GetEligibleInstalledGames(linkedCts.Token, out skipped);
                    }
                },
                new GlobalProgressOptions("Dry-run conversion analysis...")
                {
                    Cancelable = true,
                    IsIndeterminate = true
                }
            );

            if (progressResult?.Error != null)
            {
                Logger.Error($"Dry-run conversion analysis failed. {progressResult.Error}");
                Playnite.Dialogs.ShowMessage("Dry-run failed. Please check logs for details.", "EmuLibrary");
                return;
            }

            if (progressResult?.Canceled == true)
            {
                Playnite.Dialogs.ShowMessage("Dry-run canceled.", "EmuLibrary");
                return;
            }

            if (toConvert == null)
            {
                Playnite.Dialogs.ShowMessage("Dry-run produced no result.", "EmuLibrary");
                return;
            }

            if (toConvert.Count == 0)
            {
                var noGamesMessage = skipped > 0
                    ? $"Dry run for {method}: no installed games are eligible for conversion. Skipped: {skipped}."
                    : $"Dry run for {method}: no installed games found to convert.";
                Playnite.Dialogs.ShowMessage(noGamesMessage, "EmuLibrary");
                return;
            }

            var gameNames = toConvert
                .Select(g => g.Name)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            const int maxPreview = 30;
            var previewNames = gameNames.Take(maxPreview).ToList();
            var omitted = gameNames.Count - previewNames.Count;

            var details =
                $"Dry run only. No files will be modified.\n\n" +
                $"Selected install method: {method}\n" +
                $"Eligible for conversion: {toConvert.Count}\n" +
                $"Skipped (disabled mapping, missing source, or invalid metadata): {skipped}\n\n" +
                $"Preview (first {previewNames.Count}):\n" +
                string.Join("\n", previewNames.Select(name => $"- {name}"));

            if (omitted > 0)
            {
                details += $"\n... and {omitted} more.";
            }

            Playnite.Dialogs.ShowMessage(details, "EmuLibrary Dry-run");

            var convertNowResult = Playnite.Dialogs.ShowMessage(
                "Convert now using the selected install method?",
                "EmuLibrary Dry-run",
                System.Windows.MessageBoxButton.YesNo);

            if (convertNowResult == System.Windows.MessageBoxResult.Yes)
            {
                ConvertInstalledGamesToCurrentInstallMethod(false, true, CancellationToken.None);
            }
        }

        private List<Game> GetEligibleInstalledGames(CancellationToken ct, out int skipped)
        {
            var installedGames = Playnite.Database.Games
                .Where(g => g.PluginId == Id && g.IsInstalled)
                .ToList();

            return GetEligibleGamesForConversion(installedGames, ct, out skipped);
        }

        private List<Game> GetEligibleGamesForConversion(IEnumerable<Game> candidateGames, CancellationToken ct, out int skipped)
        {
            skipped = 0;
            var toConvert = new List<Game>();

            foreach (var game in candidateGames)
            {
                if (ct.IsCancellationRequested)
                    break;

                if (game.PluginId != Id || !game.IsInstalled)
                {
                    skipped++;
                    continue;
                }

                ELGameInfo info;
                try
                {
                    info = game.GetELGameInfo();
                }
                catch
                {
                    skipped++;
                    continue;
                }

                if (info?.Mapping == null || !info.Mapping.Enabled || !info.CheckSourceExists())
                {
                    skipped++;
                    continue;
                }

                if (IsGameAlreadyInSelectedInstallMethod(info))
                {
                    skipped++;
                    continue;
                }

                toConvert.Add(game);
            }

            return toConvert;
        }

        private void ConvertGamesToCurrentInstallMethod(IEnumerable<Game> candidateGames, bool promptUser, bool showCompletionDialog, CancellationToken ct, int precomputedSkipped = 0, bool candidatesArePreFiltered = false)
        {
            List<Game> toConvert;
            var skipped = precomputedSkipped;

            if (candidatesArePreFiltered)
            {
                toConvert = candidateGames.ToList();
            }
            else if (showCompletionDialog)
            {
                toConvert = null;
                var progressResult = Playnite.Dialogs.ActivateGlobalProgress(
                    (progressArgs) =>
                    {
                        progressArgs.IsIndeterminate = true;
                        progressArgs.Text = "Analyzing installed games for conversion...";

                        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, progressArgs.CancelToken))
                        {
                            toConvert = GetEligibleGamesForConversion(candidateGames, linkedCts.Token, out var skippedDynamic);
                            skipped += skippedDynamic;
                        }
                    },
                    new GlobalProgressOptions("Preparing conversion...")
                    {
                        Cancelable = true,
                        IsIndeterminate = true
                    }
                );

                if (progressResult?.Error != null)
                {
                    Logger.Error($"Conversion analysis failed. {progressResult.Error}");
                    Playnite.Dialogs.ShowMessage("Conversion analysis failed. Please check logs for details.", "EmuLibrary");
                    return;
                }

                if (progressResult?.Canceled == true)
                {
                    Playnite.Dialogs.ShowMessage("Conversion canceled.", "EmuLibrary");
                    return;
                }

                if (toConvert == null)
                {
                    Playnite.Dialogs.ShowMessage("Conversion analysis produced no result.", "EmuLibrary");
                    return;
                }
            }
            else
            {
                toConvert = GetEligibleGamesForConversion(candidateGames, ct, out var skippedDynamic);
                skipped += skippedDynamic;
            }

            if (toConvert.Count == 0)
            {
                if (showCompletionDialog)
                {
                    var noGamesMessage = skipped > 0
                        ? $"No installed games are eligible for conversion. Skipped: {skipped}."
                        : "No installed games found to convert.";
                    Playnite.Dialogs.ShowMessage(noGamesMessage, "EmuLibrary");
                }
                return;
            }

            if (promptUser)
            {
                var selectedMethod = Settings.InstallMethod;
                var res = Playnite.Dialogs.ShowMessage(
                    $"Convert {toConvert.Count} installed games to {selectedMethod}?\n\n" +
                    $"Skipped (disabled mapping, missing source, or invalid metadata): {skipped}.\n\n" +
                    "This may take a while.",
                    "Convert installed games",
                    System.Windows.MessageBoxButton.YesNo);

                if (res != System.Windows.MessageBoxResult.Yes)
                    return;
            }

            var converted = 0;
            var failed = 0;

            var gameIds = toConvert.Select(g => g.Id).ToList();
            var canceled = false;

            Action<CancellationToken, GlobalProgressActionArgs> runConversion = (effectiveToken, progressArgs) =>
            {
                for (var index = 0; index < gameIds.Count; index++)
                {
                    if (effectiveToken.IsCancellationRequested)
                    {
                        canceled = true;
                        break;
                    }

                    var game = Playnite.Database.Games.Get(gameIds[index]);
                    if (game == null || !game.IsInstalled)
                        continue;

                    if (progressArgs != null)
                    {
                        progressArgs.CurrentProgressValue = index + 1;
                        progressArgs.Text = $"Converting {index + 1}/{gameIds.Count}: {game.Name}";
                    }

                    try
                    {
                        ConvertInstalledGameToCurrentInstallMethod(game, effectiveToken).GetAwaiter().GetResult();
                        converted++;
                    }
                    catch (Exception ex)
                    {
                        failed++;
                        Logger.Error($"Failed to convert installed game '{game.Name}'. {ex}");
                    }
                }
            };

            if (showCompletionDialog)
            {
                var progressResult = Playnite.Dialogs.ActivateGlobalProgress(
                    (progressArgs) =>
                    {
                        progressArgs.IsIndeterminate = false;
                        progressArgs.ProgressMaxValue = gameIds.Count;

                        using (var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, progressArgs.CancelToken))
                        {
                            runConversion(linkedCts.Token, progressArgs);
                        }
                    },
                    new GlobalProgressOptions("Converting games to selected install method...")
                    {
                        Cancelable = true,
                        IsIndeterminate = false
                    }
                );

                if (progressResult?.Error != null)
                {
                    Logger.Error($"Conversion operation failed. {progressResult.Error}");
                }

                if (progressResult?.Canceled == true)
                {
                    canceled = true;
                }
            }
            else
            {
                runConversion(ct, null);
            }

            if (showCompletionDialog)
            {
                Playnite.Dialogs.ShowMessage(
                    $"Conversion complete. Converted: {converted}, Failed: {failed}, Skipped: {skipped}" +
                    (canceled ? ", Canceled: yes." : "."),
                    "EmuLibrary");
            }
        }

        private async Task ConvertInstalledGameToCurrentInstallMethod(Game game, CancellationToken ct)
        {
            var info = game.GetELGameInfo();

            switch (info.RomType)
            {
                case RomType.SingleFile:
                    await ConvertSingleFileGame(game, ct);
                    break;
                case RomType.MultiFile:
                    await ConvertMultiFileGame(game, ct);
                    break;
                case RomType.M3uPlaylist:
                    await ConvertM3uPlaylistGame(game, ct);
                    break;
                default:
                    throw new NotSupportedException($"Unsupported RomType '{info.RomType}' for conversion.");
            }
        }

        private async Task ConvertSingleFileGame(Game game, CancellationToken ct)
        {
            var info = game.GetSingleFileGameInfo();
            var source = new FileInfo(info.SourceFullPath);
            var destination = new DirectoryInfo(info.Mapping.DestinationPathResolved);
            await CreateConversionFileCopier(source, destination).CopyAsync(ct);
        }

        private async Task ConvertMultiFileGame(Game game, CancellationToken ct)
        {
            var info = game.GetMultiFileGameInfo();
            var source = new DirectoryInfo(info.SourceFullBaseDir);
            var destination = new DirectoryInfo(info.DestinationFullBaseDir);

            if (destination.Exists)
            {
                destination.Delete(true);
            }

            await CreateConversionFileCopier(source, destination).CopyAsync(ct);
        }

        private async Task ConvertM3uPlaylistGame(Game game, CancellationToken ct)
        {
            var info = game.GetM3uPlaylistGameInfo();
            var source = new FileInfo(info.SourceFullPath);
            var destination = new FileInfo(info.DestinationFullPath);

            var referencedDirectories = File.ReadAllLines(source.FullName)
                .Select(line => Path.GetDirectoryName(line))
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(dir => new DirectoryInfo(Path.Combine(source.DirectoryName ?? string.Empty, dir)))
                .ToList();

            foreach (var dir in referencedDirectories)
            {
                var destinationDir = new DirectoryInfo(Path.Combine(destination.DirectoryName ?? string.Empty, dir.Name));
                if (destinationDir.Exists)
                {
                    destinationDir.Delete(true);
                }

                await CreateConversionFileCopier(dir, destinationDir).CopyAsync(ct);
            }

            if (destination.Exists)
            {
                destination.Delete();
            }

            await CreateConversionFileCopier(source, destination.Directory).CopyAsync(ct);
        }

        private IFileCopier CreateConversionFileCopier(FileSystemInfo source, DirectoryInfo destination)
        {
            if (Settings.InstallMethod == InstallMethod.Symlink)
            {
                return new SymlinkFileCopier(source, destination, Settings.SymlinkFallbackToHardlink);
            }

            if (Settings.InstallMethod == InstallMethod.Hardlink)
            {
                return new HardlinkFileCopier(source, destination);
            }

            return new SimpleFileCopier(source, destination);
        }

        private bool IsGameAlreadyInSelectedInstallMethod(ELGameInfo info)
        {
            switch (info.RomType)
            {
                case RomType.SingleFile:
                    return IsSingleFileInSelectedInstallMethod(info as SingleFileGameInfo);
                case RomType.MultiFile:
                    return IsDirectoryInSelectedInstallMethod(info.SourceFullBaseDir, info.DestinationFullBaseDir);
                case RomType.M3uPlaylist:
                    return IsM3uInSelectedInstallMethod(info as M3uPlaylistGameInfo);
                default:
                    return false;
            }
        }

        private bool IsSingleFileInSelectedInstallMethod(SingleFileGameInfo info)
        {
            if (info == null)
                return false;

            return IsFileInSelectedInstallMethod(info.SourceFullPath, info.DestinationFullPath);
        }

        private bool IsM3uInSelectedInstallMethod(M3uPlaylistGameInfo info)
        {
            if (info == null)
                return false;

            if (!IsFileInSelectedInstallMethod(info.SourceFullPath, info.DestinationFullPath))
                return false;

            var sourceM3u = new FileInfo(info.SourceFullPath);
            if (!sourceM3u.Exists)
                return false;

            var referencedDirectories = File.ReadAllLines(sourceM3u.FullName)
                .Select(line => Path.GetDirectoryName(line))
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(dir =>
                {
                    var sourceDir = new DirectoryInfo(Path.Combine(sourceM3u.DirectoryName ?? string.Empty, dir));
                    var destinationDir = new DirectoryInfo(Path.Combine(Path.GetDirectoryName(info.DestinationFullPath) ?? string.Empty, sourceDir.Name));
                    return (sourceDir, destinationDir);
                })
                .ToList();

            foreach (var (sourceDir, destinationDir) in referencedDirectories)
            {
                if (!IsDirectoryInSelectedInstallMethod(sourceDir.FullName, destinationDir.FullName))
                    return false;
            }

            return true;
        }

        private bool IsDirectoryInSelectedInstallMethod(string sourceDirPath, string destinationDirPath)
        {
            var sourceDir = new DirectoryInfo(sourceDirPath);
            var destinationDir = new DirectoryInfo(destinationDirPath);

            if (!sourceDir.Exists || !destinationDir.Exists)
                return false;

            var sourceFiles = sourceDir.GetFiles("*", SearchOption.AllDirectories);
            foreach (var sourceFile in sourceFiles)
            {
                var relativePath = sourceFile.FullName.Substring(sourceDir.FullName.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var destinationFilePath = Path.Combine(destinationDir.FullName, relativePath);
                if (!IsFileInSelectedInstallMethod(sourceFile.FullName, destinationFilePath))
                    return false;
            }

            return true;
        }

        private bool IsFileInSelectedInstallMethod(string sourcePath, string destinationPath)
        {
            var sourceFile = new FileInfo(sourcePath);
            var destinationFile = new FileInfo(destinationPath);

            if (!sourceFile.Exists || !destinationFile.Exists)
                return false;

            var isSymlink = destinationFile.Attributes.HasFlag(FileAttributes.ReparsePoint);
            var isHardlink = AreSameFile(sourcePath, destinationPath);

            if (Settings.InstallMethod == InstallMethod.Symlink)
            {
                return isSymlink || (Settings.SymlinkFallbackToHardlink && isHardlink);
            }

            if (Settings.InstallMethod == InstallMethod.Hardlink)
            {
                return isHardlink;
            }

            return !isSymlink && !isHardlink;
        }

        private static bool AreSameFile(string path1, string path2)
        {
            if (!File.Exists(path1) || !File.Exists(path2))
                return false;

            if (!TryGetFileIdentity(path1, out var id1) || !TryGetFileIdentity(path2, out var id2))
                return false;

            return id1.VolumeSerialNumber == id2.VolumeSerialNumber
                   && id1.FileIndexHigh == id2.FileIndexHigh
                   && id1.FileIndexLow == id2.FileIndexLow;
        }

        private static bool TryGetFileIdentity(string path, out BY_HANDLE_FILE_INFORMATION info)
        {
            info = default;

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                return GetFileInformationByHandle(stream.SafeFileHandle, out info);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct FILETIME
        {
            public uint dwLowDateTime;
            public uint dwHighDateTime;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BY_HANDLE_FILE_INFORMATION
        {
            public uint FileAttributes;
            public FILETIME CreationTime;
            public FILETIME LastAccessTime;
            public FILETIME LastWriteTime;
            public uint VolumeSerialNumber;
            public uint FileSizeHigh;
            public uint FileSizeLow;
            public uint NumberOfLinks;
            public uint FileIndexHigh;
            public uint FileIndexLow;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(
            Microsoft.Win32.SafeHandles.SafeFileHandle hFile,
            out BY_HANDLE_FILE_INFORMATION lpFileInformation);
    }
}