using System;
using System.Collections.Generic;
using UnityEngine;

namespace SirrohDegu
{
    /// <summary>
    /// Corpse-only contact geometry attached to an EXISTING native Wolf
    /// ragdoll rigidbody. No new rigidbody, no terrain teleport, no animation
    /// changes, no changes to the living Degu or original Wolf_Ragdoll.
    ///
    /// A Wolf-sized collider rig can have no reliable contact underneath the
    /// much smaller Degu mesh. A small spherical compound collider on the
    /// physics torso supplies stable contact during tumbling in any rotation.
    /// </summary>
    public sealed class DeguCorpseGroundSupport : MonoBehaviour
    {
        internal const string ContactName="SirrohDegu_CorpseGroundContact";
        private const float WorldRadius=.19f;
        private Collider contact;

        internal static void Configure(GameObject corpse)
        {
            if(!corpse)return;
            if(corpse.GetComponent<DeguCorpseGroundSupport>())return;
            if(!corpse.GetComponent<Ragdoll>())
                throw new InvalidOperationException("Cannot install contact support without native Ragdoll.");

            Rigidbody[] native=corpse.GetComponentsInChildren<Rigidbody>(true);
            if(native.Length==0)
                throw new InvalidOperationException("Native Wolf_Ragdoll contains no physics bodies.");

            Transform pelvis=null,head=null;
            foreach(Transform bone in corpse.GetComponentsInChildren<Transform>(true))
            {
                if(!bone)continue;
                if(bone.name=="Pelvis" && pelvis==null)pelvis=bone;
                if(bone.name=="Head" && head==null)head=bone;
            }
            if(!pelvis || !head)
                throw new InvalidOperationException("Native corpse Pelvis/Head anchors missing.");

            Rigidbody torso=null;
            foreach(Rigidbody body in native)
            {
                if(!body)continue;
                if(body.gameObject.name=="Pelvis"){torso=body;break;}
                if(torso==null && (body.gameObject.name=="Spine" ||
                                   body.gameObject.name=="Spine1" ||
                                   body.gameObject.name=="Spine2"))torso=body;
            }
            if(!torso)
            {
                // Some Valheim versions put rigidbodies on generic Physics
                // children rather than the animated bones. Use the nearest
                // actual native physics body, not a new independent one.
                float distance=float.PositiveInfinity;
                foreach(Rigidbody body in native)
                {
                    if(!body)continue;
                    float d=(body.position-pelvis.position).sqrMagnitude;
                    if(d>=distance)continue;
                    distance=d;torso=body;
                }
            }
            if(!torso)
                throw new InvalidOperationException("Cannot find native torso physics body.");

            Vector3 direction=head.position-pelvis.position;
            if(direction.sqrMagnitude<.0001f)
                throw new InvalidOperationException("Invalid native Wolf torso axis.");
            Vector3 center=pelvis.position+direction*.30f;
            if((center-torso.position).magnitude>3f)
                throw new InvalidOperationException("Degu torso contact is too far from physics body.");

            var node=new GameObject(ContactName);
            node.layer=torso.gameObject.layer;
            node.transform.SetParent(torso.transform,false);
            node.transform.position=center;
            node.transform.rotation=Quaternion.identity;
            Vector3 scale=node.transform.lossyScale;
            float max=Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z));
            if(max<.00001f)
                throw new InvalidOperationException("Invalid ragdoll torso scale for contact support.");

            SphereCollider sphere=node.AddComponent<SphereCollider>();
            sphere.radius=WorldRadius/max;
            sphere.isTrigger=false;

            // ContinuousDynamic prevents a fast initial death impulse from
            // tunneling the small contact collider through the ground. Do not
            // overwrite native mass, velocity, constraints or joint settings.
            // This is also applied after native Ragdoll.Start, since a
            // prefab can serialize its rigidbodies as initially kinematic.
            if(!torso.isKinematic &&
               torso.collisionDetectionMode==CollisionDetectionMode.Discrete)
                torso.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;

            var behaviour=corpse.AddComponent<DeguCorpseGroundSupport>();
            behaviour.contact=sphere;
        }

        private void Start()
        {
            if(!contact)
            {
                foreach(Collider collider in GetComponentsInChildren<Collider>(true))
                    if(collider && collider.name==ContactName)
                    {contact=collider;break;}
            }
            if(!contact)return;
            Rigidbody torso=contact.GetComponentInParent<Rigidbody>();
            if(torso && !torso.isKinematic &&
               torso.collisionDetectionMode==CollisionDetectionMode.Discrete)
                torso.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            // A compound collider should touch the WORLD, not collide with
            // other bodies of this same ragdoll. Apply on the active clone:
            // Physics.IgnoreCollision on inactive prefab templates is unsafe.
            foreach(Collider other in GetComponentsInChildren<Collider>(true))
            {
                if(!other || other==contact)continue;
                Physics.IgnoreCollision(contact,other,true);
            }
        }
    }
}
