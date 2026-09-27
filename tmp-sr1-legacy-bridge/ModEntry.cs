using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;
using CustomSlimeCreator.Core;

[assembly: MelonInfo(typeof(SR1LegacyBridge.ModEntry), "SR1 Legacy Slimes Bridge", "2.0.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1LegacyBridge
{
    public sealed class ModEntry : MelonMod
    {
        private readonly Dictionary<string, SlimeConfig> _configs =
            new Dictionary<string, SlimeConfig>(StringComparer.OrdinalIgnoreCase);

        private bool _savedConfigs;
        private bool _builtOnce;
        private int _frames;
        private readonly Random _rng = new Random();

        public override void OnInitializeMelon()
        {
            BuildConfigs();
            LoggerInstance.Msg("SR1 Legacy Slimes Bridge v2.0.0 carregado.");
            LoggerInstance.Msg("Base: 3 clones independentes do Pink. 8=Rad | 9=Quantum | 0=Mosaic.");
            LoggerInstance.Msg("Spawn usa o motor do Custom Slime Maker, que cria o slime 3m na frente da camera.");
        }

        public override void OnUpdate()
        {
            _frames++;

            // Persist the three presets once. CSM can then rebuild them on later sessions too.
            if (!_savedConfigs && _frames > 30)
            {
                try
                {
                    foreach (var cfg in _configs.Values)
                        ConfigStore.Save(cfg);
                    _savedConfigs = true;
                    LoggerInstance.Msg("Presets SR1 salvos no Custom Slime Maker.");
                }
                catch (Exception ex)
                {
                    LoggerInstance.Warning("Salvar presets: " + ex.Message);
                }
            }

            // Pre-build when the actual save is ready, but key presses do NOT depend on this flag.
            if (!_builtOnce && _frames % 60 == 0)
                TryBuildAll();

            if (KeyPressed("digit8Key","numpad8Key","Alpha8","Keypad8"))
                SpawnNow("Rad");

            if (KeyPressed("digit9Key","numpad9Key","Alpha9","Keypad9"))
                SpawnNow("Quantum");

            if (KeyPressed("digit0Key","numpad0Key","Alpha0","Keypad0"))
                SpawnNow("Mosaic");

            // Recreated SR1-style runtime behavior.
            TickQuantumSuperposition();
            TickMosaicGlints();
            TickRadAuraPulse();
        }

        private void BuildConfigs()
        {
            // All three use the SR2 Pink slime as the structural/model base.
            // Colors/effects are adapted from the SR1 assets supplied by the user.

            var rad = NewPinkClone(
                "Rad", "Rad Slime",
                new Col(112,255,126),
                new Col(48,190,67),
                new Col(14,108,31),
                new Col(103,255,118));
            rad.FoodGroups = new List<string> { "VeggieGroup" };
            rad.RadAuraEffect = true;
            rad.SupportRadiant = true;

            var quantum = NewPinkClone(
                "Quantum", "Quantum Slime",
                new Col(255,239,102),
                new Col(247,190,43),
                new Col(215,126,19),
                new Col(255,226,82));
            quantum.FoodGroups = new List<string> { "FruitGroup" };
            // SR1 Quantum had shifting/superposition visuals. TwinEffect gives a phase-like
            // body treatment; actual position jumping is recreated below.
            quantum.TwinEffect = true;
            quantum.SupportRadiant = true;

            var mosaic = NewPinkClone(
                "Mosaic", "Mosaic Slime",
                new Col(166,232,255),
                new Col(70,151,236),
                new Col(125,66,210),
                new Col(130,213,255));
            mosaic.FoodGroups = new List<string> { "VeggieGroup" };
            // Smooth/prismatic look is reinforced at runtime by the glint pulse below.
            mosaic.TwinEffect = true;
            mosaic.SupportRadiant = true;

            _configs["Rad"] = rad;
            _configs["Quantum"] = quantum;
            _configs["Mosaic"] = mosaic;
        }

        private static SlimeConfig NewPinkClone(
            string name, string display,
            Col top, Col middle, Col bottom, Col vac)
        {
            return new SlimeConfig
            {
                Name = name,
                DisplayName = display,
                BasePreset = "Pink",

                Top = top,
                Middle = middle,
                Bottom = bottom,
                Vac = vac,

                // First stable stage: three real species, no plorts/largos yet.
                HasPlort = false,
                CanLargofy = false,
                CreateAllLargos = false,

                Vaccable = true,
                EdibleByTarrs = true,
                SinkInShallowWater = true,

                FavoriteFoods = new List<string>(),
                SpawnZones = new List<string>()
            };
        }

        private void TryBuildAll()
        {
            try
            {
                if (!SlimeEngine.EnsureReady())
                    return;

                bool all = true;
                foreach (var cfg in _configs.Values)
                {
                    if (!SlimeEngine.BuildOrUpdate(cfg, out var error))
                    {
                        all = false;
                        LoggerInstance.Warning(cfg.Name + " build: " + (error ?? "erro desconhecido"));
                    }
                }

                if (all)
                {
                    _builtOnce = true;
                    LoggerInstance.Msg("3 NOVOS SLIMES PRONTOS: Rad, Quantum e Mosaic. Todos clonados do Pink.");
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Build: " + ex);
            }
        }

        private void SpawnNow(string key)
        {
            if (!_configs.TryGetValue(key, out var cfg))
                return;

            try
            {
                // Do not silently ignore the key anymore.
                if (!SlimeEngine.EnsureReady())
                {
                    LoggerInstance.Warning("TECLA " + key + ": Custom Slime Maker ainda não reconheceu o mundo carregado.");
                    return;
                }

                if (!SlimeEngine.BuildOrUpdate(cfg, out var buildError))
                {
                    LoggerInstance.Warning("TECLA " + key + " build: " + (buildError ?? "falhou"));
                    return;
                }

                // CSM Spawn places the actor at Camera.main.position + forward*3 + up*0.5.
                if (!SlimeEngine.Spawn(cfg, out var spawnError))
                {
                    LoggerInstance.Warning("TECLA " + key + " spawn: " + (spawnError ?? "falhou"));
                    return;
                }

                LoggerInstance.Msg("SPAWN OK: " + cfg.DisplayName + " nasceu NA SUA FRENTE.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Spawn " + key + ": " + ex);
            }
        }

        // -----------------------------------------------------------------
        // SR1 BEHAVIOR PORTS (adapted to SR2 runtime)
        // -----------------------------------------------------------------

        // SR1 concept: QuantumSlimeSuperposition.
        // Every few seconds, a quantum slime has a chance to shift to a nearby possible position.
        private void TickQuantumSuperposition()
        {
            if (_frames % 360 != 0) return; // roughly every few seconds
            if (_rng.NextDouble() > 0.45) return;

            foreach (var go in GetInstances("Quantum"))
            {
                try
                {
                    var tr = GetMember(go, "transform");
                    var pos = GetMember(tr, "position");
                    if (tr == null || pos == null) continue;

                    float x = ToFloat(GetMember(pos, "x"));
                    float y = ToFloat(GetMember(pos, "y"));
                    float z = ToFloat(GetMember(pos, "z"));

                    float dx = (float)(_rng.NextDouble() * 4.0 - 2.0);
                    float dz = (float)(_rng.NextDouble() * 4.0 - 2.0);

                    var vecType = pos.GetType();
                    var next = Activator.CreateInstance(vecType, new object[] { x + dx, y + 0.15f, z + dz });
                    SetMember(tr, "position", next);
                }
                catch { }
            }
        }

        // SR1 concept: MosaicAttractor / GlintController.
        // Pulse the custom slime's materials to create the characteristic bright glassy glint.
        private void TickMosaicGlints()
        {
            if (_frames % 20 != 0) return;

            float wave = (float)((Math.Sin(_frames * 0.11) + 1.0) * 0.5);
            float emission = 0.35f + wave * 1.35f;

            foreach (var go in GetInstances("Mosaic"))
            {
                foreach (var mat in GetMaterials(go))
                {
                    SetMaterialFloat(mat, "_Metallic", 0.45f);
                    SetMaterialFloat(mat, "_Smoothness", 0.90f);
                    SetMaterialFloat(mat, "_EmissionStrength", emission);
                    EnableKeyword(mat, "_EMISSION");
                }
            }
        }

        // SR1 concept: RadSlimeExpand / RadSource.
        // The CSM RadAuraEffect supplies the aura body element when available.
        // This pulse keeps the green aura/body visibly energetic on the SR2 Pink model.
        private void TickRadAuraPulse()
        {
            if (_frames % 20 != 0) return;

            float wave = (float)((Math.Sin(_frames * 0.08) + 1.0) * 0.5);
            float emission = 0.8f + wave * 1.0f;

            foreach (var go in GetInstances("Rad"))
            {
                foreach (var mat in GetMaterials(go))
                {
                    EnableKeyword(mat, "_EMISSION");
                    SetMaterialFloat(mat, "_EmissionStrength", emission);
                }
            }
        }

        // -----------------------------------------------------------------
        // CSM instance access
        // -----------------------------------------------------------------

        private IEnumerable<object> GetInstances(string key)
        {
            object built = null;
            try
            {
                var f = typeof(SlimeEngine).GetField("Built", BindingFlags.NonPublic | BindingFlags.Static);
                built = f?.GetValue(null);
            }
            catch { }

            if (built == null) yield break;

            object custom = DictGet(built, key);
            if (custom == null) yield break;

            var instances = GetMember(custom, "Instances");
            if (!(instances is IEnumerable en)) yield break;

            foreach (var go in en)
                if (go != null) yield return go;
        }

        private IEnumerable<object> GetMaterials(object go)
        {
            if (go == null) yield break;

            var rendererType = FindType("UnityEngine.Renderer");
            if (rendererType == null) yield break;

            foreach (var renderer in GetComponentsInChildren(go, rendererType))
            {
                var mats = GetMember(renderer, "materials");
                if (mats is IEnumerable en)
                    foreach (var mat in en)
                        if (mat != null) yield return mat;
            }
        }

        // -----------------------------------------------------------------
        // Reflection helpers only for Unity runtime/input/material access.
        // CSM itself is called DIRECTLY, not through reflection.
        // -----------------------------------------------------------------

        private static object DictGet(object dict, object key)
        {
            if (dict == null) return null;

            try
            {
                var item = dict.GetType().GetProperty("Item", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (item != null) return item.GetValue(dict, new[] { key });
            }
            catch { }

            foreach (var m in dict.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != "TryGetValue" || m.GetParameters().Length != 2) continue;
                try
                {
                    var args = new object[] { key, null };
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
                try
                {
                    t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }

            return null;
        }

        private static object GetMember(object obj, string name)
        {
            if (obj == null) return null;

            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                try
                {
                    var p = t.GetProperty(name, flags);
                    if (p != null) return p.GetValue(obj);
                }
                catch { }

                try
                {
                    var f = t.GetField(name, flags);
                    if (f != null) return f.GetValue(obj);
                }
                catch { }
            }

            return null;
        }

        private static bool SetMember(object obj, string name, object value)
        {
            if (obj == null) return false;

            const BindingFlags flags =
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static |
                BindingFlags.DeclaredOnly;

            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                try
                {
                    var p = t.GetProperty(name, flags);
                    if (p != null && p.CanWrite)
                    {
                        p.SetValue(obj, value);
                        return true;
                    }
                }
                catch { }

                try
                {
                    var f = t.GetField(name, flags);
                    if (f != null)
                    {
                        f.SetValue(obj, value);
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        private static IEnumerable<object> GetComponentsInChildren(object go, Type componentType)
        {
            if (go == null || componentType == null) yield break;

            foreach (var m in go.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (m.Name != "GetComponentsInChildren" || !m.IsGenericMethodDefinition) continue;

                object result = null;
                try
                {
                    var gm = m.MakeGenericMethod(componentType);
                    var p = m.GetParameters();

                    if (p.Length == 1 && p[0].ParameterType == typeof(bool))
                        result = gm.Invoke(go, new object[] { true });
                    else if (p.Length == 0)
                        result = gm.Invoke(go, null);
                }
                catch { }

                if (result is IEnumerable en)
                {
                    foreach (var x in en)
                        if (x != null) yield return x;
                    yield break;
                }
            }
        }

        private static void EnableKeyword(object mat, string keyword)
        {
            if (mat == null) return;
            try
            {
                var m = mat.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x =>
                        x.Name == "EnableKeyword" &&
                        x.GetParameters().Length == 1 &&
                        x.GetParameters()[0].ParameterType == typeof(string));
                m?.Invoke(mat, new object[] { keyword });
            }
            catch { }
        }

        private static void SetMaterialFloat(object mat, string property, float value)
        {
            if (mat == null) return;

            try
            {
                var has = mat.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x =>
                        x.Name == "HasProperty" &&
                        x.GetParameters().Length == 1 &&
                        x.GetParameters()[0].ParameterType == typeof(string));

                if (has != null && !(bool)has.Invoke(mat, new object[] { property }))
                    return;

                var set = mat.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(x =>
                        x.Name == "SetFloat" &&
                        x.GetParameters().Length == 2 &&
                        x.GetParameters()[0].ParameterType == typeof(string));

                if (set != null)
                {
                    var t = set.GetParameters()[1].ParameterType;
                    set.Invoke(mat, new object[] { property, Convert.ChangeType(value, t) });
                }
            }
            catch { }
        }

        private static float ToFloat(object value)
        {
            try { return Convert.ToSingle(value); }
            catch { return 0f; }
        }

        private bool KeyPressed(string newA, string newB, string oldA, string oldB)
        {
            // New Input System
            try
            {
                var keyboardType = FindType("UnityEngine.InputSystem.Keyboard");
                var keyboard = keyboardType == null ? null : GetStaticMember(keyboardType, "current");

                if (keyboard != null &&
                    (ControlPressed(keyboard, newA) || ControlPressed(keyboard, newB)))
                    return true;
            }
            catch { }

            // Legacy Input fallback
            try
            {
                var inputType = FindType("UnityEngine.Input");
                var keyType = FindType("UnityEngine.KeyCode");

                var get = inputType?.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m =>
                        m.Name == "GetKeyDown" &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == keyType);

                if (get != null)
                {
                    if ((bool)get.Invoke(null, new[] { Enum.Parse(keyType, oldA) })) return true;
                    if ((bool)get.Invoke(null, new[] { Enum.Parse(keyType, oldB) })) return true;
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
                var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (p != null) return p.GetValue(null);
            }
            catch { }

            try
            {
                var f = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
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
