//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using BepInEx;
using BepInEx.Configuration;
using Diz.Utils;
using EFT;
using EFT.GameTriggers;
using EFT.Impostors;
using EFT.Interactive;
using EFT.Settings.Graphics;
using EFT.UI.Settings;
using Newtonsoft.Json;
using HarmonyLib;
using SPT.Reflection.Patching;
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using JsonType;
using GPUInstancer;
using Koenigz.PerfectCulling.EFT;

namespace SevenBoldPencil.OpenWorld;

public record MapData
(
	HashSet<string> AllowedScenes,
	Vector3 Offset
);

// TODO fix floating lighthouse ships:
// - Lighthouse_Background/SBG_Lighthouse_Background/OO/Lighthouse_ship_Omski
// - Lighthouse_Background/SBG_Lighthouse_Background/OO/Lighthouse_ship_TowUran

[BepInPlugin("7Bpencil.OpenWorld", "7Bpencil.OpenWorld", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
    public const string MapKey = "7bpencil.openworld"; // lowercase is mandatory
	public const string MapScenePath = "maps/7bpencil_openworld_preset.bundle";

	private static Dictionary<string, MapData> Maps = new()
	{
		{ Scenes.FactoryId, new(Scenes.Factory, new(0, 0, 0)) },
		{ Scenes.CustomsId, new(Scenes.Customs, new(-899.999f, 0f, 799.9879f)) },
		{ Scenes.ReserveId, new(Scenes.Reserve, new(-97.51111f, 0f, 1277.216f)) },
		{ Scenes.LighthouseId, new(Scenes.Lighthouse, new(825.877f, -65.6444f, 1845.755f)) },
		{ Scenes.ShorelineId, new(Scenes.Shoreline, new(35.87708f, 0.00289917f, 1897.755f)) },
		{ Scenes.InterchangeId, new(Scenes.Interchange, new(-1509.747f, -15.95461f, -147.6376f)) },
		{ Scenes.WoodsId, new(Scenes.Woods, new(0, 0, 0)) },
		{ Scenes.StreetsId, new(Scenes.Streets, new(-1682.855f, 0f, -2020.226f)) },
		{ Scenes.GroundZeroId, new(Scenes.GroundZero, new(-2682.517f, -15.2285f, -1985.849f)) },
	};

	public static Plugin Instance;

	public static ConfigEntry<bool> SpawnCustoms;
	public static ConfigEntry<bool> SpawnReserve;
	public static ConfigEntry<bool> SpawnLighthouse;
	public static ConfigEntry<bool> SpawnShoreline;
	public static ConfigEntry<bool> SpawnInterchange;
	public static ConfigEntry<bool> SpawnStreets;
	public static ConfigEntry<bool> SpawnGroundZero;

	private string DisabledObjectsDataPath;

	private Dictionary<string, Dictionary<string, List<List<int>>>> DisabledObjectsData;
	private HashSet<Transform> DisabledObjects;
	private Dictionary<string, Vector3> SceneOffsetTable;

	private ScenesPreset OpenWorldScenesPreset;

    private void Awake()
	{
		Instance = this;

		SpawnCustoms = Config.Bind<bool>("Maps", "Customs", true);
		SpawnReserve = Config.Bind<bool>("Maps", "Reserve", true);
		SpawnLighthouse = Config.Bind<bool>("Maps", "Lighthouse", true);
		SpawnShoreline = Config.Bind<bool>("Maps", "Shoreline", true);
		SpawnInterchange = Config.Bind<bool>("Maps", "Interchange", true);
		SpawnStreets = Config.Bind<bool>("Maps", "Streets", true);
		SpawnGroundZero = Config.Bind<bool>("Maps", "GroundZero", true);

		var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
		DisabledObjectsDataPath = Path.Combine(assemblyDir, "data", "disabled-objects.jsonc");
		var dataJson = File.ReadAllText(DisabledObjectsDataPath);
		DisabledObjectsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<List<int>>>>>(dataJson);
		DisabledObjects = new();

		SceneOffsetTable = new();
		foreach (var (mapId, mapData) in Maps)
		{
			foreach (var scene in mapData.AllowedScenes)
			{
				SceneOffsetTable.Add(scene, mapData.Offset);
			}
		}

		// patch graphics visibility setting to go to 10k

		var newSettings = Array.AsReadOnly(new float[7] { 400f, 1000f, 1500f, 2000f, 2500f, 3000f, 10000f });
		typeof(GraphicsSettingsTab)
			.GetField("_overallVisibilityVariants", BindingFlags.Static | BindingFlags.NonPublic)
			.SetValue(null, newSettings);

		// add our map to AvailableMaps array, otherwise there wont
		// be day/night time selection on map screen

		var oldLocations = LocationSettings.Location.AvailableMaps;
		var newLocations = new string[oldLocations.Length + 1];
		Array.Copy(oldLocations, newLocations, oldLocations.Length);
		newLocations[oldLocations.Length] = MapKey;

		typeof(LocationSettings.Location)
			.GetField(nameof(LocationSettings.Location.AvailableMaps), BindingFlags.Static | BindingFlags.Public)
			.SetValue(null, newLocations);

		OpenWorldScenesPreset = ScriptableObject.CreateInstance<ScenesPreset>();
		OpenWorldScenesPreset.ChildPresets = [];
		OpenWorldScenesPreset._scenesResourceKeys = [];

		new Patch_GraphicsSettingsGroup().Enable();
		new Patch_LoadScenesFromPresetOperation_LoadPresetFromConfigAsync().Enable();
		new Patch_BotDoorsController_RefreshData().Enable();
		new Patch_LocalClientTriggersModule_Awake().Enable();
		new Patch_DistantShadow_Awake().Enable();
	}

	public static readonly HashSet<string> StartScenes = new()
	{
		"Assets/Content/Locations/Woods/woods_Scripts.unity",
		"Assets/Content/Locations/Woods/woods_terrain.unity",
		"Assets/Content/Locations/Woods/woods_combined.unity",
	};
	public static readonly HashSet<string> EndScenes = new()
	{
		"Assets/Content/Locations/Woods/woods_DesignMain.unity",
		"Assets/Content/Locations/Woods/woods_AI.unity",
		"Assets/Content/Locations/Woods/Woods_Sound.unity",
	};

	public ScenesPreset GetOpenWorldMapScenesList()
	{
		var scenes = OpenWorldScenesPreset._scenesResourceKeys;
		scenes.Clear();

		AddScenesList(StartScenes, scenes);

		if (SpawnCustoms.Value) AddScenesList(Scenes.Customs, scenes);
		if (SpawnReserve.Value) AddScenesList(Scenes.Reserve, scenes);
		if (SpawnLighthouse.Value) AddScenesList(Scenes.Lighthouse, scenes);
		if (SpawnShoreline.Value) AddScenesList(Scenes.Shoreline, scenes);
		if (SpawnInterchange.Value) AddScenesList(Scenes.Interchange, scenes);
		if (SpawnStreets.Value) AddScenesList(Scenes.Streets, scenes);
		if (SpawnGroundZero.Value) AddScenesList(Scenes.GroundZero, scenes);

		AddScenesList(EndScenes, scenes);

		return OpenWorldScenesPreset;
	}

	// this works only for scenes from vanilla maps,
	// custom maps scenes can have different path and rcid
	public static void AddScenesList(HashSet<string> scenesList, List<SceneResourceKey> target)
	{
		foreach (var scene in scenesList)
		{
			target.Add(new() { path = scene, rcid = scene });
		}
	}

#if DEBUG
	public void DisabledObjectsAdd(GameObject go)
	{
		go.SetActive(false);
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

	public void DisabledObjectsRemove(GameObject go)
	{
		go.SetActive(true);
		var tr = go.transform;
		if (DisabledObjects.Remove(tr))
		{
			var (scenePath, rootPath, objectPath) = GetTransformPath(tr);
			if (DisabledObjectsData.TryGetValue(scenePath, out var oldScene))
			{
				if (oldScene.TryGetValue(rootPath, out var oldRoot))
				{
					var index = oldRoot.FindIndex(e => e.SequenceEqual(objectPath));
					if (index != -1)
					{
					    oldRoot.RemoveAt(index);
					}
					if (oldRoot.Count == 0)
					{
						oldScene.Remove(rootPath);
					}
					if (oldScene.Count == 0)
					{
						DisabledObjectsData.Remove(scenePath);
					}
				}
			}
		}
	}

	// why List<int>? because some objects have duplicate names
	// and Transform.Find will return only first occurance
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
				// (but what happens if we have roots with identical names?)

	            result.Add(current.GetSiblingIndex());
	            current = parent;
			}
			else
			{
				rootPath = current.name;
				break;
			}
        }

		if (result.Count == 0)
		{
			result.Add(-1);
		}

        result.Reverse();

		return (scenePath, rootPath, result);
	}
