using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MelonLoader;

[assembly: MelonInfo(typeof(SR1LegacySlimes.ModEntry), "SR1 Legacy Slimes", "1.0.0", "Hei Games Studio")]
[assembly: MelonGame("MonomiPark", "SlimeRancher2")]

namespace SR1LegacySlimes
{
    public sealed class ModEntry : MelonMod
    {
        private sealed class Spec
        {
            public string Key;
            public string Display;
            public string RefId;
            public string PediaSuffix;
            public float[] Top;
            public float[] Middle;
            public float[] Bottom;
            public int IconMode;
            public string Description;
        }

        private sealed class BuiltSlime
        {
            public Spec Spec;
            public object Def;
            public object Appearance;
            public object Prefab;
            public object Icon;
            public object PediaEntry;
        }

        private readonly Spec[] _specs =
        {
            new Spec
            {
                Key="Rad", Display="Slime Rad", RefId="SlimeDefinition.SR1Rad", PediaSuffix="sr1rad", IconMode=0,
                Top=new[]{0.49f,0.98f,0.46f,1f}, Middle=new[]{0.25f,0.73f,0.28f,1f}, Bottom=new[]{0.12f,0.42f,0.16f,1f},
                Description=@"SLIMEOLOGIA

O Slime Rad é uma espécie radioativa clássica da Far, Far Range. Seu corpo emite um brilho verde intenso e uma aura energética característica. Nesta recriação para as novas regiões, ele mantém sua identidade como um slime vegetal associado historicamente à Oca Oca.

RISCOS PARA O RANCHEIRO

A energia emitida pelo Slime Rad torna a proximidade prolongada perigosa. Sua aura é a característica que o diferencia de outros slimes e exige cuidado quando vários exemplares ficam juntos.

PLORTONOMIA

Os plorts Rad são conhecidos por concentrar grande quantidade de energia em pouco espaço. O sistema de plorts será integrado em uma etapa posterior desta recriação."
            },
            new Spec
            {
                Key="Quantum", Display="Slime Quântico", RefId="SlimeDefinition.SR1Quantum", PediaSuffix="sr1quantum", IconMode=1,
                Top=new[]{1.00f,0.88f,0.26f,1f}, Middle=new[]{1.00f,0.59f,0.05f,1f}, Bottom=new[]{0.84f,0.34f,0.02f,1f},
                Description=@"SLIMEOLOGIA

O Slime Quântico é uma espécie associada a fenômenos de probabilidade e deslocamento incomum. Sua aparência dourada e suas antigas projeções fantasmagóricas fizeram dele uma das espécies mais estranhas já documentadas na Far, Far Range.

RISCOS PARA O RANCHEIRO

Historicamente, slimes Quânticos muito agitados podiam trocar de posição com suas projeções e escapar de currais. O comportamento quântico completo será integrado em uma etapa posterior desta recriação.

PLORTONOMIA

Os plorts Quânticos são ligados a propriedades físicas extremamente incomuns. O sistema de plorts será integrado em uma etapa posterior."
            },
            new Spec
            {
                Key="Mosaic", Display="Slime Mosaico", RefId="SlimeDefinition.SR1Mosaic", PediaSuffix="sr1mosaic", IconMode=2,
                Top=new[]{0.91f,0.98f,1.00f,1f}, Middle=new[]{0.47f,0.78f,1.00f,1f}, Bottom=new[]{0.67f,0.40f,0.96f,1f},
                Description=@"SLIMEOLOGIA

O Slime Mosaico possui uma superfície brilhante e facetada que refrata a luz em várias cores. Sua aparência cristalina transformou a espécie em uma das mais reconhecíveis da Far, Far Range.

RISCOS PARA O RANCHEIRO

Os Mosaicos são famosos por produzir reflexos luminosos capazes de se transformar em glints perigosos. Esse comportamento completo será integrado em uma etapa posterior desta recriação.

PLORTONOMIA

Os plorts Mosaico são valorizados por suas propriedades ópticas e estrutura incomum. O sistema de plorts será integrado posteriormente."
            }
        };

        private readonly Dictionary<string,BuiltSlime> _built = new Dictionary<string,BuiltSlime>(StringComparer.OrdinalIgnoreCase);
        private object _gameContext, _sceneContext, _pink, _holder;
        private bool _coreReady, _pediaReady;
        private int _frames;
        private DateTime _nextAttempt = DateTime.MinValue;
        private string _status = "SR1 Legacy Slimes: aguardando o save...";
        private DateTime _statusUntil = DateTime.UtcNow.AddSeconds(20);
        private Type _rectType,_guiType;
        private ConstructorInfo _rectCtor;
        private MethodInfo _guiBox,_guiLabel;

        public override void OnInitializeMelon()
        {
            LoggerInstance.Msg("SR1 Legacy Slimes v1.0.0 — reescrita completa.");
            LoggerInstance.Msg("Objetivo: Rad, Quantum e Mosaic reais + Vacpack + Slimepedia/Slimes. Sem plorts/largos/spawn natural.");
        }

        public override void OnSceneWasInitialized(int buildIndex,string sceneName) { ResetSceneRefs(); }
        public override void OnSceneWasLoaded(int buildIndex,string sceneName) { ResetSceneRefs(); }

        public override void OnUpdate()
        {
            _frames++;
            if(!_coreReady && DateTime.UtcNow>=_nextAttempt)
            {
                _nextAttempt=DateTime.UtcNow.AddSeconds(1);
                TryInitializeCore();
            }
            if(_coreReady && !_pediaReady && _frames%30==0) TryInstallPedia();

            if(KeyPressed("digit8Key","numpad8Key","Alpha8","Keypad8")) Spawn("Rad");
            if(KeyPressed("digit9Key","numpad9Key","Alpha9","Keypad9")) Spawn("Quantum");
            if(KeyPressed("digit0Key","numpad0Key","Alpha0","Keypad0")) Spawn("Mosaic");
        }

        public override void OnGUI()
        {
            try
            {
                if(DateTime.UtcNow>_statusUntil || !EnsureGui()) return;
                _guiBox?.Invoke(null,new[]{Rect(16,16,710,58),""});
                _guiLabel?.Invoke(null,new[]{Rect(30,32,680,26),_status});
            }
            catch { }
        }

