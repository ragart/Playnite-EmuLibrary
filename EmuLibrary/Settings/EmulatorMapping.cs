using EmuLibrary.RomTypes;
using Newtonsoft.Json;
using Playnite.SDK.Models;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml.Serialization;

namespace EmuLibrary.Settings
{
    public class EmulatorMapping : ObservableObject
    {
        public EmulatorMapping()
        {
            MappingId = Guid.NewGuid();
            SourcePaths = new ObservableCollection<string>();
        }

        public Guid MappingId { get; set; }

        [DefaultValue(true)]
        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Populate)]
        public bool Enabled { get; set; }

        [JsonIgnore]
        public Emulator Emulator
        {
            get => AvailableEmulators.FirstOrDefault(e => e.Id == EmulatorId);
            set
            {
                var newEmulatorId = value?.Id ?? Guid.Empty;
                if (EmulatorId == newEmulatorId)
                {
                    return;
                }

                EmulatorId = newEmulatorId;
                EmulatorProfileId = null;
                PlatformId = null;

                OnPropertyChanged(nameof(EmulatorId));
                OnPropertyChanged(nameof(Emulator));
                OnPropertyChanged(nameof(AvailableProfiles));
                OnPropertyChanged(nameof(EmulatorProfileId));
                OnPropertyChanged(nameof(EmulatorProfile));
                OnPropertyChanged(nameof(AvailablePlatforms));
                OnPropertyChanged(nameof(PlatformId));
                OnPropertyChanged(nameof(Platform));
                OnPropertyChanged(nameof(ImageExtensionsLower));
            }
        }
        public Guid EmulatorId { get; set; }

        [JsonIgnore]
        public EmulatorProfile EmulatorProfile
        {
            get => Emulator?.SelectableProfiles.FirstOrDefault(p => p.Id == EmulatorProfileId);
            set
            {
                var newProfileId = value?.Id;
                if (EmulatorProfileId == newProfileId)
                {
                    return;
                }

                EmulatorProfileId = newProfileId;
                PlatformId = null;

                OnPropertyChanged(nameof(EmulatorProfileId));
                OnPropertyChanged(nameof(EmulatorProfile));
                OnPropertyChanged(nameof(AvailablePlatforms));
                OnPropertyChanged(nameof(PlatformId));
                OnPropertyChanged(nameof(Platform));
                OnPropertyChanged(nameof(ImageExtensionsLower));
            }
        }
        public string EmulatorProfileId { get; set; }

        [JsonIgnore]
        public Platform Platform
        {
            get
            {
                if (Guid.TryParse(PlatformId, out var guid))
                {
                    return AvailablePlatforms.FirstOrDefault(p => p.Id == guid);
                }
                else
                {
                    return AvailablePlatforms.FirstOrDefault(p => p.SpecificationId == PlatformId);
                }
            }
            set
            {
                var newPlatformId = value?.Id.ToString();
                if (PlatformId == newPlatformId)
                {
                    return;
                }

                PlatformId = newPlatformId;
                OnPropertyChanged(nameof(PlatformId));
                OnPropertyChanged(nameof(Platform));
            }
        }
        public string PlatformId { get; set; }

        // Keep old property for backward compatibility with old settings files
        [JsonProperty("SourcePath")]
        [Obsolete("Use SourcePaths instead. This is for backwards compatibility with settings saved before multi-source support.")]
        private string OldSourcePath { set { if (!string.IsNullOrEmpty(value) && (SourcePaths == null || SourcePaths.Count == 0)) { SourcePaths = new ObservableCollection<string> { value }; } } }

        public ObservableCollection<string> SourcePaths { get; set; }
        public string DestinationPath { get; set; }
        public RomType RomType { get; set; }

        public static IEnumerable<Emulator> AvailableEmulators => Settings.Instance.PlayniteAPI.Database.Emulators.OrderBy(x => x.Name);

        [JsonIgnore]
        public IEnumerable<EmulatorProfile> AvailableProfiles => Emulator?.SelectableProfiles ?? Enumerable.Empty<EmulatorProfile>();

        [JsonIgnore]
        public IEnumerable<Platform> AvailablePlatforms
        {
            get
            {
                var playnite = Settings.Instance.PlayniteAPI;

                if (EmulatorProfile is CustomEmulatorProfile customProfile)
                {
                    var customProfilePlatforms = customProfile.Platforms ?? new List<Guid>();

                    if (!customProfilePlatforms.Any())
                    {
                        return new List<Platform>();
                    }

                    return playnite.Database.Platforms
                        .Where(p => p != null && customProfilePlatforms.Contains(p.Id));
                }
                else if (EmulatorProfile is BuiltInEmulatorProfile builtInProfile)
                {
                    var emulator = Emulator;
                    if (emulator == null)
                    {
                        return new List<Platform>();
                    }

                    var builtInEmulator = playnite.Emulation.Emulators
                        .FirstOrDefault(e => e.Id == emulator.BuiltInConfigId);

                    var builtInEmulatorProfile = builtInEmulator?.Profiles?
                        .FirstOrDefault(p => p.Name == builtInProfile.Name);

                    var validPlatformIds = new HashSet<string>(
                        (builtInEmulatorProfile?.Platforms ?? Enumerable.Empty<string>())
                            .Where(id => !string.IsNullOrEmpty(id)),
                        StringComparer.OrdinalIgnoreCase);

                    if (validPlatformIds.Count == 0)
                    {
                        return new List<Platform>();
                    }

                    var emuPlatforms = Settings.Instance.PlayniteAPI.Emulation.Platforms
                        .Where(p => p != null && !string.IsNullOrEmpty(p.Id) && validPlatformIds.Contains(p.Id));
                    var validPlatformsInfo = new HashSet<(string, string)>(emuPlatforms.Select(p => (p.Id, p.Name)));
                    return playnite.Database.Platforms.Where(p =>
                        p != null &&
                        p.SpecificationId != null &&
                        validPlatformsInfo.Contains((p.SpecificationId, p.Name))
                    );
                }
                else
                {
                    return new List<Platform>();
                }
            }
        }

        [JsonIgnore]
        [XmlIgnore]
        public string DestinationPathResolved
        {
            get
            {
                var playnite = Settings.Instance.PlayniteAPI;
                return playnite.Paths.IsPortable ? DestinationPath?.Replace(ExpandableVariables.PlayniteDirectory, playnite.Paths.ApplicationPath) : DestinationPath;
            }
        }

        [JsonIgnore]
        [XmlIgnore]
        public string EmulatorBasePath => Emulator?.InstallDir;

        [JsonIgnore]
        [XmlIgnore]
        public string EmulatorBasePathResolved
        {
            get
            {
                var playnite = Settings.Instance.PlayniteAPI;
                var ret = Emulator?.InstallDir;
                if (playnite.Paths.IsPortable)
                {
                    ret = ret?.Replace(ExpandableVariables.PlayniteDirectory, playnite.Paths.ApplicationPath);
                }
                return ret;
            }
        }

        [JsonIgnore]
        public IEnumerable<string> ImageExtensionsLower
        {
            get
            {
                IEnumerable<string> imageExtensionsLower;
                if (EmulatorProfile is CustomEmulatorProfile)
                {
                    imageExtensionsLower = (EmulatorProfile as CustomEmulatorProfile).ImageExtensions?.Where(ext => !ext.IsNullOrEmpty()).Select(ext => ext.Trim().ToLowerInvariant());
                }
                else if (EmulatorProfile is BuiltInEmulatorProfile)
                {
                    var emulator = Emulator;
                    if (emulator == null)
                    {
                        return Enumerable.Empty<string>();
                    }

                    imageExtensionsLower = Settings.Instance?.PlayniteAPI.Emulation.Emulators
                        .FirstOrDefault(e => e.Id == emulator.BuiltInConfigId)?
                        .Profiles?
                        .FirstOrDefault(p => p.Name == EmulatorProfile.Name)?
                        .ImageExtensions?
                        .Where(ext => !ext.IsNullOrEmpty())
                        .Select(ext => ext.Trim().ToLowerInvariant());
                }
                else
                {
                    throw new NotImplementedException("Unknown emulator profile type.");
                }

                return imageExtensionsLower ?? Enumerable.Empty<string>();
            }
        }

        public IEnumerable<string> GetDescriptionLines()
        {
            yield return $"{nameof(EmulatorId)}: {EmulatorId}";
            yield return $"{nameof(Emulator)}*: {Emulator?.Name ?? "<Unknown>"}";
            yield return $"{nameof(EmulatorProfileId)}: {EmulatorProfileId ?? "<Unknown>"}";
            yield return $"{nameof(EmulatorProfile)}*: {EmulatorProfile?.Name ?? "<Unknown>"}";
            yield return $"{nameof(PlatformId)}: {PlatformId ?? "<Unknown>"}";
            yield return $"{nameof(Platform)}*: {Platform?.Name ?? "<Unknown>"}";
            if (SourcePaths != null && SourcePaths.Any())
            {
                yield return "Source Paths:";
                foreach (var path in SourcePaths)
                {
                    yield return $"    - {path}";
                }
            }
            else
            {
                yield return "Source Paths: <None>";
            }
            yield return $"{nameof(DestinationPath)}: {DestinationPath ?? "<Unknown>"}";
            yield return $"{nameof(DestinationPathResolved)}*: {DestinationPathResolved ?? "<Unknown>"}";
            yield return $"{nameof(EmulatorBasePathResolved)}*: {EmulatorBasePathResolved ?? "<Unknown>"}";
        }
    }
}
