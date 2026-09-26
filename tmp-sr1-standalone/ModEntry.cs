using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1SlimesStandalone.ModEntry), "SR1 Slimes Standalone", "0.7.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1SlimesStandalone
{
    public sealed class ModEntry : MelonMod
    {
        private sealed class Spec
        {
            public string Key;
            public string Display;
            public string RefId;
            public string[] ShaderNames;
            public float[] Top, Mid, Bottom;
            public string Description;
        }

        private readonly Spec[] _specs = new[]
        {
            new Spec {
                Key="Rad", Display="Rad Slime", RefId="SlimeDefinition.SR1Rad",
                ShaderNames=new[]{"SR/AMP/Slime/Body/Rad","SlimeBody_rad","SR/AMP/Slime/Body/Rad/Normal"},
                Top=new[]{0.48f,0.95f,0.48f,1f}, Mid=new[]{0.25f,0.68f,0.29f,1f}, Bottom=new[]{0.16f,0.48f,0.20f,1f},
                Description="SLIMEOLOGIA — O Slime Rad emite uma aura de radiação intensa e brilha com um tom verde característico. No Slime Rancher original, sua comida favorita é Oca Oca.\n\nRISCOS PARA O RANCHEIRO — Ficar dentro de sua aura por muito tempo aumenta a exposição à radiação e pode causar dano. O melhor é manter distância quando o medidor de exposição sobe.\n\nPLORTONOMIA — Plorts Rad são valorizados como fontes compactas de energia e aparecem em tecnologias que exigem grande potência."
            },
            new Spec {
                Key="Quantum", Display="Quantum Slime", RefId="SlimeDefinition.SR1Quantum",
                ShaderNames=new[]{"SR/AMP/Slime/Body/Quantum","SlimeBody_quantum","SR/AMP/Slime/Body/QuantumTimeOverride"},
                Top=new[]{1f,0.84f,0.24f,1f}, Mid=new[]{1f,0.61f,0.06f,1f}, Bottom=new[]{0.88f,0.39f,0.03f,1f},
                Description="SLIMEOLOGIA — O Slime Quantum está ligado às misteriosas Ruínas Antigas. Ele produz projeções fantasmagóricas de possíveis posições e pode trocar de lugar com uma delas, parecendo se teleportar. Sua comida favorita no primeiro jogo é Phase Lemon.\n\nRISCOS PARA O RANCHEIRO — Quando fica muito agitado, suas projeções se tornam um ótimo caminho de fuga. Manter o slime bem alimentado e controlar sua agitação ajuda a evitar escapadas.\n\nPLORTONOMIA — Plorts Quantum despertam interesse por suas propriedades ligadas a estados e realidades alternativas."
            },
            new Spec {
                Key="Mosaic", Display="Mosaic Slime", RefId="SlimeDefinition.SR1Mosaic",
                ShaderNames=new[]{"SR/AMP/Slime/Mosaic","MosaicSlime","SR/AMP/Slime/Body/Mosaic"},
                Top=new[]{0.94f,0.98f,1f,1f}, Mid=new[]{0.57f,0.83f,1f,1f}, Bottom=new[]{0.65f,0.48f,0.96f,1f},
                Description="SLIMEOLOGIA — O Slime Mosaic é coberto por placas brilhantes semelhantes a vidro, capazes de refletir a luz em cores intensas. Sua comida favorita no Slime Rancher original é Silver Parsnip.\n\nRISCOS PARA O RANCHEIRO — Seus glints podem cair no chão e explodir em chamas. Água é uma ferramenta importante para apagar esses focos antes que virem um problema.\n\nPLORTONOMIA — Plorts Mosaic são procurados por suas propriedades ópticas e pela semelhança com o misterioso vidro encontrado pela Grande, Grande Extensão."
            }
        };

        private readonly Dictionary<string, object> _defs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _prefabs = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> _pedia = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        private object _pinkDef;
        private int _tick;
        private bool _ready;
        private bool _pediaReady;
        private string _status = "SR1 Slimes carregando...";
        private DateTime _statusUntil = DateTime.UtcNow.AddSeconds(12);

        private Type _guiType, _rectType;
        private ConstructorInfo _rectCtor;
        private MethodInfo _guiBox, _guiLabel;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("SR1 Slimes Standalone v0.7 iniciado. IDs reais + compatibilidade com saves antigos.");
            LoggerInstance.Msg("Hotkeys: 8 = Rad, 9 = Quantum, 0 = Mosaic.");
            _status = "SR1 Slimes v0.7: criando Rad, Quantum e Mosaic REAIS...";
            _statusUntil = DateTime.UtcNow.AddSeconds(15);
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            try
            {
                if (!_ready) _ready = EnsureReady();
                if (_ready)
                {
                    Show("SR1 SLIMES PRONTOS — IDs reais registrados; saves antigos compatíveis.", 8);
                    LoggerInstance.Msg("Registro antecipado pronto na cena: " + sceneName);
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Bootstrap de cena: " + ex.Message);
            }
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            try
            {
                if (!_ready) _ready = EnsureReady();
                if (_ready) InjectNaturalSpawns();
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Scene load: " + ex.Message);
            }
        }

        public override void OnUpdate()
        {
            try
            {
                _tick++;

                if (!_ready && _tick % 30 == 0)
                {
                    if (EnsureReady())
                    {
                        _ready = true;
                        Show("SR1 Slimes ATIVOS — 8 Rad | 9 Quantum | 0 Mosaic", 10);
                        LoggerInstance.Msg("Rad, Quantum e Mosaic foram criados localmente e registrados.");
                    }
                }

                if (_ready && !_pediaReady && _tick % 60 == 0)
                {
                    _pediaReady = TryInstallAllPedia();
                    if (_pediaReady)
                    {
                        Show("Slimepedia: Rad, Quantum e Mosaic adicionados!", 8);
                        LoggerInstance.Msg("Entradas da Slimepedia instaladas.");
                    }
                }

                if (_ready && _tick % 180 == 0)
                    InjectNaturalSpawns();

                if (KeyPressed("digit8Key","numpad8Key","Alpha8","Keypad8")) Spawn("Rad");
                if (KeyPressed("digit9Key","numpad9Key","Alpha9","Keypad9")) Spawn("Quantum");
                if (KeyPressed("digit0Key","numpad0Key","Alpha0","Keypad0")) Spawn("Mosaic");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Update: " + ex.Message);
            }
        }

        public override void OnGUI()
        {
            try
            {
                if (DateTime.UtcNow > _statusUntil) return;
                if (!EnsureGui()) return;
                _guiBox?.Invoke(null, new[] { Rect(18f,18f,520f,54f), "" });
                _guiLabel?.Invoke(null, new[] { Rect(31f,32f,495f,28f), _status });
            }
            catch { }
        }

        private bool EnsureReady()
        {
            try
            {
                var slimeDefType = FindType("Il2Cpp.SlimeDefinition") ?? FindTypeBySimpleName("SlimeDefinition");
                if (slimeDefType == null) return false;

                var allDefs = DiscoverSlimeDefinitions(slimeDefType);
                if (allDefs.Count == 0) return false;

                _pinkDef = FindPink(allDefs);
                if (_pinkDef == null) return false;

                foreach (var sp in _specs)
                {
                    if (_defs.ContainsKey(sp.Key)) continue;
                    var def = BuildDefinition(sp);
                    if (def == null) return false;
                    _defs[sp.Key] = def;
                }

                return _defs.Count == _specs.Length;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Preparando slimes locais: " + ex.Message);
                return false;
            }
        }

        private List<object> DiscoverSlimeDefinitions(Type slimeDefType)
        {
            var result = FindAllResources(slimeDefType).ToList();

            try
            {
                var gc = GetGameContext();
                var defs = gc == null ? null : GetMember(gc, "SlimeDefinitions");
                var slimes = defs == null ? null : GetMember(defs, "Slimes");
                if (slimes is IEnumerable en)
                {
                    foreach (var d in en)
                        if (d != null && !result.Any(x => ReferenceEquals(x,d) || x.Equals(d)))
                            result.Add(d);
                }

                var map = defs == null ? null : GetMember(defs, "_slimeDefinitionsByIdentifiable");
                if (map is IEnumerable men)
                {
                    foreach (var pair in men)
                    {
                        var value = GetMember(pair, "Value", "value");
                        if (value != null && !result.Any(x => ReferenceEquals(x,value) || x.Equals(value)))
                            result.Add(value);
                    }
                }
            }
            catch { }

            return result;
        }

        private bool TryInstallAllPedia()
        {
            if (!_ready) return false;
            bool ok = true;
            foreach (var sp in _specs)
            {
                if (!_defs.TryGetValue(sp.Key, out var def) || def == null) { ok = false; continue; }
                ok &= EnsurePedia(sp, def);
            }
            return ok;
        }

        private object FindPink(List<object> defs)
        {
            foreach (var d in defs)
            {
                var rid = GetString(d, "ReferenceId", "referenceId");
                var n = GetString(d, "name", "Name");
                if (string.Equals(rid, "SlimeDefinition.Pink", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n, "Pink", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(n, "PinkSlime", StringComparison.OrdinalIgnoreCase))
                    return d;
            }
            foreach (var d in defs)
            {
                var rid = GetString(d, "ReferenceId", "referenceId") ?? "";
                if (rid.IndexOf("Pink", StringComparison.OrdinalIgnoreCase) >= 0) return d;
            }
            return null;
        }

        private object BuildDefinition(Spec s)
        {
            try
            {
                var def = InstantiateObject(_pinkDef);
                if (def == null) return null;

                SetMember(def, "name", "SR1" + s.Key + "Slime");
                PinObject(def);
                ForceReferenceId(def, s.RefId);

                var ln = MakeLocalized("Actor", "sr1slimes.name." + s.Key.ToLowerInvariant(), s.Display);
                if (ln != null) SetMember(def, "localizedName", ln);

                // Give the species its OWN appearance instead of sharing Pink's appearance.
                var customApp = CloneAppearanceFor(s);
                if (customApp != null)
                {
                    var baseApps = GetMember(_pinkDef, "AppearancesDefault");
                    if (baseApps != null)
                    {
                        var apps = CreateCollection(baseApps.GetType(), new List<object> { customApp });
                        if (apps != null) SetMember(def, "AppearancesDefault", apps);
                    }
                }

                var basePrefab = GetMember(_pinkDef, "prefab", "Prefab");
                if (basePrefab == null) return null;
                var prefab = InstantiateObject(basePrefab);
                if (prefab == null) return null;

                TryCall(prefab, "SetActive", false);
                SetMember(prefab, "name", "SR1" + s.Key + "SlimePrefab");
                PinObject(prefab);
                SetMember(def, "prefab", prefab);
                WirePrefab(prefab, def);

                // Register canonical ID + every ID used by our earlier test builds.
                RegisterSlime(def, OldSlimeAlias(s.Key));

                // Also recover/create a real species plort instead of mapping it to Pink Plort.
                var plort = BuildPlortFor(s);
                if (plort != null)
                    TryWireProduceIdent(def, plort);

                _prefabs[s.Key] = prefab;
                Recolor(prefab, s);

                LoggerInstance.Msg("Criado " + s.Display + " com ID real " + s.RefId + ".");
                return def;
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("Build " + s.Key + ": " + ex);
                return null;
            }
        }

        private static void PinObject(object obj)
        {
            if (obj == null) return;
            try { SetMember(obj, "hideFlags", 32); } catch { }
        }

        private void ForceReferenceId(object def, string refId)
        {
            SetMember(def, "referenceId", refId);
            SetMember(def, "initializedHashId", false);
            SetMember(def, "stableHashedId", 0);
            try { var _ = GetMember(def, "StableHashedId"); } catch { }
        }

        private object GetGameContext()
        {
            var t = FindType("Il2CppMonomiPark.SlimeRancher.GameContext") ?? FindTypeBySimpleName("GameContext");
            if (t == null) return null;

            var inst = GetStaticMember(t, "Instance");
            if (inst != null) return inst;

            return FindFirstResource(t);
        }

        private static string OldSlimeAlias(string key)
            => "SlimeDefinition.Custom" + key + "Slime";

        private static string PrimaryPlortId(string key)
            => "IdentifiableType.SR1" + key + "Plort";

        private static string OldPlortAlias(string key)
            => "IdentifiableType.Custom" + key + "SlimePlort";

        private void EnsurePersistenceAlias(object pid, string id)
        {
            if (pid == null || string.IsNullOrWhiteSpace(id)) return;
            var primary = GetMember(pid, "_primaryIndex");
            int index = IndexOfString(primary, id);
            if (index < 0)
            {
                index = CollectionCount(primary);
                AppendMemberCollection(pid, "_primaryIndex", id);
            }
            var rev = GetMember(pid, "_reverseIndex");
            if (!DictContains(rev, id)) DictAdd(rev, id, index);
            else DictSet(rev, id, index);
        }

        private static int IndexOfString(object collection, string value)
        {
            if (collection is IEnumerable en)
            {
                int i = 0;
                foreach (var x in en)
                {
                    if (string.Equals(x?.ToString(), value, StringComparison.Ordinal)) return i;
                    i++;
                }
            }
            return -1;
        }

        private static int CollectionCount(object collection)
        {
            if (collection == null) return 0;
            var v = GetMember(collection, "Count", "Length");
            try { return Convert.ToInt32(v); } catch { }
            int n = 0;
            if (collection is IEnumerable en) foreach (var _ in en) n++;
            return n;
        }

        private object CloneAppearanceFor(Spec spec)
        {
            try
            {
                var baseApps = GetMember(_pinkDef, "AppearancesDefault");
                var baseApp = FirstOf(baseApps);
                if (baseApp == null) return null;

                var app = InstantiateObject(baseApp);
                if (app == null) return null;
                PinObject(app);
                SetMember(app, "name", "SR1_App_" + spec.Key);

                var structs = GetMember(baseApp, "Structures");
                if (structs is IEnumerable en)
                {
                    var cloned = new List<object>();
                    foreach (var bs in en)
                    {
                        if (bs == null) continue;
                        object ns = null;
                        try
                        {
                            var ctor = bs.GetType().GetConstructor(new[] { bs.GetType() });
                            if (ctor != null) ns = ctor.Invoke(new[] { bs });
                        }
                        catch { }
                        if (ns == null) continue;

                        var mats = GetMember(bs, "DefaultMaterials");
                        if (mats is IEnumerable men)
                        {
                            var newMats = new List<object>();
                            foreach (var m in men)
                            {
                                var nm = m == null ? null : InstantiateObject(m);
                                if (nm != null) RecolorMaterial(nm, spec);
                                newMats.Add(nm);
                            }
                            if (mats != null)
                            {
                                var mcol = CreateCollection(mats.GetType(), newMats);
                                if (mcol != null) SetMember(ns, "DefaultMaterials", mcol);
                            }
                        }
                        cloned.Add(ns);
                    }

                    if (cloned.Count > 0 && structs != null)
                    {
                        var scol = CreateCollection(structs.GetType(), cloned);
                        if (scol != null) SetMember(app, "Structures", scol);
                    }
                }
                return app;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Appearance " + spec.Key + ": " + ex.Message);
                return null;
            }
        }

        private object BuildPlortFor(Spec spec)
        {
            try
            {
                var plortType = FindTypeBySimpleName("IdentifiableType");
                if (plortType == null) return null;

                object basePlort = null;
                foreach (var p in FindAllResources(plortType))
                {
                    var n = GetString(p, "name", "Name") ?? "";
                    var r = GetString(p, "ReferenceId", "referenceId") ?? "";
                    if (string.Equals(n, "PinkPlort", StringComparison.OrdinalIgnoreCase) ||
                        r.IndexOf("PinkPlort", StringComparison.OrdinalIgnoreCase) >= 0)
                    { basePlort = p; break; }
                }
                if (basePlort == null) return null;

                var plort = InstantiateObject(basePlort);
                if (plort == null) return null;
                PinObject(plort);
                SetMember(plort, "name", "SR1" + spec.Key + "Plort");
                ForceReferenceId(plort, PrimaryPlortId(spec.Key));

                var l = MakeLocalized("Actor", "sr1slimes.plort." + spec.Key.ToLowerInvariant(), spec.Display + " Plort");
                if (l != null) SetMember(plort, "localizedName", l);

                var basePrefab = GetMember(basePlort, "prefab", "Prefab");
                if (basePrefab != null)
                {
                    var pf = InstantiateObject(basePrefab);
                    if (pf != null)
                    {
                        PinObject(pf);
                        SetMember(pf, "name", "SR1" + spec.Key + "PlortPrefab");
                        foreach (var ia in GetComponentsInChildren(pf, FindTypeBySimpleName("IdentifiableActor")))
                            SetMember(ia, "identType", plort);
                        SetMember(plort, "prefab", pf);
                        Recolor(pf, spec);
                    }
                }

                RegisterIdentifiable(plort, PrimaryPlortId(spec.Key), OldPlortAlias(spec.Key));
                return plort;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Plort " + spec.Key + ": " + ex.Message);
                return null;
            }
        }

        private void RegisterIdentifiable(object ident, string primaryId, params string[] aliases)
        {
            var gc = GetGameContext();
            if (gc == null || ident == null) return;
            var lookup = GetMember(gc, "LookupDirector");
            var asd = GetMember(gc, "AutoSaveDirector");
            if (lookup == null || asd == null) return;

            var ids = new List<string> { primaryId };
            if (aliases != null)
                foreach (var a in aliases)
                    if (!string.IsNullOrWhiteSpace(a) && !ids.Contains(a)) ids.Add(a);

            var map = GetMember(lookup, "_identifiableTypeByRefId");
            var trans = GetMember(asd, "_saveReferenceTranslation");
            var identLookup = trans == null ? null : GetMember(trans, "_identifiableTypeLookup");
            var pid = trans == null ? null : GetMember(trans, "_identifiableTypeToPersistenceId");

            foreach (var id in ids)
            {
                DictSet(map, id, ident);
                DictSet(identLookup, id, ident);
                EnsurePersistenceAlias(pid, id);
            }

            var cfg = GetMember(asd, "_configuration");
            var master = cfg == null ? null : GetMember(cfg, "_identifiableTypes");
            if (master != null) InvokeBest(lookup, "AddIdentifiableTypeToGroup", ident, master);

            var groupType = FindTypeBySimpleName("IdentifiableTypeGroup");
            if (groupType != null)
            {
                var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                    "PlortGroup","VaccablePlortGroup","IdentifiableTypesGroup","PlortsGroup"
                };
                foreach (var g in FindAllResources(groupType))
                {
                    var n = GetString(g, "name", "Name");
                    if (n != null && wanted.Contains(n))
                        InvokeBest(lookup, "AddIdentifiableTypeToGroup", ident, g);
                }
            }
        }

        private void TryWireProduceIdent(object def, object plort)
        {
            try
            {
                var diet = GetMember(def, "Diet");
                if (diet == null) return;
                var old = GetMember(diet, "ProduceIdents");
                if (old == null) return;
                var arr = CreateCollection(old.GetType(), new List<object> { plort });
                if (arr != null) SetMember(diet, "ProduceIdents", arr);
            }
            catch { }
        }

        private void RecolorMaterial(object m, Spec spec)
        {
            if (m == null) return;
            var shaderType = FindType("UnityEngine.Shader");
            var colorType = FindType("UnityEngine.Color");
            if (colorType == null) return;

            object shader = null;
            if (shaderType != null)
            {
                var find = shaderType.GetMethods(BindingFlags.Public|BindingFlags.Static)
                    .FirstOrDefault(x => x.Name == "Find" && x.GetParameters().Length == 1);
                if (find != null)
                {
                    foreach (var n in spec.ShaderNames)
                    {
                        try { shader = find.Invoke(null, new object[]{n}); if (shader != null) break; } catch { }
                    }
                }
            }
            if (shader != null) SetMember(m, "shader", shader);

            var top = MakeColor(colorType, spec.Top);
            var mid = MakeColor(colorType, spec.Mid);
            var bottom = MakeColor(colorType, spec.Bottom);
            SetMatColor(m, "_TopColor", top);
            SetMatColor(m, "_MiddleColor", mid);
            SetMatColor(m, "_BottomColor", bottom);
            SetMatColor(m, "_SpecColor", mid);
            SetMatColor(m, "_Color", mid);
            SetMatColor(m, "_BaseColor", mid);
        }

        private void RegisterSlime(object def, params string[] aliases)
        {
            var gc = GetGameContext();
            if (gc == null) return;

            var refId = GetString(def, "ReferenceId", "referenceId");
            var lookup = GetMember(gc, "LookupDirector");
            var defs = GetMember(gc, "SlimeDefinitions");
            var asd = GetMember(gc, "AutoSaveDirector");

            if (defs != null)
            {
                var map = GetMember(defs, "_slimeDefinitionsByIdentifiable");
                DictAddIfMissing(map, def, def);
                AppendMemberCollection(defs, "Slimes", def);
            }

            var allIds = new List<string>();
            if (!string.IsNullOrEmpty(refId)) allIds.Add(refId);
            if (aliases != null)
                foreach (var a in aliases)
                    if (!string.IsNullOrWhiteSpace(a) && !allIds.Contains(a))
                        allIds.Add(a);

            if (lookup != null)
            {
                var map = GetMember(lookup, "_identifiableTypeByRefId");
                foreach (var id in allIds) DictSet(map, id, def);
            }

            if (lookup != null && asd != null)
            {
                var cfg = GetMember(asd, "_configuration");
                var allGroup = GetMember(cfg, "_identifiableTypes");
                if (allGroup != null) InvokeBest(lookup, "AddIdentifiableTypeToGroup", def, allGroup);

                var trans = GetMember(asd, "_saveReferenceTranslation");
                if (trans != null)
                {
                    var identLookup = GetMember(trans, "_identifiableTypeLookup");
                    var pid = GetMember(trans, "_identifiableTypeToPersistenceId");
                    foreach (var id in allIds)
                    {
                        DictSet(identLookup, id, def);
                        if (pid != null) EnsurePersistenceAlias(pid, id);
                    }
                }
            }

            if (lookup != null)
            {
                var groupType = FindType("Il2Cpp.IdentifiableTypeGroup") ?? FindTypeBySimpleName("IdentifiableTypeGroup");
                if (groupType != null)
                {
                    var wanted = new HashSet<string>(StringComparer.OrdinalIgnoreCase) {
                        "VaccableBaseSlimeGroup","BaseSlimeGroup","SlimesGroup","SmallSlimeGroup",
                        "EdibleSlimeGroup","IdentifiableTypesGroup","SlimesSinkInShallowWaterGroup"
                    };
                    foreach (var g in FindAllResources(groupType))
                    {
                        var n = GetString(g, "name", "Name");
                        if (n != null && wanted.Contains(n))
                            InvokeBest(lookup, "AddIdentifiableTypeToGroup", def, g);
                    }
                }
            }
        }

        private void WirePrefab(object go, object def)
        {
            if (go == null || def == null) return;
            var appType = FindTypeBySimpleName("SlimeAppearanceApplicator");
            var identType = FindTypeBySimpleName("IdentifiableActor");
            var eatType = FindTypeBySimpleName("SlimeEat");

            if (appType != null)
            {
                var c = GetComponent(go, appType);
                if (c != null)
                {
                    SetMember(c, "SlimeDefinition", def);
                    var apps = GetMember(def, "AppearancesDefault");
                    var first = FirstOf(apps);
                    if (first != null) SetMember(c, "Appearance", first);
                }
            }
            if (identType != null)
            {
                var c = GetComponent(go, identType);
                if (c != null) SetMember(c, "identType", def);
            }
            if (eatType != null)
            {
                var c = GetComponent(go, eatType);
                if (c != null) SetMember(c, "SlimeDefinition", def);
            }
        }

        private bool EnsurePedia(Spec s, object def)
        {
            try
            {
                if (_pedia.ContainsKey(s.Key)) return true;

                var entryType = FindType("Il2CppMonomiPark.SlimeRancher.Pedia.IdentifiablePediaEntry") ?? FindTypeBySimpleName("IdentifiablePediaEntry");
                var catType = FindType("Il2CppMonomiPark.SlimeRancher.Pedia.PediaCategory") ?? FindTypeBySimpleName("PediaCategory");
                if (entryType == null || catType == null) return false;

                object pinkEntry = null;
                foreach (var e in FindAllResources(entryType))
                {
                    var ident = GetMember(e, "_identifiableType", "IdentifiableType");
                    var ename = GetString(e, "name", "Name");
                    if (ident != null && _pinkDef != null && (ReferenceEquals(ident,_pinkDef) || ident.Equals(_pinkDef))) { pinkEntry=e; break; }
                    if (pinkEntry == null && string.Equals(ename,"Pink",StringComparison.OrdinalIgnoreCase)) pinkEntry=e;
                }
                if (pinkEntry == null) return false;

                object entry = null;
                var wantedName = "SR1" + s.Key + "Pedia";
                foreach (var e in FindAllResources(entryType))
                {
                    if (string.Equals(GetString(e,"name","Name"), wantedName, StringComparison.OrdinalIgnoreCase))
                    { entry=e; break; }
                }

                if (entry == null)
                {
                    entry = InstantiateObject(pinkEntry);
                    if (entry == null) return false;
                    SetMember(entry, "name", wantedName);
                    PinObject(entry);
                    SetMember(entry, "_identifiableType", def);
                    var title = GetMember(def, "localizedName");
                    if (title != null) SetMember(entry, "_title", title);
                    var desc = MakeLocalized("Actor", "sr1slimes.pedia." + s.Key.ToLowerInvariant(), s.Description);
                    if (desc != null) SetMember(entry, "_description", desc);
                    SetMember(entry, "_isUnlockedInitially", true);
                    ClearMemberCollection(entry, "_details");
                }

                object slimesCategory = null;
                foreach (var c in FindAllResources(catType))
                {
                    if (string.Equals(GetString(c,"name","Name"),"Slimes",StringComparison.OrdinalIgnoreCase))
                    { slimesCategory=c; break; }
                }
                if (slimesCategory == null) return false;

                AppendMemberCollection(slimesCategory, "_items", entry);

                var runtime = InvokeBest(slimesCategory, "GetRuntimeCategory");
                if (runtime != null)
                    AddToLiveCollection(GetMember(runtime, "_items", "Items"), entry);

                var gc = GetGameContext();
                var lookup = gc == null ? null : GetMember(gc, "LookupDirector");
                if (lookup != null)
                    InvokeBest(lookup, "AddPediaEntryToCategory", entry, slimesCategory);

                UnlockPedia(entry, def);

                _pedia[s.Key] = entry;
                LoggerInstance.Msg(s.Display + " adicionado a Slimepedia.");
                return true;
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Pedia " + s.Key + ": " + ex.Message);
                return false;
            }
        }

        private void UnlockPedia(object entry, object def)
        {
            try
            {
                var sceneType = FindType("Il2CppMonomiPark.SlimeRancher.SceneContext") ?? FindTypeBySimpleName("SceneContext");
                if (sceneType == null) return;
                var scene = GetStaticMember(sceneType, "Instance");
                if (scene == null) return;
                var dir = GetMember(scene, "PediaDirector");
                if (dir == null) return;

                foreach (var m in dir.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if (m.Name != "Unlock") continue;
                    var p=m.GetParameters();
                    if (p.Length != 2 || p[1].ParameterType != typeof(bool)) continue;
                    try
                    {
                        if (entry != null && p[0].ParameterType.IsInstanceOfType(entry)) { m.Invoke(dir,new[]{entry,(object)false}); return; }
                        if (def != null && p[0].ParameterType.IsInstanceOfType(def)) { m.Invoke(dir,new[]{def,(object)false}); return; }
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void InjectNaturalSpawns()
        {
            try
            {
                var spawnerType = FindTypeBySimpleName("DirectedSlimeSpawner");
                if (spawnerType == null) return;

                int touched = 0;
                foreach (var spawner in FindAllResources(spawnerType))
                {
                    if (spawner == null) continue;
                    var go = GetMember(spawner, "gameObject");
                    if (go == null) continue;
                    var scene = GetMember(go, "scene");
                    var sceneName = scene == null ? null : GetString(scene, "name");
                    if (string.IsNullOrEmpty(sceneName)) continue;

                    bool radHere =
                        sceneName.IndexOf("zoneGorge_Area4", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sceneName.IndexOf("zoneGorge_Area5", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sceneName.IndexOf("LabValley", StringComparison.OrdinalIgnoreCase) >= 0;

                    bool qmHere =
                        sceneName.IndexOf("zoneLabyrinthTerrarium", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sceneName.IndexOf("zoneRainbowCore", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        sceneName.IndexOf("zoneLabyrinthHub", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (radHere && _defs.TryGetValue("Rad", out var rad))
                        if (AddIdentToSpawner(spawner, rad, 0.35f)) touched++;

                    if (qmHere)
                    {
                        if (_defs.TryGetValue("Quantum", out var quantum))
                            if (AddIdentToSpawner(spawner, quantum, 0.22f)) touched++;
                        if (_defs.TryGetValue("Mosaic", out var mosaic))
                            if (AddIdentToSpawner(spawner, mosaic, 0.22f)) touched++;
                    }
                }

                if (touched > 0)
                    LoggerInstance.Msg("Spawns naturais atualizados em " + touched + " conjunto(s).");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Natural spawn: " + ex.Message);
            }
        }

        private bool AddIdentToSpawner(object spawner, object def, float weight)
        {
            try
            {
                var constraints = GetMember(spawner, "Constraints") as IEnumerable;
                if (constraints == null) return false;
                bool changed = false;

                foreach (var constraint in constraints)
                {
                    if (constraint == null) continue;
                    var slimeSet = GetMember(constraint, "Slimeset");
                    if (slimeSet == null) continue;
                    var members = GetMember(slimeSet, "Members");
                    if (members == null) continue;

                    bool exists = false;
                    Type memberType = null;
                    if (members is IEnumerable en)
                    {
                        foreach (var m in en)
                        {
                            if (m == null) continue;
                            if (memberType == null) memberType = m.GetType();
                            var ident = GetMember(m, "IdentType");
                            if (ident != null && (ReferenceEquals(ident, def) || ident.Equals(def)))
                            {
                                exists = true;
                                break;
                            }
                        }
                    }
                    if (exists) continue;

                    if (memberType == null)
                    {
                        var setType = slimeSet.GetType();
                        memberType = setType.GetNestedType("Member", BindingFlags.Public|BindingFlags.NonPublic);
                    }
                    if (memberType == null) continue;

                    object member = null;
                    try { member = Activator.CreateInstance(memberType); } catch { }
                    if (member == null) continue;

                    SetMember(member, "_prefab", GetMember(def, "prefab", "Prefab"));
                    SetMember(member, "IdentType", def);
                    SetMember(member, "Weight", weight);
                    AppendMemberCollection(slimeSet, "Members", member);
                    changed = true;
                }
                return changed;
            }
            catch { return false; }
        }

        private void Spawn(string key)
        {
            try
            {
                if (!_ready)
                {
                    _ready = EnsureReady();
                    if (!_ready)
                    {
                        Show("Ainda preparando os slimes locais. Tente novamente em 1 segundo.", 3);
                        return;
                    }
                }

                var s = _specs.First(x => x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
                var def = _defs[key];
                var prefab = _prefabs[key];

                object pos, rot;
                GetSpawnTransform(out pos, out rot);

                object go = TryActorSpawn(def, pos, rot);
                if (go == null) go = InstantiateAt(prefab, pos, rot);
                if (go == null)
                {
                    Show("Falha ao spawnar " + s.Display, 4);
                    return;
                }

                TryCall(go, "SetActive", true);
                SetMember(go, "name", s.Display + " [SR1 Standalone]");
                WirePrefab(go, def);

                var appType = FindTypeBySimpleName("SlimeAppearanceApplicator");
                if (appType != null)
                {
                    var a = GetComponent(go, appType);
                    if (a != null) TryCall(a, "ApplyAppearance");
                }

                Recolor(go, s);

                var eatType = FindTypeBySimpleName("SlimeEat");
                if (eatType != null)
                {
                    var e = GetComponent(go,eatType);
                    if (e != null) TryCall(e,"CalculateAllEats");
                }

                if (!_pediaReady) _pediaReady = TryInstallAllPedia();
                Show(s.Display + " spawnado!   8 Rad | 9 Quantum | 0 Mosaic", 3);
                LoggerInstance.Msg(s.Display + " spawnado.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Error("Spawn " + key + ": " + ex);
                Show("Erro no spawn: " + ex.Message, 5);
            }
        }

        private object TryActorSpawn(object def, object pos, object rot)
        {
            try
            {
                var sceneType = FindType("Il2CppMonomiPark.SlimeRancher.SceneContext") ?? FindTypeBySimpleName("SceneContext");
                var scene = sceneType == null ? null : GetStaticMember(sceneType, "Instance");
                if (scene == null) return null;
                var modelSvc = GetMember(scene, "GameModel");
                var registry = GetMember(scene, "RegionRegistry");
                var group = registry == null ? null : GetMember(registry, "CurrentSceneGroup");
                if (modelSvc == null || group == null) return null;

                object model = null;
                foreach (var m in modelSvc.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if (m.Name != "InstantiateActorModel") continue;
                    var p=m.GetParameters();
                    if (p.Length != 5) continue;
                    try { model=m.Invoke(modelSvc,new[]{def,group,pos,rot,(object)false}); if(model!=null) break; } catch { }
                }
                if (model == null) return null;

                var helpers = FindTypeBySimpleName("InstantiationHelpers");
                if (helpers == null) return null;
                foreach (var m in helpers.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static))
                {
                    if (m.Name != "InstantiateActorFromModel") continue;
                    var p=m.GetParameters();
                    if (p.Length != 1) continue;
                    try { return m.Invoke(null,new[]{model}); } catch { }
                }
            }
            catch { }
            return null;
        }

        private void GetSpawnTransform(out object pos, out object rot)
        {
            var v3 = FindType("UnityEngine.Vector3");
            var quat = FindType("UnityEngine.Quaternion");
            pos = Activator.CreateInstance(v3, new object[]{0f,5f,0f});
            rot = GetStaticMember(quat, "identity");

            try
            {
                var camType=FindType("UnityEngine.Camera");
                var cam=GetStaticMember(camType, "main");
                if(cam==null) return;
                var tr=GetMember(cam,"transform");
                var p=GetMember(tr,"position");
                var f=GetMember(tr,"forward");
                float px=Num(GetMember(p,"x")), py=Num(GetMember(p,"y")), pz=Num(GetMember(p,"z"));
                float fx=Num(GetMember(f,"x")), fy=Num(GetMember(f,"y")), fz=Num(GetMember(f,"z"));
                pos=Activator.CreateInstance(v3,new object[]{px+fx*3f,py+fy*3f+0.5f,pz+fz*3f});
            }
            catch { }
        }

        private void Recolor(object go, Spec s)
        {
            var rendererType = FindType("UnityEngine.Renderer");
            var materialType = FindType("UnityEngine.Material");
            var shaderType = FindType("UnityEngine.Shader");
            var colorType = FindType("UnityEngine.Color");
            if (rendererType == null || materialType == null || colorType == null) return;

            object shader = null;
            if (shaderType != null)
            {
                var find=shaderType.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(m=>m.Name=="Find"&&m.GetParameters().Length==1);
                if(find!=null)
                    foreach(var n in s.ShaderNames)
                    {
                        try { shader=find.Invoke(null,new object[]{n}); if(shader!=null) break; } catch { }
                    }
            }

            var top=MakeColor(colorType,s.Top);
            var mid=MakeColor(colorType,s.Mid);
            var bottom=MakeColor(colorType,s.Bottom);
            var white=MakeColor(colorType,new[]{1f,1f,1f,1f});

            foreach(var r in GetComponentsInChildren(go,rendererType))
            {
                var mats=GetMember(r,"materials","Materials") as IEnumerable;
                if(mats==null) continue;
                foreach(var m in mats)
                {
                    if(m==null) continue;
                    bool body=HasMatProp(m,"_TopColor")||HasMatProp(m,"_MiddleColor")||HasMatProp(m,"_BottomColor");
                    if(!body) continue;
                    RecolorMaterial(m, s);
                }
            }
        }

        private static object MakeColor(Type colorType,float[] c)
            => Activator.CreateInstance(colorType,new object[]{c[0],c[1],c[2],c[3]});

        private static bool HasMatProp(object mat,string p)
        {
            try { var m=mat.GetType().GetMethod("HasProperty",new[]{typeof(string)}); return m!=null && (bool)m.Invoke(mat,new object[]{p}); } catch { return false; }
        }

        private static void SetMatColor(object mat,string p,object color)
        {
            try
            {
                if(!HasMatProp(mat,p)) return;
                var m=mat.GetType().GetMethods().FirstOrDefault(x=>x.Name=="SetColor"&&x.GetParameters().Length==2&&x.GetParameters()[0].ParameterType==typeof(string));
                m?.Invoke(mat,new[]{(object)p,color});
            }
            catch { }
        }

        private object MakeLocalized(string tableName, string key, string text)
        {
            try
            {
                var settings=FindType("UnityEngine.Localization.Settings.LocalizationSettings");
                if(settings==null) return null;
                var db=GetStaticMember(settings, "StringDatabase");
                if(db==null) return null;

                object table=null;
                foreach(var m in db.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if(m.Name!="GetTable") continue;
                    var p=m.GetParameters();
                    if(p.Length<1) continue;
                    try
                    {
                        var args=new object[p.Length];
                        args[0]=ConvertArg(tableName,p[0].ParameterType);
                        for(int i=1;i<p.Length;i++) args[i]=p[i].HasDefaultValue?p[i].DefaultValue:null;
                        table=m.Invoke(db,args);
                        if(table!=null) break;
                    }
                    catch { }
                }
                if(table==null) return null;

                object entry=null;
                foreach(var m in table.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if(m.Name!="AddEntry") continue;
                    var p=m.GetParameters();
                    if(p.Length<2) continue;
                    try
                    {
                        var args=new object[p.Length];
                        args[0]=ConvertArg(key,p[0].ParameterType);
                        args[1]=ConvertArg(text,p[1].ParameterType);
                        for(int i=2;i<p.Length;i++) args[i]=p[i].HasDefaultValue?p[i].DefaultValue:null;
                        entry=m.Invoke(table,args);
                        if(entry!=null) break;
                    }
                    catch { }
                }
                if(entry==null)
                {
                    // key may already exist: try GetEntry
                    foreach(var m in table.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                    {
                        if(m.Name!="GetEntry") continue;
                        var p=m.GetParameters(); if(p.Length!=1) continue;
                        try { entry=m.Invoke(table,new[]{ConvertArg(key,p[0].ParameterType)}); if(entry!=null) break; } catch { }
                    }
                }
                if(entry==null) return null;

                var tableRef=GetMember(table,"TableCollectionName","TableCollectionNameReference");
                var keyId=GetMember(entry,"KeyId","Key");
                var lsType=FindType("UnityEngine.Localization.LocalizedString");
                if(lsType==null) return null;

                foreach(var ctor in lsType.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    var p=ctor.GetParameters();
                    if(p.Length!=2) continue;
                    try
                    {
                        var a0=ConvertArg(tableRef,p[0].ParameterType);
                        var a1=ConvertArg(keyId,p[1].ParameterType);
                        return ctor.Invoke(new[]{a0,a1});
                    }
                    catch { }
                }

                var ls=Activator.CreateInstance(lsType);
                if(ls!=null)
                {
                    SetMember(ls,"TableReference",tableRef);
                    SetMember(ls,"TableEntryReference",keyId);
                }
                return ls;
            }
            catch(Exception ex)
            {
                LoggerInstance.Warning("Localization: "+ex.Message);
                return null;
            }
        }

        private static object ConvertArg(object value, Type target)
        {
            if(value==null) return null;
            if(target.IsInstanceOfType(value)) return value;
            if(target.IsEnum)
            {
                try { return Enum.ToObject(target, Convert.ToInt32(value)); } catch { }
            }
            try
            {
                foreach(var m in target.GetMethods(BindingFlags.Public|BindingFlags.Static))
                {
                    if(m.Name!="op_Implicit"&&m.Name!="op_Explicit") continue;
                    var p=m.GetParameters();
                    if(p.Length==1 && p[0].ParameterType.IsInstanceOfType(value))
                        return m.Invoke(null,new[]{value});
                }
            }
            catch { }
            try
            {
                foreach(var ctor in target.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    var p=ctor.GetParameters();
                    if(p.Length!=1) continue;
                    try
                    {
                        object v=value;
                        if(!p[0].ParameterType.IsInstanceOfType(v))
                            v=Convert.ChangeType(value,p[0].ParameterType);
                        return ctor.Invoke(new[]{v});
                    }
                    catch { }
                }
            }
            catch { }
            try { return Convert.ChangeType(value,target); } catch { return value; }
        }

        private bool KeyPressed(string primary,string secondary,string oldPrimary,string oldSecondary)
        {
            try
            {
                var kt=FindType("UnityEngine.InputSystem.Keyboard");
                var kb=kt==null?null:GetStaticMember(kt, "current");
                if(kb!=null && (ControlPressed(kb,primary)||ControlPressed(kb,secondary))) return true;
            }
            catch { }

            try
            {
                var input=FindType("UnityEngine.Input");
                var keyCode=FindType("UnityEngine.KeyCode");
                if(input!=null&&keyCode!=null)
                {
                    var m=input.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(x=>x.Name=="GetKeyDown"&&x.GetParameters().Length==1&&x.GetParameters()[0].ParameterType==keyCode);
                    if(m!=null)
                    {
                        if((bool)m.Invoke(null,new[]{Enum.Parse(keyCode,oldPrimary)})) return true;
                        if((bool)m.Invoke(null,new[]{Enum.Parse(keyCode,oldSecondary)})) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private static bool ControlPressed(object kb,string prop)
        {
            try
            {
                var c=GetMember(kb,prop);
                var v=c==null?null:GetMember(c,"wasPressedThisFrame");
                return v is bool b&&b;
            }
            catch { return false; }
        }

        private void Show(string text,int seconds)
        {
            _status=text;
            _statusUntil=DateTime.UtcNow.AddSeconds(seconds);
        }

        private bool EnsureGui()
        {
            if(_guiLabel!=null) return true;
            _rectType=FindType("UnityEngine.Rect");
            _guiType=FindType("UnityEngine.GUI");
            if(_rectType==null||_guiType==null) return false;
            _rectCtor=_rectType.GetConstructor(new[]{typeof(float),typeof(float),typeof(float),typeof(float)});
            _guiBox=_guiType.GetMethod("Box",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            _guiLabel=_guiType.GetMethod("Label",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            return _rectCtor!=null&&_guiLabel!=null;
        }
        private object Rect(float x,float y,float w,float h)=>_rectCtor.Invoke(new object[]{x,y,w,h});

        // ---------------- reflection helpers ----------------

        private static Type FindType(string full)
        {
            if(string.IsNullOrEmpty(full)) return null;
            var t=Type.GetType(full);
            if(t!=null) return t;
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try { t=a.GetType(full,false); if(t!=null) return t; } catch { }
            }
            return null;
        }

        private static Type FindTypeBySimpleName(string name)
        {
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach(var t in a.GetTypes())
                        if(t!=null&&t.Name==name) return t;
                }
                catch(ReflectionTypeLoadException ex)
                {
                    foreach(var t in ex.Types)
                        if(t!=null&&t.Name==name) return t;
                }
                catch { }
            }
            return null;
        }

        private static object GetMember(object obj, params string[] names)
            => obj==null?null:GetMemberCore(obj.GetType(),obj,names);

        private static object GetStaticMember(Type type, params string[] names)
            => GetMemberCore(type,null,names);

        private static object GetMemberCore(Type type, object instance, params string[] names)
        {
            if(type==null) return null;
            const BindingFlags f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            foreach(var name in names)
            {
                try { var p=type.GetProperty(name,f); if(p!=null) return p.GetValue(instance); } catch { }
                try { var fi=type.GetField(name,f); if(fi!=null) return fi.GetValue(instance); } catch { }
            }
            return null;
        }

        private static bool SetMember(object obj,string name,object value)
        {
            if(obj==null) return false;
            return SetMember(obj.GetType(),obj,name,value);
        }

        private static bool SetMember(Type type,object instance,string name,object value)
        {
            const BindingFlags f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            try
            {
                var p=type.GetProperty(name,f);
                if(p!=null&&p.CanWrite){p.SetValue(instance,ConvertArg(value,p.PropertyType));return true;}
            }catch{}
            try
            {
                var fi=type.GetField(name,f);
                if(fi!=null){fi.SetValue(instance,ConvertArg(value,fi.FieldType));return true;}
            }catch{}
            return false;
        }

        private static string GetString(object obj,params string[] names)
        {
            var v=GetMember(obj,names);
            return v==null?null:v.ToString();
        }

        private static float Num(object o)
        {
            if(o==null) return 0;
            try{return Convert.ToSingle(o);}catch{return 0;}
        }

        private static object InstantiateObject(object original)
        {
            if(original==null)return null;
            var uo=FindType("UnityEngine.Object");
            if(uo==null)return null;
            foreach(var m in uo.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod)continue;
                var p=m.GetParameters();
                if(p.Length!=1)continue;
                try{return m.Invoke(null,new[]{original});}catch{}
            }
            return null;
        }

        private static object InstantiateAt(object original,object pos,object rot)
        {
            if(original==null)return null;
            var uo=FindType("UnityEngine.Object");
            foreach(var m in uo.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod)continue;
                var p=m.GetParameters();
                if(p.Length!=3)continue;
                if(p[1].ParameterType.Name!="Vector3"||p[2].ParameterType.Name!="Quaternion")continue;
                try{return m.Invoke(null,new[]{original,pos,rot});}catch{}
            }
            return null;
        }

        private static object GetComponent(object go,Type componentType)
        {
            if(go==null||componentType==null)return null;

            foreach(var m in go.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="GetComponent" || !m.IsGenericMethodDefinition) continue;
                if(m.GetGenericArguments().Length!=1 || m.GetParameters().Length!=0) continue;
                try { return m.MakeGenericMethod(componentType).Invoke(go,null); } catch { }
            }

            foreach(var m in go.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="GetComponent"||m.IsGenericMethod)continue;
                var p=m.GetParameters();
                if(p.Length!=1)continue;
                try{return m.Invoke(go,new[]{ConvertArg(componentType,p[0].ParameterType)});}catch{}
            }
            return null;
        }

        private static IEnumerable<object> GetComponentsInChildren(object go,Type componentType)
        {
            if(go==null||componentType==null)yield break;
            foreach(var m in go.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="GetComponentsInChildren")continue;
                if(!m.IsGenericMethod)
                {
                    var p=m.GetParameters();
                    if(p.Length==2&&p[0].ParameterType==typeof(Type)&&p[1].ParameterType==typeof(bool))
                    {
                        object arr=null;try{arr=m.Invoke(go,new object[]{componentType,true});}catch{}
                        if(arr is IEnumerable en)foreach(var x in en)if(x!=null)yield return x;
                        yield break;
                    }
                }
            }
            foreach(var m in go.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="GetComponentsInChildren"||!m.IsGenericMethodDefinition)continue;
                var p=m.GetParameters();
                object arr=null;
                try
                {
                    var gm=m.MakeGenericMethod(componentType);
                    if(p.Length==1&&p[0].ParameterType==typeof(bool)) arr=gm.Invoke(go,new object[]{true});
                    else if(p.Length==0) arr=gm.Invoke(go,null);
                }
                catch{}
                if(arr is IEnumerable en)
                {
                    foreach(var x in en)if(x!=null)yield return x;
                    yield break;
                }
            }
        }

        private static IEnumerable<object> FindAllResources(Type type)
        {
            if(type==null) yield break;
            var r=FindType("UnityEngine.Resources");
            if(r==null) yield break;

            // Unity's IL2CPP wrappers expose the generic API reliably.
            foreach(var m in r.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="FindObjectsOfTypeAll" || !m.IsGenericMethodDefinition) continue;
                if(m.GetGenericArguments().Length!=1 || m.GetParameters().Length!=0) continue;
                object arr=null;
                try { arr=m.MakeGenericMethod(type).Invoke(null,null); } catch { }
                if(arr is IEnumerable en)
                {
                    foreach(var x in en) if(x!=null) yield return x;
                    yield break;
                }
            }

            // Fallback for any non-generic overload exposed by the wrapper.
            foreach(var m in r.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="FindObjectsOfTypeAll" || m.IsGenericMethod) continue;
                var p=m.GetParameters();
                if(p.Length!=1) continue;

                object arr=null;
                try
                {
                    var arg=ConvertArg(type,p[0].ParameterType);
                    arr=m.Invoke(null,new[]{arg});
                }
                catch { }

                if(arr is IEnumerable en)
                {
                    foreach(var x in en) if(x!=null) yield return x;
                    yield break;
                }
            }
        }

        private static object FindFirstResource(Type type)
        {
            foreach(var x in FindAllResources(type)) return x;
            return null;
        }

        private static object FirstOf(object collection)
        {
            if(collection is IEnumerable en)foreach(var x in en)if(x!=null)return x;
            return null;
        }

        private static object InvokeBest(object obj,string name,params object[] args)
        {
            if(obj==null)return null;
            const BindingFlags f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            foreach(var m in obj.GetType().GetMethods(f))
            {
                if(m.Name!=name)continue;
                var p=m.GetParameters();
                if(p.Length!=args.Length)continue;
                try
                {
                    var converted=new object[args.Length];
                    for(int i=0;i<args.Length;i++)converted[i]=ConvertArg(args[i],p[i].ParameterType);
                    return m.Invoke(obj,converted);
                }catch{}
            }
            return null;
        }

        private static object TryCall(object obj,string name,params object[] args)=>InvokeBest(obj,name,args);

        private static bool DictContains(object dict,object key)
        {
            if(dict==null)return false;
            try
            {
                var m=dict.GetType().GetMethods().FirstOrDefault(x=>x.Name=="ContainsKey"&&x.GetParameters().Length==1);
                if(m==null)return false;
                var p=m.GetParameters();
                return (bool)m.Invoke(dict,new[]{ConvertArg(key,p[0].ParameterType)});
            }catch{return false;}
        }

        private static bool DictSet(object dict,object key,object value)
        {
            if(dict==null)return false;
            try
            {
                var item=dict.GetType().GetProperty("Item",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if(item!=null&&item.CanWrite)
                {
                    var idx=item.GetIndexParameters();
                    object k=idx.Length>0?ConvertArg(key,idx[0].ParameterType):key;
                    object v=ConvertArg(value,item.PropertyType);
                    bool existed=DictContains(dict,key);
                    item.SetValue(dict,v,new[]{k});
                    return !existed;
                }
            }catch{}
            if(!DictContains(dict,key)){DictAdd(dict,key,value);return true;}
            return false;
        }

        private static void DictAddIfMissing(object dict,object key,object value)
        {
            if(dict==null||DictContains(dict,key))return;
            DictAdd(dict,key,value);
        }

        private static void DictAdd(object dict,object key,object value)
        {
            if(dict==null)return;
            foreach(var m in dict.GetType().GetMethods())
            {
                if(m.Name!="Add"||m.GetParameters().Length!=2)continue;
                try
                {
                    var p=m.GetParameters();
                    m.Invoke(dict,new[]{ConvertArg(key,p[0].ParameterType),ConvertArg(value,p[1].ParameterType)});
                    return;
                }catch{}
            }
        }

        private static int DictCount(object dict)
        {
            if(dict==null)return 0;
            var v=GetMember(dict,"Count");
            try{return Convert.ToInt32(v);}catch{return 0;}
        }

        private static void AppendMemberCollection(object owner,string member,object value)
        {
            if(owner==null)return;
            var current=GetMember(owner,member);
            if(current==null)return;
            if(CollectionContains(current,value))return;

            if(TryCollectionAdd(current,value))return;

            var vals=new List<object>();
            if(current is IEnumerable en)foreach(var x in en)vals.Add(x);
            vals.Add(value);
            var repl=CreateCollection(current.GetType(),vals);
            if(repl!=null)SetMember(owner,member,repl);
        }

        private static void ClearMemberCollection(object owner,string member)
        {
            if(owner==null)return;
            var current=GetMember(owner,member);
            if(current==null)return;
            var repl=CreateCollection(current.GetType(),new List<object>());
            if(repl!=null)SetMember(owner,member,repl);
        }

        private static void AddToLiveCollection(object collection,object value)
        {
            if(collection==null||value==null)return;
            if(CollectionContains(collection,value))return;
            TryCollectionAdd(collection,value);
        }

        private static bool CollectionContains(object collection,object value)
        {
            if(collection==null)return false;
            try
            {
                var m=collection.GetType().GetMethods().FirstOrDefault(x=>x.Name=="Contains"&&x.GetParameters().Length==1);
                if(m!=null)return (bool)m.Invoke(collection,new[]{value});
            }catch{}
            if(collection is IEnumerable en)foreach(var x in en)if(x!=null&&(ReferenceEquals(x,value)||x.Equals(value)))return true;
            return false;
        }

        private static bool TryCollectionAdd(object collection,object value)
        {
            if(collection==null)return false;
            foreach(var m in collection.GetType().GetMethods())
            {
                if(m.Name!="Add"||m.GetParameters().Length!=1)continue;
                try{m.Invoke(collection,new[]{ConvertArg(value,m.GetParameters()[0].ParameterType)});return true;}catch{}
            }
            return false;
        }

        private static object CreateCollection(Type t,List<object> vals)
        {
            if(t==null)return null;
            try
            {
                if(t.IsArray)
                {
                    var et=t.GetElementType();
                    var arr=Array.CreateInstance(et,vals.Count);
                    for(int i=0;i<vals.Count;i++)arr.SetValue(ConvertArg(vals[i],et),i);
                    return arr;
                }

                object obj=null;
                foreach(var arg in new object[]{vals.Count,(long)vals.Count})
                {
                    try{obj=Activator.CreateInstance(t,new[]{arg});if(obj!=null)break;}catch{}
                }
                if(obj==null)return null;

                var item=t.GetProperty("Item",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if(item!=null&&item.CanWrite)
                {
                    var ix=item.GetIndexParameters();
                    for(int i=0;i<vals.Count;i++)
                    {
                        object idx=(ix.Length>0&&ix[0].ParameterType==typeof(long))?(object)(long)i:i;
                        try{item.SetValue(obj,ConvertArg(vals[i],item.PropertyType),new[]{idx});}catch{}
                    }
                    return obj;
                }

                for(int i=0;i<vals.Count;i++)TryCollectionAdd(obj,vals[i]);
                return obj;
            }catch{return null;}
        }
    }
}
