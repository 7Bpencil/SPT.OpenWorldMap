//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using SPTarkov.Common.Models.Logging;
using SPTarkov.DI.Annotations;
using SPTarkov.Reflection.Patching;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Config;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Routers;
using SPTarkov.Server.Core.Utils.Cloners;
using SPTarkov.Server.Core.Utils;

namespace SevenBoldPencil.OpenWorld;

[Injectable(TypePriority = OnLoadOrder.Preload + 90000)]
public class Plugin(
    LocationTable locationTable,
    LocaleTable localeTable,
    BotConfig botConfig,
    LocationConfig locationConfig,
    ICloner cloner,
    JsonUtil jsonUtil,
    ImageRouter imageRouter,
    IEnumerable<IRuntimePatch> patches,
    ISptLogger<Plugin> logger
) : IOnLoad
{
    public const string MapId = "6ab5219833741f2ddd2d8134";
    public const string MapKey = "7bpencil.openworld"; // lowercase is mandatory
    public const string MapName = "Open World";
    public const string MapDescription = "All outdoor maps combined";
    public const string MapBannerId = "6ab52b2c9d8466dceac13e5a";

    public static Plugin Instance;
    public ISptLogger<Plugin> Logger = logger;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Instance = this;

        foreach (var patch in patches)
        {
            patch.Enable();
        }

        foreach (var localeLazy in localeTable.Global.Values)
        {
            localeLazy.AddTransformer(locale =>
            {
                locale[MapKey] = MapName;
                locale[$"{MapId} Name"] = MapName;
                locale[$"{MapId} Description"] = MapDescription;
                locale[$"{MapBannerId} Name"] = MapName;
                locale[$"{MapBannerId} Description"] = MapDescription;
                return locale;
            });
        }

        locationConfig.StaticLootMultiplier[MapKey] = 1;
        locationConfig.LooseLootMultiplier[MapKey] = 1;

        var banner = new Banner()
        {
            Id = MapBannerId,
            Picture = new Pic()
            {
                File = "67e404ffbec96f5d8e097333.jpg",
                Path = "banners/67e404ffbec96f5d8e097333.jpg",
                Rcid = "",
                Type = "banners"
            }
        };
        var looseLoot = new LooseLoot()
        {
            SpawnpointCount = new() { Mean = 0, Std = 0 },
            SpawnpointsForced = [],
            Spawnpoints = [],
        };
        var staticContainers = new StaticContainerDetails()
        {
            StaticWeapons = [],
            StaticContainers = [],
            StaticForced = [],
        };
        var statics = new StaticContainer()
        {
            ContainersGroups = [],
            Containers = [],
        };
        var location = new SPTarkov.Server.Core.Models.Eft.Common.Location
        {
            Base = new LocationBase()
            {
                Id = MapKey,
                IdField = MapId,
                Scene = new()
                {
                   Path = "maps/customs_preset.bundle",
                   Rcid = "bigmap.scenespreset.asset"
                },
                Enabled = true,
                IconX = -10,
                IconY = 80,
                Insurance = true,
                IsSecret = false,
                Locked = false,
                Name = MapKey,
                Banners = new() { banner },
                BossLocationSpawn = [],
                RequiredPlayerLevelMin = 0,
                RequiredPlayerLevelMax = 100,
                AveragePlayTime = 120,
                MinPlayers = 50,
                MaxPlayers = 100,
                BotLocationModifier = new()
                {
                    AdditionalHostilitySettings = []
                },
                Waves = [],
                Exits = [],
            },
            LooseLoot = new(() => looseLoot, cacheValue: true),
            StaticLoot = new(() => [], cacheValue: true),
            StaticContainers = new(() => staticContainers, cacheValue: true),
            StaticAmmo = [],
            Statics = statics,
            AllExtracts = [],
        };

        locationTable.GetDictionary().Add(MapKey, location);

        return Task.CompletedTask;
    }
}
