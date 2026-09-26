//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using Audio;
using Audio.AudioCulling;
using Audio.SpatialSystem;
using Audio.SpatialSystem.Data;
using BepInEx;
using Comfort.Common;
using Diz.Utils;
using EFT;
using EFT.AssetsManager;
using EFT.InventoryLogic;
using EFT.Impostors;
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
using Koenigz.PerfectCulling.EFT;

// TODO dont use path for idetifying scenes

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

// TODO make custom location with scenes from customs and woods to test if it fixes trees
// TODO port matsix clouds to 4.1 just for the vid

[BepInPlugin("7Bpencil.OpenWorld", "7Bpencil.OpenWorld", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
	public static Plugin Instance;

	// WoodsOffset, I first got all offsets relative to Customs, but Woods is easier to work with
	private static readonly Vector3 WO = new(-899.999f, 0f, 799.9879f);

	private Dictionary<string, MapData> Maps = new()
	{
		{ "55f2d3fd4bdc2d5f408b4567", new(Scenes.Factory, new(0, 0, 0)) },
		{ "56f40101d2720b2a4d8b45d6", new(Scenes.Customs, WO) },
		{ "5704e5fad2720bc05b8b4567", new(Scenes.Reserve, new Vector3(802.4879f, 0, 477.2278f) + WO) },
		{ "5704e4dad2720bb55b8b4567", new(Scenes.Lighthouse, new Vector3(921.452f, -38.4145f, -691.3636f) - new Vector3(-804.424f, 27.2299f, -1737.131f) + WO) },
		{ "5704e554d2720bac5b8b456e", new(Scenes.Shoreline, new Vector3(1162.224f, -92.2317f, 1436.709f) - new Vector3(226.3479f, -92.2346f, 338.9418f) + WO) },
		{ "5714dbc024597771384a510d", new(Scenes.Interchange, new Vector3(-596.6475f, 19.7394f, -740.3831f) - new Vector3(13.1f, 21.43f, -54.5f) + WO) },
		{ "5704e3c2d2720bac5b8b4567", new(Scenes.Woods, new(0, 0, 0)) },
		{ "5714dc692459777137212e12", new(Scenes.Streets, new Vector3(-840.5616f, 5.0597f, -2238.547f) - new Vector3(-57.7054f, 5.0597f, 581.6671f)) },
		{ "65b8d6f5cdde2479cb2a3125", new(Scenes.GroundZero, new Vector3(-1782.518f, -15.2285f, -2785.837f)) },
	};

	public string DisabledObjectsDataPath;

	public Dictionary<string, Dictionary<string, List<List<int>>>> DisabledObjectsData;
	public HashSet<Transform> DisabledObjects;
	public Dictionary<string, Vector3> SceneOffsetTable;

	public ScenesPreset OpenWorldScenesPreset;

    private void Awake()
	{
		Instance = this;

		var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		DisabledObjectsDataPath = Path.Combine(assemblyDir, "data", "disabled-objects.jsonc");
		var dataJson = File.ReadAllText(DisabledObjectsDataPath);
		DisabledObjectsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<List<int>>>>>(dataJson);
		DisabledObjects = new();

		SceneOffsetTable = new();
		foreach (var mapData in Maps.Values)
		{
			foreach (var scene in mapData.AllowedScenes)
			{
				SceneOffsetTable.Add(scene, mapData.Offset);
			}
		}

		// add our map to AvailableMaps array, otherwise there wont
		// be day/night time selection on map screen

		var oldLocations = LocationSettings.Location.AvailableMaps;
		var newLocations = new string[oldLocations.Length + 1];
		Array.Copy(oldLocations, newLocations, oldLocations.Length);
		newLocations[oldLocations.Length] = "7bpencil.openworld";

		typeof(LocationSettings.Location)
			.GetField(nameof(LocationSettings.Location.AvailableMaps), BindingFlags.Static | BindingFlags.Public)
			.SetValue(null, newLocations);

		OpenWorldScenesPreset = ScriptableObject.CreateInstance<ScenesPreset>();
		OpenWorldScenesPreset.ChildPresets = [];

		string[] start =
		[
			"Assets/Content/Locations/Woods/woods_Scripts.unity",
			"Assets/Content/Locations/Woods/woods_terrain.unity",
			"Assets/Content/Locations/Woods/woods_combined.unity",
		];
		string[] end =
		[
			"Assets/Content/Locations/Woods/woods_DesignMain.unity",
			"Assets/Content/Locations/Woods/woods_AI.unity",
			"Assets/Content/Locations/Woods/Woods_Sound.unity",
		];

		var scenes = new List<string>();
		scenes.AddRange(start);
		scenes.AddRange(Scenes.Customs);
		scenes.AddRange(Scenes.Reserve);
		scenes.AddRange(Scenes.Lighthouse);
		scenes.AddRange(Scenes.Shoreline);
		scenes.AddRange(Scenes.Interchange);
		scenes.AddRange(Scenes.Streets);
		scenes.AddRange(Scenes.GroundZero);
		scenes.AddRange(end);

		OpenWorldScenesPreset._scenesResourceKeys = ConvertScenesList(scenes);

		new Patch_LoadScenesFromPresetOperation_LoadPresetFromConfigAsync().Enable();
		new Patch_BotDoorsController_RefreshData().Enable();
	}

	// this works only for vanilla scenes,
	// custom scenes can have different path and rcid
	public static List<SceneResourceKey> ConvertScenesList(List<string> scenes)
	{
		var result = new List<SceneResourceKey>(scenes.Count);
		foreach (var scene in scenes)
		{
			result.Add(new() { path = scene, rcid = scene });
		}
		return result;
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
			TweakMaps();
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
	}

	public void TweakMaps()
	{
		DisableObjects();
		MoveScenes();
		UpdateTreeImpostors();
	}

	public void DisableObjects()
	{
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

	public void MoveScenes()
	{
		foreach (var scene in SceneManager.GetAllScenes())
		{
			if (SceneOffsetTable.TryGetValue(scene.path, out var offset))
			{
				foreach (var root in scene.GetRootGameObjects())
				{
					root.transform.position += offset;
				}
			}
		}
	}

	public void UpdateTreeImpostors()
	{
		foreach (var impostorsRenderer in UnityEngine.Object.FindObjectsOfType<ImpostorsRenderer>())
		{
			impostorsRenderer.Refresh();
		}
	}

    public void DisableAllCullingObjects()
    {
        foreach (var cullingObject in FindObjectsOfType<DisablerCullingObjectBase>())
        {
            if (!cullingObject.HasEntered)
            {
	            cullingObject.SetComponentsEnabled(true);
            }
        }
		foreach (var perfectCullingAdaptiveGrid in FindObjectsOfType<PerfectCullingAdaptiveGrid>())
        {
            if (perfectCullingAdaptiveGrid.RuntimeGroupMapping.Count > 0)
            {
                foreach (var sceneGroup in perfectCullingAdaptiveGrid.RuntimeGroupMapping)
                {
                    foreach (var bakeGroup in sceneGroup.bakeGroups)
                    {
                        if (!bakeGroup.IsEnabled)
                        {
                            bakeGroup.IsEnabled = true;
                            continue;
                        }
                    }

                    sceneGroup.enabled = false;
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
	}
}

public class Patch_LoadScenesFromPresetOperation_LoadPresetFromConfigAsync : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LoadScenesFromPresetOperation), nameof(LoadScenesFromPresetOperation.LoadPresetFromConfigAsync));
    }

    [PatchPrefix]
    public static bool Prefix(ref Task __result, LoadScenesFromPresetOperation __instance, ScenePresetLoadConfig preset)
	{
		if (preset.key.path == "maps/7bpencil_openworld_preset.bundle")
		{
			__result = Mine(__instance, preset);
			return false;
		}

		return true;
	}

	// copy-paste of original method, but instead of loading bundle that contains scriptable object
	// with list of scenes, we pass our own list without all the bundle bullshit
	public static async Task Mine(LoadScenesFromPresetOperation __instance, ScenePresetLoadConfig preset)
	{
		__instance._scenesLoaded = 0f;
		__instance._progress?.Report(0f);

		var scenesPreset = Plugin.Instance.OpenWorldScenesPreset;

		scenesPreset.DisableServerScenes(preset.DisableServerScenes);
		__instance._totalScenesToLoad = scenesPreset.ScenesResourceKeys.Length;
		__instance._progress = __instance._progress.Select(delegate(float x)
		{
			__instance._scenesLoaded += x;
			__instance._scenesLoaded = Mathf.Clamp(__instance._scenesLoaded, 0f, __instance._totalScenesToLoad);
			return __instance._scenesLoaded / (float)__instance._totalScenesToLoad;
		});

		await __instance.LoadPresetAsync(scenesPreset);
	}
}

public class Patch_BotDoorsController_RefreshData : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(BotDoorsController), nameof(BotDoorsController.RefreshData));
    }

    [PatchPrefix]
    public static bool Prefix()
	{
		return false;
	}
}
