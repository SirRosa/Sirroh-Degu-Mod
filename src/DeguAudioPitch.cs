using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SirrohDegu
{
    /// <summary>
    /// Give Degu a higher-pitched copy of each sound referenced by its Wolf
    /// EffectLists, never changing the original Wolf's shared SFX prefabs.
    /// Setup runs on the inactive Degu network prefab after ZNetScene.Awake.
    /// </summary>
    internal static class DeguAudioPitch
    {
        private const string Prefix="SirrohDegu_Audio_";
        private static readonly BindingFlags DeclaredFields=
            BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly;
        private static readonly MethodInfo CloneObject=typeof(object).GetMethod(
            "MemberwiseClone",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly Dictionary<GameObject,GameObject> SfxCopies=
            new Dictionary<GameObject,GameObject>();
        private static readonly List<GameObject> Owned=new List<GameObject>();
        private static readonly HashSet<int> ConfiguredActors=new HashSet<int>();
        internal static float Multiplier=1.45f;

        private static FieldInfo FindField(Type type,string name)
        {
            for(Type current=type;current!=null;current=current.BaseType)
            {
                FieldInfo field=current.GetField(name,DeclaredFields);
                if(field!=null)return field;
            }
            return null;
        }

        private static void RegisterClone(ZNetScene scene,GameObject clone)
        {
            // Network ZSFX prefabs must resolve through the same ZNetScene
            // registry as vanilla sounds for both host and connected clients.
            // A unique stable name prevents rewriting vanilla Wolf lookups.
            DeguPlugin.RegisterSupplementalPrefab(scene,clone);
        }

        private static GameObject CloneSound(GameObject sound,ZNetScene scene,Transform holder,float factor)
        {
            if(!sound)return null;
            if(sound.name.StartsWith(Prefix,StringComparison.Ordinal))return sound;
            if(SfxCopies.TryGetValue(sound,out GameObject cached) && cached)
            {
                RegisterClone(scene,cached);
                return cached;
            }
            ZSFX[] sounds=sound.GetComponentsInChildren<ZSFX>(true);
            if(sounds==null || sounds.Length==0)return null;

            bool force=ZNetView.m_forceDisableInit;
            GameObject copy=null;
            try
            {
                ZNetView.m_forceDisableInit=true;
                copy=UnityEngine.Object.Instantiate(sound,holder);
            }
            finally { ZNetView.m_forceDisableInit=force; }
            if(!copy)return null;
            copy.name=Prefix+sound.name;
            if(copy.activeInHierarchy)
            {
                UnityEngine.Object.Destroy(copy);
                throw new InvalidOperationException("Degu sound prefab unexpectedly activated during registration.");
            }
            try
            {
                int count=0;
                foreach(ZSFX soundEffect in copy.GetComponentsInChildren<ZSFX>(true))
                {
                    if(!soundEffect)continue;
                    float min=Mathf.Max(0.01f,soundEffect.m_minPitch);
                    float max=Mathf.Max(min,soundEffect.m_maxPitch);
                    soundEffect.m_minPitch=Mathf.Clamp(min*factor,0.1f,3f);
                    soundEffect.m_maxPitch=Mathf.Clamp(max*factor,soundEffect.m_minPitch,3f);
                    count++;
                }
                if(count==0)throw new InvalidOperationException("Cloned audio prefab had no ZSFX.");
                RegisterClone(scene,copy);
                SfxCopies[sound]=copy;
                Owned.Add(copy);
                return copy;
            }
            catch
            {
                UnityEngine.Object.Destroy(copy);
                throw;
            }
        }

        private static int RewireList(object component,FieldInfo field,ZNetScene scene,
            Transform holder,float factor)
        {
            object list=field.GetValue(component);
            if(list==null)return 0;
            FieldInfo effects=FindField(list.GetType(),"m_effectPrefabs");
            Array originals=effects?.GetValue(list) as Array;
            if(originals==null || originals.Length==0)return 0;
            Array rewritten=(Array)originals.Clone();
            int changed=0;
            for(int i=0;i<originals.Length;i++)
            {
                object entry=originals.GetValue(i);
                if(entry==null)continue;
                FieldInfo prefabField=FindField(entry.GetType(),"m_prefab");
                GameObject source=prefabField?.GetValue(entry) as GameObject;
                if(!source)continue;
                GameObject clone=CloneSound(source,scene,holder,factor);
                if(!clone || clone==source)continue;
                // Deep-copy both the list and its changed EffectData entry:
                // Unity sometimes shares serializable references across prefab
                // copies; never mutate a nested list owned by the source Wolf.
                object copiedEntry=CloneObject.Invoke(entry,null);
                prefabField.SetValue(copiedEntry,clone);
                rewritten.SetValue(copiedEntry,i);
                changed++;
            }
            if(changed==0)return 0;
            object copiedList=CloneObject.Invoke(list,null);
            effects.SetValue(copiedList,rewritten);
            field.SetValue(component,copiedList);
            return changed;
        }

        internal static void Configure(ZNetScene scene,GameObject degu,Transform inactiveHolder)
        {
            if(!scene || !degu || !inactiveHolder)return;
            if(inactiveHolder.gameObject.activeInHierarchy)
                throw new InvalidOperationException("Degu sound clone holder must be inactive.");
            if(!ConfiguredActors.Add(degu.GetInstanceID()))
            {
                // ZNetScene can be recreated when loading another world.
                // The creature's own EffectList is already rewritten; simply
                // restore audio-prefab lookup in the new network scene.
                foreach(GameObject prefab in Owned)
                    if(prefab)RegisterClone(scene,prefab);
                return;
            }
            int links=0;
            foreach(Component component in degu.GetComponentsInChildren<Component>(true))
            {
                if(!component)continue;
                for(Type type=component.GetType();type!=null && type!=typeof(object);type=type.BaseType)
                {
                    foreach(FieldInfo field in type.GetFields(DeclaredFields))
                    {
                        if(field.IsInitOnly || field.FieldType.Name!="EffectList")continue;
                        links+=RewireList(component,field,scene,inactiveHolder,Multiplier);
                    }
                }
            }
            // Also support Wolf prefabs that embed AudioSources/ZSFX directly
            // in the creature hierarchy rather than spawning an effect.
            int embedded=0;
            foreach(ZSFX sfx in degu.GetComponentsInChildren<ZSFX>(true))
            {
                if(!sfx)continue;
                sfx.m_minPitch=Mathf.Clamp(sfx.m_minPitch*Multiplier,0.1f,3f);
                sfx.m_maxPitch=Mathf.Clamp(sfx.m_maxPitch*Multiplier,sfx.m_minPitch,3f);
                embedded++;
            }
            if(links>0 || embedded>0)
                DeguPlugin.Log?.LogInfo("Degu voice pitch x"+Multiplier.ToString("0.00")+
                    "; isolated sound effect links="+links+", embedded sources="+embedded+
                    ". Vanilla Wolf remains unchanged.");
            else
                DeguPlugin.Log?.LogWarning("Degu voice pitch: no sound EffectList or embedded ZSFX found on prefab; Wolf voice unchanged.");
        }

        internal static void Shutdown()
        {
            foreach(GameObject sfx in Owned)if(sfx)UnityEngine.Object.Destroy(sfx);
            Owned.Clear();
            SfxCopies.Clear();
            ConfiguredActors.Clear();
        }
    }
}