        private void ResetSceneRefs()
        {
            _sceneContext=null;
            if(_coreReady){_pediaReady=false;_nextAttempt=DateTime.UtcNow.AddMilliseconds(500);}
        }

        private void TryInitializeCore()
        {
            try
            {
                _gameContext=GetGameContext();
                _sceneContext=GetSceneContext();
                if(_gameContext==null || _sceneContext==null){SetStatus("Aguardando GameContext/SceneContext...",2);return;}

                var slimeType=FindTypeBySimpleName("SlimeDefinition");
                if(slimeType==null) return;
                var defs=FindAllResources(slimeType).ToList();
                if(defs.Count<40){SetStatus("Aguardando o mundo carregar por completo...",2);return;}

                _pink=FindPink(defs);
                if(_pink==null){LoggerInstance.Warning("Pink SlimeDefinition não encontrado.");return;}

                EnsureInactiveHolder();
                foreach(var spec in _specs)
                {
                    if(_built.ContainsKey(spec.Key)) continue;
                    var b=BuildSpecies(spec);
                    if(b==null){LoggerInstance.Warning("Falha ao construir "+spec.Key+".");return;}
                    _built[spec.Key]=b;
                }

                _coreReady=_built.Count==3;
                if(!_coreReady) return;

                SetStatus("3 espécies prontas — 8 Rad | 9 Quântico | 0 Mosaico",10);
                LoggerInstance.Msg("CORE OK: 3 SlimeDefinitions próprios criados e registrados.");
                foreach(var b in _built.Values) LogSpeciesState(b);
                TryInstallPedia();
            }
            catch(Exception ex){LoggerInstance.Error("Inicialização: "+ex);}
        }

        private BuiltSlime BuildSpecies(Spec spec)
        {
            var def=InstantiateObject(_pink);
            if(def==null) return null;
            PinAsset(def);
            SetMember(def,"name","SR1Legacy_"+spec.Key+"Slime");
            ForceReferenceId(def,spec.RefId);
            SetMember(def,"_pediaPersistenceSuffix",spec.PediaSuffix);

            var name=MakeLocalized("Actor","sr1legacy.name."+spec.Key.ToLowerInvariant(),spec.Display);
            if(name!=null) SetMember(def,"localizedName",name);
            SetColor(def,"color",spec.Middle);

            var icon=BuildIcon(spec.IconMode);
            if(icon!=null){SetMember(def,"icon",icon);SetMember(def,"debugIcon",icon);}

            var baseApp=FirstOf(GetMember(_pink,"AppearancesDefault"));
            var app=CloneAppearance(baseApp,spec,icon);
            if(app==null) return null;

            var oldApps=GetMember(_pink,"AppearancesDefault");
            var newApps=oldApps==null?null:CreateCollection(oldApps.GetType(),new List<object>{app});
            if(newApps==null) return null;
            SetMember(def,"AppearancesDefault",newApps);
            SetMember(def,"CanLargofy",false);
            PrepareNoPlortDiet(def);

            var pinkPrefab=GetMember(_pink,"prefab");
            var prefab=InstantiateUnderParent(pinkPrefab,GetMember(_holder,"transform"));
            if(prefab==null) return null;

            SetMember(prefab,"name","SR1Legacy_"+spec.Key+"_Prefab");
            SetHideFlags(prefab,32);
            SetMember(def,"prefab",prefab);
            WirePrefab(prefab,def,app);
            RecolorObject(prefab,spec);
            RegisterSlime(def);

            return new BuiltSlime{Spec=spec,Def=def,Appearance=app,Prefab=prefab,Icon=icon};
        }

        private object CloneAppearance(object baseApp,Spec spec,object icon)
        {
            if(baseApp==null) return null;
            var app=InstantiateObject(baseApp);
            if(app==null) return null;
            PinAsset(app);
            SetMember(app,"name","SR1Legacy_App_"+spec.Key);
            if(icon!=null) SetMember(app,"_icon",icon);

            var baseStructs=GetMember(baseApp,"Structures","_structures");
            if(baseStructs is IEnumerable structures)
            {
                var cloned=new List<object>();
                foreach(var s in structures)
                {
                    if(s==null) continue;
                    object ns=null;
                    try
                    {
                        var ctor=s.GetType().GetConstructor(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{s.GetType()},null);
                        if(ctor!=null) ns=ctor.Invoke(new[]{s});
                    }
                    catch { }
                    if(ns==null) continue;

                    var mats=GetMember(s,"DefaultMaterials");
                    if(mats is IEnumerable me)
                    {
                        var newMats=new List<object>();
                        foreach(var m in me)
                        {
                            var nm=m==null?null:InstantiateObject(m);
                            if(nm!=null) RecolorMaterial(nm,spec);
                            newMats.Add(nm);
                        }
                        var matCollection=CreateCollection(mats.GetType(),newMats);
                        if(matCollection!=null) SetMember(ns,"DefaultMaterials",matCollection);
                    }
                    cloned.Add(ns);
                }
                if(cloned.Count>0)
                {
                    var coll=CreateCollection(baseStructs.GetType(),cloned);
                    if(coll!=null)
                    {
                        if(!SetMember(app,"Structures",coll)) SetMember(app,"_structures",coll);
                    }
                }
            }

            SetAppearancePalette(app,spec);
            SetColor(app,"_splatColor",spec.Middle);
            return app;
        }

        private void SetAppearancePalette(object app,Spec spec)
        {
            try
            {
                var pType=app.GetType().GetNestedType("Palette",BindingFlags.Public|BindingFlags.NonPublic);
                if(pType==null) return;
                var p=Activator.CreateInstance(pType);
                var top=MakeColor(spec.Top);var mid=MakeColor(spec.Middle);var bot=MakeColor(spec.Bottom);
                SetMember(p,"Ammo",mid);SetMember(p,"Top",top);SetMember(p,"Middle",mid);SetMember(p,"Bottom",bot);
                SetMember(app,"_colorPalette",p);
            }
            catch { }
        }

