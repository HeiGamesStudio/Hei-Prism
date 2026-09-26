using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1SaveRecovery.ModEntry), "SR1 Slimes Save Recovery", "0.6.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1SaveRecovery
{
    public sealed class ModEntry : MelonMod
    {
        private static readonly string[] SlimeAliases =
        {
            "SlimeDefinition.SR1Rad",
            "SlimeDefinition.SR1Quantum",
            "SlimeDefinition.SR1Mosaic",
            "SlimeDefinition.CustomRadSlime",
            "SlimeDefinition.CustomQuantumSlime",
            "SlimeDefinition.CustomMosaicSlime"
        };

        private static readonly string[] PlortAliases =
        {
            "IdentifiableType.CustomRadSlimePlort",
            "IdentifiableType.CustomQuantumSlimePlort",
            "IdentifiableType.CustomMosaicSlimePlort"
        };

        private int _tick;
        private bool _installed;
        private string _status = "RECOVERY: preparando aliases do save...";
        private DateTime _until = DateTime.UtcNow.AddSeconds(60);

        private Type _rectType, _guiType;
        private ConstructorInfo _rectCtor;
        private MethodInfo _box, _label;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("SR1 Save Recovery v0.6 iniciado.");
            LoggerInstance.Msg("Esta build NAO cria novos IDs. Ela recupera saves feitos pelas builds antigas.");
            TryInstallRecoveryAliases();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            TryInstallRecoveryAliases();
        }

        public override void OnUpdate()
        {
            _tick++;
            if (!_installed || _tick % 120 == 0)
                TryInstallRecoveryAliases();
        }

        public override void OnGUI()
        {
            try
            {
                if (DateTime.UtcNow > _until) return;
                if (!EnsureGui()) return;
                _box?.Invoke(null, new[] { Rect(18f,18f,650f,58f), "" });
                _label?.Invoke(null, new[] { Rect(31f,32f,620f,30f), _status });
            }
            catch { }
        }

        private void TryInstallRecoveryAliases()
        {
            try
            {
                var gc = GetGameContext();
                if (gc == null)
                {
                    Show("RECOVERY: aguardando GameContext no menu...", 4);
                    return;
                }

                var pink = FindIdent(gc, true);
                var pinkPlort = FindIdent(gc, false);
                if (pink == null || pinkPlort == null)
                {
                    Show("RECOVERY: aguardando Pink Slime/Pink Plort...", 4);
                    return;
                }

                int added = 0;
                foreach (var id in SlimeAliases)
                    if (InstallAlias(gc, id, pink)) added++;
                foreach (var id in PlortAliases)
                    if (InstallAlias(gc, id, pinkPlort)) added++;

                _installed = VerifyAliases(gc);
                if (_installed)
                {
                    _status = "RECOVERY PRONTA — pode abrir o save. IDs antigos serão carregados como Pink temporariamente.";
                    _until = DateTime.UtcNow.AddSeconds(45);
                    if (added > 0)
                        LoggerInstance.Msg("Recovery aliases instalados/atualizados: " + added);
                }
                else
                {
                    Show("RECOVERY: tabela de save ainda não está pronta. Aguarde no menu.", 5);
                }
            }
            catch (Exception ex)
            {
                Show("RECOVERY erro: " + ex.Message, 8);
                LoggerInstance.Error("Recovery install: " + ex);
            }
        }

        private bool InstallAlias(object gc, string refId, object ident)
        {
            bool changed = false;
            var lookup = GetMember(gc, "LookupDirector");
            var asd = GetMember(gc, "AutoSaveDirector");
            if (lookup == null || asd == null) return false;

            var lookupMap = GetMember(lookup, "_identifiableTypeByRefId");
            changed |= DictSet(lookupMap, refId, ident);

            var cfg = GetMember(asd, "_configuration");
            var masterGroup = cfg == null ? null : GetMember(cfg, "_identifiableTypes");
            if (masterGroup != null)
                InvokeBest(lookup, "AddIdentifiableTypeToGroup", ident, masterGroup);

            var translation = GetMember(asd, "_saveReferenceTranslation");
            if (translation == null) return changed;

            var identLookup = GetMember(translation, "_identifiableTypeLookup");
            changed |= DictSet(identLookup, refId, ident);

            var pid = GetMember(translation, "_identifiableTypeToPersistenceId");
            if (pid == null) return changed;

            var primary = GetMember(pid, "_primaryIndex");
            int index = IndexOf(primary, refId);
            if (index < 0)
            {
                index = CollectionCount(primary);
                var replacement = AppendStringCollection(primary, refId);
                if (replacement != null)
                {
                    SetMember(pid, "_primaryIndex", replacement);
                    changed = true;
                }
            }

            var reverse = GetMember(pid, "_reverseIndex");
            if (!DictContains(reverse, refId))
            {
                DictAdd(reverse, refId, index);
                changed = true;
            }
            else
            {
                DictSet(reverse, refId, index);
            }

            return changed;
        }

        private bool VerifyAliases(object gc)
        {
            try
            {
                var asd = GetMember(gc, "AutoSaveDirector");
                var tr = asd == null ? null : GetMember(asd, "_saveReferenceTranslation");
                var lookup = tr == null ? null : GetMember(tr, "_identifiableTypeLookup");
                var pid = tr == null ? null : GetMember(tr, "_identifiableTypeToPersistenceId");
                var primary = pid == null ? null : GetMember(pid, "_primaryIndex");
                var reverse = pid == null ? null : GetMember(pid, "_reverseIndex");

                foreach (var id in SlimeAliases.Concat(PlortAliases))
                {
                    if (!DictContains(lookup, id)) return false;
                    if (IndexOf(primary, id) < 0) return false;
                    if (!DictContains(reverse, id)) return false;
                }
                return true;
            }
            catch { return false; }
        }

        private object FindIdent(object gc, bool slime)
        {
            if (slime)
            {
                var defs = GetMember(gc, "SlimeDefinitions");
                var slimes = defs == null ? null : GetMember(defs, "Slimes");
                if (slimes is IEnumerable en)
                {
                    foreach (var d in en)
                    {
                        if (d == null) continue;
                        var name = GetString(d, "name", "Name");
                        var rid = GetString(d, "ReferenceId", "referenceId");
                        if (Eq(name,"Pink") || Eq(name,"PinkSlime") || Eq(rid,"SlimeDefinition.Pink"))
                            return d;
                    }
                }
            }

            var typeName = slime ? "SlimeDefinition" : "IdentifiableType";
            var t = FindTypeBySimpleName(typeName);
            if (t != null)
            {
                foreach (var o in FindAllResources(t))
                {
                    if (o == null) continue;
                    var name = GetString(o, "name", "Name") ?? "";
                    var rid = GetString(o, "ReferenceId", "referenceId") ?? "";
                    if (slime)
                    {
                        if (Eq(name,"Pink") || Eq(name,"PinkSlime") || Eq(rid,"SlimeDefinition.Pink"))
                            return o;
                    }
                    else
                    {
                        if (Eq(name,"PinkPlort") || rid.IndexOf("PinkPlort", StringComparison.OrdinalIgnoreCase) >= 0)
                            return o;
                    }
                }
            }
            return null;
        }

        private static bool Eq(string a,string b) => string.Equals(a,b,StringComparison.OrdinalIgnoreCase);

        private object GetGameContext()
        {
            var t = FindType("Il2CppMonomiPark.SlimeRancher.GameContext") ?? FindTypeBySimpleName("GameContext");
            if (t == null) return null;
            var inst = GetStaticMember(t, "Instance");
            if (inst != null) return inst;
            foreach (var o in FindAllResources(t)) return o;
            return null;
        }

        private static int IndexOf(object collection, string text)
        {
            if (collection is IEnumerable en)
            {
                int i=0;
                foreach (var x in en)
                {
                    if (string.Equals(x?.ToString(), text, StringComparison.Ordinal)) return i;
                    i++;
                }
            }
            return -1;
        }

        private static int CollectionCount(object collection)
        {
            if (collection == null) return 0;
            var c = GetMember(collection, "Count", "Length");
            try { return Convert.ToInt32(c); } catch { }
            int n=0;
            if (collection is IEnumerable en) foreach(var _ in en) n++;
            return n;
        }

        private static object AppendStringCollection(object current, string value)
        {
            if (current == null) return null;
            var vals = new List<string>();
            if (current is IEnumerable en)
                foreach (var x in en) vals.Add(x?.ToString());

            if (!vals.Contains(value)) vals.Add(value);

            var t = current.GetType();
            try
            {
                if (t.IsArray)
                {
                    var arr = Array.CreateInstance(t.GetElementType(), vals.Count);
                    for (int i=0;i<vals.Count;i++) arr.SetValue(vals[i],i);
                    return arr;
                }

                object obj = null;
                foreach (var len in new object[]{ vals.Count, (long)vals.Count })
                {
                    try { obj = Activator.CreateInstance(t, new[]{len}); if (obj != null) break; } catch { }
                }
                if (obj == null) return null;

                var item = t.GetProperty("Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (item != null && item.CanWrite)
                {
                    var idxType = item.GetIndexParameters().FirstOrDefault()?.ParameterType ?? typeof(int);
                    for (int i=0;i<vals.Count;i++)
                    {
                        object idx = idxType == typeof(long) ? (object)(long)i : i;
                        item.SetValue(obj, ConvertArg(vals[i], item.PropertyType), new[]{idx});
                    }
                    return obj;
                }
            }
            catch { }
            return null;
        }

        private static bool DictContains(object dict, object key)
        {
            if (dict == null) return false;
            try
            {
                var m = dict.GetType().GetMethods().FirstOrDefault(x=>x.Name=="ContainsKey"&&x.GetParameters().Length==1);
                if (m == null) return false;
                return (bool)m.Invoke(dict,new[]{ConvertArg(key,m.GetParameters()[0].ParameterType)});
            }
            catch { return false; }
        }

        private static bool DictSet(object dict, object key, object value)
        {
            if (dict == null) return false;
            try
            {
                var item = dict.GetType().GetProperty("Item", BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if (item != null && item.CanWrite)
                {
                    var idx = item.GetIndexParameters();
                    var k = idx.Length>0 ? ConvertArg(key,idx[0].ParameterType) : key;
                    var v = ConvertArg(value,item.PropertyType);
                    bool existed = DictContains(dict,key);
                    item.SetValue(dict,v,new[]{k});
                    return !existed;
                }
            }
            catch { }

            if (!DictContains(dict,key))
            {
                DictAdd(dict,key,value);
                return true;
            }
            return false;
        }

        private static void DictAdd(object dict, object key, object value)
        {
            if (dict == null) return;
            foreach(var m in dict.GetType().GetMethods())
            {
                if(m.Name!="Add"||m.GetParameters().Length!=2) continue;
                try
                {
                    var p=m.GetParameters();
                    m.Invoke(dict,new[]{ConvertArg(key,p[0].ParameterType),ConvertArg(value,p[1].ParameterType)});
                    return;
                }
                catch { }
            }
        }

        private static object InvokeBest(object obj,string name,params object[] args)
        {
            if(obj==null)return null;
            foreach(var m in obj.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))
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
                    if(m.Name!="op_Implicit"&&m.Name!="op_Explicit")continue;
                    var p=m.GetParameters();
                    if(p.Length==1&&p[0].ParameterType.IsInstanceOfType(value))
                        return m.Invoke(null,new[]{value});
                }
            }catch{}
            try { return Convert.ChangeType(value,target); } catch { return value; }
        }

        private static Type FindType(string full)
        {
            var t=Type.GetType(full);
            if(t!=null)return t;
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try{t=a.GetType(full,false);if(t!=null)return t;}catch{}
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
                        if(t!=null&&t.Name==name)return t;
                }
                catch(ReflectionTypeLoadException ex)
                {
                    foreach(var t in ex.Types)
                        if(t!=null&&t.Name==name)return t;
                }
                catch{}
            }
            return null;
        }

        private static object GetMember(object obj,params string[] names)
            => obj==null?null:GetMemberCore(obj.GetType(),obj,names);

        private static object GetStaticMember(Type t,params string[] names)
            => GetMemberCore(t,null,names);

        private static object GetMemberCore(Type t,object instance,params string[] names)
        {
            if(t==null)return null;
            var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            foreach(var n in names)
            {
                try{var p=t.GetProperty(n,flags);if(p!=null)return p.GetValue(instance);}catch{}
                try{var f=t.GetField(n,flags);if(f!=null)return f.GetValue(instance);}catch{}
            }
            return null;
        }

        private static bool SetMember(object obj,string name,object value)
        {
            if(obj==null)return false;
            var t=obj.GetType();
            var flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            try
            {
                var p=t.GetProperty(name,flags);
                if(p!=null&&p.CanWrite){p.SetValue(obj,ConvertArg(value,p.PropertyType));return true;}
            }catch{}
            try
            {
                var f=t.GetField(name,flags);
                if(f!=null){f.SetValue(obj,ConvertArg(value,f.FieldType));return true;}
            }catch{}
            return false;
        }

        private static string GetString(object obj,params string[] names)
        {
            var v=GetMember(obj,names);
            return v?.ToString();
        }

        private static IEnumerable<object> FindAllResources(Type type)
        {
            if(type==null)yield break;
            var r=FindType("UnityEngine.Resources");
            if(r==null)yield break;

            foreach(var m in r.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="FindObjectsOfTypeAll"||!m.IsGenericMethodDefinition)continue;
                if(m.GetGenericArguments().Length!=1||m.GetParameters().Length!=0)continue;
                object arr=null;try{arr=m.MakeGenericMethod(type).Invoke(null,null);}catch{}
                if(arr is IEnumerable en)
                {
                    foreach(var x in en)if(x!=null)yield return x;
                    yield break;
                }
            }
        }

        private void Show(string text,int sec)
        {
            _status=text;
            _until=DateTime.UtcNow.AddSeconds(sec);
        }

        private bool EnsureGui()
        {
            if(_label!=null)return true;
            _rectType=FindType("UnityEngine.Rect");
            _guiType=FindType("UnityEngine.GUI");
            if(_rectType==null||_guiType==null)return false;
            _rectCtor=_rectType.GetConstructor(new[]{typeof(float),typeof(float),typeof(float),typeof(float)});
            _box=_guiType.GetMethod("Box",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            _label=_guiType.GetMethod("Label",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            return _rectCtor!=null&&_label!=null;
        }

        private object Rect(float x,float y,float w,float h)
            => _rectCtor.Invoke(new object[]{x,y,w,h});
    }
}
