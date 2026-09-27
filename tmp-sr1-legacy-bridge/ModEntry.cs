using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1LegacyBridge.ModEntry), "SR1 Legacy Slimes Bridge", "1.0.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1LegacyBridge
{
    public sealed class ModEntry : MelonMod
    {
        private Type _engineType;
        private Type _configType;
        private Type _colType;
        private Type _storeType;
        private Type _gameAccessType;
        private bool _built;
        private int _frames;
        private DateTime _nextTry = DateTime.MinValue;
        private readonly Dictionary<string, object> _configs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("SR1 Legacy Slimes Bridge v1.0.0 carregado.");
            LoggerInstance.Msg("Usa o Custom Slime Maker como motor de registro/save/Vacpack e cria Rad, Quantum e Mosaic a partir do Pink.");
        }

        public override void OnUpdate()
        {
            _frames++;

            if (!_built && DateTime.UtcNow >= _nextTry)
            {
                _nextTry = DateTime.UtcNow.AddSeconds(1);
                TryBuildAll();
            }

            if (_built)
            {
                if (KeyPressed("digit8Key","numpad8Key","Alpha8","Keypad8")) Spawn("Rad");
                if (KeyPressed("digit9Key","numpad9Key","Alpha9","Keypad9")) Spawn("Quantum");
                if (KeyPressed("digit0Key","numpad0Key","Alpha0","Keypad0")) Spawn("Mosaic");
            }
        }

        private void TryBuildAll()
        {
            try
            {
                if (!ResolveCSM())
                    return;

                if (!ForceCSMReady())
                    return;

                var rad = CreateConfig(
                    "Rad", "Rad Slime",
                    88, 255, 102,
                    25, 177, 48,
                    7, 103, 29,
                    94, 255, 112
                );
                SetField(rad, "HasPlort", false);
                SetField(rad, "CanLargofy", false);
                SetField(rad, "CreateAllLargos", false);
                SetField(rad, "FoodGroups", new List<string>{"VeggieGroup"});
                SetField(rad, "RadAuraEffect", true);
                SetField(rad, "SupportRadiant", true);

                var quantum = CreateConfig(
                    "Quantum", "Quantum Slime",
                    255, 246, 116,
                    246, 204, 58,
                    219, 143, 21,
                    255, 229, 89
                );
                SetField(quantum, "HasPlort", false);
                SetField(quantum, "CanLargofy", false);
                SetField(quantum, "CreateAllLargos", false);
                SetField(quantum, "FoodGroups", new List<string>{"FruitGroup"});
                SetField(quantum, "TwinEffect", true);
                SetField(quantum, "SupportRadiant", true);

                var mosaic = CreateConfig(
                    "Mosaic", "Mosaic Slime",
                    160, 233, 255,
                    82, 164, 239,
                    130, 72, 218,
                    128, 214, 255
                );
                SetField(mosaic, "HasPlort", false);
                SetField(mosaic, "CanLargofy", false);
                SetField(mosaic, "CreateAllLargos", false);
                SetField(mosaic, "FoodGroups", new List<string>{"VeggieGroup"});
                SetField(mosaic, "TwinEffect", true);
                SetField(mosaic, "SupportRadiant", true);

                bool a = BuildAndSave(rad);
                bool b = BuildAndSave(quantum);
                bool c = BuildAndSave(mosaic);

                if (!(a && b && c))
                    return;

                _configs["Rad"] = rad;
                _configs["Quantum"] = quantum;
                _configs["Mosaic"] = mosaic;

                ApplyLegacyShader("Rad", new[]{
                    "SR/AMP/Slime/Body/Rad",
                    "SlimeBody_rad",
                    "SR/AMP/Slime/Body/Rad/Normal"
                });

                ApplyLegacyShader("Quantum", new[]{
                    "SR/AMP/Slime/Body/Quantum",
                    "SlimeBody_quantum",
                    "SR/AMP/Slime/Body/QuantumTimeOverride"
                });

                ApplyLegacyShader("Mosaic", new[]{
                    "SR/AMP/Slime/Mosaic",
                    "MosaicSlime",
                    "SR/Slime/Mosaic"
                });

                _built = true;
                LoggerInstance.Msg("PRONTO: Rad, Quantum e Mosaic foram criados como 3 clones independentes do Pink.");
                LoggerInstance.Msg("Spawn manual: 8 = Rad | 9 = Quantum | 0 = Mosaic");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Build SR1 slimes: " + ex);
            }
        }

        private bool ResolveCSM()
        {
            if (_engineType != null) return true;

            _engineType = FindType("CustomSlimeCreator.Core.SlimeEngine");
            _configType = FindType("CustomSlimeCreator.Core.SlimeConfig");
            _colType = FindType("CustomSlimeCreator.Core.Col");
            _storeType = FindType("CustomSlimeCreator.Core.ConfigStore");
            _gameAccessType = FindType("CustomSlimeCreator.Core.GameAccess");

            if (_engineType == null || _configType == null || _colType == null || _storeType == null || _gameAccessType == null)
            {
                if (_frames % 300 == 0)
                    LoggerInstance.Warning("CustomSlimeCreator.dll não encontrado. Instale o Custom Slime Maker v1.0.2 em Mods.");
                return false;
            }

            return true;
        }

        // CSM 1.0.2 waits for >= 40 SlimeDefinition resources.
        // Some current Xbox/1.3.0 scenes expose fewer despite the save already being active.
        // We preserve every REAL definition found, then pad the private cache with Pink references
        // only to satisfy that old readiness threshold. Registration itself is still done by CSM.
        private bool ForceCSMReady()
        {
            try
            {
                var gcProp = _gameAccessType.GetProperty("GC", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                var gc = gcProp?.GetValue(null);
                if (gc == null) return false;

                var slimeDefType = FindTypeBySimpleName("SlimeDefinition");
                var resourcesType = FindType("UnityEngine.Resources");
                if (slimeDefType == null || resourcesType == null) return false;

                object raw = null;
                foreach (var m in resourcesType.GetMethods(BindingFlags.Public|BindingFlags.Static))
                {
                    if (m.Name != "FindObjectsOfTypeAll" || !m.IsGenericMethodDefinition || m.GetParameters().Length != 0)
                        continue;
                    try { raw = m.MakeGenericMethod(slimeDefType).Invoke(null, null); break; } catch { }
                }

                if (!(raw is IEnumerable en)) return false;

                var real = new List<object>();
                object pink = null;

                foreach (var d in en)
                {
                    if (d == null) continue;
                    real.Add(d);
                    var rid = GetString(d, "ReferenceId", "referenceId");
                    var name = GetString(d, "name", "Name");
                    if (pink == null &&
                        (string.Equals(rid, "SlimeDefinition.Pink", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "Pink", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(name, "PinkSlime", StringComparison.OrdinalIgnoreCase)))
                        pink = d;
                }

                if (pink == null || real.Count < 3) return false;

                var listType = typeof(List<>).MakeGenericType(slimeDefType);
                var list = Activator.CreateInstance(listType);
                var add = listType.GetMethod("Add");

                foreach (var d in real)
                    add.Invoke(list, new[]{d});

                while ((int)listType.GetProperty("Count").GetValue(list) < 40)
                    add.Invoke(list, new[]{pink});

                var allDefs = _engineType.GetField("_allDefs", BindingFlags.NonPublic|BindingFlags.Static);
                allDefs?.SetValue(null, list);

                var readyProp = _engineType.GetProperty("Ready", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                if (readyProp != null)
                {
                    var setter = readyProp.GetSetMethod(true);
                    if (setter != null) setter.Invoke(null, new object[]{true});
                    else
                    {
                        var bf = _engineType.GetField("<Ready>k__BackingField", BindingFlags.NonPublic|BindingFlags.Static);
                        bf?.SetValue(null, true);
                    }
                }

                // Supply a SlimeAppearanceDirector if CSM has not cached one yet.
                var directorField = _engineType.GetField("_director", BindingFlags.NonPublic|BindingFlags.Static);
                if (directorField != null && directorField.GetValue(null) == null)
                {
                    var dirType = FindTypeBySimpleName("SlimeAppearanceDirector");
                    if (dirType != null)
                    {
                        object chosen = null;
                        object arr = null;
                        foreach (var m in resourcesType.GetMethods(BindingFlags.Public|BindingFlags.Static))
                        {
                            if (m.Name != "FindObjectsOfTypeAll" || !m.IsGenericMethodDefinition || m.GetParameters().Length != 0) continue;
                            try { arr = m.MakeGenericMethod(dirType).Invoke(null, null); break; } catch { }
                        }
                        if (arr is IEnumerable dirs)
                        {
                            foreach (var d in dirs)
                            {
                                if (d == null) continue;
                                if (chosen == null) chosen = d;
                                if (string.Equals(GetString(d, "name"), "MainSlimeAppearanceDirector", StringComparison.OrdinalIgnoreCase))
                                { chosen = d; break; }
                            }
                        }
                        if (chosen != null) directorField.SetValue(null, chosen);
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("ForceCSMReady: " + ex.Message);
                return false;
            }
        }

        private object CreateConfig(
            string name, string display,
            byte tr, byte tg, byte tb,
            byte mr, byte mg, byte mb,
            byte br, byte bg, byte bb,
            byte vr, byte vg, byte vb)
        {
            var cfg = Activator.CreateInstance(_configType);

            SetField(cfg, "Name", name);
            SetField(cfg, "DisplayName", display);
            SetField(cfg, "BasePreset", "Pink");

            SetField(cfg, "Top", MakeCol(tr,tg,tb));
            SetField(cfg, "Middle", MakeCol(mr,mg,mb));
            SetField(cfg, "Bottom", MakeCol(br,bg,bb));
            SetField(cfg, "Vac", MakeCol(vr,vg,vb));

            SetField(cfg, "Vaccable", true);
            SetField(cfg, "EdibleByTarrs", true);
            SetField(cfg, "SinkInShallowWater", true);

            return cfg;
        }

        private object MakeCol(byte r, byte g, byte b)
        {
            foreach (var ctor in _colType.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            {
                var p = ctor.GetParameters();
                if (p.Length == 3 &&
                    p[0].ParameterType == typeof(byte) &&
                    p[1].ParameterType == typeof(byte) &&
                    p[2].ParameterType == typeof(byte))
                    return ctor.Invoke(new object[]{r,g,b});
            }

            var col = Activator.CreateInstance(_colType);
            SetField(col, "r", r);
            SetField(col, "g", g);
            SetField(col, "b", b);
            return col;
        }

        private bool BuildAndSave(object cfg)
        {
            try
            {
                var build = _engineType.GetMethod("BuildOrUpdate", BindingFlags.Public|BindingFlags.Static);
                if (build == null) return false;

                var args = new object[]{cfg, null};
                var ok = (bool)build.Invoke(null, args);
                var error = args[1]?.ToString();

                if (!ok)
                {
                    LoggerInstance.Warning("Custom Slime Maker recusou o slime: " + (error ?? "erro desconhecido"));
                    return false;
                }

                var save = _storeType.GetMethod("Save", BindingFlags.Public|BindingFlags.Static);
                save?.Invoke(null, new[]{cfg});
                return true;
            }
            catch (TargetInvocationException tie)
            {
                LoggerInstance.Warning("BuildAndSave: " + (tie.InnerException ?? tie));
                return false;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("BuildAndSave: " + ex);
                return false;
            }
        }

        private void Spawn(string key)
        {
            if (!_configs.TryGetValue(key, out var cfg))
                return;

            try
            {
                var spawn = _engineType.GetMethod("Spawn", BindingFlags.Public|BindingFlags.Static);
                if (spawn == null) return;

                var args = new object[]{cfg, null};
                var ok = (bool)spawn.Invoke(null, args);
                var error = args[1]?.ToString();

                if (!ok)
                    LoggerInstance.Warning("Spawn " + key + ": " + (error ?? "falhou"));
                else
                {
                    ApplyLegacyShader(key, ShaderCandidates(key));
                    LoggerInstance.Msg(key + " spawnado.");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Spawn " + key + ": " + ex.Message);
            }
        }

        private string[] ShaderCandidates(string key)
        {
            if (key.Equals("Rad", StringComparison.OrdinalIgnoreCase))
                return new[]{"SR/AMP/Slime/Body/Rad","SlimeBody_rad","SR/AMP/Slime/Body/Rad/Normal"};
            if (key.Equals("Quantum", StringComparison.OrdinalIgnoreCase))
                return new[]{"SR/AMP/Slime/Body/Quantum","SlimeBody_quantum","SR/AMP/Slime/Body/QuantumTimeOverride"};
            return new[]{"SR/AMP/Slime/Mosaic","MosaicSlime","SR/Slime/Mosaic"};
        }

        private void ApplyLegacyShader(string key, string[] candidates)
        {
            try
            {
                var builtField = _engineType.GetField("Built", BindingFlags.NonPublic|BindingFlags.Static);
                var built = builtField?.GetValue(null);
                if (built == null) return;

                object cs = DictGet(built, key);
                if (cs == null) return;

                var app = GetMember(cs, "App");
                if (app == null) return;

                var shaderType = FindType("UnityEngine.Shader");
                if (shaderType == null) return;

                object shader = null;
                var find = shaderType.GetMethods(BindingFlags.Public|BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Find" && m.GetParameters().Length == 1 &&
                                         m.GetParameters()[0].ParameterType == typeof(string));

                if (find != null)
                {
                    foreach (var n in candidates)
                    {
                        try
                        {
                            shader = find.Invoke(null, new object[]{n});
                            if (shader != null) break;
                        }
                        catch { }
                    }
                }

                if (shader == null)
                {
                    LoggerInstance.Msg(key + ": shader legado SR2 não localizado; mantendo shader do Pink com a paleta SR1.");
                    return;
                }

                var structures = GetMember(app, "Structures", "_structures");
                if (!(structures is IEnumerable en)) return;

                int changed = 0;
                foreach (var s in en)
                {
                    if (s == null) continue;

                    var element = GetMember(s, "Element");
                    var typeName = GetMember(element, "Type")?.ToString() ?? "";
                    if (!typeName.Equals("BODY", StringComparison.OrdinalIgnoreCase) &&
                        !typeName.Equals("SURFACE", StringComparison.OrdinalIgnoreCase))
                        continue;

                    var mats = GetMember(s, "DefaultMaterials");
                    if (!(mats is IEnumerable men)) continue;

                    foreach (var mat in men)
                    {
                        if (mat == null) continue;
                        if (SetMember(mat, "shader", shader))
                            changed++;
                        EnhanceMaterial(key, mat);
                    }
                }

                LoggerInstance.Msg(key + ": shader legado aplicado a " + changed + " material(is).");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Visual " + key + ": " + ex.Message);
            }
        }

        private void EnhanceMaterial(string key, object mat)
        {
            if (mat == null) return;

            if (key.Equals("Rad", StringComparison.OrdinalIgnoreCase))
            {
                EnableKeyword(mat, "_EMISSION");
                SetMaterialFloat(mat, "_EmissionStrength", 1.4f);
            }
            else if (key.Equals("Quantum", StringComparison.OrdinalIgnoreCase))
            {
                EnableKeyword(mat, "_EMISSION");
                SetMaterialFloat(mat, "_EmissionStrength", 0.65f);
            }
            else if (key.Equals("Mosaic", StringComparison.OrdinalIgnoreCase))
            {
                SetMaterialFloat(mat, "_Metallic", 0.45f);
                SetMaterialFloat(mat, "_Smoothness", 0.90f);
            }
        }

        private void EnableKeyword(object mat, string keyword)
        {
            try
            {
                var m = mat.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance)
                    .FirstOrDefault(x => x.Name == "EnableKeyword" && x.GetParameters().Length == 1 &&
                                         x.GetParameters()[0].ParameterType == typeof(string));
                m?.Invoke(mat, new object[]{keyword});
            }
            catch { }
        }

        private void SetMaterialFloat(object mat, string property, float value)
        {
            try
            {
                var has = mat.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance)
                    .FirstOrDefault(x => x.Name == "HasProperty" && x.GetParameters().Length == 1 &&
                                         x.GetParameters()[0].ParameterType == typeof(string));
                if (has != null && !(bool)has.Invoke(mat, new object[]{property})) return;

                var m = mat.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance)
                    .FirstOrDefault(x => x.Name == "SetFloat" && x.GetParameters().Length == 2 &&
                                         x.GetParameters()[0].ParameterType == typeof(string));
                if (m != null)
                    m.Invoke(mat, new object[]{property, Convert.ChangeType(value, m.GetParameters()[1].ParameterType)});
            }
            catch { }
        }

        private object DictGet(object dict, object key)
        {
            if (dict == null) return null;
            try
            {
                var item = dict.GetType().GetProperty("Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (item != null)
                    return item.GetValue(dict, new[]{key});
            }
            catch { }

            foreach (var m in dict.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if (m.Name != "TryGetValue" || m.GetParameters().Length != 2) continue;
                try
                {
                    var args = new object[]{key, null};
                    if ((bool)m.Invoke(dict, args)) return args[1];
                }
                catch { }
            }
            return null;
        }

        private static Type FindType(string fullName)
        {
            var t = Type.GetType(fullName);
            if (t != null) return t;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t = asm.GetType(fullName, false); if (t != null) return t; } catch { }
            }
            return null;
        }

        private static Type FindTypeBySimpleName(string name)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                        if (t != null && t.Name == name) return t;
                }
                catch (ReflectionTypeLoadException ex)
                {
                    foreach (var t in ex.Types)
                        if (t != null && t.Name == name) return t;
                }
                catch { }
            }
            return null;
        }

        private static object GetMember(object obj, params string[] names)
        {
            if (obj == null) return null;
            const BindingFlags f = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                foreach (var n in names)
                {
                    try { var p = t.GetProperty(n, f); if (p != null) return p.GetValue(obj); } catch { }
                    try { var fi = t.GetField(n, f); if (fi != null) return fi.GetValue(obj); } catch { }
                }
            }
            return null;
        }

        private static string GetString(object obj, params string[] names)
        {
            return GetMember(obj, names)?.ToString();
        }

        private static bool SetField(object obj, string name, object value)
        {
            if (obj == null) return false;
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.DeclaredOnly);
                if (f == null) continue;
                try { f.SetValue(obj, value); return true; } catch { return false; }
            }
            return false;
        }

        private static bool SetMember(object obj, string name, object value)
        {
            if (obj == null) return false;
            const BindingFlags f = BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                try
                {
                    var p = t.GetProperty(name, f);
                    if (p != null && p.CanWrite) { p.SetValue(obj, value); return true; }
                }
                catch { }

                try
                {
                    var fi = t.GetField(name, f);
                    if (fi != null) { fi.SetValue(obj, value); return true; }
                }
                catch { }
            }
            return false;
        }

        private bool KeyPressed(string newA, string newB, string oldA, string oldB)
        {
            try
            {
                var keyboardType = FindType("UnityEngine.InputSystem.Keyboard");
                var keyboard = keyboardType == null ? null : GetStaticMember(keyboardType, "current");
                if (keyboard != null && (ControlPressed(keyboard, newA) || ControlPressed(keyboard, newB)))
                    return true;
            }
            catch { }

            try
            {
                var inputType = FindType("UnityEngine.Input");
                var keyType = FindType("UnityEngine.KeyCode");
                var get = inputType?.GetMethods(BindingFlags.Public|BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "GetKeyDown" && m.GetParameters().Length == 1 &&
                                         m.GetParameters()[0].ParameterType == keyType);
                if (get != null)
                {
                    if ((bool)get.Invoke(null, new[]{Enum.Parse(keyType, oldA)})) return true;
                    if ((bool)get.Invoke(null, new[]{Enum.Parse(keyType, oldB)})) return true;
                }
            }
            catch { }

            return false;
        }

        private static object GetStaticMember(Type type, string name)
        {
            if (type == null) return null;
            try
            {
                var p = type.GetProperty(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                if (p != null) return p.GetValue(null);
            }
            catch { }
            try
            {
                var f = type.GetField(name, BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static);
                if (f != null) return f.GetValue(null);
            }
            catch { }
            return null;
        }

        private static bool ControlPressed(object keyboard, string controlName)
        {
            try
            {
                var control = GetMember(keyboard, controlName);
                var value = GetMember(control, "wasPressedThisFrame");
                return value is bool b && b;
            }
            catch { return false; }
        }
    }
}