#endif

    private static Transform Find(Transform root, List<int> path)
    {
		if (path.Count == 1 && path[0] == -1)
		{
			return root;
		}

        var result = root;
        foreach (var index in path)
        {
            result = result.GetChild(index);
        }
        return result;
    }

#if DEBUG
	public void DisabledObjectsDump()
	{
        var json = JsonConvert.SerializeObject(DisabledObjectsData);
		File.WriteAllTextAsync(DisabledObjectsDataPath, json);
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
#endif

	public void TweakMaps()
	{
		DisableObjects();
		MoveScenes();
		UpdateTreeImpostors();
		UpdateGrass();
		DisableAllCullingObjects();
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

	public void UpdateGrass()
	{
		foreach (var detailManager in UnityEngine.Object.FindObjectsOfType<GPUInstancerDetailManager>())
		{
			if (SceneOffsetTable.TryGetValue(detailManager.gameObject.scene.path, out var offset))
			{
				detailManager.SetGlobalPositionOffset(offset);
			}
		}
	}

	// TODO this is only needed for good looking freecam views,
	// so objects and terrain do not dissapear at distance,
	// and should not be used in normal gameplay
    public void DisableAllCullingObjects()
    {
        foreach (var cullingObject in FindObjectsOfType<DisablerCullingObjectBase>())
        {
            if (!cullingObject.HasEntered)
            {
	            cullingObject.SetComponentsEnabled(true);
            }
			cullingObject.OnDestroy();
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

#if DEBUG
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
#endif
}

public class Patch_GraphicsSettingsGroup : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Constructor(typeof(GraphicsSettingsGroup), [typeof(GraphicsSettingsController)]);
    }

    [PatchPostfix]
	public static void Postfix(GraphicsSettingsGroup __instance)
	{
		__instance.OverallVisibility._asyncPreProcessor = value => Task.FromResult(Mathf.Clamp(value, 400f, 10000f));
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
		if (preset.key.path == Plugin.MapScenePath)
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

		var scenesPreset = Plugin.Instance.GetOpenWorldMapScenesList();

		scenesPreset.DisableServerScenes(preset.DisableServerScenes);
		__instance._totalScenesToLoad = scenesPreset.ScenesResourceKeys.Length;
		__instance._progress = __instance._progress.Select(delegate(float x)
		{
			__instance._scenesLoaded += x;
			__instance._scenesLoaded = Mathf.Clamp(__instance._scenesLoaded, 0f, __instance._totalScenesToLoad);
			return __instance._scenesLoaded / (float)__instance._totalScenesToLoad;
		});

		await __instance.LoadPresetAsync(scenesPreset);

		Plugin.Instance.TweakMaps();
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
		if (TarkovApplication.Exist(out var tarkovApplication))
		{
			if (tarkovApplication.CurrentRaidSettings.SelectedLocation.Scene.path == Plugin.MapScenePath)
			{
				return false;
			}
		}
		return true;
	}
}

public class Patch_LocalClientTriggersModule_Awake : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(LocalClientTriggersModule), nameof(LocalClientTriggersModule.Awake));
    }

    [PatchPrefix]
    public static bool Prefix(ref Dictionary<string, WorldInteractiveObject> ____worldInteractiveObjects)
	{
		if (TarkovApplication.Exist(out var tarkovApplication))
		{
			if (tarkovApplication.CurrentRaidSettings.SelectedLocation.Scene.path == Plugin.MapScenePath)
			{
				____worldInteractiveObjects = [];
				return false;
			}
		}
		return true;
	}
}

// for some reason distant shadow captures terrain quad,
// which creates giant ugly non-sensical shadow
public class Patch_DistantShadow_Awake : ModulePatch
{
    protected override MethodBase GetTargetMethod()
    {
        return AccessTools.Method(typeof(DistantShadow), nameof(DistantShadow.Awake));
    }

    [PatchPrefix]
    public static bool Prefix(DistantShadow __instance)
	{
		if (TarkovApplication.Exist(out var tarkovApplication))
		{
			if (tarkovApplication.CurrentRaidSettings.SelectedLocation.Scene.path == Plugin.MapScenePath)
			{
				__instance.gameObject.SetActive(false);
				return false;
			}
		}
		return true;
	}
}
