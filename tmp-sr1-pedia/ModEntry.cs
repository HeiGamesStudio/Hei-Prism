using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1PediaOnly.ModEntry), "SR1 Slimepedia Only", "1.0.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1PediaOnly
{
    public sealed class ModEntry : MelonMod
    {
        private sealed class EntrySpec
        {
            public string Key;
            public string Title;
            public string Description;
            public int IconMode;
        }

        private readonly EntrySpec[] _entries = new[]
        {
            new EntrySpec {
                Key = "Rad",
                Title = "Slime Rad",
                IconMode = 0,
                Description =
@"SLIMEOLOGIA

O Slime Rad é um slime incomum cuja energia radioativa produz um brilho verde intenso e uma aura perigosa ao redor do corpo. Registros da Far, Far Range indicam que ele se alimenta de vegetais e demonstra uma preferência especial por Oca Oca. Mesmo longe de suas antigas áreas de ocorrência, continua sendo uma das espécies mais reconhecíveis já catalogadas.

RISCOS PARA O RANCHEIRO

A aura do Slime Rad expõe quem permanece perto dele por tempo demais a níveis crescentes de radiação. Quanto maior a exposição, maior o perigo. Água e distância continuam sendo as formas mais seguras de lidar com uma concentração excessiva de energia radioativa.

PLORTONOMIA

Plorts Rad armazenam uma quantidade extraordinária de energia em um volume muito pequeno. Por isso, sempre foram valiosos para pesquisas e tecnologias que precisam de grande potência sem ocupar muito espaço."
            },
            new EntrySpec {
                Key = "Quantum",
                Title = "Slime Quântico",
                IconMode = 1,
                Description =
@"SLIMEOLOGIA

O Slime Quântico é uma espécie ligada às antigas anomalias da Far, Far Range. Ele manifesta cópias fantasmagóricas de si mesmo que representam possíveis posições e, quando suficientemente agitado, pode trocar de lugar com uma dessas projeções. Frutas fazem parte de sua dieta, e o Phase Lemon continua sendo seu alimento favorito clássico.

RISCOS PARA O RANCHEIRO

Um Slime Quântico agitado é especialista em escapar de currais. Suas projeções começam como simples possibilidades, mas podem se tornar o próprio slime em um instante. Mantê-lo alimentado e reduzir sua agitação é essencial para evitar desaparecimentos inesperados.

PLORTONOMIA

Plorts Quânticos são estudados por sua relação com estados alternativos da matéria e possibilidades paralelas. Mesmo hoje, suas propriedades continuam entre as mais estranhas já observadas em um plort."
            },
            new EntrySpec {
                Key = "Mosaic",
                Title = "Slime Mosaico",
                IconMode = 2,
                Description =
@"SLIMEOLOGIA

O Slime Mosaico possui o corpo coberto por placas brilhantes semelhantes a vidro, capazes de refratar a luz em cores intensas. É um slime vegetal, conhecido historicamente por sua preferência pelo Silver Parsnip. Seu visual é hipnotizante, mas os efeitos luminosos que o acompanham não são apenas decorativos.

RISCOS PARA O RANCHEIRO

Os reflexos do Slime Mosaico podem formar glints que caem ao chão e liberam rajadas de calor e chamas. Em grupos numerosos, isso pode transformar uma área segura em um incêndio rapidamente. Água continua sendo a resposta mais confiável para apagar esses focos.

PLORTONOMIA

Plorts Mosaico são valorizados por suas propriedades ópticas e pela estrutura cristalina incomum. Cientistas ainda estudam como esse material mantém efeitos de refração tão intensos mesmo depois de separado do slime."
            }
        };

        private bool _installed;
        private int _ticks;
        private DateTime _statusUntil = DateTime.UtcNow.AddSeconds(12);
        private string _status = "Slimepedia SR1: carregando...";

        private Type _rectType, _guiType;
        private ConstructorInfo _rectCtor;
        private MethodInfo _box, _label;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("SR1 Slimepedia Only iniciado.");
            TryInstall();
        }

        public override void OnSceneWasInitialized(int buildIndex, string sceneName)
        {
            if (!_installed) TryInstall();
            else TryUnlockAll();
        }

        public override void OnUpdate()
        {
            if (_installed) return;
            _ticks++;
            if (_ticks % 20 == 0) TryInstall();
        }

        public override void OnGUI()
        {
            try
            {
                if (DateTime.UtcNow > _statusUntil) return;
                if (!EnsureGui()) return;
                _box?.Invoke(null, new[] { Rect(18f,18f,580f,54f), "" });
                _label?.Invoke(null, new[] { Rect(31f,32f,550f,26f), _status });
            }
            catch { }
        }

        private void TryInstall()
        {
            try
            {
                var fixedType = FindTypeBySimpleName("FixedPediaEntry");
                var categoryType = FindTypeBySimpleName("PediaCategory");
                if (fixedType == null || categoryType == null) return;

                var template = FindAllResources(fixedType).FirstOrDefault();
                if (template == null) return;

                object slimesCategory = null;
                foreach (var c in FindAllResources(categoryType))
                {
                    if (string.Equals(GetString(c,"name","Name"), "Slimes", StringComparison.OrdinalIgnoreCase))
                    {
                        slimesCategory = c;
                        break;
                    }
                }
                if (slimesCategory == null) return;

                foreach (var spec in _entries)
                {
                    var existing = FindExistingEntry("SR1Pedia_" + spec.Key);
                    if (existing == null)
                    {
                        var entry = InstantiateObject(template);
                        if (entry == null) return;

                        SetMember(entry, "name", "SR1Pedia_" + spec.Key);
                        SetMember(entry, "_persistenceSuffix", "sr1_" + spec.Key.ToLowerInvariant());
                        SetMember(entry, "_isUnlockedInitially", true);

                        var title = MakeLocalized("Pedia", "sr1pedia.title." + spec.Key.ToLowerInvariant(), spec.Title);
                        var desc  = MakeLocalized("Pedia", "sr1pedia.desc." + spec.Key.ToLowerInvariant(), spec.Description);
                        if (title != null) SetMember(entry, "_title", title);
                        if (desc  != null) SetMember(entry, "_description", desc);

                        ClearCollection(entry, "_details");

                        var sprite = BuildIcon(spec.IconMode);
                        if (sprite != null) SetMember(entry, "_icon", sprite);

                        AppendCollection(slimesCategory, "_items", entry);

                        var runtime = InvokeBest(slimesCategory, "GetRuntimeCategory");
                        if (runtime != null)
                            TryAdd(GetMember(runtime, "_items", "Items"), entry);

                        var gc = GetGameContext();
                        var lookup = gc == null ? null : GetMember(gc, "LookupDirector");
                        if (lookup != null)
                            InvokeBest(lookup, "AddPediaEntryToCategory", entry, slimesCategory);
                    }
                }

                _installed = true;
                TryUnlockAll();

                _status = "Slimepedia pronta: Rad, Quântico e Mosaico já aparecem desbloqueados.";
                _statusUntil = DateTime.UtcNow.AddSeconds(8);
                LoggerInstance.Msg("3 entradas SR1 adicionadas à categoria Slimes e desbloqueadas.");
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Slimepedia install: " + ex.Message);
            }
        }

        private object FindExistingEntry(string name)
        {
            var baseType = FindTypeBySimpleName("PediaEntry");
            if (baseType == null) return null;
            foreach (var e in FindAllResources(baseType))
                if (string.Equals(GetString(e,"name","Name"), name, StringComparison.OrdinalIgnoreCase))
                    return e;
            return null;
        }

        private void TryUnlockAll()
        {
            try
            {
                var sceneType = FindTypeBySimpleName("SceneContext");
                var scene = sceneType == null ? null : GetStaticMember(sceneType, "Instance");
                var director = scene == null ? null : GetMember(scene, "PediaDirector");
                if (director == null) return;

                var baseType = FindTypeBySimpleName("PediaEntry");
                if (baseType == null) return;

                foreach (var spec in _entries)
                {
                    var entry = FindExistingEntry("SR1Pedia_" + spec.Key);
                    if (entry == null) continue;
                    SetMember(entry, "_isUnlockedInitially", true);

                    foreach (var m in director.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                    {
                        if (m.Name != "Unlock") continue;
                        var p = m.GetParameters();
                        if (p.Length != 2 || p[1].ParameterType != typeof(bool)) continue;
                        try
                        {
                            if (p[0].ParameterType.IsInstanceOfType(entry))
                            {
                                m.Invoke(director, new[]{entry,(object)false});
                                break;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        private object BuildIcon(int mode)
        {
            try
            {
                const int W = 128, H = 128;
                var data = new byte[W*H*4];

                for (int y=0; y<H; y++)
                for (int x=0; x<W; x++)
                {
                    double dx = (x - 63.5) / 58.0;
                    double dy = (y - 63.5) / 58.0;
                    double r = Math.Sqrt(dx*dx + dy*dy);

                    byte R=0,G=0,B=0,A=0;
                    if (r <= 1.0)
                    {
                        A = 255;
                        if (mode == 0)
                        {
                            // Rad: green body + luminous edge
                            if (r > 0.88) { R=96; G=255; B=112; }
                            else { R=66; G=166; B=77; }
                        }
                        else if (mode == 1)
                        {
                            // Quantum: warm orange/yellow
                            if (r > 0.88) { R=255; G=240; B=65; }
                            else { R=255; G=166; B=22; }
                        }
                        else
                        {
                            // Mosaic: concentric rainbow rings
                            if      (r > 0.82) { R=112; G=52;  B=235; }
                            else if (r > 0.62) { R=23;  G=163; B=245; }
                            else if (r > 0.42) { R=73;  G=230; B=132; }
                            else               { R=255; G=214; B=46;  }
                        }

                        // simple Slime-Rancher-like face
                        double lx = dx + 0.38, rx = dx - 0.38;
                        bool eye = (lx*lx + (dy+0.12)*(dy+0.12) < 0.035) ||
                                   (rx*rx + (dy+0.12)*(dy+0.12) < 0.035);
                        if (eye)
                        {
                            if (mode==0) { R=38; G=42; B=68; }
                            else if (mode==1) { R=55; G=40; B=47; }
                            else { R=56; G=13; B=94; }
                        }

                        bool mouth = dy > 0.20 && dy < 0.33 && Math.Abs(dx) < 0.30 &&
                                     dy > 0.29 - 0.55*Math.Abs(dx);
                        if (mouth)
                        {
                            if (mode==0) { R=38; G=42; B=68; }
                            else if (mode==1) { R=65; G=44; B=34; }
                            else { R=61; G=18; B=96; }
                        }
                    }

                    int i = (y*W+x)*4;
                    data[i]=R; data[i+1]=G; data[i+2]=B; data[i+3]=A;
                }

                var texType = FindType("UnityEngine.Texture2D");
                var fmtType = FindType("UnityEngine.TextureFormat");
                var spriteType = FindType("UnityEngine.Sprite");
                var rectType = FindType("UnityEngine.Rect");
                var vec2Type = FindType("UnityEngine.Vector2");
                if (texType==null || fmtType==null || spriteType==null || rectType==null || vec2Type==null) return null;

                var rgba32 = Enum.Parse(fmtType, "RGBA32");
                object tex = null;
                foreach (var c in texType.GetConstructors())
                {
                    var p=c.GetParameters();
                    if (p.Length==4 && p[0].ParameterType==typeof(int) && p[1].ParameterType==typeof(int) && p[2].ParameterType==fmtType && p[3].ParameterType==typeof(bool))
                    {
                        tex=c.Invoke(new object[]{W,H,rgba32,false});
                        break;
                    }
                }
                if (tex==null) return null;

                SetMember(tex,"name","SR1PediaIcon_"+mode);
                var load = texType.GetMethods(BindingFlags.Public|BindingFlags.Instance)
                    .FirstOrDefault(m => m.Name=="LoadRawTextureData" && m.GetParameters().Length==1 && m.GetParameters()[0].ParameterType==typeof(byte[]));
                load?.Invoke(tex,new object[]{data});
                InvokeBest(tex,"Apply");

                var rect = Activator.CreateInstance(rectType,new object[]{0f,0f,(float)W,(float)H});
                var pivot = Activator.CreateInstance(vec2Type,new object[]{0.5f,0.5f});

                foreach (var m in spriteType.GetMethods(BindingFlags.Public|BindingFlags.Static))
                {
                    if (m.Name!="Create") continue;
                    var p=m.GetParameters();
                    if (p.Length < 3) continue;
                    try
                    {
                        var args = new object[p.Length];
                        args[0]=ConvertArg(tex,p[0].ParameterType);
                        args[1]=ConvertArg(rect,p[1].ParameterType);
                        args[2]=ConvertArg(pivot,p[2].ParameterType);
                        for(int i=3;i<p.Length;i++) args[i]=p[i].HasDefaultValue?p[i].DefaultValue:null;
                        var sprite=m.Invoke(null,args);
                        if(sprite!=null) return sprite;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                LoggerInstance.Warning("Icon: " + ex.Message);
            }
            return null;
        }

        private object MakeLocalized(string tableName, string key, string text)
        {
            try
            {
                var settings=FindType("UnityEngine.Localization.Settings.LocalizationSettings");
                if(settings==null) return null;
                var db=GetStaticMember(settings,"StringDatabase");
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
                    foreach(var m in table.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                    {
                        if(m.Name!="GetEntry") continue;
                        var p=m.GetParameters();
                        if(p.Length!=1) continue;
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
                    try { return ctor.Invoke(new[]{ConvertArg(tableRef,p[0].ParameterType),ConvertArg(keyId,p[1].ParameterType)}); } catch { }
                }

                var ls=Activator.CreateInstance(lsType);
                if(ls!=null)
                {
                    SetMember(ls,"TableReference",tableRef);
                    SetMember(ls,"TableEntryReference",keyId);
                }
                return ls;
            }
            catch { return null; }
        }

        private object GetGameContext()
        {
            var t=FindTypeBySimpleName("GameContext");
            if(t==null) return null;
            return GetStaticMember(t,"Instance") ?? FindAllResources(t).FirstOrDefault();
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

        private static IEnumerable<object> FindAllResources(Type type)
        {
            if(type==null)yield break;
            var r=FindType("UnityEngine.Resources");
            if(r==null)yield break;
            foreach(var m in r.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="FindObjectsOfTypeAll"||!m.IsGenericMethodDefinition)continue;
                if(m.GetGenericArguments().Length!=1||m.GetParameters().Length!=0)continue;
                object arr=null; try{arr=m.MakeGenericMethod(type).Invoke(null,null);}catch{}
                if(arr is IEnumerable en)
                {
                    foreach(var x in en)if(x!=null)yield return x;
                    yield break;
                }
            }
        }

        private static object InstantiateObject(object original)
        {
            if(original==null)return null;
            var uo=FindType("UnityEngine.Object");
            if(uo==null)return null;
            foreach(var m in uo.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod)continue;
                if(m.GetParameters().Length!=1)continue;
                try{return m.Invoke(null,new[]{original});}catch{}
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

        private static string GetString(object obj,params string[] names)
            => GetMember(obj,names)?.ToString();

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

        private static object ConvertArg(object value,Type target)
        {
            if(value==null)return null;
            if(target.IsInstanceOfType(value))return value;
            if(target.IsEnum){try{return Enum.ToObject(target,Convert.ToInt32(value));}catch{}}
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
            try{return Convert.ChangeType(value,target);}catch{return value;}
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
                    var a=new object[args.Length];
                    for(int i=0;i<args.Length;i++)a[i]=ConvertArg(args[i],p[i].ParameterType);
                    return m.Invoke(obj,a);
                }catch{}
            }
            return null;
        }

        private static void ClearCollection(object owner,string member)
        {
            var cur=GetMember(owner,member);
            if(cur==null)return;
            var t=cur.GetType();
            try
            {
                if(t.IsArray)
                {
                    SetMember(owner,member,Array.CreateInstance(t.GetElementType(),0));
                    return;
                }
                foreach(var len in new object[]{0,(long)0})
                {
                    try
                    {
                        var empty=Activator.CreateInstance(t,new[]{len});
                        if(empty!=null){SetMember(owner,member,empty);return;}
                    }
                    catch{}
                }
            }catch{}
        }

        private static void AppendCollection(object owner,string member,object value)
        {
            var cur=GetMember(owner,member);
            if(cur==null||value==null)return;
            if(TryAdd(cur,value))return;

            var vals=new List<object>();
            if(cur is IEnumerable en)
                foreach(var x in en) if(x!=null) vals.Add(x);
            if(vals.Any(x=>ReferenceEquals(x,value)||x.Equals(value)))return;
            vals.Add(value);

            var t=cur.GetType();
            try
            {
                if(t.IsArray)
                {
                    var et=t.GetElementType();
                    var arr=Array.CreateInstance(et,vals.Count);
                    for(int i=0;i<vals.Count;i++)arr.SetValue(ConvertArg(vals[i],et),i);
                    SetMember(owner,member,arr);
                    return;
                }
                object obj=null;
                foreach(var len in new object[]{vals.Count,(long)vals.Count})
                {
                    try{obj=Activator.CreateInstance(t,new[]{len});if(obj!=null)break;}catch{}
                }
                if(obj==null)return;
                var item=t.GetProperty("Item",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if(item!=null&&item.CanWrite)
                {
                    var ip=item.GetIndexParameters().FirstOrDefault()?.ParameterType??typeof(int);
                    for(int i=0;i<vals.Count;i++)
                    {
                        object idx=ip==typeof(long)?(object)(long)i:i;
                        item.SetValue(obj,ConvertArg(vals[i],item.PropertyType),new[]{idx});
                    }
                    SetMember(owner,member,obj);
                }
            }catch{}
        }

        private static bool TryAdd(object collection,object value)
        {
            if(collection==null||value==null)return false;
            try
            {
                var contains=collection.GetType().GetMethods().FirstOrDefault(m=>m.Name=="Contains"&&m.GetParameters().Length==1);
                if(contains!=null&&(bool)contains.Invoke(collection,new[]{ConvertArg(value,contains.GetParameters()[0].ParameterType)}))return true;
            }catch{}
            foreach(var m in collection.GetType().GetMethods())
            {
                if(m.Name!="Add"||m.GetParameters().Length!=1)continue;
                try{m.Invoke(collection,new[]{ConvertArg(value,m.GetParameters()[0].ParameterType)});return true;}catch{}
            }
            return false;
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
