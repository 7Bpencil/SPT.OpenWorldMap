//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using Audio.AudioCulling;
using Audio.SpatialSystem;
using BepInEx;
using Diz.Utils;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using EFT.UI;
using EFT.Weather;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Common.Http;
using SPT.Common.Utils;
using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using JsonType;

// 56f40101d2720b2a4d8b45d6 Customs
// 56db0b3bd2720bb0678b4567 Arena
// 55f2d3fd4bdc2d5f408b4567 Factory
// 59fc81d786f774390775787e Factory
// 599319c986f7740dca3070a6 Hideout
// 5714dbc024597771384a510d Interchange
// 5b0fc42d86f7744a585f9105 Laboratory
// 5704e4dad2720bb55b8b4567 Lighthouse
// 5704e64ad2720bb55b8b456e Private Sector
// 5704e5fad2720bc05b8b4567 ReserveBase
// 5704e554d2720bac5b8b456e Shoreline
// 5714dc342459777137212e0b Suburbs
// 5714dc692459777137212e12 Streets of Tarkov
// 6733700029c367a3d40b02af Labyrinth
// 5704e5a4d2720bb45b8b4567 Terminal
// 5704e47ed2720bb35b8b4568 Town
// 5704e3c2d2720bac5b8b4567 Woods
// 653e6760052c01c1c805532f Sandbox
// 65b8d6f5cdde2479cb2a3125 Sandbox

namespace SevenBoldPencil.OpenWorld;

public record MapData
(
	HashSet<string> AllowedScenes,
	Vector3 Offset
);

