using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // One-way integration of existing native Tresura roster, menu and server
    // RPC. Every Harmony hook is installed independently; missing preview/UI
    // symbols cannot disable adoption, ownership or cosmetics.
    internal static class DeguFamiliarBridge
    {
        const string Owner="sirroh.tresura.owner.v1";
        const string ColorKey="sirroh.tresura.namecolor.v1";
        const string LevelKey="sirroh.pet.level.v1";
        static readonly string[] CoatNames={
            "Naturalne umaszczenie","Kość słoniowa","Wilcza czerń",
            "Ciemny brąz","Popielata szarość","Krwawy Księżyc","Nocne Niebo"
        };
        static readonly HashSet<string> Warned=new HashSet<string>();
        static Harmony harmony;
        static Type taming,panel,infoType,wolfVisuals;
        static FieldInfo idsField,detailsField,pendingField,dirtyField;
        static MethodInfo send,save,slots,capture,selected,eyeName;
        static float nextRecovery;

        static void Warn(string msg)
        {
            if(Warned.Add(msg))DeguPlugin.Log?.LogWarning("DEGU_TRESURA: "+msg);
        }
        static Type FindOverhaul(string name)
        {
            if(Chainloader.PluginInfos.TryGetValue("sirro.valheim.pracafizyczna",out var p))
                return p.Instance?.GetType().Assembly.GetType("Sirro.PracaFizyczna."+name,false);
            return AccessTools.TypeByName("Sirro.PracaFizyczna."+name);
        }
        static FieldInfo InfoField(string field)=>infoType?.GetField(field,
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        static object Get(object record,string field)=>record==null?null:InfoField(field)?.GetValue(record);
        static void Set(object record,string field,object value)=>InfoField(field)?.SetValue(record,value);
        static int Int(object record,string field,int otherwise=0)=>Get(record,field) is int i?i:otherwise;
        static bool Degu(object record)=>string.Equals(Get(record,"Prefab") as string,"Degu",StringComparison.Ordinal);
        static bool Alive(object record)=>Get(record,"Alive") is bool b && b;
        static ZDOID Id(object record)=>Get(record,"Id") is ZDOID id?id:ZDOID.None;
        static void Dirty()=>dirtyField?.SetValue(null,true);

        static void Patch(Type cls,string name,string hook,bool prefix)
        {
            MethodInfo target=AccessTools.Method(cls,name);
            MethodInfo method=AccessTools.Method(typeof(DeguFamiliarBridge),hook);
            if(target==null || method==null){Warn("feature skipped: "+name+" / "+hook+" unavailable");return;}
            try{
                harmony.Patch(target,prefix?new HarmonyMethod(method):null,
                    prefix?null:new HarmonyMethod(method));
            }catch(Exception ex){Warn("patch "+name+": "+ex.GetBaseException().Message);}
        }

        internal static void Install()
        {
            if(harmony!=null)return;
            taming=FindOverhaul("TamingSkill");
            panel=FindOverhaul("FamiliarMenuPanel");
            wolfVisuals=FindOverhaul("WolfVisuals");
            if(taming==null){Warn("Overhaul.TamingSkill not loaded yet; will retry after scene init.");return;}
            infoType=taming.GetNestedType("FamiliarInfo",BindingFlags.NonPublic|BindingFlags.Public);
            if(infoType==null){Warn("Overhaul FamiliarInfo type not found.");return;}
            idsField=AccessTools.Field(taming,"Familiars");
            detailsField=AccessTools.Field(taming,"Details");
            pendingField=AccessTools.Field(taming,"Pending");
            dirtyField=AccessTools.Field(taming,"_dirtyRecords");
            send=AccessTools.Method(taming,"Send");
            save=AccessTools.Method(taming,"Save");
            capture=AccessTools.Method(taming,"Capture");
            slots=AccessTools.Method(taming,"Slots");
            selected=panel==null?null:AccessTools.Method(panel,"Selected");
            eyeName=wolfVisuals==null?null:AccessTools.Method(wolfVisuals,"EyeName");
            harmony=new Harmony("sirroh.degu.rebuild.tresura.v040");
            Patch(taming,"SkinCount",nameof(SkinCount),false);
            Patch(taming,"SkinName",nameof(SkinName),false);
            Patch(taming,"ChooseSkin",nameof(ChooseSkin),true);
            Patch(taming,"ChooseEyeColor",nameof(ChooseEyes),true);
            Patch(taming,"ChangeNameColor",nameof(ChangeName),true);
            Patch(taming,"ToggleElementRunes",nameof(ToggleRunes),true);
            Patch(taming,"HandleLifecycleRequest",nameof(ServerOperation),true);
            Patch(taming,"Capture",nameof(CaptureDone),false);
            Patch(taming,"LifecycleTick",nameof(BeforeLifecycleTick),true);
            Patch(taming,"OnAnswer",nameof(AfterAdoption),false);
            if(panel!=null)
            {
                Patch(panel,"Refresh",nameof(BeforeRefresh),true);
                Patch(panel,"UpdateDetail",nameof(AfterDetail),false);
            }
            DeguPlugin.Log?.LogInfo("DEGU_TRESURA: independent native hooks registered; inspect warnings for skipped features.");
        }

        internal static void Shutdown()
        {
            harmony?.UnpatchSelf();harmony=null;
            Warned.Clear();nextRecovery=0f;
        }

        static void SkinCount(object info,ref int __result)
        {if(Degu(info))__result=7;}
        static void SkinName(object info,ref string __result)
        {if(Degu(info))__result=CoatNames[Mathf.Clamp(Int(info,"Skin"),0,6)];}

        static bool Request(object info,int op,int choice)
        {
            Player p=Player.m_localPlayer;
            if(!p || !Alive(info) || Id(info)==ZDOID.None || ZRoutedRpc.instance==null || send==null)
            {Warn("native request unavailable: op "+op);return false;}
            try
            {
                send.Invoke(null,new object[]{p,op,Id(info),Vector3.zero,choice});
                return true;
            }
            catch(Exception ex){Warn("native request "+op+": "+ex.GetBaseException().Message);return false;}
        }
        static bool ChooseSkin(object info,int direction)
        {
            if(!Degu(info))return true;
            int next=(Int(info,"Skin")+direction+7)%7;
            if(Request(info,10,next)){Set(info,"Skin",next);Dirty();}
            return false;
        }
        static bool ChooseEyes(object info,int direction)
        {
            if(!Degu(info))return true;
            int current=Mathf.Clamp(Int(info,"EyeColor",1),1,6);
            int next=1+(current-1+direction+6)%6;
            if(Request(info,18,next)){Set(info,"EyeColor",next);Dirty();}
            return false;
        }
        static bool ChangeName(object familiar,int direction)
        {
            if(!Degu(familiar))return true;
            int next=(Int(familiar,"NameColor",1)+direction+16)%16;
            if(Request(familiar,11,next)){Set(familiar,"NameColor",next);Dirty();}
            return false;
        }
        static bool ToggleRunes(object info)
        {
            if(!Degu(info))return true;
            if(Int(info,"PetLevel",1)<25)return false;
            bool state=Get(info,"ShowRunes") is bool b && b;
            if(Request(info,17,state?0:1)){Set(info,"ShowRunes",!state);Dirty();}
            return false;
        }

        // Only run for Degu-specific cosmetic opcodes after validating the
        // native server's owner roster and creature owner. Never bypass
        // Overhaul authorization for a missing, foreign or invalid ZDO.
        static bool ServerOperation(long sender,int op,long userId,
            Vector3 playerPosition,ZDOID target,ZPackage payload)
        {
            if(op!=10 && op!=11 && op!=17 && op!=18)return true;
            ZDO zdo=ZDOMan.instance?.GetZDO(target);
            if(zdo==null || ZNetScene.instance==null)return true;
            GameObject prefab=ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            if(!prefab || prefab.name!="Degu")return true;
            if(userId<=0 || zdo.GetLong(Owner,0L)!=userId ||
               !(pendingField?.GetValue(null) is List<ZDOID> roster) ||
               !roster.Contains(target) || payload==null)
            {
                Warn("rejected unauthorized Degu appearance command.");
                return false;
            }
            int value;
            try{value=payload.ReadInt();}catch(Exception){return false;}
            if(op==10)
            {
                if(value<0 || value>6)return false;
                zdo.Set(DeguWolfPalette.SkinKey,value);
                zdo.Set(DeguWolfPalette.ActiveKey,true);
            }
            else if(op==18)
            {
                if(value<1 || value>6)return false;
                zdo.Set(DeguWolfPalette.EyeKey,value);
            }
            else if(op==11)
            {
                if(value<0 || value>15)return false;
                zdo.Set(ColorKey,value);
            }
            else
            {
                if(value!=0 && value!=1 || zdo.GetInt(LevelKey,1)<25)return false;
                zdo.Set(DeguWolfPalette.RunesKey,value==1);
            }
            ZDOMan.instance.ForceSendZDO(target);
            DeguPlugin.Log?.LogInfo("DEGU_TRESURA: server saved op="+op+
                " value="+value+" ZDO="+target);
            return false;
        }

        static void UpdateInfo(ZDOID id)
        {
            if(id==ZDOID.None || ZDOMan.instance==null ||
               Player.m_localPlayer==null || ZNetScene.instance==null)return;
            var list=idsField?.GetValue(null) as List<ZDOID>;
            var data=detailsField?.GetValue(null) as IDictionary;
            if(list==null || !list.Contains(id) || data==null)return;
            ZDO zdo=ZDOMan.instance.GetZDO(id);
            if(zdo==null || zdo.GetLong(Owner,0L)!=Player.m_localPlayer.GetPlayerID())return;
            GameObject prefab=ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            if(!prefab || prefab.name!="Degu")return;
            object info=data.Contains(id)?data[id]:null;
            if(info==null)
            {
                info=Activator.CreateInstance(infoType,true);
                Set(info,"Id",id);
                data[id]=info;
            }
            string known=Get(info,"Prefab") as string;
            if(!string.IsNullOrEmpty(known) && known!="Degu")return;
            if(known!="Degu"){Set(info,"Prefab","Degu");Dirty();}
            Set(info,"Skin",Mathf.Clamp(zdo.GetInt(DeguWolfPalette.SkinKey,0),0,6));
            Set(info,"EyeColor",Mathf.Clamp(zdo.GetInt(DeguWolfPalette.EyeKey,0),0,6));
            Set(info,"ShowRunes",zdo.GetBool(DeguWolfPalette.RunesKey,true));
            Set(info,"PetLevel",Mathf.Clamp(zdo.GetInt(LevelKey,1),1,100));
            Set(info,"NameColor",Mathf.Clamp(zdo.GetInt(ColorKey,1),0,15));
        }

        static void CaptureDone(Character animal)
        {
            if(!animal || !animal.name.StartsWith("Degu",StringComparison.Ordinal))return;
            ZNetView view=animal.GetComponent<ZNetView>();
            if(view && view.IsValid())UpdateInfo(view.GetZDO().m_uid);
        }
        static void BeforeLifecycleTick(Player player)
        {
            if(player==null)return;
            var list=idsField?.GetValue(null) as List<ZDOID>;
            if(list==null)return;
            foreach(ZDOID id in list.ToArray())UpdateInfo(id);
        }
        static void AfterAdoption(ZPackage pkg)
        {
            if(pkg==null)return;
            try
            {
                var copy=new ZPackage(pkg.GetArray());
                UpdateInfo(copy.ReadZDOID());
            }catch(Exception){}
        }
        static void BeforeRefresh()
        {
            var list=idsField?.GetValue(null) as List<ZDOID>;
            if(list==null)return;
            foreach(ZDOID id in list.ToArray())UpdateInfo(id);
            RecoverExistingDegu();
        }
        static void RecoverExistingDegu()
        {
            Player p=Player.m_localPlayer;
            if(!p || !ZNetScene.instance || Time.unscaledTime<nextRecovery ||
               idsField==null || slots==null || save==null || capture==null)return;
            nextRecovery=Time.unscaledTime+6f;
            var list=idsField.GetValue(null) as List<ZDOID>;
            if(list==null)return;
            int limit;
            try{limit=(int)slots.Invoke(null,new object[]{p});}
            catch(Exception){return;}
            if(list.Count>=limit)return;
            int count=0;
            foreach(Character animal in Character.GetAllCharacters())
            {
                if(!animal || !animal.IsTamed() || animal.gameObject.name!="Degu(Clone)")continue;
                ZNetView view=animal.GetComponent<ZNetView>();
                if(!view || !view.IsValid())continue;
                ZDO zdo=view.GetZDO();
                if(zdo==null || zdo.GetLong(Owner,0L)!=p.GetPlayerID() ||
                   list.Contains(zdo.m_uid))continue;
                GameObject prefab=ZNetScene.instance.GetPrefab(zdo.GetPrefab());
                if(!prefab || prefab.name!="Degu" || list.Count>=limit)continue;
                list.Add(zdo.m_uid);
                capture.Invoke(null,new object[]{animal});
                count++;
            }
            if(count>0)
            {
                save.Invoke(null,new object[]{p});
                DeguPlugin.Log?.LogInfo("DEGU_TRESURA: restored "+count+
                    " already-owned Degu roster entries; no new creatures created.");
            }
        }

        static object Panel(string name)
        {
            return panel==null?null:AccessTools.Field(panel,name)?.GetValue(null);
        }
        static void Control(string field,bool enable)
        {
            object obj=Panel(field);
            var prop=obj?.GetType().GetProperty("interactable");
            if(prop!=null && prop.CanWrite)prop.SetValue(obj,enable,null);
        }
        static void Label(string field,string content)
        {
            object obj=Panel(field);
            var prop=obj?.GetType().GetProperty("text");
            if(prop!=null && prop.CanWrite)prop.SetValue(obj,content,null);
        }
        static void AfterDetail()
        {
            object info=selected?.Invoke(null,null);
            if(!Degu(info))return;
            bool alive=Alive(info);
            Label("_furName",CoatNames[Mathf.Clamp(Int(info,"Skin"),0,6)]);
            int eye=Mathf.Clamp(Int(info,"EyeColor"),0,6);
            Label("_eyeName",eyeName?.Invoke(null,new object[]{eye}) as string ?? "Losowe");
            Control("_left",alive);Control("_right",alive);
            Control("_eyeLeft",alive);Control("_eyeRight",alive);
            Control("_fontLeft",alive);Control("_fontRight",alive);
            Control("_runesToggle",alive && Int(info,"PetLevel",1)>=25);
            // Native panel explicitly treats only Wolf as rune-eligible.
            object image=Panel("_runesIcon");
            bool on=Get(info,"ShowRunes") is bool b && b;
            var property=image?.GetType().GetProperty("color");
            if(property!=null && property.CanWrite)
                property.SetValue(image,alive && Int(info,"PetLevel",1)>=25?
                    (on?new Color(.24f,.86f,.32f):new Color(.93f,.24f,.22f)):
                    new Color(.6f,.6f,.6f,.45f),null);
        }
    }
}
