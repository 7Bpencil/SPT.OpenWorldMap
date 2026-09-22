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
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using System.Reflection;
using SPTarkov.Server.Core.Models.Spt.Servers;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Server.Core.Utils;

namespace SevenBoldPencil.OpenWorld;

[Injectable(InjectionType = InjectionType.Singleton, TypePriority = OnLoadOrder.Watermark)]
public class Plugin(IEnumerable<IRuntimePatch> patches, IReadOnlyList<SptMod> mods, TemplateTable templateTable, ISptLogger<Plugin> logger) : IOnLoad
{
    public static Plugin Instance;

    public Task OnLoadAsync(CancellationToken cancellationToken)
    {
        Instance = this;
        return Task.CompletedTask;
    }
}
