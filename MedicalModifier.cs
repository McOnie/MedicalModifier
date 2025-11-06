using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Helpers;
using SPTarkov.Server.Core.Models.Eft.Common.Tables;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Utils;
using SPTarkov.Server.Core.Servers;
using System.Reflection;
using System.Text.Json.Serialization;
using Path = System.IO.Path;

namespace MedicalModifier
{
    public record ModMetadata : AbstractModMetadata
    {
        public override string ModGuid { get; init; } = "com.mconie.medicalmodifier";
        public override string Name { get; init; } = "MedicalModifier";
        public override string Author { get; init; } = "McOnie";
        public override List<string>? Contributors { get; init; } = null;
        public override SemanticVersioning.Version Version { get; init; } = new("1.0.0"); // Incremented version
        public override SemanticVersioning.Range SptVersion { get; init; } = new("~4.0.2");
        public override List<string>? Incompatibilities { get; init; } = null;
        public override Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = null;
        public override string? Url { get; init; } = null;
        public override bool? IsBundleMod { get; init; } = false;
        public override string? License { get; init; } = "MIT";
    }

    public class ItemConfig
    {
        public bool Enable { get; set; } = false;
        [JsonPropertyName("MaxHpPool")]
        public int? MaxHpPool { get; set; }
        [JsonPropertyName("HpPoolUseRate")]
        public int? HpPoolUseRate { get; set; }
        [JsonPropertyName("MedUseTime")]
        public int? MedUseTime { get; set; }
    }

    public class ModConfig
    {
        public Dictionary<string, ItemConfig> MedKits { get; set; } = new Dictionary<string, ItemConfig>();
        public Dictionary<string, ItemConfig> Painkillers { get; set; } = new Dictionary<string, ItemConfig>();
        public Dictionary<string, ItemConfig> Bandages { get; set; } = new Dictionary<string, ItemConfig>();
        public Dictionary<string, ItemConfig> Splints { get; set; } = new Dictionary<string, ItemConfig>();
        public Dictionary<string, ItemConfig> SurgicalKits { get; set; } = new Dictionary<string, ItemConfig>();
        public Dictionary<string, ItemConfig> Tourniquets { get; set; } = new Dictionary<string, ItemConfig>();
        public Dictionary<string, ItemConfig> Injectors { get; set; } = new Dictionary<string, ItemConfig>();
    }

    [Injectable(TypePriority = OnLoadOrder.PostDBModLoader + 1010)]
    public class MedicalModifier(ISptLogger<MedicalModifier> _logger, ModHelper _modHelper, DatabaseServer _databaseServer) : IOnLoad
    {
        private static readonly Dictionary<string, string> ItemIDMap = new Dictionary<string, string>
        {
            // MedKits
            { "AI-2",         "5755356824597772cb798962" },
            { "Car",          "590c661e86f7741e566b646a" },
            { "Salewa",       "544fb45d4bdc2dee738b4568" },
            { "IFAK",         "590c678286f77426c9660122" },
            { "AFAK",         "60098ad7c2240c0fe85c570a" },
            { "Grizzly",      "590c657e86f77412b013051d" },

            // Injectors
            { "Morphine",     "544fb3f34bdc2d03748b456a" },
            { "L1",           "5ed515e03a40a50460332579" },
            { "Trimadol",     "637b620db7afa97bfc3d7009" },
            { "Adrenaline",   "5c10c8fd86f7743d7d706df3" },
            { "Propital",     "5c0e530286f7747fa1419862" },
            { "eTG",          "5c0e534186f7747fa1419867" },
            { "xTG",          "5fca138c2a7b221b2852a5c6" },
            { "Perfotoran",   "637b6251104668754b72f8f9" },
            { "AHF1",         "5ed515f6915ec335206e4152" },
            { "Zagustin",     "5c0e533786f7747fa23f4d47" },
            { "PNB",          "637b6179104668754b72f8f5" },
            { "P22",          "5ed515ece452db0eb56fc028" },
            { "Meldonin",     "5ed5160a87bb8443d10680b5" },
            { "SJ1",          "5c0e531286f7747fa54205c2" },
            { "SJ6",          "5c0e531d86f7747fa23f4d42" },
            { "3-b-TG",       "5ed515c8d380ab312177c0fa" },
            { "2A2-b-TG",     "66507eabf5ddb0818b085b68" },
            { "Obdolbos",     "5ed5166ad380ab312177c100" },
            { "Obdolbos2",    "637b60c3b7afa97bfc3d7001" },
            { "M.U.L.E",      "5ed51652f6c34d2cc26336a1" },
            { "SJ9",          "5fca13ca637ee0341a484f46" },
            { "SJ12",         "637b612fb7afa97bfc3d7005" },

            // Painkillers
            { "Analgin",      "544fb37f4bdc2dee738b4567" },
            { "Augmentin",    "590c695186f7741e566b64a2" },
            { "Ibuprofen",    "5af0548586f7743a532b7e99" },
            { "Vaseline",     "5755383e24597772cb798966" },
            { "GoldenStar",   "5751a89d24597722aa0e8db0" },
      
            // Tourniquets
            { "Esmarch",      "5e831507ea0a7c419c2f9bd9" },
            { "CALOK-B",      "5e8488fa988a8701445df1e4" },
            { "CAT",          "60098af40accd37ef2175f27" },

            // Bandages
            { "Aseptic",      "544fb25a4bdc2dfb738b4567" },
            { "Army",         "5751a25924597722c463c472" },
      
            // Splints
            { "Immobilizing", "544fb3364bdc2d34748b456a" },
            { "Aluminum",     "5af0454c86f7746bf20992e8" },
      
            // SurgicalKits
            { "CMS",          "5d02778e86f774203e7dedbe" },
            { "Surv12",       "5d02797c86f774203f38e30a" }
        };

