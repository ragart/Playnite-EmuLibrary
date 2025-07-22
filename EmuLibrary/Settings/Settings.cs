﻿using EmuLibrary.RomTypes;
using Newtonsoft.Json;
using Playnite.SDK;
using Playnite.SDK.Plugins;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace EmuLibrary.Settings
{
    public class Settings : ObservableObject, ISettings
    {
        private readonly Plugin _plugin;
        private Settings _editingClone;

        [JsonIgnore]
        internal readonly IPlayniteAPI PlayniteAPI;

        [JsonIgnore]
        internal readonly IEmuLibrary EmuLibrary;

        public static Settings Instance { get; private set; }

        public bool ScanGamesInFullScreen { get; set; } = false;
        public bool NotifyOnInstallComplete { get; set; } = false;
        public bool AutoRemoveUninstalledGamesMissingFromSource { get; set; } = false;
        public bool UseWindowsCopyDialogInDesktopMode { get; set; } = false;
        public bool UseWindowsCopyDialogInFullscreenMode { get; set; } = false;
        public ObservableCollection<EmulatorMapping> Mappings { get; set; }

        // Hidden settings
        public int Version { get; set; }

        // Parameterless constructor must exist if you want to use LoadPluginSettings method.
        public Settings()
        {
        }

        internal Settings(Plugin plugin, IEmuLibrary emuLibrary)
        {
            EmuLibrary = emuLibrary;
            PlayniteAPI = emuLibrary.Playnite;
            Instance = this;
            _plugin = plugin;

            bool forceSave = false;

            var settings = plugin.LoadPluginSettings<Settings>();
            if (settings == null || settings.Version == 0)
            {
                // Settings didn't load cleanly or need to be upgraded. Make sure we save in new format
                forceSave = true;

                var settingsV0 = plugin.LoadPluginSettings<SettingsV0>();
                if (settingsV0 != null)
                {
                    settings = settingsV0.ToV1Settings();
                }
            }

            if (settings != null)
            {
                settings.Version = 1;
                LoadValues(settings);
            }

            // Need to initialize this if missing, else we don't have a valid list for UI to add to
            if (Mappings == null)
            {
                Mappings = new ObservableCollection<EmulatorMapping>();
            }

            var mappingsWithoutId = Mappings.Where(m => m.MappingId == default);
            if (mappingsWithoutId.Any())
            {
                mappingsWithoutId.ForEach(m => m.MappingId = Guid.NewGuid());
                forceSave = true;
            }

            if (Mappings.Count == 0)
            {
                // We want this to default to true for new installs, but not enable automatically for existing users
                AutoRemoveUninstalledGamesMissingFromSource = true;
            }

            if (forceSave)
            {
                _plugin.SavePluginSettings(this);
            }
        }

        public void BeginEdit()
        {
            _editingClone = this.GetClone();
        }

        public void CancelEdit()
        {
            LoadValues(_editingClone);
        }

        public void EndEdit()
        {
            _plugin.SavePluginSettings(this);
        }

        public bool VerifySettings(out List<string> errors)
        {
            var mappingErrors = new List<string>();

            Mappings.Where(m => m.Enabled)?.ForEach(m =>
            {
                if (m.ImageExtensionsLower == null || !m.ImageExtensionsLower.Any())
                {
                    mappingErrors.Add($"{m.MappingId}: No image extensions specified for profile {m.EmulatorProfile.Name} with emulator {m.Emulator.Name}. There is nothing for EmuLibrary to scan.");
                }

                if (string.IsNullOrEmpty(m.SourcePath))
                {
                    mappingErrors.Add($"{m.MappingId}: No source path specified.");
                }
                else if (!Directory.Exists(m.SourcePath))
                {
                    mappingErrors.Add($"{m.MappingId}: Source path doesn't exist ({m.SourcePath}).");
                }

                if (string.IsNullOrEmpty(m.DestinationPathResolved))
                {
                    mappingErrors.Add($"{m.MappingId}: No destination path specified.");
                }
                else if (!Directory.Exists(m.DestinationPathResolved))
                {
                    mappingErrors.Add($"{m.MappingId}: Destination path doesn't exist ({m.DestinationPathResolved}).");
                }
            });

            errors = mappingErrors;
            return errors.Count == 0;
        }

        private void LoadValues(Settings source)
        {
            source.CopyProperties(this, false, null, true);
        }
    }
}