        private void PrepareNoPlortDiet(object def)
        {
            try
            {
                var diet=GetMember(def,"Diet");
                if(diet==null) return;
                EnsureEmptyCollection(diet,"FavoriteIdents");
                EnsureEmptyCollection(diet,"AdditionalFoodIdents");
                EnsureEmptyCollection(diet,"ProduceIdents");
                EnsureEmptyCollection(diet,"MajorFoodIdentifiableTypeGroups");
                Invoke(GetMember(diet,"EatMap"),"Clear");
            }
            catch(Exception ex){LoggerInstance.Warning("Diet: "+ex.Message);}
        }

        private void WirePrefab(object prefab,object def,object app)
        {
            if(prefab==null||def==null) return;
            var ident=GetComponent(prefab,FindTypeBySimpleName("IdentifiableActor"));
            var applicator=GetComponent(prefab,FindTypeBySimpleName("SlimeAppearanceApplicator"));
            var eat=GetComponent(prefab,FindTypeBySimpleName("SlimeEat"));
            if(ident!=null) SetMember(ident,"identType",def);
            if(applicator!=null){SetMember(applicator,"SlimeDefinition",def);SetMember(applicator,"Appearance",app);}
            if(eat!=null) SetMember(eat,"SlimeDefinition",def);
        }

        private void ForceLiveInstance(object go,BuiltSlime slime)
        {
            if(go==null||slime==null) return;
            WirePrefab(go,slime.Def,slime.Appearance);
            var app=GetComponent(go,FindTypeBySimpleName("SlimeAppearanceApplicator"));
            if(app!=null)
            {
                SetMember(app,"SlimeDefinition",slime.Def);
                SetMember(app,"Appearance",slime.Appearance);
                Invoke(app,"ApplyAppearance");
            }
            RecolorObject(go,slime.Spec);
            var ident=GetComponent(go,FindTypeBySimpleName("IdentifiableActor"));
            if(ident!=null){SetMember(ident,"identType",slime.Def);Invoke(ident,"RegistryUpdate");}
        }

        private void RegisterSlime(object def)
        {
            var gc=_gameContext??GetGameContext();
            if(gc==null||def==null) return;
            var refId=GetString(def,"ReferenceId","referenceId");
            if(string.IsNullOrEmpty(refId)) throw new InvalidOperationException("SlimeDefinition sem ReferenceId.");

            var lookup=GetMember(gc,"LookupDirector");
            var defs=GetMember(gc,"SlimeDefinitions");
            var autosave=GetMember(gc,"AutoSaveDirector");

            DictAddIfMissing(GetMember(defs,"_slimeDefinitionsByIdentifiable"),def,def);
            AppendCollection(defs,"Slimes",def);
            DictAddIfMissing(GetMember(lookup,"_identifiableTypeByRefId"),refId,def);

            try
            {
                var master=GetMember(GetMember(autosave,"_configuration"),"_identifiableTypes");
                if(master!=null) Invoke(lookup,"AddIdentifiableTypeToGroup",def,master);
            }
            catch { }

            try
            {
                var tr=GetMember(autosave,"_saveReferenceTranslation");
                DictAddIfMissing(GetMember(tr,"_identifiableTypeLookup"),refId,def);
                var pid=GetMember(tr,"_identifiableTypeToPersistenceId");
                if(IndexOfString(GetMember(pid,"_primaryIndex"),refId)<0) AppendCollection(pid,"_primaryIndex",refId);
                var reverse=GetMember(pid,"_reverseIndex");
                if(!DictContains(reverse,refId)) DictAddIfMissing(reverse,refId,Count(reverse));
            }
            catch(Exception ex){LoggerInstance.Warning("Save registration "+refId+": "+ex.Message);}

            string[] groups={"VaccableBaseSlimeGroup","BaseSlimeGroup","SlimesGroup","SmallSlimeGroup","EdibleSlimeGroup","IdentifiableTypesGroup","SlimesSinkInShallowWaterGroup"};
            foreach(var groupName in groups)
            {
                var group=FindGroup(groupName);
                if(group==null){LoggerInstance.Warning(refId+": grupo ausente: "+groupName);continue;}
                Invoke(lookup,"AddIdentifiableTypeToGroup",def,group);
                LoggerInstance.Msg(refId+": grupo "+groupName+" registrado.");
            }
        }

        private object FindGroup(string exactName)
        {
            var groupType=FindTypeBySimpleName("IdentifiableTypeGroup");
            if(groupType==null) return null;
            foreach(var g in FindAllResources(groupType))
                if(string.Equals(GetString(g,"name"),exactName,StringComparison.OrdinalIgnoreCase)) return g;
            return null;
        }

