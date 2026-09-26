using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1SlimeSpawner.ModEntry), "SR1 Slime Spawner", "0.1.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1SlimeSpawner
{
    public sealed class ModEntry : MelonMod
    {
        private Type _rectType;
        private Type _guiType;
        private ConstructorInfo _rectCtor;
        private MethodInfo _button;
        private MethodInfo _box;
        private MethodInfo _label;
        private string _status = "Pronto. Entre em um save e clique em um slime.";

        public override void OnInitializeMelon()
        {
            try
            {
                EnsurePresets();
                LoggerInstance.Msg("SR1 Slime Spawner carregado. Painel RAD / QUANTUM / MOSAIC ativo.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("Falha ao preparar presets: " + ex);
            }
        }

        public override void OnGUI()
        {
            try
            {
                if (!EnsureGui())
                    return;

                Box(18f, 18f, 238f, 190f, "SR1 Slimes");
                Label(34f, 48f, 205f, 22f, "Spawner de teste");

                if (Button(34f, 78f, 205f, 28f, "Spawn RAD"))
                    Spawn("RadSlime");
                if (Button(34f, 112f, 205f, 28f, "Spawn QUANTUM"))
                    Spawn("QuantumSlime");
                if (Button(34f, 146f, 205f, 28f, "Spawn MOSAIC"))
                    Spawn("MosaicSlime");

                Label(34f, 178f, 205f, 26f, _status);
            }
            catch
            {
            }
        }

        private bool EnsureGui()
        {
            if (_button != null)
                return true;

            _rectType = Type.GetType("UnityEngine.Rect, UnityEngine.CoreModule");
            _guiType = Type.GetType("UnityEngine.GUI, UnityEngine.IMGUIModule");
            if (_rectType == null || _guiType == null)
                return false;

            _rectCtor = _rectType.GetConstructor(new[] { typeof(float), typeof(float), typeof(float), typeof(float) });
            _button = _guiType.GetMethod("Button", BindingFlags.Public | BindingFlags.Static, null, new[] { _rectType, typeof(string) }, null);
            _box = _guiType.GetMethod("Box", BindingFlags.Public | BindingFlags.Static, null, new[] { _rectType, typeof(string) }, null);
            _label = _guiType.GetMethod("Label", BindingFlags.Public | BindingFlags.Static, null, new[] { _rectType, typeof(string) }, null);
            return _rectCtor != null && _button != null;
        }

        private object Rect(float x, float y, float w, float h)
            => _rectCtor.Invoke(new object[] { x, y, w, h });

        private bool Button(float x, float y, float w, float h, string text)
            => (bool)_button.Invoke(null, new[] { Rect(x, y, w, h), text });

        private void Box(float x, float y, float w, float h, string text)
        {
            if (_box != null)
                _box.Invoke(null, new[] { Rect(x, y, w, h), text });
        }

        private void Label(float x, float y, float w, float h, string text)
        {
            if (_label != null)
                _label.Invoke(null, new[] { Rect(x, y, w, h), text });
        }

        private void Spawn(string key)
        {
            try
            {
                EnsurePresets();

                var csc = AppDomain.CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => string.Equals(a.GetName().Name, "CustomSlimeCreator", StringComparison.OrdinalIgnoreCase));

                if (csc == null)
                {
                    _status = "CustomSlimeCreator.dll nao encontrado.";
                    return;
                }

                var storeType = csc.GetType("CustomSlimeCreator.Core.ConfigStore");
                var engineType = csc.GetType("CustomSlimeCreator.Core.SlimeEngine");
                if (storeType == null || engineType == null)
                {
                    _status = "Versao do Custom Slime Maker incompativel.";
                    return;
                }

                var loadAll = storeType.GetMethod("LoadAll", BindingFlags.Public | BindingFlags.Static);
                var spawn = engineType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (loadAll == null || spawn == null)
                {
                    _status = "API do Custom Slime Maker nao encontrada.";
                    return;
                }

                object selected = null;
                var configs = loadAll.Invoke(null, null) as IEnumerable;
                if (configs != null)
                {
                    foreach (var cfg in configs)
                    {
                        if (cfg == null) continue;
                        var f = cfg.GetType().GetField("Name", BindingFlags.Public | BindingFlags.Instance);
                        var p = cfg.GetType().GetProperty("Name", BindingFlags.Public | BindingFlags.Instance);
                        var name = f != null ? f.GetValue(cfg) as string : p?.GetValue(cfg) as string;
                        if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                        {
                            selected = cfg;
                            break;
                        }
                    }
                }

                if (selected == null)
                {
                    _status = "Preset " + key + " nao foi carregado.";
                    return;
                }

                var args = new object[] { selected, null };
                var ok = (bool)spawn.Invoke(null, args);
                var error = args[1] as string;

                _status = ok ? key.Replace("Slime", "") + " spawnado!" : "Falhou: " + (error ?? "erro desconhecido");
            }
            catch (TargetInvocationException tie)
            {
                _status = "Falhou: " + (tie.InnerException?.Message ?? tie.Message);
            }
            catch (Exception ex)
            {
                _status = "Falhou: " + ex.Message;
            }
        }

        private static void EnsurePresets()
        {
            var gameRoot = AppDomain.CurrentDomain.BaseDirectory;
            var dir = Path.Combine(gameRoot, "UserData", "CustomSlimeCreator");
            Directory.CreateDirectory(dir);

            WriteIfMissing(Path.Combine(dir, "01_RadSlime.json"), RadJson);
            WriteIfMissing(Path.Combine(dir, "02_QuantumSlime.json"), QuantumJson);
            WriteIfMissing(Path.Combine(dir, "03_MosaicSlime.json"), MosaicJson);
        }

        private static void WriteIfMissing(string path, string content)
        {
            if (!File.Exists(path))
                File.WriteAllText(path, content);
        }

        private const string RadJson = @"{
  ""Name"": ""RadSlime"",
  ""DisplayName"": ""Rad Slime"",
  ""BasePreset"": ""Pink"",
  ""Top"": { ""r"": 103, ""g"": 224, ""b"": 111 },
  ""Middle"": { ""r"": 70, ""g"": 164, ""b"": 74 },
  ""Bottom"": { ""r"": 58, ""g"": 137, ""b"": 63 },
  ""Vac"": { ""r"": 114, ""g"": 235, ""b"": 121 },
  ""HasPlort"": true,
  ""PlortValue"": 45,
  ""PlortTop"": { ""r"": 111, ""g"": 231, ""b"": 117 },
  ""PlortMiddle"": { ""r"": 65, ""g"": 167, ""b"": 70 },
  ""PlortBottom"": { ""r"": 47, ""g"": 127, ""b"": 53 },
  ""FoodGroups"": [ ""VeggieGroup"" ],
  ""FavoriteFoods"": [ ""OcaOca"" ],
  ""CanLargofy"": true,
  ""CreateAllLargos"": false,
  ""EdibleByTarrs"": true,
  ""Vaccable"": true,
  ""SinkInShallowWater"": true,
  ""SupportRadiant"": true,
  ""SpawnZones"": [],
  ""TwinEffect"": false,
  ""SloomberEffect"": false,
  ""RadAuraEffect"": true,
  ""CrystalShardsEffect"": false,
  ""RockPlatingEffect"": false,
  ""AnglerLureEffect"": false,
  ""HunterPatternEffect"": false,
  ""RingtailPatternEffect"": false,
  ""Parts"": [],
  ""IconOffX"": 0.0,
  ""IconOffY"": 0.0,
  ""IconZoom"": 0.55
}";

        private const string QuantumJson = @"{
  ""Name"": ""QuantumSlime"",
  ""DisplayName"": ""Quantum Slime"",
  ""BasePreset"": ""Pink"",
  ""Top"": { ""r"": 255, ""g"": 194, ""b"": 37 },
  ""Middle"": { ""r"": 247, ""g"": 165, ""b"": 16 },
  ""Bottom"": { ""r"": 234, ""g"": 145, ""b"": 13 },
  ""Vac"": { ""r"": 255, ""g"": 214, ""b"": 68 },
  ""HasPlort"": true,
  ""PlortValue"": 60,
  ""PlortTop"": { ""r"": 255, ""g"": 201, ""b"": 46 },
  ""PlortMiddle"": { ""r"": 242, ""g"": 153, ""b"": 18 },
  ""PlortBottom"": { ""r"": 218, ""g"": 125, ""b"": 8 },
  ""FoodGroups"": [ ""FruitGroup"" ],
  ""FavoriteFoods"": [ ""PhaseLemon"" ],
  ""CanLargofy"": true,
  ""CreateAllLargos"": false,
  ""EdibleByTarrs"": true,
  ""Vaccable"": true,
  ""SinkInShallowWater"": true,
  ""SupportRadiant"": true,
  ""SpawnZones"": [],
  ""TwinEffect"": false,
  ""SloomberEffect"": false,
  ""RadAuraEffect"": false,
  ""CrystalShardsEffect"": false,
  ""RockPlatingEffect"": false,
  ""AnglerLureEffect"": false,
  ""HunterPatternEffect"": false,
  ""RingtailPatternEffect"": false,
  ""Parts"": [],
  ""IconOffX"": 0.0,
  ""IconOffY"": 0.0,
  ""IconZoom"": 0.55
}";

        private const string MosaicJson = @"{
  ""Name"": ""MosaicSlime"",
  ""DisplayName"": ""Mosaic Slime"",
  ""BasePreset"": ""Pink"",
  ""Top"": { ""r"": 231, ""g"": 236, ""b"": 247 },
  ""Middle"": { ""r"": 204, ""g"": 213, ""b"": 233 },
  ""Bottom"": { ""r"": 176, ""g"": 189, ""b"": 217 },
  ""Vac"": { ""r"": 219, ""g"": 228, ""b"": 248 },
  ""HasPlort"": true,
  ""PlortValue"": 75,
  ""PlortTop"": { ""r"": 83, ""g"": 211, ""b"": 255 },
  ""PlortMiddle"": { ""r"": 103, ""g"": 235, ""b"": 95 },
  ""PlortBottom"": { ""r"": 255, ""g"": 144, ""b"": 45 },
  ""FoodGroups"": [ ""VeggieGroup"" ],
  ""FavoriteFoods"": [ ""SilverParsnip"" ],
  ""CanLargofy"": true,
  ""CreateAllLargos"": false,
  ""EdibleByTarrs"": true,
  ""Vaccable"": true,
  ""SinkInShallowWater"": true,
  ""SupportRadiant"": true,
  ""SpawnZones"": [],
  ""TwinEffect"": false,
  ""SloomberEffect"": false,
  ""RadAuraEffect"": false,
  ""CrystalShardsEffect"": false,
  ""RockPlatingEffect"": false,
  ""AnglerLureEffect"": false,
  ""HunterPatternEffect"": false,
  ""RingtailPatternEffect"": false,
  ""Parts"": [],
  ""IconOffX"": 0.0,
  ""IconOffY"": 0.0,
  ""IconZoom"": 0.55
}";
    }
}
