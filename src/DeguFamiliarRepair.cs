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
        private static FieldInfo pending, dirty, details;
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

        internal static void Install()
        {
            if(!Chainloader.PluginInfos.TryGetValue(OverhaulId,out var p) ||
               p.Metadata.Version.ToString()!=RequiredVersion) return;
            try
            {
                Assembly asm=p.Instance.GetType().Assembly;
                Type taming=asm.GetType("Sirro.PracaFizyczna.TamingSkill",true);
                Type panel=asm.GetType("Sirro.PracaFizyczna.FamiliarMenuPanel",true);
                Type wolfVisuals=asm.GetType("Sirro.PracaFizyczna.WolfVisuals",true);
                eyeName=AccessTools.Method(wolfVisuals,"EyeName");
                infoType=taming.GetNestedType("FamiliarInfo",BindingFlags.NonPublic);
                if(infoType==null)throw new MissingMemberException("TamingSkill.FamiliarInfo");
                selected=AccessTools.Method(panel,"Selected");
                send=AccessTools.Method(taming,"Send");
                pending=AccessTools.Field(taming,"Pending");
                dirty=AccessTools.Field(taming,"_dirtyRecords");
                details=AccessTools.Field(taming,"Details");
                furPrev=AccessTools.Field(panel,"_left");
                furNext=AccessTools.Field(panel,"_right");
                eyePrev=AccessTools.Field(panel,"_eyeLeft");
                eyeNext=AccessTools.Field(panel,"_eyeRight");
                namePrev=AccessTools.Field(panel,"_fontLeft");
                nameNext=AccessTools.Field(panel,"_fontRight");
                rune=AccessTools.Field(panel,"_runesToggle");
                if(selected==null || send==null || eyeName==null || pending==null || dirty==null ||
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
                Patch(panel,"UpdateDetail",nameof(AfterDetail),false,true);
                DeguPlugin.Log?.LogInfo(
                    "Degu v0.2.5 Tresura: controls, 7 coats, 6 eyes, name colors, level-25 runes and validated RPCs registered.");
            }
            catch(Exception e)
            {
                harmony?.UnpatchSelf();
                harmony=null;
                DeguPlugin.Log?.LogError("Degu Tresura adapter unavailable: "+e);
            }
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

        private static void SendRequest(object info,int opcode,int value)
        {
            Player player=Player.m_localPlayer;
            if(!player || !Alive(info) || GetId(info)==ZDOID.None) return;
            send.Invoke(null,new object[]{player,opcode,GetId(info),Vector3.zero,value});
        }
        private static bool ChooseSkin(object info,int direction)
        {
            if(!IsDegu(info))return true;
            if(!Alive(info))return false;
            int next=(GetInt(info,"Skin")+direction+7)%7;
            SendRequest(info,10,next);
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
            SendRequest(info,18,next);
            Set(info,"EyeColor",next);
            Dirty();
            return false;
        }
        private static bool ChooseName(object familiar,int direction)
        {
            if(!IsDegu(familiar))return true;
            if(!Alive(familiar))return false;
            int next=(GetInt(familiar,"NameColor",1)+direction+16)%16;
            SendRequest(familiar,11,next);
            Set(familiar,"NameColor",next);
            Dirty();
            return false;
        }
        private static bool ToggleRunes(object info)
        {
            if(!IsDegu(info))return true;
            if(!Alive(info) || GetInt(info,"PetLevel",1)<25)return false;
            bool previous=Get(info,"ShowRunes") is bool b && b;
            SendRequest(info,17,previous?0:1);
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
            if(!animal || !animal.name.StartsWith("Degu",StringComparison.Ordinal) ||
               !animal.m_nview || !animal.m_nview.IsValid())return;
            ZDO zdo=animal.m_nview.GetZDO();
            if(zdo==null || !(details.GetValue(null) is IDictionary dict) ||
               !dict.Contains(zdo.m_uid))return;
            object info=dict[zdo.m_uid];
            if(!IsDegu(info))return;
            Set(info,"EyeColor",Mathf.Clamp(zdo.GetInt(EyeKey,0),0,6));
            Set(info,"ShowRunes",zdo.GetBool(RunesKey,true));
            Set(info,"Skin",Mathf.Clamp(zdo.GetInt(SkinKey,0),0,6));
            Set(info,"NameColor",Mathf.Clamp(zdo.GetInt(NameColorKey,1),0,15));
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
                DeguPlugin.Log?.LogInfo("Degu Tresura UI 0.2.5: "+message);
            }
        }
    }
}