        private void TryInstallPedia()
        {
            if(!_coreReady||_pediaReady) return;
            try
            {
                var entryType=FindTypeBySimpleName("IdentifiablePediaEntry");
                var categoryType=FindTypeBySimpleName("PediaCategory");
                if(entryType==null||categoryType==null) return;

                object pinkEntry=null;
                foreach(var e in FindAllResources(entryType))
                {
                    if(SameObject(GetMember(e,"_identifiableType"),_pink)){pinkEntry=e;break;}
                }
                if(pinkEntry==null){LoggerInstance.Warning("Slimepedia: entrada do Pink não encontrada.");return;}

                object slimeCategory=null;
                foreach(var c in FindAllResources(categoryType))
                {
                    if(CollectionContains(GetMember(c,"_items"),pinkEntry)){slimeCategory=c;break;}
                }
                if(slimeCategory==null)
                {
                    foreach(var c in FindAllResources(categoryType))
                        if(string.Equals(GetString(c,"name"),"Slimes",StringComparison.OrdinalIgnoreCase)){slimeCategory=c;break;}
                }
                if(slimeCategory==null){LoggerInstance.Warning("Slimepedia: categoria real de Slimes não encontrada.");return;}

                var lookup=GetMember(_gameContext??GetGameContext(),"LookupDirector");
                var pediaDirector=GetMember(_sceneContext??GetSceneContext(),"PediaDirector");
                int ok=0;

                foreach(var pair in _built)
                {
                    var slime=pair.Value;
                    object entry=pediaDirector==null?null:Invoke(pediaDirector,"GetEntry",slime.Def);
                    if(entry==null)
                    {
                        entry=InstantiateObject(pinkEntry);
                        if(entry==null) continue;
                        PinAsset(entry);
                        SetMember(entry,"name","SR1Legacy_Pedia_"+slime.Spec.Key);
                        SetMember(entry,"_identifiableType",slime.Def);
                        SetMember(entry,"_title",GetMember(slime.Def,"localizedName"));
                        var desc=MakeLocalized("Pedia","sr1legacy.pedia."+slime.Spec.Key.ToLowerInvariant(),slime.Spec.Description);
                        if(desc!=null) SetMember(entry,"_description",desc);
                        SetMember(entry,"_isUnlockedInitially",true);
                        if(pediaDirector!=null) SetMember(entry,"_unlockInfoProvider",pediaDirector);
                    }

                    slime.PediaEntry=entry;
                    AppendCollection(slimeCategory,"_items",entry);
                    if(lookup!=null) Invoke(lookup,"AddPediaEntryToCategory",entry,slimeCategory);
                    var runtime=Invoke(slimeCategory,"GetRuntimeCategory");
                    if(runtime!=null) TryAdd(GetMember(runtime,"_items","Items"),entry);

                    if(pediaDirector!=null)
                    {
                        DictSet(GetMember(pediaDirector,"_customIdentToEntryMap"),slime.Def,entry);
                        DictSet(GetMember(pediaDirector,"_identDict"),slime.Def,entry);
                        Invoke(pediaDirector,"Unlock",entry,false);
                        Invoke(pediaDirector,"Unlock",slime.Def,false);
                        Invoke(pediaDirector,"Discover",slime.Def);
                    }

                    var found=pediaDirector==null?entry:Invoke(pediaDirector,"GetEntry",slime.Def);
                    bool inCategory=CollectionContains(GetMember(slimeCategory,"_items"),entry);
                    LoggerInstance.Msg("PEDIA "+slime.Spec.Key+": categoria="+(GetString(slimeCategory,"name")??"?")+" item="+inCategory+" director="+(found!=null));
                    if(inCategory&&found!=null) ok++;
                }

                if(ok==3)
                {
                    _pediaReady=true;
                    SetStatus("Slimepedia OK: Rad, Quântico e Mosaico em Slimes.",10);
                    LoggerInstance.Msg("PEDIA OK: 3/3 espécies ligadas à categoria Slimes.");
                }
            }
            catch(Exception ex){LoggerInstance.Error("Slimepedia: "+ex);}
        }

        private void Spawn(string key)
        {
            if(!_coreReady)
            {
                TryInitializeCore();
                if(!_coreReady){SetStatus("O save ainda não terminou de carregar.",4);return;}
            }
            if(!_built.TryGetValue(key,out var slime)) return;

            try
            {
                object pos,rot;GetSpawnTransform(out pos,out rot);
                var scene=_sceneContext??GetSceneContext();
                if(scene==null){SetStatus("SceneContext indisponível.",4);return;}

                object go=null;
                try
                {
                    var model=Invoke(GetMember(scene,"GameModel"),"InstantiateActorModel",slime.Def,GetMember(GetMember(scene,"RegionRegistry"),"CurrentSceneGroup"),pos,rot,false);
                    go=InvokeStatic(FindTypeBySimpleName("InstantiationHelpers"),"InstantiateActorFromModel",model);
                }
                catch(Exception ex){LoggerInstance.Warning("Actor spawn "+key+": "+ex.Message);}

                if(go==null) go=InstantiateAt(slime.Prefab,pos,rot);
                if(go==null){SetStatus("Falha ao criar "+slime.Spec.Display+".",4);return;}

                Invoke(go,"SetActive",true);
                ForceLiveInstance(go,slime);

                var ident=GetComponent(go,FindTypeBySimpleName("IdentifiableActor"));
                var vac=GetComponent(go,FindTypeBySimpleName("Vacuumable"));
                var actual=GetMember(ident,"identType");
                string actualId=GetString(actual,"ReferenceId","referenceId","name")??"NULL";
                LoggerInstance.Msg("SPAWN "+key+": ident="+actualId+" vacuumable="+(vac!=null));
                SetStatus(slime.Spec.Display+" | ident="+actualId+" | Vacpack="+(vac!=null?"SIM":"NÃO"),6);
            }
            catch(Exception ex){LoggerInstance.Error("Spawn "+key+": "+ex);SetStatus("Erro ao spawnar "+key+". Veja Latest.log.",5);}
        }

        private void RecolorObject(object go,Spec spec)
        {
            if(go==null) return;
            var rendererType=FindType("UnityEngine.Renderer");
            if(rendererType==null) return;
            foreach(var renderer in GetComponentsInChildren(go,rendererType))
            {
                RecolorMaterial(GetMember(renderer,"material"),spec);
                RecolorMaterial(GetMember(renderer,"sharedMaterial"),spec);
                if(GetMember(renderer,"materials") is IEnumerable me) foreach(var m in me) RecolorMaterial(m,spec);
                if(GetMember(renderer,"sharedMaterials") is IEnumerable se) foreach(var m in se) RecolorMaterial(m,spec);
            }
        }

        private void RecolorMaterial(object mat,Spec spec)
        {
            if(mat==null) return;
            var top=MakeColor(spec.Top);var mid=MakeColor(spec.Middle);var bottom=MakeColor(spec.Bottom);
            SetMaterialColor(mat,"_TopColor",top);SetMaterialColor(mat,"_MiddleColor",mid);SetMaterialColor(mat,"_BottomColor",bottom);
            SetMaterialColor(mat,"_SpecColor",mid);SetMaterialColor(mat,"_Color",mid);SetMaterialColor(mat,"_BaseColor",mid);
            if(spec.Key=="Rad"){EnableKeyword(mat,"_EMISSION");SetMaterialColor(mat,"_EmissionColor",top);}
        }

