using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // Independent 0.22.65-only adapter for the COMPLETE Tresura action path.
    // A Harmony transpiler which changes a Wolf comparison alone does not
    // guarantee that controls, local record, server RPC and ZDO all agree.
    internal static class DeguFamiliarRepair
    {
        private const string OverhaulId = "sirro.valheim.pracafizyczna";
        private const string RequiredVersion = "0.22.65";
        private const string OwnerKey = "sirroh.tresura.owner.v1";
        private const string NameColorKey = "sirroh.tresura.namecolor.v1";
        private const string PetLevelKey = "sirroh.pet.level.v1";
        private const string ElementKey = "sirroh.pet.element.v1";
        private const string RunesKey = "sirroh.wolf.showrunes.v1";
        private const string EyeKey = "sirroh.wolf.eye.v1";
        private const string SkinKey = "furRandomizerSet";
        private const string ActiveKey = "furRandomizerSetActive";

        private static Harmony harmony;
        private static MethodInfo selected, send, eyeName;
        private static FieldInfo pending, dirty, details, familiars;
        private static readonly FieldInfo PlayerNetworkView=AccessTools.Field(typeof(Character),"m_nview");
        private static FieldInfo furPrev, furNext, eyePrev, eyeNext, namePrev, nameNext, rune;
        private static Type infoType;
        private static string lastState;
        private static readonly string[] FurNames={
            "Naturalne umaszczenie","Kość słoniowa","Wilcza czerń",
            "Ciemny brąz","Popielata szarość","Krwawy Księżyc","Nocne Niebo"
        };
        private static FieldInfo InstanceField(string name)=>
            infoType.GetField(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
        private static object Get(object record,string field)=>InstanceField(field)?.GetValue(record);
        private static void Set(object record,string field,object value)=>InstanceField(field)?.SetValue(record,value);
        private static bool IsDegu(object record)=>record!=null && string.Equals(Get(record,"Prefab") as string,"Degu",StringComparison.Ordinal);
        private static bool Alive(object record)=>Get(record,"Alive") is bool live && live;
        private static int GetInt(object record,string field,int fallback=0)=>Get(record,field) is int n?n:fallback;
        private static ZDOID GetId(object record)=>Get(record,"Id") is ZDOID id?id:ZDOID.None;
        private static void Dirty()=>dirty?.SetValue(null,true);

        private static string lastFailure;
        internal static void Install()
        {
            if(harmony!=null)return;
            try
            {
                Assembly asm=null;
                if(Chainloader.PluginInfos.TryGetValue(OverhaulId,out var plugin))
                {
                    System.Version version=plugin.Metadata.Version;
                    if(version.Major!=0 || version.Minor!=22 || version.Build!=65)
                    {
                        ReportUnavailable("installed Overhaul "+version+"; expected "+RequiredVersion);
                        return;
                    }
                    asm=plugin.Instance.GetType().Assembly;
                }
                // BepInEx may not expose the plugin through PluginInfos
                // during this mod's Awake. Re-resolve at ZNetScene setup.
                Type fallback=AccessTools.TypeByName("Sirro.PracaFizyczna.TamingSkill");
                if(asm==null)asm=fallback?.Assembly;
                if(asm==null)
                {
                    ReportUnavailable("Overhaul TamingSkill has not loaded yet");
                    return;
                }
                Type taming=asm.GetType("Sirro.PracaFizyczna.TamingSkill",true);
                Type panel=asm.GetType("Sirro.PracaFizyczna.FamiliarMenuPanel",true);
                Type wolfVisuals=asm.GetType("Sirro.PracaFizyczna.WolfVisuals",true);
                eyeName=AccessTools.Method(wolfVisuals,"EyeName");
                infoType=taming.GetNestedType("FamiliarInfo",BindingFlags.NonPublic);
                if(infoType==null)throw new MissingMemberException("TamingSkill.FamiliarInfo");
                selected=AccessTools.Method(panel,"Selected");
                send=AccessTools.Method(taming,"Send");
                pending=AccessTools.Field(taming,"Pending");
                familiars=AccessTools.Field(taming,"Familiars");
                dirty=AccessTools.Field(taming,"_dirtyRecords");
                details=AccessTools.Field(taming,"Details");
                furPrev=AccessTools.Field(panel,"_left");
                furNext=AccessTools.Field(panel,"_right");
                eyePrev=AccessTools.Field(panel,"_eyeLeft");
                eyeNext=AccessTools.Field(panel,"_eyeRight");
                namePrev=AccessTools.Field(panel,"_fontLeft");
                nameNext=AccessTools.Field(panel,"_fontRight");
                rune=AccessTools.Field(panel,"_runesToggle");
                if(selected==null || send==null || eyeName==null || pending==null || familiars==null || dirty==null ||
                   details==null || furPrev==null || furNext==null ||
                   eyePrev==null || eyeNext==null || namePrev==null ||
                   nameNext==null || rune==null)
                    throw new MissingMemberException("Overhaul 0.22.65 Tresura surface");
                foreach(string field in new[]{"Prefab","Id","Alive","Skin","EyeColor",
                                             "NameColor","ShowRunes","PetLevel"})
                    if(InstanceField(field)==null)throw new MissingFieldException("FamiliarInfo."+field);
                harmony=new Harmony("sirroh.degu.familiar.controls.v02265");
                Patch(taming,"SkinCount",nameof(SkinCount),false,true);
                Patch(taming,"SkinName",nameof(SkinName),false,true);
                Patch(taming,"ChooseSkin",nameof(ChooseSkin),true,false);
                Patch(taming,"ChooseEyeColor",nameof(ChooseEyes),true,false);
                Patch(taming,"ChangeNameColor",nameof(ChooseName),true,false);
                Patch(taming,"ToggleElementRunes",nameof(ToggleRunes),true,false);
                Patch(taming,"HandleLifecycleRequest",nameof(ServerRequest),true,false);
                Patch(taming,"Capture",nameof(AfterCapture),false,true);
                Patch(taming,"OnStatus",nameof(BeforeStatus),true,false);
                Patch(taming,"OnAnswer",nameof(AfterAdopt),false,true);
                Patch(panel,"UpdateDetail",nameof(AfterDetail),false,true);
                lastFailure=null;
                DeguPlugin.Log?.LogInfo(
                    "DEGU_TRESURA: native menu/RPC hooks ACTIVE; 7 coats, 6 eyes, 16 name colors and level-25 runes.");
            }
            catch(Exception e)
            {
                harmony?.UnpatchSelf();
                harmony=null;
                ReportUnavailable("menu/RPC patch initialization exception: "+e.GetBaseException());
            }
        }
        private static void ReportUnavailable(string cause)
        {
            if(cause==lastFailure)return;
            lastFailure=cause;
            DeguPlugin.Log?.LogWarning("DEGU_TRESURA unavailable: "+cause);
        }
        private static void Patch(Type type,string target,string patch,bool prefix,bool postfix)
        {
            MethodInfo method=AccessTools.Method(type,target);
            MethodInfo handler=AccessTools.Method(typeof(DeguFamiliarRepair),patch);
            if(method==null || handler==null)
                throw new MissingMethodException(type.FullName,target+"/"+patch);
            harmony.Patch(method,prefix:prefix?new HarmonyMethod(handler):null,
                         postfix:postfix?new HarmonyMethod(handler):null);
        }
        internal static void Shutdown()
        {
            harmony?.UnpatchSelf();
            harmony=null;
            lastState=null;
        }
        private static object Selection() => selected?.Invoke(null,null);
        private static bool IsSelectedDegu(object info) => IsDegu(info);

        // Original MaterialCatalog does not know the Degu metadata. Avoid
        // depending on another adapter's string-param alias patch.
        private static void SkinCount(object info,ref int __result)
        {if(IsDegu(info))__result=7;}
        private static void SkinName(object info,ref string __result)
        {if(IsDegu(info))__result=FurNames[Mathf.Clamp(GetInt(info,"Skin"),0,6)];}

        private static bool SendRequest(object info,int opcode,int value)
        {
            Player player=Player.m_localPlayer;
            if(!player || !Alive(info) || GetId(info)==ZDOID.None ||
                ZRoutedRpc.instance==null)
            {
                DeguPlugin.Log?.LogWarning("DEGU_TRESURA: menu request blocked: local player, valid familiar or network unavailable; op="+opcode);
                return false;
            }
            ZNetView ownView=PlayerNetworkView?.GetValue(player) as ZNetView;
            if(!ownView || !ownView.IsValid())
            {
                DeguPlugin.Log?.LogWarning("DEGU_TRESURA: native Player.m_nview unavailable; op="+opcode);
                return false;
            }
            try
            {
                // Match the exact native Overhaul Player.m_nview condition.
                // Redundant guards can silently suppress every menu click.
                send.Invoke(null,new object[]{player,opcode,GetId(info),Vector3.zero,value});
                DeguPlugin.Log?.LogInfo("DEGU_TRESURA: sent native request op="+opcode+
                    " value="+value+" target="+GetId(info));
                return true;
            }
            catch(Exception e)
            {
                DeguPlugin.Log?.LogError("DEGU_TRESURA: failed to send opcode "+opcode+": "+e.GetBaseException());
                return false;
            }
        }
        private static bool ChooseSkin(object info,int direction)
        {
            if(!IsDegu(info))return true;
            if(!Alive(info))return false;
            int next=(GetInt(info,"Skin")+direction+7)%7;
            if(!SendRequest(info,10,next))return false;
            Set(info,"Skin",next);
            Dirty();
            return false;
        }
        private static bool ChooseEyes(object info,int direction)
        {
            if(!IsDegu(info))return true;
            if(!Alive(info))return false;
            int current=Mathf.Clamp(GetInt(info,"EyeColor",1),1,6);
            int next=1+(current-1+direction+6)%6;
            if(!SendRequest(info,18,next))return false;
            Set(info,"EyeColor",next);
            Dirty();
            return false;
        }
        private static bool ChooseName(object familiar,int direction)
        {
            if(!IsDegu(familiar))return true;
            if(!Alive(familiar))return false;
            int next=(GetInt(familiar,"NameColor",1)+direction+16)%16;
            if(!SendRequest(familiar,11,next))return false;
            Set(familiar,"NameColor",next);
            Dirty();
            return false;
        }
        private static bool ToggleRunes(object info)
        {
            if(!IsDegu(info))return true;
            if(!Alive(info) || GetInt(info,"PetLevel",1)<25)return false;
            bool previous=Get(info,"ShowRunes") is bool b && b;
            if(!SendRequest(info,17,previous?0:1))return false;
            Set(info,"ShowRunes",!previous);
            Dirty();
            return false;
        }

        // Called only after Overhaul has checked that sender owns the player's
        // ZDO and its persistent playerID tag matches userId. Preserve its
        // roster check and independently verify pet ownership.
        private static bool ServerRequest(long sender,int op,long userId,
            Vector3 playerPosition,ZDOID target,ZPackage payload)
        {
            if(op!=10 && op!=11 && op!=17 && op!=18)return true;
            ZDO pet=ZDOMan.instance?.GetZDO(target);
            if(pet==null || ZNetScene.instance==null)return true;
            GameObject prefab=ZNetScene.instance.GetPrefab(pet.GetPrefab());
            if(!prefab || prefab.name!="Degu")return true;
            if(userId<=0L || pet.GetLong(OwnerKey,0L)!=userId ||
               !(pending.GetValue(null) is List<ZDOID> roster) ||
               !roster.Contains(target))
            {
                DeguPlugin.Log?.LogWarning("Degu Tresura: rejected unauthorized cosmetic request.");
                return false;
            }
            int requested=payload.ReadInt();
            if(op==10)
            {
                if(requested<0 || requested>=7)return false;
                pet.Set(SkinKey,requested);
                pet.Set(ActiveKey,true);
            }
            else if(op==11)
            {
                if(requested<0 || requested>15)return false;
                pet.Set(NameColorKey,requested);
            }
            else if(op==18)
            {
                if(requested<1 || requested>6)return false;
                pet.Set(EyeKey,requested);
            }
            else
            {
                if(requested!=0 && requested!=1 || pet.GetInt(PetLevelKey,1)<25)
                    return false;
                pet.Set(RunesKey,requested==1);
            }
            ZDOMan.instance.ForceSendZDO(target);
            DeguPlugin.Log?.LogInfo("Degu Tresura saved appearance op="+op+" value="+requested);
            return false;
        }
        private static void AfterCapture(Character animal)
        {
            if(!animal || !animal.name.StartsWith("Degu",StringComparison.Ordinal))return;
            ZNetView view=animal.GetComponent<ZNetView>();
            if(!view || !view.IsValid())return;
            ZDO zdo=view.GetZDO();
            if(zdo==null || !(details.GetValue(null) is IDictionary dict) ||
               !dict.Contains(zdo.m_uid))return;
            object info=dict[zdo.m_uid];
            if(!IsDegu(info))return;
            Set(info,"EyeColor",Mathf.Clamp(zdo.GetInt(EyeKey,0),0,6));
            Set(info,"ShowRunes",zdo.GetBool(RunesKey,true));
            Set(info,"Skin",Mathf.Clamp(zdo.GetInt(SkinKey,0),0,6));
            Set(info,"NameColor",Mathf.Clamp(zdo.GetInt(NameColorKey,1),0,15));
        }
        // Overhaul 0.22.65 may discard a roster slot when its FamiliarInfo
        // record has no species and the pet was not in the currently loaded
        // scene when OnAdoptReply called Capture. Preserve the identity of an
        // EXISTING owned Degu ZDO before that orphan cleanup runs.
        private static bool HydrateExistingOwnedDegu(ZDOID id)
        {
            if(id==ZDOID.None || Player.m_localPlayer==null ||
                ZDOMan.instance==null || ZNetScene.instance==null)return false;
            var ids=familiars?.GetValue(null) as List<ZDOID>;
            var records=details?.GetValue(null) as IDictionary;
            if(ids==null || !ids.Contains(id) || records==null)return false;
            ZDO pet=ZDOMan.instance.GetZDO(id);
            if(pet==null || pet.GetLong(OwnerKey,0L)!=Player.m_localPlayer.GetPlayerID())
                return false;
            GameObject prefab=ZNetScene.instance.GetPrefab(pet.GetPrefab());
            if(!prefab || prefab.name!="Degu")return false;
            object info=records.Contains(id)?records[id]:null;
            if(info==null)
            {
                info=Activator.CreateInstance(infoType,true);
                Set(info,"Id",id);
                records[id]=info;
            }
            if(IsDegu(info))return false; // already identified; preserve details
            string current=Get(info,"Prefab") as string;
            if(!string.IsNullOrEmpty(current))return false; // never overwrite another species
            Set(info,"Prefab","Degu");
            Set(info,"Skin",Mathf.Clamp(pet.GetInt(SkinKey,0),0,6));
            Set(info,"EyeColor",Mathf.Clamp(pet.GetInt(EyeKey,0),0,6));
            Set(info,"ShowRunes",pet.GetBool(RunesKey,true));
            Set(info,"NameColor",Mathf.Clamp(pet.GetInt(NameColorKey,1),0,15));
            Set(info,"PetLevel",Mathf.Clamp(pet.GetInt(PetLevelKey,1),1,100));
            Dirty();
            DeguPlugin.Log?.LogInfo("DEGU_TRESURA: recovered existing owned Degu identity, id="+id+
                ", skin="+GetInt(info,"Skin")+" (no new pet or roster entry created).");
            return true;
        }
        private static void BeforeStatus(long sender)
        {
            if(ZRoutedRpc.instance==null ||
                sender!=ZRoutedRpc.instance.GetServerPeerID())return;
            var ids=familiars?.GetValue(null) as List<ZDOID>;
            if(ids==null)return;
            // Iterate snapshot; no mutation of the native roster itself.
            foreach(ZDOID id in ids.ToArray())
                HydrateExistingOwnedDegu(id);
        }
        private static void AfterAdopt(long sender,ZPackage pkg)
        {
            if(ZRoutedRpc.instance==null || pkg==null ||
                sender!=ZRoutedRpc.instance.GetServerPeerID())return;
            try
            {
                ZDOID id=new ZPackage(pkg.GetArray()).ReadZDOID();
                HydrateExistingOwnedDegu(id);
            }
            catch(Exception ex)
            {
                DeguPlugin.Log?.LogWarning("DEGU_TRESURA: adoption status hydration failed: "+
                    ex.GetBaseException().Message);
            }
        }

        // The Degu project intentionally does not reference UnityEngine.UI.dll.
        // Set this existing Overhaul UI property by reflection instead.
        private static void Interactable(FieldInfo f,bool enabled)
        {
            object ui=f?.GetValue(null);
            PropertyInfo p=ui?.GetType().GetProperty("interactable");
            if(p!=null && p.CanWrite)p.SetValue(ui,enabled,null);
        }
        private static bool IsInteractable(FieldInfo f)
        {
            object ui=f?.GetValue(null);
            PropertyInfo p=ui?.GetType().GetProperty("interactable");
            return p!=null && p.CanRead && p.GetValue(ui,null) is bool state && state;
        }

        private static void Label(string field,string value)
        {
            object label=PanelField(field);
            PropertyInfo property=label?.GetType().GetProperty("text");
            if(property!=null && property.CanWrite)property.SetValue(label,value,null);
        }
        private static object PanelField(string name)
        {
            Type p=selected?.DeclaringType;
            return p?.GetField(name,BindingFlags.Static|BindingFlags.NonPublic)?.GetValue(null);
        }
        private static void AfterDetail()
        {
            object info=Selection();
            if(!IsSelectedDegu(info))return;
            bool alive=Alive(info);
            // Only change the six Degu controls; never touch Wolf or other
            // familiars. If Alive is stale, validate the actual owned ZDO.
            if(!alive && Player.m_localPlayer && ZNetScene.instance)
            {
                GameObject go=ZNetScene.instance.FindInstance(GetId(info));
                ZNetView view=go ? go.GetComponent<ZNetView>():null;
                Character actor=go ? go.GetComponent<Character>():null;
                ZDO z=view && view.IsValid()?view.GetZDO():null;
                if(actor && actor.IsTamed() && z!=null &&
                   z.GetLong(OwnerKey,0L)==Player.m_localPlayer.GetPlayerID() &&
                   z.m_uid==GetId(info))
                {
                    Set(info,"Alive",true);
                    alive=true;
                }
            }
            Label("_furName",FurNames[Mathf.Clamp(GetInt(info,"Skin"),0,6)]);
            int eye=Mathf.Clamp(GetInt(info,"EyeColor",0),0,6);
            string eyeText=eyeName.Invoke(null,new object[]{eye}) as string;
            Label("_eyeName",eyeText ?? "—");
            Interactable(furPrev,alive);
            Interactable(furNext,alive);
            Interactable(eyePrev,alive);
            Interactable(eyeNext,alive);
            Interactable(namePrev,alive);
            Interactable(nameNext,alive);
            Interactable(rune,alive && GetInt(info,"PetLevel",1)>=25);
            object runeImage=PanelField("_runesIcon");
            PropertyInfo runeColor=runeImage?.GetType().GetProperty("color");
            if(runeColor!=null && runeColor.CanWrite)
            {
                bool active=Get(info,"ShowRunes") is bool b && b;
                // Native menu considers only Wolf when selecting the toggle
                // icon. Override its visual state for Degu, not other species.
                try
                {
                    Type icons=AccessTools.TypeByName("Sirro.PracaFizyczna.SearchUiIcons");
                    MethodInfo toggle=icons==null?null:AccessTools.Method(icons,"ToggleMask");
                    object sprite=toggle?.Invoke(null,new object[]{active});
                    PropertyInfo image=runeImage?.GetType().GetProperty("sprite");
                    if(sprite!=null && image!=null && image.CanWrite)
                        image.SetValue(runeImage,sprite,null);
                }
                catch(Exception ex)
                {
                    DeguPlugin.Log?.LogWarning("DEGU_TRESURA: rune icon unavailable: "+ex.GetBaseException().Message);
                }
                Color display=alive && GetInt(info,"PetLevel",1)>=25?
                    active?new Color(.24f,.86f,.32f,1f):new Color(.93f,.24f,.22f,1f):
                    new Color(.6f,.6f,.6f,.45f);
                runeColor.SetValue(runeImage,display,null);
            }
            string message="alive="+alive+" fur="+IsInteractable(furNext)+
                " eye="+IsInteractable(eyeNext)+" name="+IsInteractable(nameNext)+
                " rune="+IsInteractable(rune);
            if(message!=lastState)
            {
                lastState=message;
                DeguPlugin.Log?.LogInfo("DEGU_TRESURA: selected Degu UI "+message);
            }
        }
    }
}
