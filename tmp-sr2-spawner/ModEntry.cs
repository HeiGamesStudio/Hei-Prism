using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1SlimeSpawner.ModEntry), "SR1 Slime Spawner", "0.2.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1SlimeSpawner
{
    public sealed class ModEntry : MelonMod
    {
        private int _pediaTick;
        private bool _pediaDone;

        public override void OnInitializeMelon()
        {
            try
            {
                EnsurePresets();
                LoggerInstance.Msg("SR1 Slime Spawner v0.2 carregado.");
                LoggerInstance.Msg("8 = Rad | 9 = Quantum | 0 = Mosaic");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("Falha ao preparar presets: " + ex);
            }
        }

        public override void OnUpdate()
        {
            try
            {
                if (KeyPressed("digit8Key", "numpad8Key")) Spawn("RadSlime");
                if (KeyPressed("digit9Key", "numpad9Key")) Spawn("QuantumSlime");
                if (KeyPressed("digit0Key", "numpad0Key")) Spawn("MosaicSlime");

                if (!_pediaDone && ++_pediaTick >= 90)
                {
                    _pediaTick = 0;
                    _pediaDone = TryInstallPedia();
                }
            }
            catch { }
        }

        private bool KeyPressed(string primary, string secondary)
        {
            try
            {
                var keyboardType = FindType("UnityEngine.InputSystem.Keyboard");
                if (keyboardType != null)
                {
                    var current = GetMember(keyboardType, null, "current");
                    if (current != null && (ControlPressed(current, primary) || ControlPressed(current, secondary)))
                        return true;
                }
            }
            catch { }

            return false;
        }

        private static bool ControlPressed(object keyboard, string property)
        {
            try
            {
                var control = GetMember(keyboard.GetType(), keyboard, property);
                if (control == null) return false;
                var v = GetMember(control.GetType(), control, "wasPressedThisFrame");
                return v is bool b && b;
            }
            catch { return false; }
        }

        private void Spawn(string key)
        {
            try
            {
                EnsurePresets();
                var csc = FindAssembly("CustomSlimeCreator");
                if (csc == null)
                {
                    LoggerInstance.Warning("CustomSlimeCreator.dll nao encontrado.");
                    return;
                }

                var storeType = csc.GetType("CustomSlimeCreator.Core.ConfigStore");
                var engineType = csc.GetType("CustomSlimeCreator.Core.SlimeEngine");
                var loadAll = storeType?.GetMethod("LoadAll", BindingFlags.Public | BindingFlags.Static);
                var spawn = engineType?.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
                if (loadAll == null || spawn == null)
                {
                    LoggerInstance.Warning("API do Custom Slime Maker nao encontrada.");
                    return;
                }

                var selected = FindConfig(loadAll, key);
                if (selected == null)
                {
                    LoggerInstance.Warning("Preset " + key + " nao encontrado.");
                    return;
                }

                var args = new object[] { selected, null };
                var ok = (bool)spawn.Invoke(null, args);
                var error = args[1] as string;
                if (ok)
                {
                    LoggerInstance.Msg(key.Replace("Slime", "") + " spawnado pela tecla.");
                    if (!_pediaDone) _pediaDone = TryInstallPedia();
                }
                else
                    LoggerInstance.Warning("Falha ao spawnar " + key + ": " + (error ?? "erro desconhecido"));
            }
            catch (TargetInvocationException tie)
            {
                LoggerInstance.Warning("Falha no spawn: " + (tie.InnerException?.Message ?? tie.Message));
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Falha no spawn: " + ex.Message);
            }
        }

        private bool TryInstallPedia()
        {
            try
            {
                var csc = FindAssembly("CustomSlimeCreator");
                if (csc == null) return false;

                var storeType = csc.GetType("CustomSlimeCreator.Core.ConfigStore");
                var engineType = csc.GetType("CustomSlimeCreator.Core.SlimeEngine");
                var gameAccessType = csc.GetType("CustomSlimeCreator.Core.GameAccess");
                if (storeType == null || engineType == null || gameAccessType == null) return false;

                var loadAll = storeType.GetMethod("LoadAll", BindingFlags.Public | BindingFlags.Static);
                var build = engineType.GetMethod("BuildOrUpdate", BindingFlags.Public | BindingFlags.Static);
                var findBuilt = engineType.GetMethod("FindBuilt", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (loadAll == null || build == null || findBuilt == null) return false;

                bool all = true;
                all &= EnsurePediaFor("RadSlime", RadPedia, loadAll, build, findBuilt, gameAccessType);
                all &= EnsurePediaFor("QuantumSlime", QuantumPedia, loadAll, build, findBuilt, gameAccessType);
                all &= EnsurePediaFor("MosaicSlime", MosaicPedia, loadAll, build, findBuilt, gameAccessType);

                if (all)
                    LoggerInstance.Msg("Rad, Quantum e Mosaic adicionados a Slimepedia.");
                return all;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Slimepedia ainda nao esta pronta: " + ex.Message);
                return false;
            }
        }

        private bool EnsurePediaFor(
            string key,
            string description,
            MethodInfo loadAll,
            MethodInfo build,
            MethodInfo findBuilt,
            Type gameAccessType)
        {
            var cfg = FindConfig(loadAll, key);
            if (cfg == null) return false;

            var buildArgs = new object[] { cfg, null };
            var builtOk = (bool)build.Invoke(null, buildArgs);
            if (!builtOk) return false;

            var built = findBuilt.Invoke(null, new object[] { key });
            if (built == null) return false;
            var def = GetMember(built.GetType(), built, "Def");
            if (def == null) return false;

            if (!HasPediaEntryFor(def))
            {
                var entry = CreatePediaEntry(def, description, gameAccessType);
                if (entry == null) return false;
                if (!RegisterPediaEntry(entry, gameAccessType)) return false;
            }

            UnlockPedia(def);
            return true;
        }

        private static object FindConfig(MethodInfo loadAll, string key)
        {
            var configs = loadAll.Invoke(null, null) as IEnumerable;
            if (configs == null) return null;
            foreach (var cfg in configs)
            {
                if (cfg == null) continue;
                var name = GetMember(cfg.GetType(), cfg, "Name") as string;
                if (string.Equals(name, key, StringComparison.OrdinalIgnoreCase))
                    return cfg;
            }
            return null;
        }

        private static bool HasPediaEntryFor(object def)
        {
            try
            {
                var entryType = FindType("Il2CppMonomiPark.SlimeRancher.Pedia.IdentifiablePediaEntry");
                if (entryType == null) return false;
                foreach (var o in FindAllResources(entryType))
                {
                    var id = GetMember(o.GetType(), o, "_identifiableType");
                    if (ReferenceEquals(id, def) || (id != null && id.Equals(def)))
                        return true;
                }
            }
            catch { }
            return false;
        }

        private object CreatePediaEntry(object def, string description, Type gameAccessType)
        {
            try
            {
                var entryType = FindType("Il2CppMonomiPark.SlimeRancher.Pedia.IdentifiablePediaEntry");
                if (entryType == null) return null;

                object pink = null;
                foreach (var o in FindAllResources(entryType))
                {
                    var n = GetMember(o.GetType(), o, "name") as string;
                    if (string.Equals(n, "Pink", StringComparison.OrdinalIgnoreCase))
                    {
                        pink = o;
                        break;
                    }
                    if (pink == null) pink = o;
                }
                if (pink == null) return null;

                var clone = CloneUnityObject(pink);
                if (clone == null) return null;

                var defName = GetMember(def.GetType(), def, "name") as string ?? "CustomSlime";
                SetMember(clone.GetType(), clone, "name", defName + "Pedia");
                SetMember(clone.GetType(), clone, "_identifiableType", def);

                var title = GetMember(def.GetType(), def, "localizedName");
                if (title != null) SetMember(clone.GetType(), clone, "_title", title);

                var makeName = gameAccessType.GetMethod("MakeName", BindingFlags.Public | BindingFlags.Static);
                var localizedDesc = makeName?.Invoke(null, new object[] { description });
                if (localizedDesc != null)
                    SetMember(clone.GetType(), clone, "_description", localizedDesc);

                SetMember(clone.GetType(), clone, "_isUnlockedInitially", true);
                ClearCollectionField(clone, "_details");

                return clone;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Falha criando entrada da Slimepedia: " + ex.Message);
                return null;
            }
        }

        private bool RegisterPediaEntry(object entry, Type gameAccessType)
        {
            try
            {
                var catType = FindType("Il2CppMonomiPark.SlimeRancher.Pedia.PediaCategory");
                if (catType == null) return false;

                object slimes = null;
                foreach (var o in FindAllResources(catType))
                {
                    var n = GetMember(o.GetType(), o, "name") as string;
                    if (string.Equals(n, "Slimes", StringComparison.OrdinalIgnoreCase))
                    {
                        slimes = o;
                        break;
                    }
                }
                if (slimes == null) return false;

                AppendCollectionField(slimes, "_items", entry);

                var gc = GetMember(gameAccessType, null, "GC");
                var lookup = gc == null ? null : GetMember(gc.GetType(), gc, "LookupDirector");
                if (lookup != null)
                {
                    var add = lookup.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .FirstOrDefault(m => m.Name == "AddPediaEntryToCategory" && m.GetParameters().Length == 2);
                    add?.Invoke(lookup, new[] { entry, slimes });
                }

                var runtime = slimes.GetType().GetMethod("GetRuntimeCategory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                runtime?.Invoke(slimes, null);
                return true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Falha registrando Slimepedia: " + ex.Message);
                return false;
            }
        }

        private static void UnlockPedia(object def)
        {
            try
            {
                var sceneType = FindType("Il2CppMonomiPark.SlimeRancher.SceneContext") ?? FindType("Il2Cpp.SceneContext");
                if (sceneType == null) return;
                var scene = GetMember(sceneType, null, "Instance");
                if (scene == null) return;
                var director = GetMember(scene.GetType(), scene, "PediaDirector");
                if (director == null) return;

                var unlock = director.GetType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != "Unlock") return false;
                        var p = m.GetParameters();
                        return p.Length == 2 && p[1].ParameterType == typeof(bool) &&
                               p[0].ParameterType.Name.Contains("IdentifiableType");
                    });
                unlock?.Invoke(director, new object[] { def, false });
            }
            catch { }
        }

        private static IEnumerable<object> FindAllResources(Type t)
        {
            var resources = FindType("UnityEngine.Resources");
            if (resources == null) yield break;
            var find = resources.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m => m.Name == "FindObjectsOfTypeAll" &&
                                     m.GetParameters().Length == 1 &&
                                     m.GetParameters()[0].ParameterType == typeof(Type));
            if (find == null) yield break;
            var arr = find.Invoke(null, new object[] { t }) as IEnumerable;
            if (arr == null) yield break;
            foreach (var x in arr)
                if (x != null) yield return x;
        }

        private static object CloneUnityObject(object source)
        {
            var uo = FindType("UnityEngine.Object");
            if (uo == null || source == null) return null;
            var inst = uo.GetMethods(BindingFlags.Public | BindingFlags.Static)
                .FirstOrDefault(m =>
                {
                    if (m.Name != "Instantiate" || m.IsGenericMethod) return false;
                    var p = m.GetParameters();
                    return p.Length == 1 && p[0].ParameterType.FullName == "UnityEngine.Object";
                });
            return inst?.Invoke(null, new[] { source });
        }

        private static void ClearCollectionField(object target, string fieldName)
        {
            try
            {
                var f = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return;
                var empty = CreateCollection(f.FieldType, new List<object>());
                if (empty != null) f.SetValue(target, empty);
            }
            catch { }
        }

        private static void AppendCollectionField(object target, string fieldName, object value)
        {
            try
            {
                var f = target.GetType().GetField(fieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f == null) return;

                var vals = new List<object>();
                var current = f.GetValue(target) as IEnumerable;
                if (current != null)
                    foreach (var x in current) if (x != null) vals.Add(x);

                if (vals.Any(x => ReferenceEquals(x, value) || x.Equals(value))) return;
                vals.Add(value);

                var replacement = CreateCollection(f.FieldType, vals);
                if (replacement != null) f.SetValue(target, replacement);
            }
            catch { }
        }

        private static object CreateCollection(Type collectionType, List<object> values)
        {
            try
            {
                if (collectionType.IsArray)
                {
                    var et = collectionType.GetElementType();
                    var a = Array.CreateInstance(et, values.Count);
                    for (int i = 0; i < values.Count; i++) a.SetValue(values[i], i);
                    return a;
                }

                object arr = null;
                foreach (var len in new object[] { values.Count, (long)values.Count })
                {
                    try
                    {
                        arr = Activator.CreateInstance(collectionType, new[] { len });
                        if (arr != null) break;
                    }
                    catch { }
                }
                if (arr == null) return null;

                var item = collectionType.GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (item != null && item.CanWrite)
                {
                    var idxType = item.GetIndexParameters().FirstOrDefault()?.ParameterType ?? typeof(int);
                    for (int i = 0; i < values.Count; i++)
                    {
                        object idx = idxType == typeof(long) ? (object)(long)i : i;
                        item.SetValue(arr, values[i], new[] { idx });
                    }
                    return arr;
                }

                var set = collectionType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .FirstOrDefault(m => (m.Name == "set_Item" || m.Name == "Set") && m.GetParameters().Length == 2);
                if (set != null)
                {
                    var idxType = set.GetParameters()[0].ParameterType;
                    for (int i = 0; i < values.Count; i++)
                    {
                        object idx = idxType == typeof(long) ? (object)(long)i : i;
                        set.Invoke(arr, new[] { idx, values[i] });
                    }
                }
                return arr;
            }
            catch { return null; }
        }

        private static Assembly FindAssembly(string name)
            => AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, name, StringComparison.OrdinalIgnoreCase));

        private static Type FindType(string fullName)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = a.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }

        private static object GetMember(Type type, object instance, string name)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            try
            {
                var p = type.GetProperty(name, all);
                if (p != null) return p.GetValue(instance);
                var f = type.GetField(name, all);
                if (f != null) return f.GetValue(instance);
            }
            catch { }
            return null;
        }

        private static bool SetMember(Type type, object instance, string name, object value)
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            try
            {
                var p = type.GetProperty(name, all);
                if (p != null && p.CanWrite) { p.SetValue(instance, value); return true; }
                var f = type.GetField(name, all);
                if (f != null) { f.SetValue(instance, value); return true; }
            }
            catch { }
            return false;
        }

        private static void EnsurePresets()
        {
            var gameRoot = AppDomain.CurrentDomain.BaseDirectory;
            var dir = Path.Combine(gameRoot, "UserData", "CustomSlimeCreator");
            Directory.CreateDirectory(dir);

            WritePreset(Path.Combine(dir, "01_RadSlime.json"), RadJson);
            WritePreset(Path.Combine(dir, "02_QuantumSlime.json"), QuantumJson);
            WritePreset(Path.Combine(dir, "03_MosaicSlime.json"), MosaicJson);
        }

        private static void WritePreset(string path, string content)
        {
            File.WriteAllText(path, content);
        }

        private const string RadPedia =
            "SLIMEOLOGIA: Nao se sabe se os Slimes Rad surgiram por uma fonte externa de radiacao ou por algo natural da Grande, Grande Extensao. Sua aura radioativa e tao intensa que eles brilham no escuro.\n\n" +
            "RISCOS PARA O RANCHEIRO: Permanecer dentro da aura por muito tempo aumenta a exposicao a radiacao e pode causar danos serios. Mantenha distancia quando a exposicao subir.\n\n" +
            "PLORTONOMIA: Plorts Rad funcionam como pequenas baterias de altissima energia e sao muito valorizados em tecnologias que exigem grande potencia.";

        private const string QuantumPedia =
            "SLIMEOLOGIA: Os Slimes Quantum parecem ligados a um antigo evento ocorrido nas Ruinas Antigas. Eles projetam fantasmas de realidades possiveis e podem se alinhar a uma dessas projecoes, efetivamente se teleportando.\n\n" +
            "RISCOS PARA O RANCHEIRO: Quando ficam agitados, podem escapar ao trocar de lugar com um fantasma. Alimenta-los bem e usar agua pode ajudar a manter a situacao sob controle.\n\n" +
            "PLORTONOMIA: Os Plorts Quantum sao estudados pela possibilidade de manipular realidades alternativas e, em teoria, duplicar recursos.";

        private const string MosaicPedia =
            "SLIMEOLOGIA: O Slime Mosaic recebe esse nome pelas placas de vidro brilhantes que cobrem seu corpo. Elas criam um efeito deslumbrante, mas tambem podem produzir perigosas anomalias de luz.\n\n" +
            "RISCOS PARA O RANCHEIRO: Sua aparencia brilhante pode atrair outros slimes, e seus glints podem cair no chao e explodir em chamas. Agua e a melhor resposta para apagar esses focos.\n\n" +
            "PLORTONOMIA: Plorts Mosaic sao valiosos para pesquisas sobre as estruturas de vidro da Grande, Grande Extensao, que preservam propriedades incomuns fora do corpo do slime.";

        private const string RadJson = @"{
  ""Name"": ""RadSlime"", ""DisplayName"": ""Rad Slime"", ""BasePreset"": ""Pink"",
  ""Top"": { ""r"": 103, ""g"": 224, ""b"": 111 }, ""Middle"": { ""r"": 70, ""g"": 164, ""b"": 74 }, ""Bottom"": { ""r"": 58, ""g"": 137, ""b"": 63 }, ""Vac"": { ""r"": 114, ""g"": 235, ""b"": 121 },
  ""HasPlort"": true, ""PlortValue"": 45,
  ""PlortTop"": { ""r"": 111, ""g"": 231, ""b"": 117 }, ""PlortMiddle"": { ""r"": 65, ""g"": 167, ""b"": 70 }, ""PlortBottom"": { ""r"": 47, ""g"": 127, ""b"": 53 },
  ""FoodGroups"": [ ""VeggieGroup"" ], ""FavoriteFoods"": [ ""OcaOca"" ],
  ""CanLargofy"": true, ""CreateAllLargos"": false, ""EdibleByTarrs"": true, ""Vaccable"": true, ""SinkInShallowWater"": true, ""SupportRadiant"": true,
  ""SpawnZones"": [], ""TwinEffect"": false, ""SloomberEffect"": false, ""RadAuraEffect"": true,
  ""CrystalShardsEffect"": false, ""RockPlatingEffect"": false, ""AnglerLureEffect"": false, ""HunterPatternEffect"": false, ""RingtailPatternEffect"": false,
  ""Parts"": [], ""IconOffX"": 0.0, ""IconOffY"": 0.0, ""IconZoom"": 0.55
}";

        private const string QuantumJson = @"{
  ""Name"": ""QuantumSlime"", ""DisplayName"": ""Quantum Slime"", ""BasePreset"": ""Pink"",
  ""Top"": { ""r"": 255, ""g"": 194, ""b"": 37 }, ""Middle"": { ""r"": 247, ""g"": 165, ""b"": 16 }, ""Bottom"": { ""r"": 234, ""g"": 145, ""b"": 13 }, ""Vac"": { ""r"": 255, ""g"": 214, ""b"": 68 },
  ""HasPlort"": true, ""PlortValue"": 60,
  ""PlortTop"": { ""r"": 255, ""g"": 201, ""b"": 46 }, ""PlortMiddle"": { ""r"": 242, ""g"": 153, ""b"": 18 }, ""PlortBottom"": { ""r"": 218, ""g"": 125, ""b"": 8 },
  ""FoodGroups"": [ ""FruitGroup"" ], ""FavoriteFoods"": [ ""PhaseLemon"" ],
  ""CanLargofy"": true, ""CreateAllLargos"": false, ""EdibleByTarrs"": true, ""Vaccable"": true, ""SinkInShallowWater"": true, ""SupportRadiant"": true,
  ""SpawnZones"": [], ""TwinEffect"": false, ""SloomberEffect"": false, ""RadAuraEffect"": false,
  ""CrystalShardsEffect"": false, ""RockPlatingEffect"": false, ""AnglerLureEffect"": false, ""HunterPatternEffect"": false, ""RingtailPatternEffect"": false,
  ""Parts"": [], ""IconOffX"": 0.0, ""IconOffY"": 0.0, ""IconZoom"": 0.55
}";

        private const string MosaicJson = @"{
  ""Name"": ""MosaicSlime"", ""DisplayName"": ""Mosaic Slime"", ""BasePreset"": ""Pink"",
  ""Top"": { ""r"": 231, ""g"": 236, ""b"": 247 }, ""Middle"": { ""r"": 204, ""g"": 213, ""b"": 233 }, ""Bottom"": { ""r"": 176, ""g"": 189, ""b"": 217 }, ""Vac"": { ""r"": 219, ""g"": 228, ""b"": 248 },
  ""HasPlort"": true, ""PlortValue"": 75,
  ""PlortTop"": { ""r"": 83, ""g"": 211, ""b"": 255 }, ""PlortMiddle"": { ""r"": 103, ""g"": 235, ""b"": 95 }, ""PlortBottom"": { ""r"": 255, ""g"": 144, ""b"": 45 },
  ""FoodGroups"": [ ""VeggieGroup"" ], ""FavoriteFoods"": [ ""SilverParsnip"" ],
  ""CanLargofy"": true, ""CreateAllLargos"": false, ""EdibleByTarrs"": true, ""Vaccable"": true, ""SinkInShallowWater"": true, ""SupportRadiant"": true,
  ""SpawnZones"": [], ""TwinEffect"": false, ""SloomberEffect"": false, ""RadAuraEffect"": false,
  ""CrystalShardsEffect"": false, ""RockPlatingEffect"": false, ""AnglerLureEffect"": false, ""HunterPatternEffect"": false, ""RingtailPatternEffect"": false,
  ""Parts"": [], ""IconOffX"": 0.0, ""IconOffY"": 0.0, ""IconZoom"": 0.55
}";
    }
}
