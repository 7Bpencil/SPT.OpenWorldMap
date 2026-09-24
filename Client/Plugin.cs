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
using Newtonsoft.Json;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Common.Http;
using SPT.Common.Utils;
using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using JsonType;
using GPUInstancer;

// Customs           56f40101d2720b2a4d8b45d6 path=maps/customs_preset.bundle       rcid=bigmap.scenespreset.asset
// Arena             56db0b3bd2720bb0678b4567 path=maps/develop_preset.bundle       rcid=develop.scenespreset.asset
// Factory           55f2d3fd4bdc2d5f408b4567 path=maps/factory_day_preset.bundle   rcid=factory_day.scenespreset.asset
// Factory           59fc81d786f774390775787e path=maps/factory_night_preset.bundle rcid=factory_night.scenespreset.asset
// Hideout           599319c986f7740dca3070a6 path=maps/bunker_preset.bundle        rcid=bunker.ScenesPreset.asset
// Interchange       5714dbc024597771384a510d path=maps/shopping_mall.bundle        rcid=Shopping_Mall.ScenesPreset.asset
// Laboratory        5b0fc42d86f7744a585f9105 path=maps/laboratory_preset.bundle    rcid=laboratory.ScenesPreset.asset
// Lighthouse        5704e4dad2720bb55b8b4567 path=maps/lighthouse_preset.bundle    rcid=lighthouse.scenespreset.asset
// Private Sector    5704e64ad2720bb55b8b456e path=                                 rcid=
// ReserveBase       5704e5fad2720bc05b8b4567 path=maps/rezerv_base_preset.bundle   rcid=Rezerv_Base.scenespreset.asset
// Shoreline         5704e554d2720bac5b8b456e path=maps/shoreline_preset.bundle     rcid=shoreline.scenespreset.asset
// Suburbs           5714dc342459777137212e0b path=                                 rcid=
// Streets of Tarkov 5714dc692459777137212e12 path=maps/city_preset.bundle          rcid=city.scenespreset.asset
// Labyrinth         6733700029c367a3d40b02af path=maps/labyrinth_preset.bundle     rcid=Labyrinth.scenespreset.asset
// Terminal          5704e5a4d2720bb45b8b4567 path=                                 rcid=
// Town              5704e47ed2720bb35b8b4568 path=                                 rcid=
// Woods             5704e3c2d2720bac5b8b4567 path=maps/woods_preset.bundle         rcid=woods.scenespreset.asset
// Sandbox           653e6760052c01c1c805532f path=maps/sandbox_preset.bundle       rcid=sandbox.scenespreset.asset
// Sandbox           65b8d6f5cdde2479cb2a3125 path=maps/sandbox_high_preset.bundle  rcid=sandbox_high.scenespreset.asset

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
		{ "5714dbc024597771384a510d", new(GetInterchangeScenes(), new Vector3(-596.6475f, 19.7394f, -740.3831f) - new Vector3(13.1f, 21.43f, -54.5f)) },
		{ "5704e3c2d2720bac5b8b4567", new(GetWoodsScenes(), new(899.999f, 0f, -799.9879f)) },
		{ "5714dc692459777137212e12", new(GetStreetsScenes(), new Vector3(-840.5616f, 5.0597f, -2238.547f) - new Vector3(-57.7054f, 5.0597f, 581.6671f)) },
		{ "65b8d6f5cdde2479cb2a3125", new(GetGroundZeroScenes(), new Vector3(-1782.518f, -15.2285f, -2785.837f)) },
	};

	public string DisabledObjectsDataPath;

	public Dictionary<string, Dictionary<string, List<List<int>>>> DisabledObjectsData;
	public HashSet<Transform> DisabledObjects;

	private void Awake()
	{
		Instance = this;

		var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		DisabledObjectsDataPath = Path.Combine(assemblyDir, "data", "disabled-objects.jsonc");
		var dataJson = File.ReadAllText(DisabledObjectsDataPath);
		DisabledObjectsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<List<int>>>>>(dataJson);
		DisabledObjects = new();

		new Patch_AudioCullingController_StartWorkCoroutine().Enable();
		// new Patch_WeatherController_method_4().Enable();
		new Patch_SpatialAudioSystem_LateUpdate().Enable();
		new Patch_SpatialAudioSystem_Update().Enable();
		new Patch_GPUInstancerDetailManager_GenerateCellsInstanceDataFromTerrain().Enable();
	}

	public void AddDisabledObject(GameObject go)
	{
		var tr = go.transform;
		if (DisabledObjects.Add(tr))
		{
			var (scenePath, rootPath, objectPath) = GetTransformPath(tr);
			if (DisabledObjectsData.TryGetValue(scenePath, out var oldScene))
			{
				if (oldScene.TryGetValue(rootPath, out var oldRoot))
				{
					oldRoot.Add(objectPath);
				}
				else
				{
					oldScene.Add(rootPath, new() { objectPath });
				}
			}
			else
			{
				DisabledObjectsData.Add(scenePath, new() { { rootPath, new() { objectPath }}});
			}
		}
		else
		{
			Logger.LogError("Already added");
		}
	}

	// why? because some objects have duplicate names and Transform.Find will
	// return only first occurance
	public static (string, string, List<int>) GetTransformPath(Transform tr)
	{
		var scenePath = tr.gameObject.scene.path;
		string rootPath = null;
        var result = new List<int>();
        var current = tr;

        while (current)
        {
			var parent = current.parent;
			if (parent)
			{
				// scene root gameObjects dont have sibling index (its always zero),
				// they are meant to be identified by name

	            result.Add(current.GetSiblingIndex());
	            current = parent;
			}
			else
			{
				// btw what happens if we have roots with identical names?
				rootPath = current.name;
				break;
			}
        }

        result.Reverse();

		return (scenePath, rootPath, result);
	}

    private static Transform Find(Transform root, List<int> path)
    {
        var result = root;
        foreach (var index in path)
        {
            result = result.GetChild(index);
        }
        return result;
    }

	public void DumpDisabledObjects()
	{
        var json = JsonConvert.SerializeObject(DisabledObjectsData, Formatting.Indented);
		File.WriteAllTextAsync(DisabledObjectsDataPath, json);
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

	public const int longNameLength = 17; // "Streets of Tarkov"
	public const int longPathLength = 32; // "maps/factory_night_preset.bundle"
	public const int longRcidLength = 32; // "factory_night.scenespreset.asset"

	public void DumpMaps()
	{
		if (TarkovApplication.Exist(out var tarkovApplication))
        {
			var locations = tarkovApplication.Session.LocationSettings.locations;
			foreach (var (id, location) in locations)
			{
				Logger.LogWarning($"{location.Name,-longNameLength} {id} path={location.Scene.path,-longPathLength} rcid={location.Scene.rcid,-longRcidLength}");
			}
        }
	}

	public async Task LoadAll()
	{
		foreach (var (id, data) in Maps)
		{
			Logger.LogError($"Loading: {id}");
			await LoadAnotherMap(id, data);
		}
		Logger.LogError("LOADING DONE!");

		for (var i = 0; i < 10; i++)
		{
			await Task.Yield();
		}

		foreach (var scene in SceneManager.GetAllScenes())
		{
			if (!DisabledObjectsData.TryGetValue(scene.path, out var disabledRoots))
			{
				continue;
			}
			foreach (var root in scene.GetRootGameObjects())
			{
				if (!disabledRoots.TryGetValue(root.name, out var disabledGOs))
				{
					continue;
				}
				if (disabledGOs.Count == 0)
				{
					root.SetActive(false);
					DisabledObjects.Add(root.transform);
				}
				else
				{
					var rootTransform = root.transform;
					foreach (var path in disabledGOs)
					{
						var goTransform = Find(rootTransform, path);
						if (goTransform)
						{
							goTransform.gameObject.SetActive(false);
							DisabledObjects.Add(goTransform);
						}
						else
						{
							Logger.LogError($"NOT FOUND: {scene.path} {root.name} {string.Join(",", path)}");
						}
					}
				}
			}
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

		// Logger.LogWarning($"scenes list:");
		// foreach (var scene in preset.ScenesResourceKeys)
		// {
		// 	Logger.LogWarning($"{scene.path} {scene.rcid}");
		// }
		// foreach (var childPreset in preset.ChildPresets)
		// {
		// 	foreach (var scene in childPreset.ScenesResourceKeys)
		// 	{
		// 		Logger.LogWarning($"{scene.path} {scene.rcid}");
		// 	}
		// }

		foreach (var scene in preset.ScenesResourceKeys)
		{
			if (allowedScenes.Contains(scene.path))
			{
				await assetsManager.LoadScene(scene, LoadSceneMode.Additive, true);
			}
		}
		foreach (var childPreset in preset.ChildPresets)
		{
			foreach (var scene in childPreset.ScenesResourceKeys)
			{
				if (allowedScenes.Contains(scene.path))
				{
					await assetsManager.LoadScene(scene, LoadSceneMode.Additive, true);
				}
			}
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
			"Assets/Content/Locations/Shopping_Mall/Shopping_Mall_Terrain.unity",
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

	private static HashSet<string> GetWoodsScenes()
	{
		return new()
		{
			// "Assets/Content/Locations/Woods/woods_Scripts.unity",
			"Assets/Content/Locations/Woods/woods_terrain.unity",
			"Assets/Content/Locations/Woods/woods_combined.unity",
			// "Assets/Content/Locations/Woods/woods_light.unity",
			// "Assets/Content/Locations/Woods/woods_design_stuff.unity",
			// "Assets/Content/Locations/Woods/woods_DesignMain.unity",
			// "Assets/Content/Locations/Woods/woods_AI.unity",
			// "Assets/Content/Locations/Woods/Woods_Sound.unity",
			// "Assets/Content/Locations/Woods/woods_Culling.unity",
		};
	}

	private static HashSet<string> GetStreetsScenes()
	{
		return new()
		{
			// "Assets/Content/Locations/City/City_Scripts.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_courtyard_A.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_courtyard_B.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_Klimova_24.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_Lenina_74.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_Lenina_74_Indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_Nikitskaya_1.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_Nikitskaya_1_Indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_Pinewood_Hotel.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_01/City_SE_01_Pinewood_Hotel_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_courtyard_A.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_courtyard_B.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Lenina_76.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Lenina_78.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Malevicha_1.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Malevicha_1_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/CIty_SE_02_Malevicha_2.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/CIty_SE_02_Malevicha_2_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Malevicha_3.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Malevicha_3_Indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Malevicha_5.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Malevicha_5_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/CIty_SE_02_Nikitskaya_2.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/CIty_SE_02_Nikitskaya_2_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_4.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_6.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_6_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_8.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_8_Indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_10.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_10_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_10a.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Nikitskaya_10a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Primorskiy_49.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Primorskiy_49_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Primorskiy_51.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Primorskiy_51_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Verhnyaya_Sadovaya_1.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Verhnyaya_Sadovaya_3.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_02/City_SE_02_Verhnyaya_Sadovaya_3_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_03/City_SE_03_Cinema.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_03/City_SE_03_Cinema_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_03/City_SE_03_Cinema_Street.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_03/City_SE_03_Lenina_80.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_03/City_SE_03_Square_Gagarina.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_03/City_SE_03_Verhnyaya_Sadovaya_4.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Lenina_84.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhnyaya_Sadovaya_2.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhnyaya_Sadovaya_2_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhnyaya_Sadovaya_4.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhnyaya_Sadovaya_4_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhnyaya_Sadovaya_4a.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhnyaya_Sadovaya_4a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhnyaya_Sadovaya_6.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Nizhynaya_Sadovaya_8.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Primorskiy_53.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Primorskiy_53_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Primorskiy_55.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_04/City_SE_04_Primorskiy_57.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_05/City_SE_05_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_05/City_SE_05_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_05/City_SE_05_Lenina_75.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_05/City_SE_05_Nikitskiy_Market.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_06/City_SE_06_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_06/City_SE_06_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_07/City_SE_07_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_SE_07/City_SE_07_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Chekannaya_15.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Chekannaya_15_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Klimova_18.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Klimova_18_Indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Klimova_20.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_44.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_44_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_46.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_46_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_48.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_48_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_50.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_50_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_52.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Primorskiy_52_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Zmeiskiy_1.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Zmeiskiy_3.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Zmeiskiy_3_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Zmeiskiy_3a.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Zmeiskiy_3a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Zmeiskiy_5.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_A/City_SW_01_A_Zmeiskiy_5_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Chekannaya_13.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Chekannaya_13_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_1.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_1a.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_1a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_3.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_3_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_5.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_5b.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Kamchatskaya_5b_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_12.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_14.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_14a.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_14a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_16.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_16_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_16a.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Klimova_16a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_School_30.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_School_30_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_2.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_2_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_4.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_4a.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_4a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_6.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_6_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_8.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_01_B/City_SW_01_B_Zmeiskiy_8_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Construction.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Construction_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Construction_outdoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_LexOs_AutoService.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_LexOs_AutoService_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_LexOs_AutoService_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_LexOs_blockpost.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_LexOs_RemBox.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_LexOs_RemBox_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Primorskiy_56.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Primorskiy_56_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Razvedchikov_7.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Razvedchikov_7_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Razvedchikov_9.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Razvedchikov_9_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Razvedchikov_9_Parking.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Razvedchikov_Courtyard_A.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Sparja.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Sparja_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_02/City_SW_02_Sparja_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_courtyad_A.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_courtyad_B.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_Kamchatskaya_2.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_Kamchatskaya_4.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_Klimova_2.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_Klimova_6.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_Klimova_8.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_03/City_SW_03_Klimova_10.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_04/City_SW_04_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_04/City_SW_04_Primorskiy_58.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_04/City_SW_04_Primorskiy_58_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_04/City_SW_04_Primorskiy_58_str1.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_04/City_SW_04_Primorskiy_58_str1_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_04/City_SW_04_Primorskiy_60.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_A_Kamchatskaya_12.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_A_Kamchatskaya_16.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_A_Razvedchikov_5.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_B_Razvedchikov_4.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_B_Razvedchikov_4_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_B_Razvedchikov_6.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_Building_Back.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_courtyard_A.unity",
			"Assets/Content/Locations/City/City_Areas/City_SW_05/City_SW_05_courtyard_B.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_01/City_NW_01_Cardinal.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_01/City_NW_01_Cardinal_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_01/City_NW_01_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_02/City_NW_02_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_02/City_NW_02_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_02/City_NW_02_Tetris.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_02/City_NW_02_Tetris_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_03/City_NW_03_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_03/City_NW_03_courtyard_A.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_03/City_NW_03_Klimova_1a.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_03/City_NW_03_Klimova_1a_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_03/City_NW_03_Senator.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_03/City_NW_03_Sportmarket_Lermontova.unity",
			"Assets/Content/Locations/City/City_Areas/City_NW_03/City_NW_03_Sportmarket_Lermontova_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_01/City_NE_01_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_01/City_NE_01_courtyard_A.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_buildings_back.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_Primorskiy_43.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_Primorskiy_45.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_Primorskiy_45_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_TD_Klimova.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_TD_Klimova_Beluga_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_TD_Klimova_courtyard.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_TD_Klimova_Foodcourt_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_TD_Klimova_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_TD_Klimova_Toy_Store_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_Transtechexport.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_Transtechexport_indoor.unity",
			"Assets/Content/Locations/City/City_Areas/City_NE_02/City_NE_02_Tyaglovoy_Per_1.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Chekannaya.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Kamchatskaya.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Klimova.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Lenina.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Malevicha.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Nikitskaya.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Primorskiy.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Razvedchikov.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Rohlina.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Sadovaya.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Sahalinskaya.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Tunnel.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Tyaglovoy_Per.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Underground.unity",
			"Assets/Content/Locations/City/City_Areas/City_Roads/City_Roads_Zmeiskiy.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Grass.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_Portals.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_Stencil.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_NE_02_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_NW_01_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_NW_02_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_NW_03_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_Roads_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SE_01_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SE_02_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SE_03_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SE_04_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SE_05_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SE_06_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SW_01_A_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SW_01_B_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SW_02_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SW_04_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_Light/City_SW_05_Light.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_NE_02_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_NW_01_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_NW_03_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_Roads_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SE_01_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SE_02_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SE_03_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SE_04_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SW_01_A_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SW_02_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SW_04_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Areas/City_DesignStuff/City_SW_05_DesignStuff.unity",
			// "Assets/Content/Locations/City/City_Design_Main.unity",
			// "Assets/Content/Locations/City/City_Quests.unity",
			// "Assets/Content/Locations/City/City_LevelBorders.unity",
			// "Assets/Content/Locations/City/City_AI.unity",
			// "Assets/Content/Locations/City/City_Sound.unity",
			// "Assets/Content/Locations/City/City_culling.unity",
		};
	}

	private static HashSet<string> GetGroundZeroScenes()
	{
		return new()
		{
			// "Assets/Content/Locations/Sandbox/Sandbox_Scripts.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_bottom_area.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_roads.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_01.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_01_courtyard.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_01_indoor.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_02.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_02_courtyard.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_02_indoor.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_03.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_03_courtyard.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_03_indoor.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_04.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_04_courtyrad.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_04_indoor.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_05.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_05_courtyard.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_05_indoor.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_07.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_07_courtyard.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_07_indoor.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_08.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_08_courtyard.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_Area_08_indoor.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_background_01.unity",
			"Assets/Content/Locations/Sandbox/Sandbox_Areas/Sandbox_background_02.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Grass.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Portals.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Stencil.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Area_01_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Area_02_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Area_03_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Area_04_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Area_05_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Area_07_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_Area_08_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_bottom_area_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_background_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Light/Sandbox_roads_Light.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Design_Stuff.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Design_Main.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Quests.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_AI_high.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_LevelBorders.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_Sound.unity",
			// "Assets/Content/Locations/Sandbox/Sandbox_culling.unity",
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

public class Patch_GPUInstancerDetailManager_GenerateCellsInstanceDataFromTerrain : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(GPUInstancerDetailManager), nameof(GPUInstancerDetailManager.GenerateCellsInstanceDataFromTerrain));
    }

    [PatchPrefix]
    public static bool Prefix()
	{
		return false;
	}
}