        private void SetMaterialColor(object mat,string property,object color)
        {
            try
            {
                var has=mat.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance).FirstOrDefault(m=>m.Name=="HasProperty"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(string));
                if(has!=null && !(bool)has.Invoke(mat,new object[]{property})) return;
                foreach(var m in mat.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
                {
                    if(m.Name!="SetColor") continue;var p=m.GetParameters();
                    if(p.Length!=2||p[0].ParameterType!=typeof(string)) continue;
                    m.Invoke(mat,new[]{(object)property,ConvertArg(color,p[1].ParameterType)});return;
                }
            }
            catch { }
        }

        private void EnableKeyword(object mat,string keyword)
        {
            try
            {
                foreach(var m in mat.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
                {
                    if(m.Name!="EnableKeyword") continue;var p=m.GetParameters();
                    if(p.Length==1&&p[0].ParameterType==typeof(string)){m.Invoke(mat,new object[]{keyword});return;}
                }
            }
            catch { }
        }

        private object BuildIcon(int mode)
        {
            try
            {
                const int W=128,H=128;var data=new byte[W*H*4];
                for(int y=0;y<H;y++) for(int x=0;x<W;x++)
                {
                    double dx=(x-63.5)/57.0,dy=(y-63.5)/57.0,r=Math.Sqrt(dx*dx+dy*dy);
                    byte R=0,G=0,B=0,A=0;
                    if(r<=1.0)
                    {
                        A=255;
                        if(mode==0){R=55;G=184;B=74;if(r>.82){R=106;G=255;B=118;}}
                        else if(mode==1){R=255;G=161;B=22;if(r>.82){R=255;G=232;B=63;}}
                        else
                        {
                            double a=Math.Atan2(dy,dx)+Math.PI;int band=(int)(a/(Math.PI*2)*6.0);
                            byte[][] colors={new byte[]{255,93,173},new byte[]{126,72,255},new byte[]{48,171,255},new byte[]{72,235,192},new byte[]{255,225,74},new byte[]{255,130,62}};
                            var c=colors[Math.Max(0,Math.Min(colors.Length-1,band))];R=c[0];G=c[1];B=c[2];
                        }
                        bool le=((dx+0.34)*(dx+0.34)+(dy+0.11)*(dy+0.11))<0.025;
                        bool re=((dx-0.34)*(dx-0.34)+(dy+0.11)*(dy+0.11))<0.025;
                        bool mouth=dy>0.18&&dy<0.33&&Math.Abs(dx)<0.28&&dy>0.28-0.45*Math.Abs(dx);
                        if(le||re||mouth){R=42;G=32;B=53;}
                    }
                    int i=(y*W+x)*4;data[i]=R;data[i+1]=G;data[i+2]=B;data[i+3]=A;
                }

                var textureType=FindType("UnityEngine.Texture2D");var formatType=FindType("UnityEngine.TextureFormat");
                var spriteType=FindType("UnityEngine.Sprite");var rectType=FindType("UnityEngine.Rect");var vector2Type=FindType("UnityEngine.Vector2");
                if(textureType==null||formatType==null||spriteType==null||rectType==null||vector2Type==null) return null;
                var rgba32=Enum.Parse(formatType,"RGBA32");object texture=null;
                foreach(var c in textureType.GetConstructors())
                {
                    var p=c.GetParameters();
                    if(p.Length==4&&p[0].ParameterType==typeof(int)&&p[1].ParameterType==typeof(int)&&p[2].ParameterType==formatType&&p[3].ParameterType==typeof(bool))
                    {texture=c.Invoke(new object[]{W,H,rgba32,false});break;}
                }
                if(texture==null) return null;PinAsset(texture);
                var load=textureType.GetMethods(BindingFlags.Public|BindingFlags.Instance).FirstOrDefault(m=>m.Name=="LoadRawTextureData"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==typeof(byte[]));
                load?.Invoke(texture,new object[]{data});Invoke(texture,"Apply");
                var rect=Activator.CreateInstance(rectType,new object[]{0f,0f,(float)W,(float)H});
                var pivot=Activator.CreateInstance(vector2Type,new object[]{0.5f,0.5f});
                foreach(var m in spriteType.GetMethods(BindingFlags.Public|BindingFlags.Static))
                {
                    if(m.Name!="Create") continue;var p=m.GetParameters();if(p.Length<3) continue;
                    try
                    {
                        var args=new object[p.Length];args[0]=ConvertArg(texture,p[0].ParameterType);args[1]=ConvertArg(rect,p[1].ParameterType);args[2]=ConvertArg(pivot,p[2].ParameterType);
                        for(int i=3;i<p.Length;i++) args[i]=p[i].HasDefaultValue?p[i].DefaultValue:null;
                        var sprite=m.Invoke(null,args);if(sprite!=null){PinAsset(sprite);return sprite;}
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private void LogSpeciesState(BuiltSlime slime)
        {
            try
            {
                var ident=GetComponent(slime.Prefab,FindTypeBySimpleName("IdentifiableActor"));
                var app=GetComponent(slime.Prefab,FindTypeBySimpleName("SlimeAppearanceApplicator"));
                var eat=GetComponent(slime.Prefab,FindTypeBySimpleName("SlimeEat"));
                var vac=GetComponent(slime.Prefab,FindTypeBySimpleName("Vacuumable"));
                var actual=GetMember(ident,"identType");
                LoggerInstance.Msg("VERIFY "+slime.Spec.Key+" ref="+(GetString(slime.Def,"ReferenceId","referenceId")??"?")+" prefabIdent="+(GetString(actual,"ReferenceId","referenceId","name")??"NULL")+" app="+(app!=null)+" eat="+(eat!=null)+" vacuumable="+(vac!=null));
            }
            catch { }
        }

        private object GetGameContext()
        {
            var t=FindTypeBySimpleName("GameContext");if(t==null) return null;
            return GetStaticMember(t,"Instance")??FindAllResources(t).FirstOrDefault();
        }

        private object GetSceneContext()
        {
            var t=FindTypeBySimpleName("SceneContext");if(t==null) return null;
            return GetStaticMember(t,"Instance")??FindAllResources(t).FirstOrDefault();
        }

        private object FindPink(List<object> defs)
        {
            foreach(var d in defs)
            {
                var refId=GetString(d,"ReferenceId","referenceId")??"";var name=GetString(d,"name")??"";
                if(refId=="SlimeDefinition.Pink"||refId.EndsWith(".Pink",StringComparison.OrdinalIgnoreCase)||name=="Pink"||name=="PinkSlime") return d;
            }
            foreach(var d in defs)
            {
                var refId=GetString(d,"ReferenceId","referenceId")??"";
                if(refId.IndexOf("Pink",StringComparison.OrdinalIgnoreCase)>=0) return d;
            }
            return null;
        }

        private void EnsureInactiveHolder()
        {
            if(_holder!=null) return;
            var goType=FindType("UnityEngine.GameObject");if(goType==null) return;object holder=null;
            try{var ctor=goType.GetConstructor(new[]{typeof(string)});if(ctor!=null) holder=ctor.Invoke(new object[]{"SR1LegacySlimes_Holder"});}catch{}
            if(holder==null) try{holder=Activator.CreateInstance(goType);}catch{}
            _holder=holder;
            if(_holder!=null){SetMember(_holder,"name","SR1LegacySlimes_Holder");Invoke(_holder,"SetActive",false);DontDestroy(_holder);}
        }

        private object MakeLocalized(string tableName,string key,string text)
        {
            try
            {
                var db=GetStaticMember(FindType("UnityEngine.Localization.Settings.LocalizationSettings"),"StringDatabase");if(db==null) return null;
                var table=Invoke(db,"GetTable",tableName);if(table==null) return null;
                var entry=Invoke(table,"AddEntry",key,text)??Invoke(table,"GetEntry",key);if(entry==null) return null;
                var collection=GetMember(table,"TableCollectionName","TableCollectionNameReference");var keyId=GetMember(entry,"KeyId","Key");
                var localizedType=FindType("UnityEngine.Localization.LocalizedString");if(localizedType==null) return null;
                foreach(var ctor in localizedType.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
                {
                    var p=ctor.GetParameters();if(p.Length!=2) continue;
                    try{return ctor.Invoke(new[]{ConvertArg(collection,p[0].ParameterType),ConvertArg(keyId,p[1].ParameterType)});}catch{}
                }
            }
            catch(Exception ex){LoggerInstance.Warning("Localization: "+ex.Message);}
            return null;
        }

        private void ForceReferenceId(object ident,string refId)
        {
            if(ident==null) return;
            SetMember(ident,"referenceId",refId);SetMember(ident,"initializedHashId",false);SetMember(ident,"stableHashedId",0);GetMember(ident,"StableHashedId");
        }

        private static int IndexOfString(object collection,string value)
        {
            if(collection is IEnumerable en){int i=0;foreach(var x in en){if(string.Equals(x?.ToString(),value,StringComparison.Ordinal)) return i;i++;}}
            return -1;
        }

        private void GetSpawnTransform(out object position,out object rotation)
        {
            var v3=FindType("UnityEngine.Vector3");var quat=FindType("UnityEngine.Quaternion");
            position=Activator.CreateInstance(v3,new object[]{0f,5f,0f});rotation=GetStaticMember(quat,"identity");
            try
            {
                var cam=GetStaticMember(FindType("UnityEngine.Camera"),"main");var tr=GetMember(cam,"transform");var p=GetMember(tr,"position");var f=GetMember(tr,"forward");
                float px=ToFloat(GetMember(p,"x")),py=ToFloat(GetMember(p,"y")),pz=ToFloat(GetMember(p,"z"));
                float fx=ToFloat(GetMember(f,"x")),fy=ToFloat(GetMember(f,"y")),fz=ToFloat(GetMember(f,"z"));
                position=Activator.CreateInstance(v3,new object[]{px+fx*3f,py+fy*3f+0.5f,pz+fz*3f});
            }
            catch { }
        }

        private static Type FindType(string fullName)
        {
            if(string.IsNullOrEmpty(fullName)) return null;var t=Type.GetType(fullName);if(t!=null) return t;
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies()){try{t=a.GetType(fullName,false);if(t!=null) return t;}catch{}}
            return null;
        }

        private static Type FindTypeBySimpleName(string name)
        {
            foreach(var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                try{foreach(var t in a.GetTypes()) if(t!=null&&t.Name==name) return t;}
                catch(ReflectionTypeLoadException ex){foreach(var t in ex.Types) if(t!=null&&t.Name==name) return t;}
                catch{}
            }
            return null;
        }

        private static IEnumerable<object> FindAllResources(Type type)
        {
            if(type==null) yield break;var resources=FindType("UnityEngine.Resources");if(resources==null) yield break;
            foreach(var m in resources.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="FindObjectsOfTypeAll"||!m.IsGenericMethodDefinition||m.GetGenericArguments().Length!=1||m.GetParameters().Length!=0) continue;
                object array=null;try{array=m.MakeGenericMethod(type).Invoke(null,null);}catch{}
                if(array is IEnumerable en){foreach(var x in en) if(x!=null) yield return x;yield break;}
            }
        }

        private static object GetMember(object obj,params string[] names){return obj==null?null:GetMemberCore(obj.GetType(),obj,names);}
        private static object GetStaticMember(Type type,params string[] names){return type==null?null:GetMemberCore(type,null,names);}

        private static object GetMemberCore(Type type,object instance,params string[] names)
        {
            const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
            for(var t=type;t!=null;t=t.BaseType)
            {
                foreach(var name in names)
                {
                    try{var p=t.GetProperty(name,flags);if(p!=null) return p.GetValue(instance);}catch{}
                    try{var f=t.GetField(name,flags);if(f!=null) return f.GetValue(instance);}catch{}
                }
            }
            return null;
        }

        private static bool SetMember(object obj,string name,object value)
        {
            if(obj==null) return false;const BindingFlags flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.DeclaredOnly;
            for(var t=obj.GetType();t!=null;t=t.BaseType)
            {
                try{var p=t.GetProperty(name,flags);if(p!=null&&p.CanWrite){p.SetValue(obj,ConvertArg(value,p.PropertyType));return true;}}catch{}
                try{var f=t.GetField(name,flags);if(f!=null){f.SetValue(obj,ConvertArg(value,f.FieldType));return true;}}catch{}
            }
            return false;
        }

        private static string GetString(object obj,params string[] names){return GetMember(obj,names)?.ToString();}
        private static bool SameObject(object a,object b){if(a==null||b==null) return false;if(ReferenceEquals(a,b)) return true;try{return a.Equals(b);}catch{return false;}}

        private static object Invoke(object obj,string name,params object[] args)
        {
            if(obj==null) return null;
            foreach(var m in obj.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            {
                if(m.Name!=name) continue;var p=m.GetParameters();if(p.Length!=args.Length) continue;
                try{var converted=new object[args.Length];for(int i=0;i<args.Length;i++) converted[i]=ConvertArg(args[i],p[i].ParameterType);return m.Invoke(obj,converted);}catch{}
            }
            return null;
        }

        private static object InvokeStatic(Type type,string name,params object[] args)
        {
            if(type==null) return null;
            foreach(var m in type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static))
            {
                if(m.Name!=name) continue;var p=m.GetParameters();if(p.Length!=args.Length) continue;
                try{var converted=new object[args.Length];for(int i=0;i<args.Length;i++) converted[i]=ConvertArg(args[i],p[i].ParameterType);return m.Invoke(null,converted);}catch{}
            }
            return null;
        }

        private static object InstantiateObject(object original)
        {
            if(original==null) return null;var u=FindType("UnityEngine.Object");if(u==null) return null;
            foreach(var m in u.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod||m.GetParameters().Length!=1) continue;
                try{return m.Invoke(null,new[]{original});}catch{}
            }
            return null;
        }

        private static object InstantiateUnderParent(object original,object parent)
        {
            if(original==null||parent==null) return null;var u=FindType("UnityEngine.Object");if(u==null) return null;
            foreach(var m in u.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod) continue;var p=m.GetParameters();
                if(p.Length==2&&p[1].ParameterType.Name=="Transform")
                    try{return m.Invoke(null,new[]{ConvertArg(original,p[0].ParameterType),ConvertArg(parent,p[1].ParameterType)});}catch{}
            }
            foreach(var m in u.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod) continue;var p=m.GetParameters();
                if(p.Length==3&&p[1].ParameterType.Name=="Transform"&&p[2].ParameterType==typeof(bool))
                    try{return m.Invoke(null,new[]{ConvertArg(original,p[0].ParameterType),ConvertArg(parent,p[1].ParameterType),(object)false});}catch{}
            }
            return InstantiateObject(original);
        }

        private static object InstantiateAt(object original,object position,object rotation)
        {
            if(original==null) return null;var u=FindType("UnityEngine.Object");
            foreach(var m in u.GetMethods(BindingFlags.Public|BindingFlags.Static))
            {
                if(m.Name!="Instantiate"||m.IsGenericMethod) continue;var p=m.GetParameters();
                if(p.Length==3&&p[1].ParameterType.Name=="Vector3"&&p[2].ParameterType.Name=="Quaternion")
                    try{return m.Invoke(null,new[]{ConvertArg(original,p[0].ParameterType),ConvertArg(position,p[1].ParameterType),ConvertArg(rotation,p[2].ParameterType)});}catch{}
            }
            return null;
        }

        private static object GetComponent(object gameObject,Type componentType)
        {
            if(gameObject==null||componentType==null) return null;
            foreach(var m in gameObject.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name=="GetComponent"&&m.IsGenericMethodDefinition&&m.GetParameters().Length==0)
                    try{return m.MakeGenericMethod(componentType).Invoke(gameObject,null);}catch{}
            }
            return null;
        }