[BepInPlugin("7Bpencil.OpenWorld", "7Bpencil.OpenWorld", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
	public static Plugin Instance;

	private Dictionary<string, MapData> Maps = new()
	{
		{ "5704e5fad2720bc05b8b4567", new(GetReserveScenes(), new(802.4879f, 0, 477.2278f)) },
		{ "5704e4dad2720bb55b8b4567", new(GetLighthouseScenes(), new Vector3(921.452f, -38.4145f, -691.3636f) - new Vector3(-804.424f, 27.2299f, -1737.131f)) },
		{ "5704e554d2720bac5b8b456e", new(GetShorelineScenes(), new Vector3(1162.224f, -92.2317f, 1436.709f) - new Vector3(226.3479f, -92.2346f, 338.9418f)) },
		// { "5714dbc024597771384a510d", new(GetInterchangeScenes(), new Vector3(-596.6475f, 19.7394f, -740.3831f) - new Vector3(13.1f, 21.43f, -54.5f)) },
	};

	private void Awake()
	{
		Instance = this;
		new Patch_AudioCullingController_StartWorkCoroutine().Enable();
		// new Patch_WeatherController_method_4().Enable();
		new Patch_SpatialAudioSystem_LateUpdate().Enable();
		new Patch_SpatialAudioSystem_Update().Enable();
	}

	private void Update()
	{
		if (Input.GetKeyDown(KeyCode.F13))
		{
			LoadAll();
		}
	}

	public LocationSettings GetLocationSettings()
	{
		if (TarkovApplication.Exist(out var tarkovApplication))
        {
            return tarkovApplication.Session.LocationSettings;
        }
		return null;
	}

	public void DumpMaps()
	{
		if (!TarkovApplication.Exist(out var tarkovApplication))
        {
            return;
        }
		var locations = tarkovApplication.Session.LocationSettings.locations;
		foreach (var (id, location) in locations)
		{
			Logger.LogWarning($"{id} {location.Name}");
		}
	}

	public async void LoadAll()
	{
		foreach (var (id, data) in Maps)
		{
			await LoadAnotherMap(id, data);
		}
	}

	public async Task LoadAnotherMap(string mapId, MapData mapData)
	{
		if (TarkovApplication.Exist(out var tarkovApplication))
        {
			var locations = tarkovApplication.Session.LocationSettings.locations;
			if (locations.TryGetValue(mapId, out var location))
			{
				await LoadAnotherMap(location.Scene, mapData);
			}
        }
	}

	public async Task LoadAnotherMap(ResourceKey mapScene, MapData mapData)
	{
		var assetsManager = EFT.Assets.Manager;

		var operation = await assetsManager.LoadAssetAsync(mapScene);
		if (!operation.Succeed)
		{
			return;
		}

		var preset = operation.Result as ScenesPreset;
		var cancellationTokenSource = new CancellationTokenSource();
		var cancellationToken = cancellationTokenSource.Token;
		LoadScenesFromPresetOperation obj = new LoadScenesFromPresetOperation
		{
			_assetsManager = assetsManager,
			_loadFirstAsSingle = false,
			_loadParallel = false,
			_allowSceneActivation = true,
			_cancellationToken = cancellationToken,
			_progress = null
		};

		var allowedScenes = mapData.AllowedScenes;

		Logger.LogWarning($"scenes list:");
		foreach (var scene in preset.ScenesResourceKeys)
		{
			Logger.LogWarning($"{scene.path} {scene.rcid}");
		}

		foreach (var scene in preset.ScenesResourceKeys)
		{
			if (!allowedScenes.Contains(scene.path))
			{
				continue;
			}
			await assetsManager.LoadScene(scene, LoadSceneMode.Additive, true);
		}

		foreach (var scene in SceneManager.GetAllScenes())
		{
			if (allowedScenes.Contains(scene.path))
			{
				foreach (var root in scene.GetRootGameObjects())
				{
					root.transform.position += mapData.Offset;
				}
			}
		}
	}

	private static HashSet<string> GetReserveScenes()
	{
		return new()
		{
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_Scripts.unity",

			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Terrain.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_VOHR_Camps.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_TrainStation.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Storages.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Roads.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_RLS_Block.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Repairing_base.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Platz.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Mortar_Position.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_MilitaryDorms.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Main_Checkpoint.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_HQ.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_BunkersBig.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_transition.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_shaft.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_Corridor_B.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_Corridor_A.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_ClimatControl_room.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_Block_A.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_Block_B.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Academy_and_Kitchens.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Casarms.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Main_RTS.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_PTOR.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_1.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_2.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_3.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_4.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_5.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_6.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_7.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_8.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_9.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_10.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_outside_BLOCK_11.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_Bunkers.unity",
			"Assets/Content/Locations/Reserve_Base/Reserve_Base_RLS_Station.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_Train.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_basement_Exit.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_Background.unity",
			// "Assets/Content/Locations/Reserve_Base/Rezerv_Base_Bunkers2.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_vegetable_warehouse.unity",

			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_Light.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_DesignStuff.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_DesignMain.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_AI.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Sound.unity",
			// "Assets/Content/Locations/Reserve_Base/Reserve_Base_Culling.unity",
		};
	}

	private static HashSet<string> GetLighthouseScenes()
	{
		return new()
		{
			// "Assets/Content/Locations/Lighthouse/Lighthouse_Scripts.unity",

			"Assets/Content/Locations/Lighthouse/Lighthouse_Terrain.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Abadonned_pier.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Fisher_Village.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Bus_Stop.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Chalet.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Background.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Complex.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Roads.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Main.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Marina.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Small_RLS_base.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_SummerHotel.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_SwampVillage.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Tower.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_TrainStation.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Logistics_Terminal.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Tunnel.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Water_filter_facility_01.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Water_filter_facility_02.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_Water_filter_facility_03.unity",
			"Assets/Content/Locations/Lighthouse/Lighthouse_WaterStation.unity",
			// "Assets/Content/Locations/Lighthouse/Lighthouse_island.unity",

			// "Assets/Content/Locations/Lighthouse/Lighthouse_Light.unity",
			// "Assets/Content/Locations/Lighthouse/Lighthouse_DesignStuff.unity",
			// "Assets/Content/Locations/Lighthouse/Lighthouse_DesignMain.unity",
			// "Assets/Content/Locations/Lighthouse/Lighthouse_AI.unity",
			// "Assets/Content/Locations/Lighthouse/Lighthouse_Sound.unity",
			// "Assets/Content/Locations/Lighthouse/Lighthouse_Culling.unity",
		};
	}

	private static HashSet<string> GetShorelineScenes()
	{
		return new()
		{
			// "Assets/Content/Locations/shorline/shoreline_scripts.unity",

			"Assets/Content/Locations/shorline/shoreline_Terrain.unity",
			"Assets/Content/Locations/shorline/Shoreline_North.unity",
			"Assets/Content/Locations/shorline/Shoreline_East.unity",
			"Assets/Content/Locations/shorline/Shoreline_South.unity",
			"Assets/Content/Locations/shorline/Shoreline_West.unity",
			"Assets/Content/Locations/shorline/Shoreline_Middle.unity",
			"Assets/Content/Locations/shorline/shoreline_sanatorium.unity",
			"Assets/Content/Locations/shorline/Shoreline_Sanatorium_indoor.unity",
			"Assets/Content/Locations/shorline/shoreline_parking_sanatorium.unity",
			"Assets/Content/Locations/shorline/shoreline_pirs02.unity",
			"Assets/Content/Locations/shorline/shoreline_azs.unity",
			"Assets/Content/Locations/shorline/shoreline_tunel.unity",
			"Assets/Content/Locations/shorline/shoreline_meteostation.unity",

			// "Assets/Content/Locations/shorline/Shoreline_Light.unity",
			// "Assets/Content/Locations/shorline/Shoreline_DesignMain.unity",
			// "Assets/Content/Locations/shorline/shoreline_DesignStuff.unity",
			// "Assets/Content/Locations/shorline/shoreline_AI.unity",
			// "Assets/Content/Locations/shorline/Shoreline_Sound.unity",
			// "Assets/Content/Locations/shorline/Shoreline_Culling.unity",
		};
	}

	private static HashSet<string> GetInterchangeScenes()
	{
		return new()
		{
			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_Scripts.unity",

			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_Terrain.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_2.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_indoor.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_outdoor.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_IDEA.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_Shops.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_parking_work.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_OLI.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_GOSHAN.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_Shops_Floor2.unity",
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_indoor_buildup.unity",

			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_light.unity",
			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_DesignStuff.unity",
			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_DesignMain.unity",
			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_AI.unity",
			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_Sound.unity",
			// "Assets/Content/Locations/Shopping_Mall/Shopping_Mall_Culling.unity",
		};
	}
}

public class Patch_AudioCullingController_StartWorkCoroutine : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(AudioCullingController), nameof(AudioCullingController.StartWorkCoroutine));
    }

    [PatchPrefix]
    public static bool Prefix()
	{
		return false;
	}
}

public class Patch_WeatherController_method_4 : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(WeatherController), nameof(WeatherController.method_4));
    }

    [PatchPrefix]
    public static bool Prefix()
	{
		return false;
	}
}

public class Patch_SpatialAudioSystem_LateUpdate : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.LateUpdate));
    }

    [PatchPrefix]
    public static bool Prefix()
	{
		return false;
	}
}

public class Patch_SpatialAudioSystem_Update : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(SpatialAudioSystem), nameof(SpatialAudioSystem.Update));
    }

    [PatchPrefix]
    public static bool Prefix()
	{
		return false;
	}
}
