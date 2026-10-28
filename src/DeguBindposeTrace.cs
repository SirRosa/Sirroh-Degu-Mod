using System;
using UnityEngine;

namespace SirrohDegu
{
    // Event-scoped, read-only renderer audit for the persistent Unity warning.
    // Count equality is diagnostic only, NOT proof that bind matrices are valid.
    internal static class DeguBindposeTrace
    {
        private static GameObject lastSpawn;
        private static int spawnReports,errors;
        internal static void Install()
        {
            lastSpawn=null;
            spawnReports=errors=0;
            Application.logMessageReceived+=OnUnityLog;
        }
        internal static void Shutdown()
        {
            Application.logMessageReceived-=OnUnityLog;
            lastSpawn=null;
        }
        internal static void BeforeSpawn(ZNetView view)
        {
            if(!view || view.gameObject.name!="Degu(Clone)")return;
            lastSpawn=view.gameObject;
            if(spawnReports<12)Inspect(view.gameObject,"before ZNetView.Awake");
        }
        internal static void AfterSpawn(ZNetView view)
        {
            if(!view || view.gameObject.name!="Degu(Clone)")return;
            lastSpawn=view.gameObject;
            if(++spawnReports<=12)Inspect(view.gameObject,"after ZNetView.Awake");
        }
        internal static void AfterPrefab(GameObject prefab)
        {
            Inspect(prefab,"staged Degu prefab");
        }
        private static void Inspect(GameObject obj,string phase)
        {
            if(!obj)return;
            foreach(var r in obj.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!r || !r.sharedMesh)continue;
                int binds=r.sharedMesh.bindposes.Length;
                Transform[] bones=r.bones;
                int missing=0;
                if(bones!=null)
                    foreach(Transform t in bones)if(!t)missing++;
                string line="DEGU_BINDPOSE_CHECK phase="+phase+
                    " object="+obj.name+" renderer="+r.name+
                    " enabled="+r.enabled+" bindposes="+binds+
                    " bones="+(bones==null?0:bones.Length)+
                    " null_bones="+missing+" root="+(r.rootBone?r.rootBone.name:"null");
                if(bones==null || bones.Length!=binds || missing!=0)
                    DeguPlugin.Log?.LogError(line+" STATUS=INVALID");
                else
                    DeguPlugin.Log?.LogInfo(line+" STATUS=COUNT_OK_MATRIX_UNVERIFIED");
            }
        }
        private static void OnUnityLog(string message,string stack,LogType type)
        {
            if(type!=LogType.Error || message==null ||
                !message.Trim().Equals("Bones do not match bindpose.",StringComparison.Ordinal))
                return;
            if(++errors>8)return;
            DeguPlugin.Log?.LogError("DEGU_BINDPOSE_EVENT unity_error="+errors+
                " (renderer identity NOT proven; before/after ZNetView snapshots are evidence only)"+
                " stack="+(string.IsNullOrEmpty(stack)?"empty":stack.Substring(0,Math.Min(1200,stack.Length)).Replace("\n"," | ")));
            if(lastSpawn)
                Inspect(lastSpawn,"near last observed Degu spawn; NOT guaranteed error origin");
        }
    }
}
