using System;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace SirrohDegu
{
    // Optional, independent bridge for the original v0.22.65 icon provider.
    // This patch works even if an unrelated cosmetic compatibility patch
    // is disabled; it does not change the Overhaul assembly or icon atlas.
    internal static class DeguCompassIcon
    {
        private const string OverhaulGuid = "sirro.valheim.pracafizyczna";
        private const string ResourceName = "SirrohDegu.Creatures.Degu.png";
        private static Texture2D texture;
        private static Sprite icon;
        private static bool attempted;

        internal static void Register(Harmony harmony)
        {
            if (harmony == null || !Chainloader.PluginInfos.TryGetValue(OverhaulGuid,out var plugin))
                return;
            try
            {
                Type library=plugin.Instance.GetType().Assembly.GetType(
                    "Sirro.PracaFizyczna.CreatureIconLibrary",false);
                MethodInfo provider=library==null?null:AccessTools.Method(
                    library,"ForPrefab",new[]{typeof(string)});
                if(provider==null || provider.ReturnType!=typeof(Sprite))
                {
                    DeguPlugin.Log?.LogWarning(
                        "Degu icon: Overhaul CreatureIconLibrary.ForPrefab not available.");
                    return;
                }
                harmony.Patch(provider,postfix:new HarmonyMethod(
                    AccessTools.Method(typeof(DeguCompassIcon),nameof(AfterPrefabIcon))));
                DeguPlugin.Log?.LogInfo(
                    "Degu compass icon bridge installed (96x96 embedded PNG, exact Degu prefab).");
            }
            catch(Exception e)
            {
                DeguPlugin.Log?.LogWarning("Degu compass icon bridge unavailable: "+e.Message);
            }
        }

        private static void AfterPrefabIcon(string prefab,ref Sprite __result)
        {
            if (__result != null || !string.Equals(prefab,"Degu",StringComparison.Ordinal))
                return;
            if (!attempted) Load();
            if (icon != null) __result=icon;
        }

        private static void Load()
        {
            attempted=true;
            Texture2D loaded=null;
            try
            {
                byte[] bytes;
                using(Stream stream=typeof(DeguCompassIcon).Assembly.GetManifestResourceStream(ResourceName))
                {
                    if(stream==null) throw new FileNotFoundException(ResourceName);
                    using(var buffer=new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        bytes=buffer.ToArray();
                    }
                }
                loaded=new Texture2D(2,2,TextureFormat.RGBA32,false);
                // Same Unity 1.0 API resolution strategy used by Overhaul.
                Type decoder=Type.GetType(
                    "UnityEngine.ImageConversion, UnityEngine.ImageConversionModule",false);
                MethodInfo load=decoder?.GetMethod("LoadImage",
                    BindingFlags.Public|BindingFlags.Static,null,
                    new[]{typeof(Texture2D),typeof(byte[]),typeof(bool)},null);
                if(load==null || !(bool)load.Invoke(null,new object[]{loaded,bytes,false}))
                    throw new InvalidDataException("Unity PNG decoder unavailable or failed");
                if(loaded.width!=96 || loaded.height!=96)
                    throw new InvalidDataException("Degu sprite must be 96x96");
                loaded.name="Sirroh Degu compass icon";
                loaded.filterMode=FilterMode.Bilinear;
                icon=Sprite.Create(texture=loaded,new Rect(0f,0f,96f,96f),
                    new Vector2(.5f,.5f),100f);
                icon.name="Degu";
                DeguPlugin.Log?.LogInfo("Degu 96x96 icon loaded from embedded DLL resource.");
            }
            catch(Exception error)
            {
                DeguPlugin.Log?.LogWarning("Degu compass icon decode failed: "+error);
                if(loaded) UnityEngine.Object.Destroy(loaded);
            }
        }

        internal static void Shutdown()
        {
            if(icon) UnityEngine.Object.Destroy(icon);
            if(texture) UnityEngine.Object.Destroy(texture);
            icon=null;
            texture=null;
            attempted=false;
        }
    }
}
