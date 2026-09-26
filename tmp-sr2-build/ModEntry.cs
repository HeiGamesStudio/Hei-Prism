using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1SlimesSpawner.ModEntry), "SR1 Slimes Spawner", "0.1.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1SlimesSpawner
{
    public sealed class ModEntry : MelonMod
    {
        private Type _configType;
        private Type _colType;
        private Type _engineType;
        private MethodInfo _spawnMethod;
        private object _rad;
        private object _quantum;
        private object _mosaic;
        private string _status = "Carregue um save e clique em um slime.";
        private bool _collapsed;
        private bool _loggedGuiError;

        private Type _guiType;
        private Type _rectType;
        private MethodInfo _guiBox;
        private MethodInfo _guiButton;
        private MethodInfo _guiLabel;

        public override void OnInitializeMelon()
        {
            MelonLogger.Msg("[SR1 Slimes Spawner] carregado. Painel de spawn ativado.");
        }

        public override void OnUpdate()
        {
            EnsureCustomSlimeCreator();
        }

        public override void OnGUI()
        {
            try
            {
                if (!EnsureGui())
                    return;

                EnsureCustomSlimeCreator();

                if (_collapsed)
                {
                    if (Button(16, 180, 145, 30, "SR1 SLIMES"))
                        _collapsed = false;
                    return;
                }

                Box(16, 145, 248, 220, "SR1 SLIMES");
                if (Button(225, 150, 30, 24, "X"))
                {
                    _collapsed = true;
                    return;
                }

                Label(30, 178, 220, 22, "Spawner de teste");
                if (Button(32, 207, 216, 36, "SPAWN RAD"))
                    Spawn("RadSlime", ref _rad, "SR/AMP/Slime/Body/Rad", 103, 224, 111, 70, 164, 74, 58, 137, 63);

                if (Button(32, 249, 216, 36, "SPAWN QUANTUM"))
                    Spawn("QuantumSlime", ref _quantum, "SR/AMP/Slime/Body/Quantum", 255, 194, 37, 247, 165, 16, 234, 145, 13);

                if (Button(32, 291, 216, 36, "SPAWN MOSAIC"))
                    Spawn("MosaicSlime", ref _mosaic, "SR/AMP/Slime/Mosaic", 231, 236, 247, 204, 213, 233, 176, 189, 217);

                Label(31, 333, 218, 28, _status ?? "");
            }
            catch (Exception ex)
            {
                if (!_loggedGuiError)
                {
                    _loggedGuiError = true;
                    MelonLogger.Warning("[SR1 Slimes Spawner] GUI: " + ex.Message);
                }
            }
        }

        private bool EnsureCustomSlimeCreator()
        {
            if (_configType != null && _engineType != null && _spawnMethod != null)
                return true;

            _configType = FindType("CustomSlimeCreator.Core.SlimeConfig");
            _colType = FindType("CustomSlimeCreator.Core.Col");
            _engineType = FindType("CustomSlimeCreator.Core.SlimeEngine");

            if (_configType == null || _colType == null || _engineType == null)
            {
                _status = "Falta CustomSlimeCreator.dll na pasta Mods.";
                return false;
            }

            _spawnMethod = _engineType.GetMethod("Spawn", BindingFlags.Public | BindingFlags.Static);
            if (_spawnMethod == null)
            {
                _status = "Custom Slime Maker incompatível: Spawn não encontrado.";
                return false;
            }

            if (_rad == null) _rad = MakeRad();
            if (_quantum == null) _quantum = MakeQuantum();
            if (_mosaic == null) _mosaic = MakeMosaic();
            return true;
        }

        private object NewConfig(string name, string displayName,
            int tr, int tg, int tb, int mr, int mg, int mb, int br, int bg, int bb,
            int pr, int pg, int pb, int pmr, int pmg, int pmb, int pbr, int pbg, int pbb,
            string foodGroup, string favorite, bool radAura, int plortValue)
        {
            var c = Activator.CreateInstance(_configType);
            Set(c, "Name", name);
            Set(c, "DisplayName", displayName);
            Set(c, "BasePreset", "Pink");

            Set(c, "Top", Col(tr, tg, tb));
            Set(c, "Middle", Col(mr, mg, mb));
            Set(c, "Bottom", Col(br, bg, bb));
            Set(c, "Vac", Col(tr, tg, tb));

            Set(c, "HasPlort", true);
            Set(c, "PlortValue", plortValue);
            Set(c, "PlortTop", Col(pr, pg, pb));
            Set(c, "PlortMiddle", Col(pmr, pmg, pmb));
            Set(c, "PlortBottom", Col(pbr, pbg, pbb));

            Set(c, "FoodGroups", new List<string> { foodGroup });
            Set(c, "FavoriteFoods", string.IsNullOrEmpty(favorite) ? new List<string>() : new List<string> { favorite });
            Set(c, "SpawnZones", new List<string>());

            Set(c, "CanLargofy", true);
            Set(c, "CreateAllLargos", false);
            Set(c, "EdibleByTarrs", true);
            Set(c, "Vaccable", true);
            Set(c, "SinkInShallowWater", true);
            Set(c, "SupportRadiant", true);
            Set(c, "RadAuraEffect", radAura);
            Set(c, "CrystalShardsEffect", false);
            Set(c, "RockPlatingEffect", false);
            Set(c, "AnglerLureEffect", false);
            Set(c, "HunterPatternEffect", false);
            Set(c, "RingtailPatternEffect", false);
            Set(c, "TwinEffect", false);
            Set(c, "SloomberEffect", false);
            return c;
        }

        private object MakeRad()
        {
            return NewConfig("RadSlime", "Rad Slime",
                103,224,111, 70,164,74, 58,137,63,
                111,231,117, 65,167,70, 47,127,53,
                "VeggieGroup", "OcaOca", true, 45);
        }

        private object MakeQuantum()
        {
            return NewConfig("QuantumSlime", "Quantum Slime",
                255,194,37, 247,165,16, 234,145,13,
                255,201,46, 242,153,18, 218,125,8,
                "FruitGroup", "PhaseLemon", false, 60);
        }

        private object MakeMosaic()
        {
            return NewConfig("MosaicSlime", "Mosaic Slime",
                231,236,247, 204,213,233, 176,189,217,
                83,211,255, 103,235,95, 255,144,45,
                "VeggieGroup", "SilverParsnip", false, 75);
        }

        private void Spawn(string name, ref object cfg, string shaderName,
            int tr, int tg, int tb, int mr, int mg, int mb, int br, int bg, int bb)
        {
            if (!EnsureCustomSlimeCreator())
                return;

            try
            {
                if (cfg == null)
                {
                    if (name == "RadSlime") cfg = MakeRad();
                    else if (name == "QuantumSlime") cfg = MakeQuantum();
                    else cfg = MakeMosaic();
                }

                object[] args = new object[] { cfg, null };
                bool ok = (bool)_spawnMethod.Invoke(null, args);
                string err = args[1] == null ? null : args[1].ToString();

                if (!ok)
                {
                    _status = string.IsNullOrEmpty(err) ? "Não foi possível spawnar." : err;
                    return;
                }

                int changed = ApplyOfficialShader(name, shaderName,
                    tr / 255f, tg / 255f, tb / 255f,
                    mr / 255f, mg / 255f, mb / 255f,
                    br / 255f, bg / 255f, bb / 255f);

                _status = changed > 0
                    ? name.Replace("Slime", "") + " spawnado + shader oficial."
                    : name.Replace("Slime", "") + " spawnado.";
            }
            catch (TargetInvocationException tie)
            {
                _status = tie.InnerException != null ? tie.InnerException.Message : tie.Message;
            }
            catch (Exception ex)
            {
                _status = ex.Message;
            }
        }

        private int ApplyOfficialShader(string key, string shaderName,
            float tr, float tg, float tb, float mr, float mg, float mb, float br, float bg, float bb)
        {
            try
            {
                var shaderType = FindType("UnityEngine.Shader");
                var rendererType = FindType("UnityEngine.Renderer");
                var colorType = FindType("UnityEngine.Color");
                if (shaderType == null || rendererType == null || colorType == null)
                    return 0;

                var find = shaderType.GetMethod("Find", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                var shader = find == null ? null : find.Invoke(null, new object[] { shaderName });
                if (shader == null)
                    return 0;

                var builtField = _engineType.GetField("Built", BindingFlags.NonPublic | BindingFlags.Static);
                var built = builtField == null ? null : builtField.GetValue(null);
                if (built == null)
                    return 0;

                var itemProp = built.GetType().GetProperty("Item");
                object custom = null;
                try { custom = itemProp == null ? null : itemProp.GetValue(built, new object[] { key }); } catch { }
                if (custom == null)
                    return 0;

                int count = 0;
                var prefabField = custom.GetType().GetField("Prefab", BindingFlags.Public | BindingFlags.Instance);
                var prefab = prefabField == null ? null : prefabField.GetValue(custom);
                if (prefab != null)
                    count += ApplyShaderToGameObject(prefab, rendererType, colorType, shader, tr,tg,tb,mr,mg,mb,br,bg,bb);

                var instancesField = custom.GetType().GetField("Instances", BindingFlags.Public | BindingFlags.Instance);
                var instances = instancesField == null ? null : instancesField.GetValue(custom);
                if (instances != null)
                {
                    var countProp = instances.GetType().GetProperty("Count");
                    var idxProp = instances.GetType().GetProperty("Item");
                    int n = countProp == null ? 0 : Convert.ToInt32(countProp.GetValue(instances));
                    if (n > 0 && idxProp != null)
                    {
                        var last = idxProp.GetValue(instances, new object[] { n - 1 });
                        if (last != null)
                            count += ApplyShaderToGameObject(last, rendererType, colorType, shader, tr,tg,tb,mr,mg,mb,br,bg,bb);
                    }
                }
                return count;
            }
            catch
            {
                return 0;
            }
        }

        private int ApplyShaderToGameObject(object go, Type rendererType, Type colorType, object shader,
            float tr, float tg, float tb, float mr, float mg, float mb, float br, float bg, float bb)
        {
            int changed = 0;
            try
            {
                var getComps = go.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != "GetComponentsInChildren" || !m.IsGenericMethodDefinition) return false;
                        var p = m.GetParameters();
                        return p.Length == 1 && p[0].ParameterType == typeof(bool);
                    });
                if (getComps == null) return 0;

                var gm = getComps.MakeGenericMethod(rendererType);
                var result = gm.Invoke(go, new object[] { true });
                if (!(result is IEnumerable renderers)) return 0;

                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    var matsProp = renderer.GetType().GetProperty("materials", BindingFlags.Public | BindingFlags.Instance);
                    var mats = matsProp == null ? null : matsProp.GetValue(renderer);
                    if (!(mats is IEnumerable materials)) continue;

                    foreach (var mat in materials)
                    {
                        if (mat == null) continue;
                        var shaderProp = mat.GetType().GetProperty("shader", BindingFlags.Public | BindingFlags.Instance);
                        if (shaderProp == null || !shaderProp.CanWrite) continue;

                        var oldShader = shaderProp.GetValue(mat);
                        string oldName = GetUnityName(oldShader);
                        if (string.IsNullOrEmpty(oldName) || oldName.IndexOf("/Slime/Body", StringComparison.OrdinalIgnoreCase) < 0)
                            continue;

                        shaderProp.SetValue(mat, shader);
                        SetMaterialColor(mat, colorType, "_TopColor", tr,tg,tb);
                        SetMaterialColor(mat, colorType, "_MiddleColor", mr,mg,mb);
                        SetMaterialColor(mat, colorType, "_BottomColor", br,bg,bb);
                        changed++;
                    }
                }
            }
            catch { }
            return changed;
        }

        private void SetMaterialColor(object mat, Type colorType, string property, float r, float g, float b)
        {
            try
            {
                object color = Activator.CreateInstance(colorType, new object[] { r, g, b, 1f });
                var setColor = mat.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m =>
                    {
                        if (m.Name != "SetColor") return false;
                        var p = m.GetParameters();
                        return p.Length == 2 && p[0].ParameterType == typeof(string) && p[1].ParameterType == colorType;
                    });
                if (setColor != null)
                    setColor.Invoke(mat, new object[] { property, color });
            }
            catch { }
        }

        private string GetUnityName(object obj)
        {
            try
            {
                if (obj == null) return null;
                var p = obj.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                return p == null ? null : p.GetValue(obj)?.ToString();
            }
            catch { return null; }
        }

        private object Col(int r, int g, int b)
        {
            return Activator.CreateInstance(_colType, new object[] { (byte)r, (byte)g, (byte)b });
        }

        private static void Set(object obj, string field, object value)
        {
            if (obj == null) return;
            var f = obj.GetType().GetField(field, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) f.SetValue(obj, value);
        }

        private bool EnsureGui()
        {
            if (_guiType != null && _rectType != null && _guiButton != null)
                return true;

            _guiType = FindType("UnityEngine.GUI");
            _rectType = FindType("UnityEngine.Rect");
            if (_guiType == null || _rectType == null)
                return false;

            _guiBox = _guiType.GetMethod("Box", BindingFlags.Public | BindingFlags.Static, null, new[] { _rectType, typeof(string) }, null);
            _guiButton = _guiType.GetMethod("Button", BindingFlags.Public | BindingFlags.Static, null, new[] { _rectType, typeof(string) }, null);
            _guiLabel = _guiType.GetMethod("Label", BindingFlags.Public | BindingFlags.Static, null, new[] { _rectType, typeof(string) }, null);
            return _guiBox != null && _guiButton != null && _guiLabel != null;
        }

        private object Rect(float x, float y, float w, float h)
        {
            return Activator.CreateInstance(_rectType, new object[] { x, y, w, h });
        }

        private void Box(float x, float y, float w, float h, string text)
        {
            _guiBox.Invoke(null, new object[] { Rect(x,y,w,h), text });
        }

        private bool Button(float x, float y, float w, float h, string text)
        {
            return (bool)_guiButton.Invoke(null, new object[] { Rect(x,y,w,h), text });
        }

        private void Label(float x, float y, float w, float h, string text)
        {
            _guiLabel.Invoke(null, new object[] { Rect(x,y,w,h), text });
        }

        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, false);
                    if (t != null) return t;
                }
                catch { }
            }
            return null;
        }
    }
}
