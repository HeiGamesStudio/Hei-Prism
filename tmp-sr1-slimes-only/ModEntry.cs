using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1SlimesOnly.ModEntry), "SR1 Slimes Only", "0.8.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1SlimesOnly
{
    public sealed class ModEntry : MelonMod
    {
        private sealed class Spec
        {
            public string Key, Display, RefId, PediaSuffix, Description;
            public string[] ShaderNames;
            public float[] Top, Mid, Bottom;
            public int IconMode;
        }

        private readonly Spec[] _specs =
        {
            new Spec {
                Key="Rad", Display="Slime Rad", RefId="SlimeDefinition.SR1Rad",
                PediaSuffix="sr1rad", IconMode=0,
                ShaderNames=new[]{"SR/AMP/Slime/Body/Rad","SlimeBody_rad","SR/AMP/Slime/Body/Rad/Normal"},
                Top=new[]{0.43f,0.94f,0.48f,1f}, Mid=new[]{0.25f,0.65f,0.30f,1f}, Bottom=new[]{0.17f,0.47f,0.21f,1f},
                Description=
@"SLIMEOLOGIA

O Slime Rad é um slime incomum cuja energia radioativa produz um brilho verde intenso e uma aura perigosa ao redor do corpo. Registros da Far, Far Range indicam que ele se alimenta de vegetais e demonstra uma preferência especial por Oca Oca. Mesmo em novas regiões, continua sendo uma das espécies mais reconhecíveis já catalogadas.

RISCOS PARA O RANCHEIRO

A aura do Slime Rad expõe quem permanece perto dele por tempo demais a níveis crescentes de radiação. Quanto maior a exposição, maior o perigo. Distância e água continuam sendo as formas mais seguras de lidar com concentrações excessivas de energia radioativa.

PLORTONOMIA

Plorts Rad armazenam uma quantidade extraordinária de energia em um volume muito pequeno. Por isso, sempre foram valiosos para pesquisas e tecnologias que precisam de grande potência sem ocupar muito espaço."
            },
            new Spec {
                Key="Quantum", Display="Slime Quântico", RefId="SlimeDefinition.SR1Quantum",
                PediaSuffix="sr1quantum", IconMode=1,
                ShaderNames=new[]{"SR/AMP/Slime/Body/Quantum","SlimeBody_quantum","SR/AMP/Slime/Body/QuantumTimeOverride"},
                Top=new[]{1f,0.83f,0.24f,1f}, Mid=new[]{1f,0.61f,0.06f,1f}, Bottom=new[]{0.88f,0.39f,0.03f,1f},
                Description=
@"SLIMEOLOGIA

O Slime Quântico é uma espécie ligada às antigas anomalias da Far, Far Range. Ele manifesta cópias fantasmagóricas de si mesmo que representam possíveis posições e, quando suficientemente agitado, pode trocar de lugar com uma dessas projeções. Frutas fazem parte de sua dieta, e o Phase Lemon continua sendo seu alimento favorito clássico.

RISCOS PARA O RANCHEIRO

Um Slime Quântico agitado é especialista em escapar de currais. Suas projeções começam como simples possibilidades, mas podem se tornar o próprio slime em um instante. Mantê-lo alimentado e reduzir sua agitação é essencial para evitar desaparecimentos inesperados.

PLORTONOMIA

Plorts Quânticos são estudados por sua relação com estados alternativos da matéria e possibilidades paralelas. Mesmo hoje, suas propriedades continuam entre as mais estranhas já observadas em um plort."
            },
            new Spec {
                Key="Mosaic", Display="Slime Mosaico", RefId="SlimeDefinition.SR1Mosaic",
                PediaSuffix="sr1mosaic", IconMode=2,
                ShaderNames=new[]{"SR/AMP/Slime/Mosaic","MosaicSlime","SR/AMP/Slime/Body/Mosaic"},
                Top=new[]{0.94f,0.98f,1f,1f}, Mid=new[]{0.55f,0.82f,1f,1f}, Bottom=new[]{0.65f,0.46f,0.96f,1f},
                Description=
@"SLIMEOLOGIA

O Slime Mosaico possui o corpo coberto por placas brilhantes semelhantes a vidro, capazes de refratar a luz em cores intensas. É um slime vegetal, conhecido historicamente por sua preferência pelo Silver Parsnip. Seu visual é hipnotizante, mas os efeitos luminosos que o acompanham não são apenas decorativos.

RISCOS PARA O RANCHEIRO

Os reflexos do Slime Mosaico podem formar glints que caem ao chão e liberam rajadas de calor e chamas. Em grupos numerosos, isso pode transformar uma área segura em um incêndio rapidamente. Água continua sendo a resposta mais confiável para apagar esses focos.

PLORTONOMIA

Plorts Mosaico são valorizados por suas propriedades ópticas e pela estrutura cristalina incomum. Cientistas ainda estudam como esse material mantém efeitos de refração tão intensos mesmo depois de separado do slime."
            }
        };

        private readonly Dictionary<string,object> _defs = new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,object> _prefabs = new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string,object> _icons = new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
        private bool _ready, _pediaReady;
        private int _ticks;
        private object _pink;
        private string _status="SR1 Slimes: criando espécies reais...";
        private DateTime _until=DateTime.UtcNow.AddSeconds(15);

        private Type _rectType,_guiType;
        private ConstructorInfo _rectCtor;
        private MethodInfo _box,_label;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("SR1 Slimes Only v0.8 iniciado.");
            LoggerInstance.Msg("Somente espécies + Slimepedia + spawn manual. Sem plorts, dieta própria, largos ou spawn natural.");
            TryBootstrap();
        }

        public override void OnSceneWasInitialized(int buildIndex,string sceneName)
        {
            TryBootstrap();
            if(_ready) TryInstallPedia();
        }

        public override void OnSceneWasLoaded(int buildIndex,string sceneName)
        {
            TryBootstrap();
            if(_ready) TryInstallPedia();
        }

        public override void OnUpdate()
        {
            _ticks++;
            if(!_ready && _ticks%15==0) TryBootstrap();
            if(_ready && !_pediaReady && _ticks%30==0) TryInstallPedia();

            if(KeyPressed("digit8Key","numpad8Key","Alpha8","Keypad8")) Spawn("Rad");
            if(KeyPressed("digit9Key","numpad9Key","Alpha9","Keypad9")) Spawn("Quantum");
            if(KeyPressed("digit0Key","numpad0Key","Alpha0","Keypad0")) Spawn("Mosaic");
        }

        public override void OnGUI()
        {
            try
            {
                if(DateTime.UtcNow>_until)return;
                if(!EnsureGui())return;
                _box?.Invoke(null,new[]{Rect(18f,18f,650f,54f),""});
                _label?.Invoke(null,new[]{Rect(31f,32f,620f,26f),_status});
            }catch{}
        }

        private void TryBootstrap()
        {
            if(_ready)return;
            try
            {
                var gc=GetGameContext();
                if(gc==null)return;

                _pink=FindPink(gc);
                if(_pink==null)return;

                foreach(var s in _specs)
                {
                    if(_defs.ContainsKey(s.Key))continue;
                    var def=BuildSlime(gc,s);
                    if(def==null)return;
                    _defs[s.Key]=def;
                }

                _ready=_defs.Count==3;
                if(_ready)
                {
                    _status="3 slimes reais criados — 8 Rad | 9 Quântico | 0 Mosaico";
                    _until=DateTime.UtcNow.AddSeconds(10);
                    LoggerInstance.Msg("Rad, Quantum e Mosaic foram registrados como SlimeDefinition reais.");
                    TryInstallPedia();
                }
            }
            catch(Exception ex)
            {
                LoggerInstance.Warning("Bootstrap: "+ex.Message);
            }
        }

        private object BuildSlime(object gc,Spec spec)
        {
            var existing=FindDefByRef(gc,spec.RefId);
            if(existing!=null)
            {
                EnsureSaveRegistration(gc,existing,spec);
                _prefabs[spec.Key]=GetMember(existing,"prefab","Prefab");
                return existing;
            }

            var def=InstantiateObject(_pink);
            if(def==null)return null;
            Pin(def);
            SetMember(def,"name","SR1"+spec.Key+"Slime");
            ForceReferenceId(def,spec.RefId);
            SetMember(def,"_pediaPersistenceSuffix",spec.PediaSuffix);
            SetMember(def,"pediaPersistenceSuffix",spec.PediaSuffix);
            SetMember(def,"CanLargofy",false);

            var icon=BuildIcon(spec.IconMode);
            if(icon!=null)
            {
                _icons[spec.Key]=icon;
                SetMember(def,"icon",icon);
                SetMember(def,"debugIcon",icon);
            }

            var localized=MakeLocalized("Actor","sr1slimes.name."+spec.Key.ToLowerInvariant(),spec.Display);
            if(localized!=null)SetMember(def,"localizedName",localized);

            SetColorMember(def,"color",spec.Mid);

            var app=CloneAppearance(spec,icon);
            if(app!=null)
            {
                var oldApps=GetMember(_pink,"AppearancesDefault");
                if(oldApps!=null)
                {
                    var newApps=CreateCollection(oldApps.GetType(),new List<object>{app});
                    if(newApps!=null)SetMember(def,"AppearancesDefault",newApps);
                }
            }

            PrepareNoOutputDiet(def);

            var basePrefab=GetMember(_pink,"prefab","Prefab");
            var prefab=InstantiateObject(basePrefab);
            if(prefab==null)return null;
            Pin(prefab);
            SetMember(prefab,"name","SR1"+spec.Key+"SlimePrefab");
            SetMember(def,"prefab",prefab);
            WirePrefab(prefab,def);
            RecolorGameObject(prefab,spec);

            RegisterDefinition(gc,def,spec);
            _prefabs[spec.Key]=prefab;
            return def;
        }

        private object CloneAppearance(Spec spec,object icon)
        {
            try
            {
                var baseApp=FirstOf(GetMember(_pink,"AppearancesDefault"));
                if(baseApp==null)return null;
                var app=InstantiateObject(baseApp);
                if(app==null)return null;
                Pin(app);
                SetMember(app,"name","SR1"+spec.Key+"Appearance");
                if(icon!=null)SetMember(app,"_icon",icon);

                var structs=GetMember(baseApp,"Structures","_structures");
                if(structs is IEnumerable en)
                {
                    var list=new List<object>();
                    foreach(var bs in en)
                    {
                        if(bs==null)continue;
                        object ns=null;
                        try
                        {
                            var ctor=bs.GetType().GetConstructor(new[]{bs.GetType()});
                            if(ctor!=null)ns=ctor.Invoke(new[]{bs});
                        }catch{}
                        if(ns==null)continue;

                        var mats=GetMember(bs,"DefaultMaterials");
                        if(mats is IEnumerable men)
                        {
                            var ml=new List<object>();
                            foreach(var m in men)
                            {
                                var nm=m==null?null:InstantiateObject(m);
                                if(nm!=null)RecolorMaterial(nm,spec);
                                ml.Add(nm);
                            }
                            var mc=CreateCollection(mats.GetType(),ml);
                            if(mc!=null)SetMember(ns,"DefaultMaterials",mc);
                        }
                        list.Add(ns);
                    }
                    if(list.Count>0)
                    {
                        var sc=CreateCollection(structs.GetType(),list);
                        if(sc!=null)
                        {
                            if(!SetMember(app,"Structures",sc))SetMember(app,"_structures",sc);
                        }
                    }
                }
                return app;
            }
            catch(Exception ex)
            {
                LoggerInstance.Warning("Appearance "+spec.Key+": "+ex.Message);
                return null;
            }
        }

        private void PrepareNoOutputDiet(object def)
        {
            try
            {
                var diet=GetMember(def,"Diet");
                if(diet==null)return;

                EmptyCollectionMember(diet,"ProduceIdents");
                EmptyCollectionMember(diet,"FavoriteIdents");

                var eatMap=GetMember(diet,"EatMap");
                InvokeBest(eatMap,"Clear");
            }
            catch{}
        }

        private void RegisterDefinition(object gc,object def,Spec spec)
        {
            var defs=GetMember(gc,"SlimeDefinitions");
            if(defs!=null)
            {
                AppendCollection(defs,"Slimes",def);
                DictSet(GetMember(defs,"_slimeDefinitionsByIdentifiable"),def,def);
            }

            var lookup=GetMember(gc,"LookupDirector");
            var asd=GetMember(gc,"AutoSaveDirector");

            var ids=new[]{
                spec.RefId,
                "SlimeDefinition.Custom"+spec.Key+"Slime"
            };

            foreach(var id in ids)
            {
                DictSet(GetMember(lookup,"_identifiableTypeByRefId"),id,def);
                if(asd!=null)
                {
                    var tr=GetMember(asd,"_saveReferenceTranslation");
                    DictSet(GetMember(tr,"_identifiableTypeLookup"),id,def);
                    EnsurePersistence(GetMember(tr,"_identifiableTypeToPersistenceId"),id);
                }
            }

            if(lookup!=null)
            {
                var groupType=FindTypeBySimpleName("IdentifiableTypeGroup");
                if(groupType!=null)
                {
                    var wanted=new HashSet<string>(StringComparer.OrdinalIgnoreCase){
                        "BaseSlimeGroup","VaccableBaseSlimeGroup","SlimesGroup","SmallSlimeGroup",
                        "EdibleSlimeGroup","IdentifiableTypesGroup","SlimesSinkInShallowWaterGroup"
                    };
                    foreach(var g in FindAllResources(groupType))
                    {
                        var n=GetString(g,"name","Name");
                        if(n!=null&&wanted.Contains(n))
                            InvokeBest(lookup,"AddIdentifiableTypeToGroup",def,g);
                    }
                }

                if(asd!=null)
                {
                    var cfg=GetMember(asd,"_configuration");
                    var master=GetMember(cfg,"_identifiableTypes");
                    if(master!=null)InvokeBest(lookup,"AddIdentifiableTypeToGroup",def,master);
                }
            }
        }

        private void EnsureSaveRegistration(object gc,object def,Spec spec)
        {
            RegisterDefinition(gc,def,spec);
        }

        private void TryInstallPedia()
        {
            if(!_ready)return;
            try
            {
                var entryType=FindTypeBySimpleName("IdentifiablePediaEntry");
                var categoryType=FindTypeBySimpleName("PediaCategory");
                if(entryType==null||categoryType==null)return;

                object category=null;
                foreach(var c in FindAllResources(categoryType))
                {
                    if(string.Equals(GetString(c,"name","Name"),"Slimes",StringComparison.OrdinalIgnoreCase))
                    {category=c;break;}
                }
                if(category==null)return;

                object pinkEntry=null;
                foreach(var e in FindAllResources(entryType))
                {
                    var id=GetMember(e,"_identifiableType","IdentifiableType");
                    if(id!=null&&_pink!=null&&(ReferenceEquals(id,_pink)||id.Equals(_pink)))
                    {pinkEntry=e;break;}
                }

                foreach(var spec in _specs)
                {
                    var def=_defs[spec.Key];
                    object entry=null;
                    foreach(var e in FindAllResources(entryType))
                    {
                        var id=GetMember(e,"_identifiableType","IdentifiableType");
                        if(id!=null&&(ReferenceEquals(id,def)||id.Equals(def)))
                        {entry=e;break;}
                    }

                    if(entry==null)
                    {
                        entry=CreateScriptable(entryType);
                        if(entry==null)continue;
                        Pin(entry);
                        SetMember(entry,"name",GetString(def,"name","Name")??("SR1"+spec.Key+"Slime"));
                        SetMember(entry,"_identifiableType",def);
                        SetMember(entry,"_title",GetMember(def,"localizedName"));
                        var desc=MakeLocalized("Pedia","sr1slimes.pedia."+spec.Key.ToLowerInvariant(),spec.Description);
                        if(desc!=null)SetMember(entry,"_description",desc);
                        SetMember(entry,"_isUnlockedInitially",true);

                        if(pinkEntry!=null)
                        {
                            var h=GetMember(pinkEntry,"_highlightSet");
                            if(h!=null)SetMember(entry,"_highlightSet",h);
                        }
                        EmptyCollectionMember(entry,"_details");
                    }

                    AppendCollection(category,"_items",entry);
                    var runtime=InvokeBest(category,"GetRuntimeCategory");
                    if(runtime!=null)TryAdd(GetMember(runtime,"_items","Items"),entry);

                    var gc=GetGameContext();
                    var lookup=GetMember(gc,"LookupDirector");
                    if(lookup!=null)InvokeBest(lookup,"AddPediaEntryToCategory",entry,category);

                    Unlock(entry,def);
                }

                _pediaReady=true;
                _status="Slimepedia pronta: Rad, Quântico e Mosaico em SLIMES, já desbloqueados.";
                _until=DateTime.UtcNow.AddSeconds(10);
                LoggerInstance.Msg("3 entradas IdentifiablePediaEntry adicionadas à categoria Slimes.");
            }
            catch(Exception ex)
            {
                LoggerInstance.Warning("Pedia: "+ex.Message);
            }
        }

        private void Unlock(object entry,object def)
        {
            try
            {
                var sceneType=FindTypeBySimpleName("SceneContext");
                var scene=sceneType==null?null:GetStaticMember(sceneType,"Instance");
                var director=scene==null?null:GetMember(scene,"PediaDirector");
                if(director==null)return;

                foreach(var m in director.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if(m.Name!="Unlock")continue;
                    var p=m.GetParameters();
                    if(p.Length!=2||p[1].ParameterType!=typeof(bool))continue;
                    try
                    {
                        if(entry!=null&&p[0].ParameterType.IsInstanceOfType(entry))
                        {m.Invoke(director,new[]{entry,(object)false});return;}
                        if(def!=null&&p[0].ParameterType.IsInstanceOfType(def))
                        {m.Invoke(director,new[]{def,(object)false});return;}
                    }catch{}
                }
            }catch{}
        }

        private void Spawn(string key)
        {
            if(!_ready)TryBootstrap();
            if(!_ready){Show("Os slimes ainda estão carregando.",3);return;}
            try
            {
                var def=_defs[key];
                var prefab=_prefabs[key];
                object pos,rot;
                GetSpawnTransform(out pos,out rot);

                var go=TryActorSpawn(def,pos,rot)??InstantiateAt(prefab,pos,rot);
                if(go==null){Show("Falha ao spawnar "+key,4);return;}

                InvokeBest(go,"SetActive",true);
                WirePrefab(go,def);
                var spec=_specs.First(x=>x.Key.Equals(key,StringComparison.OrdinalIgnoreCase));
                RecolorGameObject(go,spec);

                var appType=FindTypeBySimpleName("SlimeAppearanceApplicator");
                var app=appType==null?null:GetComponent(go,appType);
                InvokeBest(app,"ApplyAppearance");

                Show(spec.Display+" spawnado. 8 Rad | 9 Quântico | 0 Mosaico",3);
            }
            catch(Exception ex)
            {
                LoggerInstance.Warning("Spawn: "+ex.Message);
                Show("Erro ao spawnar: "+ex.Message,4);
            }
        }

        private object TryActorSpawn(object def,object pos,object rot)
        {
            try
            {
                var sceneType=FindTypeBySimpleName("SceneContext");
                var scene=sceneType==null?null:GetStaticMember(sceneType,"Instance");
                var modelSvc=GetMember(scene,"GameModel");
                var registry=GetMember(scene,"RegionRegistry");
                var group=GetMember(registry,"CurrentSceneGroup");
                if(modelSvc==null||group==null)return null;

                object model=null;
                foreach(var m in modelSvc.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if(m.Name!="InstantiateActorModel")continue;
                    var p=m.GetParameters();
                    if(p.Length!=5)continue;
                    try{model=m.Invoke(modelSvc,new[]{def,group,pos,rot,(object)false});if(model!=null)break;}catch{}
                }
                if(model==null)return null;

                var helpers=FindTypeBySimpleName("InstantiationHelpers");
                if(helpers==null)return null;
                foreach(var m in helpers.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static))
                {
                    if(m.Name!="InstantiateActorFromModel"||m.GetParameters().Length!=1)continue;
                    try{return m.Invoke(null,new[]{model});}catch{}
                }
            }catch{}
            return null;
        }

        private void GetSpawnTransform(out object pos,out object rot)
        {
            var v3=FindType("UnityEngine.Vector3");
            var q=FindType("UnityEngine.Quaternion");
            pos=Activator.CreateInstance(v3,new object[]{0f,5f,0f});
            rot=GetStaticMember(q,"identity");
            try
            {
                var cam=GetStaticMember(FindType("UnityEngine.Camera"),"main");
                var tr=GetMember(cam,"transform");
                var p=GetMember(tr,"position");
                var f=GetMember(tr,"forward");
                float px=Num(GetMember(p,"x")),py=Num(GetMember(p,"y")),pz=Num(GetMember(p,"z"));
                float fx=Num(GetMember(f,"x")),fy=Num(GetMember(f,"y")),fz=Num(GetMember(f,"z"));
                pos=Activator.CreateInstance(v3,new object[]{px+fx*3f,py+fy*3f+0.5f,pz+fz*3f});
            }catch{}
        }

        private object FindPink(object gc)
        {
            var defs=GetMember(gc,"SlimeDefinitions");
            var slimes=GetMember(defs,"Slimes");
            if(slimes is IEnumerable en)
            {
                foreach(var d in en)
                {
                    var n=GetString(d,"name","Name");
                    var r=GetString(d,"ReferenceId","referenceId");
                    if(string.Equals(n,"Pink",StringComparison.OrdinalIgnoreCase)||
                       string.Equals(n,"PinkSlime",StringComparison.OrdinalIgnoreCase)||
                       string.Equals(r,"SlimeDefinition.Pink",StringComparison.OrdinalIgnoreCase))
                        return d;
                }
            }
            var t=FindTypeBySimpleName("SlimeDefinition");
            foreach(var d in FindAllResources(t))
            {
                var r=GetString(d,"ReferenceId","referenceId")??"";
                if(r.IndexOf("Pink",StringComparison.OrdinalIgnoreCase)>=0)return d;
            }
            return null;
        }

        private object FindDefByRef(object gc,string refId)
        {
            var defs=GetMember(gc,"SlimeDefinitions");
            var slimes=GetMember(defs,"Slimes");
            if(slimes is IEnumerable en)
                foreach(var d in en)
                    if(string.Equals(GetString(d,"ReferenceId","referenceId"),refId,StringComparison.Ordinal))
                        return d;
            return null;
        }

        private void WirePrefab(object go,object def)
        {
            if(go==null||def==null)return;
            var appType=FindTypeBySimpleName("SlimeAppearanceApplicator");
            var identType=FindTypeBySimpleName("IdentifiableActor");
            var eatType=FindTypeBySimpleName("SlimeEat");

            var a=appType==null?null:GetComponent(go,appType);
            if(a!=null)
            {
                SetMember(a,"SlimeDefinition",def);
                var first=FirstOf(GetMember(def,"AppearancesDefault"));
                if(first!=null)SetMember(a,"Appearance",first);
            }

            var ia=identType==null?null:GetComponent(go,identType);
            if(ia!=null)SetMember(ia,"identType",def);

            var eat=eatType==null?null:GetComponent(go,eatType);
            if(eat!=null)SetMember(eat,"SlimeDefinition",def);
        }

        private object BuildIcon(int mode)
        {
            try
            {
                const int W=128,H=128;
                var data=new byte[W*H*4];
                for(int y=0;y<H;y++)
                for(int x=0;x<W;x++)
                {
                    double dx=(x-63.5)/58.0,dy=(y-63.5)/58.0;
                    double r=Math.Sqrt(dx*dx+dy*dy);
                    byte R=0,G=0,B=0,A=0;
                    if(r<=1)
                    {
                        A=255;
                        if(mode==0)
                        {
                            if(r>.88){R=99;G=245;B=110;} else {R=65;G=164;B=75;}
                        }
                        else if(mode==1)
                        {
                            if(r>.88){R=255;G=245;B=70;} else {R=255;G=169;B=24;}
                        }
                        else
                        {
                            if(r>.82){R=113;G=54;B=235;}
                            else if(r>.62){R=28;G=161;B=245;}
                            else if(r>.42){R=79;G=232;B=135;}
                            else {R=255;G=213;B=55;}
                        }

                        double lx=dx+.38,rx=dx-.38;
                        bool eye=(lx*lx+(dy+.12)*(dy+.12)<.034)||(rx*rx+(dy+.12)*(dy+.12)<.034);
                        bool mouth=dy>.20&&dy<.33&&Math.Abs(dx)<.30&&dy>.29-.55*Math.Abs(dx);
                        if(eye||mouth)
                        {
                            if(mode==0){R=35;G=40;B=65;}
                            else if(mode==1){R=70;G=46;B=35;}
                            else {R=60;G=18;B=95;}
                        }
                    }
                    int i=(y*W+x)*4;data[i]=R;data[i+1]=G;data[i+2]=B;data[i+3]=A;
                }

                var texType=FindType("UnityEngine.Texture2D");
                var fmtType=FindType("UnityEngine.TextureFormat");
                var spriteType=FindType("UnityEngine.Sprite");
                var rectType=FindType("UnityEngine.Rect");
                var vec2Type=FindType("UnityEngine.Vector2");
                if(texType==null||fmtType==null||spriteType==null||rectType==null||vec2Type==null)return null;

                var rgba32=Enum.Parse(fmtType,"RGBA32");
                object tex=null;
                foreach(var c in texType.GetConstructors())
                {
                    var p=c.GetParameters();
                    if(p.Length==4&&p[0].ParameterType==typeof(int)&&p[1].ParameterType==typeof(int)&&p[2].ParameterType==fmtType&&p[3].ParameterType==typeof(bool))
                    {tex=c.Invoke(new object[]{W,H,rgba32,false});break;}
                }
                if(tex==null)return null;
                Pin(tex);

                var load=texType.GetMethods().FirstOrDefault(m=>m.Name=="LoadRawTextureData"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(byte[]));
                load?.Invoke(tex,new object[]{data});
                InvokeBest(tex,"Apply");

                var rect=Activator.CreateInstance(rectType,new object[]{0f,0f,(float)W,(float)H});
                var pivot=Activator.CreateInstance(vec2Type,new object[]{.5f,.5f});
                foreach(var m in spriteType.GetMethods(BindingFlags.Public|BindingFlags.Static))
                {
                    if(m.Name!="Create")continue;
                    var p=m.GetParameters();
                    if(p.Length<3)continue;
                    try
                    {
                        var args=new object[p.Length];
                        args[0]=ConvertArg(tex,p[0].ParameterType);
                        args[1]=ConvertArg(rect,p[1].ParameterType);
                        args[2]=ConvertArg(pivot,p[2].ParameterType);
                        for(int i=3;i<p.Length;i++)args[i]=p[i].HasDefaultValue?p[i].DefaultValue:null;
                        var sprite=m.Invoke(null,args);
                        if(sprite!=null){Pin(sprite);return sprite;}
                    }catch{}
                }
            }catch{}
            return null;
        }

        private void RecolorGameObject(object go,Spec spec)
        {
            var rendererType=FindType("UnityEngine.Renderer");
            if(rendererType==null)return;
            foreach(var r in GetComponentsInChildren(go,rendererType))
            {
                var mats=GetMember(r,"materials","Materials") as IEnumerable;
                if(mats==null)continue;
                foreach(var m in mats)
                    if(m!=null&&(HasMatProp(m,"_TopColor")||HasMatProp(m,"_MiddleColor")||HasMatProp(m,"_BottomColor")))
                        RecolorMaterial(m,spec);
            }
        }

        private void RecolorMaterial(object m,Spec spec)
        {
            var shaderType=FindType("UnityEngine.Shader");
            var colorType=FindType("UnityEngine.Color");
            if(colorType==null)return;

            object shader=null;
            if(shaderType!=null)
            {
                var find=shaderType.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(x=>x.Name=="Find"&&x.GetParameters().Length==1);
                if(find!=null)
                    foreach(var n in spec.ShaderNames)
                    {
                        try{shader=find.Invoke(null,new object[]{n});if(shader!=null)break;}catch{}
                    }
            }
            if(shader!=null)SetMember(m,"shader",shader);

            var top=MakeColor(colorType,spec.Top);
            var mid=MakeColor(colorType,spec.Mid);
            var bottom=MakeColor(colorType,spec.Bottom);
            SetMatColor(m,"_TopColor",top);
            SetMatColor(m,"_MiddleColor",mid);
            SetMatColor(m,"_BottomColor",bottom);
            SetMatColor(m,"_SpecColor",mid);
            SetMatColor(m,"_Color",mid);
            SetMatColor(m,"_BaseColor",mid);
        }

        private object MakeLocalized(string tableName,string key,string text)
        {
            try
            {
                var settings=FindType("UnityEngine.Localization.Settings.LocalizationSettings");
                var db=GetStaticMember(settings,"StringDatabase");
                if(db==null)return null;
                object table=null;
                foreach(var m in db.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if(m.Name!="GetTable")continue;
                    var p=m.GetParameters();if(p.Length<1)continue;
                    try
                    {
                        var a=new object[p.Length];
                        a[0]=ConvertArg(tableName,p[0].ParameterType);
                        for(int i=1;i<p.Length;i++)a[i]=p[i].HasDefaultValue?p[i].DefaultValue:null;
                        table=m.Invoke(db,a);if(table!=null)break;
                    }catch{}
                }
                if(table==null)return null;
                object entry=null;
                foreach(var m in table.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    if(m.Name!="AddEntry")continue;
                    var p=m.GetParameters();if(p.Length<2)continue;
                    try
                    {
                        var a=new object[p.Length];
                        a[0]=ConvertArg(key,p[0].ParameterType);a[1]=ConvertArg(text,p[1].ParameterType);
                        for(int i=2;i<p.Length;i++)a[i]=p[i].HasDefaultValue?p[i].DefaultValue:null;
                        entry=m.Invoke(table,a);if(entry!=null)break;
                    }catch{}
                }
                if(entry==null)
                {
                    foreach(var m in table.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                    {
                        if(m.Name!="GetEntry"||m.GetParameters().Length!=1)continue;
                        try{entry=m.Invoke(table,new[]{ConvertArg(key,m.GetParameters()[0].ParameterType)});if(entry!=null)break;}catch{}
                    }
                }
                if(entry==null)return null;

                var tableRef=GetMember(table,"TableCollectionName","TableCollectionNameReference");
                var keyId=GetMember(entry,"KeyId","Key");
                var lsType=FindType("UnityEngine.Localization.LocalizedString");
                if(lsType==null)return null;
                foreach(var c in lsType.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    var p=c.GetParameters();if(p.Length!=2)continue;
                    try{return c.Invoke(new[]{ConvertArg(tableRef,p[0].ParameterType),ConvertArg(keyId,p[1].ParameterType)});}catch{}
                }
            }catch{}
            return null;
        }

        private object GetGameContext()
        {
            var t=FindTypeBySimpleName("GameContext");
            if(t==null)return null;
            return GetStaticMember(t,"Instance")??FindAllResources(t).FirstOrDefault();
        }

        private void ForceReferenceId(object obj,string refId)
        {
            SetMember(obj,"referenceId",refId);
            SetMember(obj,"initializedHashId",false);
            SetMember(obj,"stableHashedId",0);
            try{var _=GetMember(obj,"StableHashedId");}catch{}
        }

        private void EnsurePersistence(object pid,string id)
        {
            if(pid==null||string.IsNullOrEmpty(id))return;
            var primary=GetMember(pid,"_primaryIndex");
            int idx=IndexOf(primary,id);
            if(idx<0)
            {
                idx=Count(primary);
                AppendCollection(pid,"_primaryIndex",id);
            }
            var reverse=GetMember(pid,"_reverseIndex");
            DictSet(reverse,id,idx);
        }

        private static int IndexOf(object c,string s)
        {
            if(c is IEnumerable en){int i=0;foreach(var x in en){if(string.Equals(x?.ToString(),s,StringComparison.Ordinal))return i;i++;}}
            return -1;
        }
        private static int Count(object c)
        {
            if(c==null)return 0;var v=GetMember(c,"Count","Length");try{return Convert.ToInt32(v);}catch{}
            int n=0;if(c is IEnumerable en)foreach(var _ in en)n++;return n;
        }

        private static object CreateScriptable(Type t)
        {
            var so=FindType("UnityEngine.ScriptableObject");
            if(so==null||t==null)return null;
            foreach(var m in so.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="CreateInstance"||m.IsGenericMethod)continue;
                var p=m.GetParameters();
                if(p.Length==1)
                {
                    try{return m.Invoke(null,new[]{ConvertArg(t,p[0].ParameterType)});}catch{}
                }
            }
            return null;
        }

        private static void Pin(object obj)
        {
            if(obj==null)return;
            try{SetMember(obj,"hideFlags",61);}catch{}
            try
            {
                var uo=FindType("UnityEngine.Object");
                var m=uo?.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(x=>x.Name=="DontDestroyOnLoad"&&x.GetParameters().Length==1);
                m?.Invoke(null,new[]{obj});
            }catch{}
        }

        private static Type FindType(string full)
        {
            if(string.IsNullOrEmpty(full))return null;
            var t=Type.GetType(full);if(t!=null)return t;
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){try{t=a.GetType(full,false);if(t!=null)return t;}catch{}}
            return null;
        }
        private static Type FindTypeBySimpleName(string name)
        {
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try{foreach(var t in a.GetTypes())if(t!=null&&t.Name==name)return t;}
                catch(ReflectionTypeLoadException ex){foreach(var t in ex.Types)if(t!=null&&t.Name==name)return t;}
                catch{}
            }
            return null;
        }
        private static IEnumerable<object> FindAllResources(Type t)
        {
            if(t==null)yield break;
            var r=FindType("UnityEngine.Resources");if(r==null)yield break;
            foreach(var m in r.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="FindObjectsOfTypeAll"||!m.IsGenericMethodDefinition||m.GetGenericArguments().Length!=1||m.GetParameters().Length!=0)continue;
                object arr=null;try{arr=m.MakeGenericMethod(t).Invoke(null,null);}catch{}
                if(arr is IEnumerable en){foreach(var x in en)if(x!=null)yield return x;yield break;}
            }
        }
        private static object InstantiateObject(object original)
        {
            if(original==null)return null;
            var u=FindType("UnityEngine.Object");if(u==null)return null;
            foreach(var m in u.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod||m.GetParameters().Length!=1)continue;
                try{return m.Invoke(null,new[]{original});}catch{}
            }
            return null;
        }
        private static object InstantiateAt(object original,object pos,object rot)
        {
            if(original==null)return null;
            var u=FindType("UnityEngine.Object");
            foreach(var m in u.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod)continue;
                var p=m.GetParameters();if(p.Length!=3)continue;
                if(p[1].ParameterType.Name!="Vector3"||p[2].ParameterType.Name!="Quaternion")continue;
                try{return m.Invoke(null,new[]{original,pos,rot});}catch{}
            }
            return null;
        }
        private static object GetMember(object o,params string[] names)=>o==null?null:GetMemberCore(o.GetType(),o,names);
        private static object GetStaticMember(Type t,params string[] names)=>GetMemberCore(t,null,names);
        private static object GetMemberCore(Type t,object inst,params string[] names)
        {
            if(t==null)return null;var f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            foreach(var n in names)
            {
                try{var p=t.GetProperty(n,f);if(p!=null)return p.GetValue(inst);}catch{}
                try{var fi=t.GetField(n,f);if(fi!=null)return fi.GetValue(inst);}catch{}
            }
            return null;
        }
        private static bool SetMember(object o,string name,object val)
        {
            if(o==null)return false;var t=o.GetType();var f=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static;
            try{var p=t.GetProperty(name,f);if(p!=null&&p.CanWrite){p.SetValue(o,ConvertArg(val,p.PropertyType));return true;}}catch{}
            try{var fi=t.GetField(name,f);if(fi!=null){fi.SetValue(o,ConvertArg(val,fi.FieldType));return true;}}catch{}
            return false;
        }
        private static string GetString(object o,params string[] names)=>GetMember(o,names)?.ToString();
        private static object FirstOf(object c){if(c is IEnumerable en)foreach(var x in en)if(x!=null)return x;return null;}
        private static float Num(object o){try{return Convert.ToSingle(o);}catch{return 0f;}}

        private static object ConvertArg(object v,Type t)
        {
            if(v==null)return null;if(t.IsInstanceOfType(v))return v;
            if(t.IsEnum){try{return Enum.ToObject(t,Convert.ToInt32(v));}catch{}}
            try
            {
                foreach(var m in t.GetMethods(BindingFlags.Public|BindingFlags.Static))
                {
                    if(m.Name!="op_Implicit"&&m.Name!="op_Explicit")continue;
                    var p=m.GetParameters();if(p.Length==1&&p[0].ParameterType.IsInstanceOfType(v))return m.Invoke(null,new[]{v});
                }
            }catch{}
            try{return Convert.ChangeType(v,t);}catch{return v;}
        }

        private static object InvokeBest(object o,string name,params object[] args)
        {
            if(o==null)return null;
            foreach(var m in o.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static))
            {
                if(m.Name!=name)continue;var p=m.GetParameters();if(p.Length!=args.Length)continue;
                try
                {
                    var a=new object[args.Length];for(int i=0;i<args.Length;i++)a[i]=ConvertArg(args[i],p[i].ParameterType);
                    return m.Invoke(o,a);
                }catch{}
            }
            return null;
        }

        private static object GetComponent(object go,Type t)
        {
            if(go==null||t==null)return null;
            foreach(var m in go.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name=="GetComponent"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0)
                    try{return m.MakeGenericMethod(t).Invoke(go,null);}catch{}
            }
            return null;
        }
        private static IEnumerable<object> GetComponentsInChildren(object go,Type t)
        {
            if(go==null||t==null)yield break;
            foreach(var m in go.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="GetComponentsInChildren"||!m.IsGenericMethodDefinition)continue;
                var p=m.GetParameters();object arr=null;
                try
                {
                    var gm=m.MakeGenericMethod(t);
                    if(p.Length==1&&p[0].ParameterType==typeof(bool))arr=gm.Invoke(go,new object[]{true});
                    else if(p.Length==0)arr=gm.Invoke(go,null);
                }catch{}
                if(arr is IEnumerable en){foreach(var x in en)if(x!=null)yield return x;yield break;}
            }
        }

        private static bool HasMatProp(object m,string p)
        {
            try
            {
                var mm=m.GetType().GetMethod("HasProperty",new[]{typeof(string)});
                return mm!=null&&(bool)mm.Invoke(m,new object[]{p});
            }catch{return false;}
        }
        private static void SetMatColor(object m,string p,object c)
        {
            try
            {
                if(!HasMatProp(m,p))return;
                var mm=m.GetType().GetMethods().FirstOrDefault(x=>x.Name=="SetColor"&&x.GetParameters().Length==2&&x.GetParameters()[0].ParameterType==typeof(string));
                mm?.Invoke(m,new[]{(object)p,c});
            }catch{}
        }
        private static object MakeColor(Type t,float[] c)=>Activator.CreateInstance(t,new object[]{c[0],c[1],c[2],c[3]});
        private static void SetColorMember(object o,string name,float[] c)
        {
            var t=FindType("UnityEngine.Color");
            if(t!=null)SetMember(o,name,MakeColor(t,c));
        }

        private static object CreateCollection(Type t,List<object> vals)
        {
            if(t==null)return null;
            try
            {
                if(t.IsArray)
                {
                    var et=t.GetElementType();var arr=Array.CreateInstance(et,vals.Count);
                    for(int i=0;i<vals.Count;i++)arr.SetValue(ConvertArg(vals[i],et),i);return arr;
                }
                object obj=null;
                foreach(var len in new object[]{vals.Count,(long)vals.Count})
                {try{obj=Activator.CreateInstance(t,new[]{len});if(obj!=null)break;}catch{}}
                if(obj==null)return null;
                var item=t.GetProperty("Item",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if(item!=null&&item.CanWrite)
                {
                    var ip=item.GetIndexParameters().FirstOrDefault()?.ParameterType??typeof(int);
                    for(int i=0;i<vals.Count;i++)
                    {
                        object idx=ip==typeof(long)?(object)(long)i:i;
                        item.SetValue(obj,ConvertArg(vals[i],item.PropertyType),new[]{idx});
                    }
                    return obj;
                }
                foreach(var v in vals)TryAdd(obj,v);
                return obj;
            }catch{return null;}
        }
        private static void EmptyCollectionMember(object owner,string member)
        {
            var c=GetMember(owner,member);if(c==null)return;
            var empty=CreateCollection(c.GetType(),new List<object>());
            if(empty!=null)SetMember(owner,member,empty);
        }
        private static void AppendCollection(object owner,string member,object value)
        {
            if(owner==null||value==null)return;
            var cur=GetMember(owner,member);if(cur==null)return;
            if(CollectionContains(cur,value))return;
            if(TryAdd(cur,value))return;
            var vals=new List<object>();if(cur is IEnumerable en)foreach(var x in en)if(x!=null)vals.Add(x);
            vals.Add(value);var rep=CreateCollection(cur.GetType(),vals);if(rep!=null)SetMember(owner,member,rep);
        }
        private static bool CollectionContains(object c,object v)
        {
            if(c==null)return false;try
            {
                var m=c.GetType().GetMethods().FirstOrDefault(x=>x.Name=="Contains"&&x.GetParameters().Length==1);
                if(m!=null)return (bool)m.Invoke(c,new[]{ConvertArg(v,m.GetParameters()[0].ParameterType)});
            }catch{}
            if(c is IEnumerable en)foreach(var x in en)if(x!=null&&(ReferenceEquals(x,v)||x.Equals(v)))return true;
            return false;
        }
        private static bool TryAdd(object c,object v)
        {
            if(c==null||v==null)return false;
            foreach(var m in c.GetType().GetMethods())
            {
                if(m.Name!="Add"||m.GetParameters().Length!=1)continue;
                try{m.Invoke(c,new[]{ConvertArg(v,m.GetParameters()[0].ParameterType)});return true;}catch{}
            }
            return false;
        }
        private static bool DictContains(object d,object k)
        {
            if(d==null)return false;try
            {
                var m=d.GetType().GetMethods().FirstOrDefault(x=>x.Name=="ContainsKey"&&x.GetParameters().Length==1);
                return m!=null&&(bool)m.Invoke(d,new[]{ConvertArg(k,m.GetParameters()[0].ParameterType)});
            }catch{return false;}
        }
        private static void DictSet(object d,object k,object v)
        {
            if(d==null)return;
            try
            {
                var item=d.GetType().GetProperty("Item",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if(item!=null&&item.CanWrite)
                {
                    var ip=item.GetIndexParameters();var kk=ip.Length>0?ConvertArg(k,ip[0].ParameterType):k;
                    item.SetValue(d,ConvertArg(v,item.PropertyType),new[]{kk});return;
                }
            }catch{}
            if(!DictContains(d,k))
                foreach(var m in d.GetType().GetMethods())
                    if(m.Name=="Add"&&m.GetParameters().Length==2)
                    {try{var p=m.GetParameters();m.Invoke(d,new[]{ConvertArg(k,p[0].ParameterType),ConvertArg(v,p[1].ParameterType)});return;}catch{}}
        }

        private bool KeyPressed(string a,string b,string oldA,string oldB)
        {
            try
            {
                var kt=FindType("UnityEngine.InputSystem.Keyboard");
                var kb=kt==null?null:GetStaticMember(kt,"current");
                if(kb!=null&&(ControlPressed(kb,a)||ControlPressed(kb,b)))return true;
            }catch{}
            try
            {
                var input=FindType("UnityEngine.Input");var kc=FindType("UnityEngine.KeyCode");
                var m=input?.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(x=>x.Name=="GetKeyDown"&&x.GetParameters().Length==1&&x.GetParameters()[0].ParameterType==kc);
                if(m!=null&&((bool)m.Invoke(null,new[]{Enum.Parse(kc,oldA)})||(bool)m.Invoke(null,new[]{Enum.Parse(kc,oldB)})))return true;
            }catch{}
            return false;
        }
        private static bool ControlPressed(object kb,string name)
        {
            try{var c=GetMember(kb,name);var v=GetMember(c,"wasPressedThisFrame");return v is bool x&&x;}catch{return false;}
        }

        private void Show(string t,int sec){_status=t;_until=DateTime.UtcNow.AddSeconds(sec);}
        private bool EnsureGui()
        {
            if(_label!=null)return true;
            _rectType=FindType("UnityEngine.Rect");_guiType=FindType("UnityEngine.GUI");
            if(_rectType==null||_guiType==null)return false;
            _rectCtor=_rectType.GetConstructor(new[]{typeof(float),typeof(float),typeof(float),typeof(float)});
            _box=_guiType.GetMethod("Box",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            _label=_guiType.GetMethod("Label",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            return _rectCtor!=null&&_label!=null;
        }
        private object Rect(float x,float y,float w,float h)=>_rectCtor.Invoke(new object[]{x,y,w,h});
    }
}