        private static IEnumerable<object> GetComponentsInChildren(object go,Type type)
        {
            if(go==null||type==null) yield break;
            foreach(var m in go.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="GetComponentsInChildren"||!m.IsGenericMethodDefinition) continue;var p=m.GetParameters();object array=null;
                try{var gm=m.MakeGenericMethod(type);if(p.Length==1&&p[0].ParameterType==typeof(bool)) array=gm.Invoke(go,new object[]{true});else if(p.Length==0) array=gm.Invoke(go,null);}catch{}
                if(array is IEnumerable en){foreach(var x in en) if(x!=null) yield return x;yield break;}
            }
        }

        private static object FirstOf(object collection){if(collection is IEnumerable en) foreach(var x in en) if(x!=null) return x;return null;}

        private static object CreateCollection(Type type,List<object> values)
        {
            if(type==null) return null;
            try
            {
                if(type.IsArray){var elem=type.GetElementType();var array=Array.CreateInstance(elem,values.Count);for(int i=0;i<values.Count;i++) array.SetValue(ConvertArg(values[i],elem),i);return array;}
                object result=null;try{result=Activator.CreateInstance(type,new object[]{values.Count});}catch{} if(result==null) try{result=Activator.CreateInstance(type,new object[]{(long)values.Count});}catch{}
                if(result==null) return null;
                var item=type.GetProperty("Item",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if(item!=null&&item.CanWrite)
                {
                    var indexType=item.GetIndexParameters().FirstOrDefault()?.ParameterType??typeof(int);
                    for(int i=0;i<values.Count;i++){object index=indexType==typeof(long)?(object)(long)i:i;item.SetValue(result,ConvertArg(values[i],item.PropertyType),new[]{index});}
                    return result;
                }
                foreach(var v in values) TryAdd(result,v);return result;
            }
            catch{return null;}
        }

