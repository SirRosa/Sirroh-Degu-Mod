using System;
using System.Text;
using UnityEngine;

namespace SirrohDegu
{
    // Non-invasive native Unity bindpose error attribution.
    // Nothing is modified in the creature, the preview or Overhaul.
    internal static class DeguBindposeDiagnostics
    {
        private static bool installed;
        private static string stage;
        private static string errorStage;
        private static int pending;
        private static int total;
        private static bool reporting;
        private static bool spawnChecked;

        private sealed class StageGuard : IDisposable
        {
            private readonly string previous;
            internal StageGuard(string previous) { this.previous=previous; }
            public void Dispose() { stage=previous; }
        }

        internal static IDisposable Scope(string current)
        {
            string previous=stage;
            stage=current;
            return new StageGuard(previous);
        }

        internal static void Install()
        {
            if(installed)return;
            installed=true;
            Application.logMessageReceived+=OnLog;
        }

        internal static void Shutdown()
        {
            if(!installed)return;
            Application.logMessageReceived-=OnLog;
            installed=false;
            pending=0;
            stage=null;
        }

        private static void OnLog(string condition,string stackTrace,LogType type)
        {
            if(reporting || type!=LogType.Error || string.IsNullOrEmpty(condition) ||
               condition.IndexOf("Bones do not match bindpose",StringComparison.OrdinalIgnoreCase)<0)
                return;
            ++pending;
            ++total;
            errorStage=stage ?? "outside instrumented Degu operation";
        }

        // Run once after an error, on the Unity main thread.
        internal static void Flush()
        {
            if(pending<=0 || reporting)return;
            reporting=true;
            try
            {
                int count=pending;
                pending=0;
                var report=new StringBuilder();
                int scanned=0,bad=0;
                foreach(SkinnedMeshRenderer skin in Resources.FindObjectsOfTypeAll<SkinnedMeshRenderer>())
                {
                    if(!skin)continue;
                    bool belongs=false;
                    for(Transform t=skin.transform;t;t=t.parent)
                        if(t.name=="Degu" || t.name=="Degu(Clone)" ||
                           t.name=="Visual Degu" || t.name=="SirrohDegu_StableVisual")
                        {belongs=true;break;}
                    if(!belongs)continue;
                    ++scanned;
                    Mesh mesh=skin.sharedMesh;
                    Transform[] bones=skin.bones;
                    int poses=mesh?mesh.bindposes.Length:-1;
                    int countBones=bones==null?-1:bones.Length;
                    int nullBones=0;
                    if(bones!=null)foreach(Transform b in bones)if(!b)++nullBones;
                    if(poses!=countBones || nullBones>0)++bad;
                    if(scanned<=10)
                        report.Append(" [").Append(skin.name)
                            .Append(" mesh=").Append(mesh?mesh.name:"null")
                            .Append(" bindposes=").Append(poses)
                            .Append(" bones=").Append(countBones)
                            .Append(" null=").Append(nullBones)
                            .Append(" enabled=").Append(skin.enabled).Append("]");
                }
                DeguPlugin.Log?.LogWarning("Unity bindpose errors="+count+" total="+total+
                    " phase="+errorStage+" Degu renderers="+scanned+
                    " invalid="+bad+report);
            }
            catch(Exception ex)
            {
                DeguPlugin.Log?.LogWarning("Degu bindpose audit exception: "+ex.Message);
            }
            finally {reporting=false;}
        }

        internal static void Audit(GameObject root,string phase,bool once)
        {
            if(!root || (once && spawnChecked))return;
            if(once)spawnChecked=true;
            var report=new StringBuilder();
            foreach(SkinnedMeshRenderer skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(!skin)continue;
                Mesh mesh=skin.sharedMesh;
                int poses=mesh?mesh.bindposes.Length:-1;
                int bones=skin.bones.Length;
                report.Append(" [").Append(skin.name).Append(": bindposes=")
                    .Append(poses).Append(" bones=").Append(bones)
                    .Append(" enabled=").Append(skin.enabled).Append("]");
            }
            DeguPlugin.Log?.LogInfo("Degu skin check "+phase+report);
        }
    }
}
