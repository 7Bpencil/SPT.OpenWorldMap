//
// Copyright (c) 2026 7Bpencil
//
// This source code is licensed under the MIT license found in the
// LICENSE file in the root directory of this source tree.
//

using BepInEx;
using EFT.InventoryLogic;
using EFT.UI;
using HarmonyLib;
using SPT.Reflection.Patching;
using SPT.Common.Http;
using SPT.Common.Utils;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace SevenBoldPencil.OpenWorld;

[BepInPlugin("7Bpencil.OpenWorld", "7Bpencil.OpenWorld", "0.0.1")]
public class Plugin : BaseUnityPlugin
{
	public static Plugin Instance;

	private void Awake()
	{
		Instance = this;
	}
}
