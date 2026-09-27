using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1LegacyBridge.ModEntry), "SR1 Legacy Slimes Bridge", "1.1.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1LegacyBridge
{
    public sealed class ModEntry : MelonMod
    {
        private Type _configType;
        private Type _colType;
        private Type _engineType;
        private MethodInfo _build;
        private MethodInfo _spawn;
        private MethodInfo _ensureReady;

        private readonly Dictionary<string, object> _configs =
            new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private bool _apiFound;
        private bool _built;
        private int _frame;
        private DateTime _nextAttempt = DateTime.MinValue;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("SR1 Legacy Slimes Bridge v1.1.0 carregado.");
            LoggerInstance.Msg("Requer Custom Slime Maker. 8=Rad | 9=Quantum | 0=Mosaic");
            FindApi();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            _built = false;
            _nextAttempt = DateTime.UtcNow.AddSeconds(1);
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            _built = false;
            _nextAttempt = DateTime.UtcNow.AddSeconds(1);
        }

        public override void OnUpdate()
        {
            _frame++;

            if (!_apiFound && _frame % 120 == 0)
                FindApi();

            if (_apiFound && !_built && DateTime.UtcNow >= _nextAttempt)
            {
                _nextAttempt = DateTime.UtcNow.AddSeconds(2);
                TryBuildAll();
            }

            if (KeyPressed("digit8Key", "numpad8Key", "Alpha8", "Keypad8")) Spawn("Rad");
            if (KeyPressed("digit9Key", "numpad9Key", "Alpha9", "Keypad9")) Spawn("Quantum");
            if (KeyPressed("digit0Key", "numpad0Key", "Alpha0", "Keypad0")) Spawn("Mosaic");
        }

        private void FindApi()
        {
            try
            {
                _configType = FindType("CustomSlimeCreator.Core.SlimeConfig");
                _colType = FindType("CustomSlimeCreator.Core.Col");
                _engineType = FindType("CustomSlimeCreator.Core.SlimeEngine");

                if (_configType == null || _colType == null || _engineType == null)
                {
                    _apiFound = false;
                    return;
                }

                _build = _engineType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "BuildOrUpdate" && m.GetParameters().Length == 2);

                _spawn = _engineType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "Spawn" && m.GetParameters().Length == 2);

                _ensureReady = _engineType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "EnsureReady" && m.GetParameters().Length == 0);

                _apiFound = _build != null && _spawn != null;
                if (_apiFound)
                    LoggerInstance.Msg("Custom Slime Maker API encontrada.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("API: " + ex.Message);
                _apiFound = false;
            }
        }

        private void TryBuildAll()
        {
            if (!_apiFound) return;

            try
            {
                if (_ensureReady != null)
                {
                    var readyObj = _ensureReady.Invoke(null, null);
                    if (readyObj is bool ready && !ready)
                        return;
                }

                if (_configs.Count == 0)
                {
                    _configs["Rad"] = CreateConfig(
                        name: "SR1Rad",
                        display: "Rad Slime",
                        top: (111, 255, 116),
                        mid: (55, 184, 74),
                        bottom: (28, 116, 43),
                        vac: (99, 245, 110),
                        radAura: true
                    );

                    _configs["Quantum"] = CreateConfig(
                        name: "SR1Quantum",
                        display: "Quantum Slime",
                        top: (255, 232, 63),
                        mid: (255, 161, 22),
                        bottom: (218, 91, 11),
                        vac: (255, 210, 55),
                        radAura: false
                    );

                    _configs["Mosaic"] = CreateConfig(
                        name: "SR1Mosaic",
                        display: "Mosaic Slime",
                        top: (221, 244, 255),
                        mid: (78, 165, 255),
                        bottom: (142, 80, 235),
                        vac: (105, 190, 255),
                        radAura: false
                    );
                }

                int ok = 0;
                foreach (var kv in _configs)
                {
                    if (Build(kv.Key, kv.Value))
                        ok++;
                }

                _built = ok == 3;
                if (_built)
                    LoggerInstance.Msg("Bridge pronto: Rad, Quantum e Mosaic criados a partir do Pink do SR2.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Build: " + ex);
            }
        }

        private object CreateConfig(
            string name,
            string display,
            (byte r, byte g, byte b) top,
            (byte r, byte g, byte b) mid,
            (byte r, byte g, byte b) bottom,
            (byte r, byte g, byte b) vac,
            bool radAura)
        {
            var cfg = Activator.CreateInstance(_configType);

            Set(cfg, "Name", name);
            Set(cfg, "DisplayName", display);
            Set(cfg, "BasePreset", "Pink");

            Set(cfg, "Top", NewCol(top));
            Set(cfg, "Middle", NewCol(mid));
            Set(cfg, "Bottom", NewCol(bottom));
            Set(cfg, "Vac", NewCol(vac));

            // Keep this bridge focused on the species themselves.
            Set(cfg, "HasPlort", false);
            Set(cfg, "CanLargofy", false);
            Set(cfg, "CreateAllLargos", false);
            Set(cfg, "Vaccable", true);
            Set(cfg, "EdibleByTarrs", true);
            Set(cfg, "SinkInShallowWater", true);
            Set(cfg, "SupportRadiant", true);

            Set(cfg, "TwinEffect", false);
            Set(cfg, "SloomberEffect", false);
            Set(cfg, "RadAuraEffect", radAura);
            Set(cfg, "CrystalShardsEffect", false);
            Set(cfg, "RockPlatingEffect", false);
            Set(cfg, "AnglerLureEffect", false);
            Set(cfg, "HunterPatternEffect", false);
            Set(cfg, "RingtailPatternEffect", false);

            ClearList(cfg, "FoodGroups");
            ClearList(cfg, "FavoriteFoods");
            ClearList(cfg, "SpawnZones");
            ClearList(cfg, "Parts");

            return cfg;
        }

        private object NewCol((byte r, byte g, byte b) c)
        {
            try
            {
                var ctor = _colType.GetConstructor(new[] { typeof(byte), typeof(byte), typeof(byte) });
                if (ctor != null)
                    return ctor.Invoke(new object[] { c.r, c.g, c.b });
            }
            catch { }

            var col = Activator.CreateInstance(_colType);
            Set(col, "r", c.r);
            Set(col, "g", c.g);
            Set(col, "b", c.b);
            return col;
        }

        private bool Build(string key, object cfg)
        {
            try
            {
                var args = new object[] { cfg, null };
                var result = _build.Invoke(null, args);

                bool ok = result is bool b && b;
                string error = args[1] as string;

                if (!ok && !string.IsNullOrEmpty(error))
                    LoggerInstance.Warning(key + ": " + error);

                return ok;
            }
            catch (TargetInvocationException ex)
            {
                LoggerInstance.Warning(key + ": " + (ex.InnerException?.Message ?? ex.Message));
                return false;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning(key + ": " + ex.Message);
                return false;
            }
        }

        private void Spawn(string key)
        {
            if (!_apiFound)
            {
                LoggerInstance.Warning("Custom Slime Maker não foi encontrado.");
                return;
            }

            if (!_built)
                TryBuildAll();

            if (!_configs.TryGetValue(key, out var cfg))
                return;

            try
            {
                var args = new object[] { cfg, null };
                var result = _spawn.Invoke(null, args);

                bool ok = result is bool b && b;
                string error = args[1] as string;

                if (!ok)
                    LoggerInstance.Warning("Spawn " + key + ": " + (error ?? "falhou"));
                else
                    LoggerInstance.Msg("Spawn " + key + " OK.");
            }
            catch (TargetInvocationException ex)
            {
                LoggerInstance.Warning("Spawn " + key + ": " + (ex.InnerException?.Message ?? ex.Message));
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Spawn " + key + ": " + ex.Message);
            }
        }

        private static void ClearList(object owner, string name)
        {
            try
            {
                var value = Get(owner, name);
                if (value == null) return;

                var clear = value.GetType().GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance);
                clear?.Invoke(value, null);
            }
            catch { }
        }

        private static object Get(object obj, string name)
        {
            if (obj == null) return null;

            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null) return f.GetValue(obj);

                var p = t.GetProperty(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null) return p.GetValue(obj);
            }

            return null;
        }

        private static bool Set(object obj, string name, object value)
        {
            if (obj == null) return false;

            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                var f = t.GetField(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (f != null)
                {
                    f.SetValue(obj, value);
                    return true;
                }

                var p = t.GetProperty(name,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (p != null && p.CanWrite)
                {
                    p.SetValue(obj, value);
                    return true;
                }
            }

            return false;
        }

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

        private bool KeyPressed(string newA, string newB, string oldA, string oldB)
        {
            try
            {
                var kbType = FindType("UnityEngine.InputSystem.Keyboard");
                var keyboard = kbType?.GetProperty("current",
                    BindingFlags.Public | BindingFlags.Static)?.GetValue(null);

                if (keyboard != null &&
                    (Pressed(keyboard, newA) || Pressed(keyboard, newB)))
                    return true;
            }
            catch { }

            try
            {
                var inputType = FindType("UnityEngine.Input");
                var keyCodeType = FindType("UnityEngine.KeyCode");

                if (inputType != null && keyCodeType != null)
                {
                    var method = inputType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                        .FirstOrDefault(m =>
                            m.Name == "GetKeyDown" &&
                            m.GetParameters().Length == 1 &&
                            m.GetParameters()[0].ParameterType == keyCodeType);

                    if (method != null)
                    {
                        if ((bool)method.Invoke(null, new[] { Enum.Parse(keyCodeType, oldA) }))
                            return true;
                        if ((bool)method.Invoke(null, new[] { Enum.Parse(keyCodeType, oldB) }))
                            return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private static bool Pressed(object keyboard, string property)
        {
            try
            {
                var control = keyboard.GetType().GetProperty(property,
                    BindingFlags.Public | BindingFlags.Instance)?.GetValue(keyboard);

                if (control == null) return false;

                var p = control.GetType().GetProperty("wasPressedThisFrame",
                    BindingFlags.Public | BindingFlags.Instance);

                return p != null && p.GetValue(control) is bool b && b;
            }
            catch { return false; }
        }
    }
}