        private static void EnsureEmptyCollection(object owner,string member){var current=GetMember(owner,member);if(current==null) return;var empty=CreateCollection(current.GetType(),new List<object>());if(empty!=null) SetMember(owner,member,empty);}

        private static void AppendCollection(object owner,string member,object item)
        {
            if(owner==null||item==null) return;var current=GetMember(owner,member);if(current==null) return;if(CollectionContains(current,item)) return;if(TryAdd(current,item)) return;
            var list=new List<object>();if(current is IEnumerable en) foreach(var x in en) list.Add(x);list.Add(item);
            var replacement=CreateCollection(current.GetType(),list);if(replacement!=null) SetMember(owner,member,replacement);
        }

        private static bool CollectionContains(object collection,object item)
        {
            if(collection==null||item==null) return false;
            foreach(var m in collection.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="Contains"||m.GetParameters().Length!=1) continue;
                try{return (bool)m.Invoke(collection,new[]{ConvertArg(item,m.GetParameters()[0].ParameterType)});}catch{}
            }
            if(collection is IEnumerable en) foreach(var x in en) if(SameObject(x,item)) return true;return false;
        }

        private static bool TryAdd(object collection,object item)
        {
            if(collection==null||item==null) return false;
            foreach(var m in collection.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            {
                if(m.Name!="Add"||m.GetParameters().Length!=1) continue;
                try{m.Invoke(collection,new[]{ConvertArg(item,m.GetParameters()[0].ParameterType)});return true;}catch{}
            }
            return false;
        }

        private static bool DictContains(object dict,object key)
        {
            if(dict==null) return false;
            foreach(var m in dict.GetType().GetMethods(BindingFlags.Public|BindingFlags.Instance))
            {
                if(m.Name!="ContainsKey"||m.GetParameters().Length!=1) continue;
                try{return (bool)m.Invoke(dict,new[]{ConvertArg(key,m.GetParameters()[0].ParameterType)});}catch{}
            }
            return false;
        }

        private static void DictAddIfMissing(object dict,object key,object value)
        {
            if(dict==null||key==null||DictContains(dict,key)) return;
            foreach(var m in dict.GetType().GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance))
            {
                if(m.Name!="Add"||m.GetParameters().Length!=2) continue;
                try{var p=m.GetParameters();m.Invoke(dict,new[]{ConvertArg(key,p[0].ParameterType),ConvertArg(value,p[1].ParameterType)});return;}catch{}
            }
        }