        public Task OnLoad()
        {
            var modFolder = _modHelper.GetAbsolutePathToModFolder(Assembly.GetExecutingAssembly());
            var configDir = Path.Combine(modFolder, "config");
            var modConfig = _modHelper.GetJsonDataFromFile<ModConfig>(configDir, "config.jsonc");

            var itemDB = _databaseServer.GetTables().Templates.Items;

            var allConfigItems = new Dictionary<string, ItemConfig>();

            modConfig.MedKits.ToList().ForEach(x => allConfigItems.TryAdd(x.Key, x.Value));
            modConfig.Painkillers.ToList().ForEach(x => allConfigItems.TryAdd(x.Key, x.Value));
            modConfig.Bandages.ToList().ForEach(x => allConfigItems.TryAdd(x.Key, x.Value));
            modConfig.Splints.ToList().ForEach(x => allConfigItems.TryAdd(x.Key, x.Value));
            modConfig.SurgicalKits.ToList().ForEach(x => allConfigItems.TryAdd(x.Key, x.Value));
            modConfig.Tourniquets.ToList().ForEach(x => allConfigItems.TryAdd(x.Key, x.Value));
            modConfig.Injectors.ToList().ForEach(x => allConfigItems.TryAdd(x.Key, x.Value));

            int itemsModified = 0;

            foreach (var configEntry in allConfigItems)
            {
                string configKey = configEntry.Key;
                ItemConfig config = configEntry.Value;

                if (config == null || !config.Enable)
                {
                    continue;
                }s
                if (!ItemIDMap.TryGetValue(configKey, out string itemID))
                {
                    _logger.Warning($"[Medical Modifier] Config found for '{configKey}', but no ItemID is mapped in the mod. Skipping.");
                    continue;
                }
                if (itemDB.TryGetValue(itemID, out var item))
                {
                    ApplyItemChanges(item.Properties, config, _logger, item.Name);
                    itemsModified++;
                }
                else
                {
                    _logger.Warning($"[Medical Modifier] ItemId '{itemID}' (Name: {configKey}) not found in database. Skipping override.");
                }
            }

            _logger.Success($"[Medical Modifier] Mod Loaded: Applied overrides to {itemsModified} items.");

            return Task.CompletedTask;
        }

        private void ApplyItemChanges(TemplateItemProperties props, ItemConfig config, ISptLogger<MedicalModifier> _logger, string itemName)
        {
            if (config.MaxHpPool.HasValue)
                SetNumber(props, "MaxHpResource", new[] { "MaxHpResource" }, config.MaxHpPool.Value);
            if (config.HpPoolUseRate.HasValue)
                SetNumber(props, "HpResourceRate", new[] { "HpResourceRate" }, config.HpPoolUseRate.Value);
            if (config.MedUseTime.HasValue)
                SetNumber(props, "medUseTime", new[] { "medUseTime" }, config.MedUseTime.Value);

            _logger.Success($"[Medical Modifier] Applied config changes to {itemName}");
        }

        private static (PropertyInfo? pi, object? owner) FindProp(object obj, string jsonName, string[] candidates)
        {
            foreach (var p in obj.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (string.Equals(p.Name, jsonName, StringComparison.OrdinalIgnoreCase)) return (p, obj);
                if (Array.Exists(candidates, c => string.Equals(c, p.Name, StringComparison.OrdinalIgnoreCase))) return (p, obj);
                var j = p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name;
                if (!string.IsNullOrEmpty(j) && string.Equals(j, jsonName, StringComparison.OrdinalIgnoreCase)) return (p, obj);
            }
            return (null, null);
        }

        private static void SetNumber(object obj, string jsonName, string[] candidates, int value)
        {
            var (pi, owner) = FindProp(obj, jsonName, candidates);
            if (pi is null || owner is null) return;
            try
            {
                var tgt = Nullable.GetUnderlyingType(pi.PropertyType) ?? pi.PropertyType;
                if (tgt == typeof(int) || tgt == typeof(long) || tgt == typeof(short)) { pi.SetValue(owner, Convert.ChangeType(value, tgt)); return; }
                if (tgt == typeof(float) || tgt == typeof(double) || tgt == typeof(decimal)) { pi.SetValue(owner, Convert.ChangeType(value, tgt)); return; }
            }
            catch { }
        }
    }
}