        private static void DictSet(object dict,object key,object value)
        {
            if(dict==null||key==null) return;
            try
            {
                var item=dict.GetType().GetProperty("Item",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);
                if(item!=null&&item.CanWrite)
                {
                    var indexes=item.GetIndexParameters();var k=indexes.Length>0?ConvertArg(key,indexes[0].ParameterType):key;
                    item.SetValue(dict,ConvertArg(value,item.PropertyType),new[]{k});return;
                }
            }
            catch{}
            DictAddIfMissing(dict,key,value);
        }

        private static int Count(object obj)
        {
            if(obj==null) return 0;var v=GetMember(obj,"Count","Length");try{return Convert.ToInt32(v);}catch{}
            int n=0;if(obj is IEnumerable en) foreach(var _ in en) n++;return n;
        }

        private static object ConvertArg(object value,Type target)
        {
            if(value==null) return null;if(target.IsInstanceOfType(value)) return value;
            if(target.IsEnum) try{return Enum.ToObject(target,Convert.ToInt32(value));}catch{}
            try
            {
                foreach(var m in target.GetMethods(BindingFlags.Public|BindingFlags.Static))
                {
                    if(m.Name!="op_Implicit"&&m.Name!="op_Explicit") continue;var p=m.GetParameters();
                    if(p.Length==1&&p[0].ParameterType.IsInstanceOfType(value)) return m.Invoke(null,new[]{value});
                }
            }
            catch{}
            try{return Convert.ChangeType(value,target);}catch{return value;}
        }

        private static float ToFloat(object value){try{return Convert.ToSingle(value);}catch{return 0f;}}
        private static object MakeColor(float[] rgba){var t=FindType("UnityEngine.Color");return t==null?null:Activator.CreateInstance(t,new object[]{rgba[0],rgba[1],rgba[2],rgba[3]});}
        private static void SetColor(object obj,string member,float[] rgba){var c=MakeColor(rgba);if(c!=null) SetMember(obj,member,c);}
        private static void SetHideFlags(object obj,int value){SetMember(obj,"hideFlags",value);}
        private static void PinAsset(object obj){if(obj==null) return;SetHideFlags(obj,32);DontDestroy(obj);}

        private static void DontDestroy(object obj)
        {
            if(obj==null) return;
            try
            {
                var m=FindType("UnityEngine.Object")?.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(x=>x.Name=="DontDestroyOnLoad"&&x.GetParameters().Length==1);
                m?.Invoke(null,new[]{obj});
            }
            catch{}
        }

        private bool KeyPressed(string newA,string newB,string oldA,string oldB)
        {
            try
            {
                var keyboard=GetStaticMember(FindType("UnityEngine.InputSystem.Keyboard"),"current");
                if(keyboard!=null&&(ControlPressed(keyboard,newA)||ControlPressed(keyboard,newB))) return true;
            }
            catch{}
            try
            {
                var input=FindType("UnityEngine.Input");var keyCode=FindType("UnityEngine.KeyCode");
                var get=input?.GetMethods(BindingFlags.Public|BindingFlags.Static).FirstOrDefault(m=>m.Name=="GetKeyDown"&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType==keyCode);
                if(get!=null&&((bool)get.Invoke(null,new[]{Enum.Parse(keyCode,oldA)})||(bool)get.Invoke(null,new[]{Enum.Parse(keyCode,oldB)}))) return true;
            }
            catch{}
            return false;
        }

        private static bool ControlPressed(object keyboard,string controlName)
        {
            try{var control=GetMember(keyboard,controlName);var value=GetMember(control,"wasPressedThisFrame");return value is bool b&&b;}catch{return false;}
        }

        private void SetStatus(string text,int seconds){_status=text;_statusUntil=DateTime.UtcNow.AddSeconds(seconds);}

        private bool EnsureGui()
        {
            if(_guiLabel!=null) return true;
            _rectType=FindType("UnityEngine.Rect");_guiType=FindType("UnityEngine.GUI");if(_rectType==null||_guiType==null) return false;
            _rectCtor=_rectType.GetConstructor(new[]{typeof(float),typeof(float),typeof(float),typeof(float)});
            _guiBox=_guiType.GetMethod("Box",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            _guiLabel=_guiType.GetMethod("Label",BindingFlags.Public|BindingFlags.Static,null,new[]{_rectType,typeof(string)},null);
            return _rectCtor!=null&&_guiLabel!=null;
        }

        private object Rect(float x,float y,float w,float h){return _rectCtor.Invoke(new object[]{x,y,w,h});}
    }
}
