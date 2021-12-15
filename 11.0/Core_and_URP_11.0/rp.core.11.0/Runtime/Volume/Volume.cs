using System.Collections.Generic;

namespace UnityEngine.Rendering
{
    /*
        A generic Volume component holding a "VolumeProfile".
    */
    [HelpURL(Documentation.baseURLHDRP + Documentation.version + Documentation.subURL + "Volumes" + Documentation.endURL)]
    [ExecuteAlways]
    [AddComponentMenu("Miscellaneous/Volume")]
    public class Volume //Volume__RR
        : MonoBehaviour
    {
     
        // Specifies whether to apply the Volume to the entire Scene or not.
        [Tooltip("When enabled, HDRP applies this Volume to the entire Scene.")]
        public bool isGlobal = true;

       
        // The Volume priority in the stack. 
        // 值越高, 优先级越高, 可用负数
        [Tooltip("Sets the Volume priority in the stack. A higher value means higher priority. You can use negative values.")]
        public float priority = 0f;


   
        // The outer distance to start blending from. 
        // 0 值表示立即切换, 没有任何 blend; 好像是从 volume 的边界处向外拓展的一个 过度区间;
        [Tooltip("Sets the outer distance to start blending from. A value of 0 means no blending and Unity applies the Volume overrides immediately upon entry.")]
        public float blendDistance = 0f;


 
        /// The total weight of this volume in the Scene. 0 means no effect and 1 means full effect.
        [Range(0f, 1f), Tooltip("Sets the total weight of this Volume in the Scene. 0 means no effect and 1 means full effect.")]
        public float weight = 1f;


        /*
            The shared Profile that this Volume uses.
            Modifying sharedProfile changes every Volumes that uses this Profile and also changes
            the Profile settings stored in the Project.

            You should not modify Profiles that sharedProfile returns. 
            If you want to modify the Profile of a Volume, use "profile" instead.
        */
        public VolumeProfile sharedProfile = null;


        /*
            Gets the first instantiated "VolumeProfile" assigned to the Volume.
            Modifying profile changes the Profile for this Volume only. 
            If another Volume uses the same Profile, this clones the shared Profile and starts using it from now on.

            This property automatically instantiates the Profile and make it unique to this Volume
            so you can safely edit it via scripting at runtime without changing the original Asset in the Project.
            Note that if you pass your own Profile, you must destroy it when you finish using it.
        */
        public VolumeProfile profile
        {
            get
            {
                if (m_InternalProfile == null)
                {
                    m_InternalProfile = ScriptableObject.CreateInstance<VolumeProfile>();

                    if (sharedProfile != null)
                    {
                        foreach (var item in sharedProfile.components)
                        {
                            var itemCopy = Instantiate(item);
                            m_InternalProfile.components.Add(itemCopy);
                        }
                    }
                }

                return m_InternalProfile;
            }
            set => m_InternalProfile = value;
        }



        internal VolumeProfile profileRef => m_InternalProfile == null ? sharedProfile : m_InternalProfile;


        /*
            Checks if the Volume has an instantiated(实例化的) Profile or if it uses a shared Profile.
        */
        /// <returns>true if the profile has been instantiated.</returns>
        public bool HasInstantiatedProfile() => m_InternalProfile != null;



        // Needed for state tracking (see the comments in Update)
        int m_PreviousLayer;
        float m_PreviousPriority;
        VolumeProfile m_InternalProfile;


        void OnEnable()
        {
            m_PreviousLayer = gameObject.layer;
            VolumeManager.instance.Register(this, m_PreviousLayer);
        }

        void OnDisable()
        {
            VolumeManager.instance.Unregister(this, gameObject.layer);
        }

        void Update()
        {
            // Unfortunately we need to track the current layer to update the volume manager in
            // real-time as the user could change it at any time in the editor or at runtime.
            // Because no event is raised when the layer changes, we have to track it on every frame :/
            UpdateLayer();

            // Same for priority. We could use a property instead, but it doesn't play nice with the
            // serialization system. Using a custom Attribute/PropertyDrawer for a property is
            // possible but it doesn't work with Undo/Redo in the editor, which makes it useless for our case.
            if (priority != m_PreviousPriority)
            {
                VolumeManager.instance.SetLayerDirty(gameObject.layer);
                m_PreviousPriority = priority;
            }
        }//  函数完__


        internal void UpdateLayer()
        {
            int layer = gameObject.layer;
            if (layer != m_PreviousLayer)
            {
                VolumeManager.instance.UpdateVolumeLayer(this, m_PreviousLayer, layer);
                m_PreviousLayer = layer;
            }
        }
        

#if UNITY_EDITOR
        // TODO: Look into a better volume previsualization system
        List<Collider> m_TempColliders;

        void OnDrawGizmos()
        {
            if (m_TempColliders == null)
                m_TempColliders = new List<Collider>();

            var colliders = m_TempColliders;
            GetComponents(colliders);

            if (isGlobal || colliders == null)
                return;

            var scale = transform.localScale;
            var invScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, scale);
            Gizmos.color = CoreRenderPipelinePreferences.volumeGizmoColor;

            // Draw a separate gizmo for each collider
            foreach (var collider in colliders)
            {
                if (!collider.enabled)
                    continue;

                // We'll just use scaling as an approximation for volume skin. It's far from being
                // correct (and is completely wrong in some cases). Ultimately we'd use a distance
                // field or at least a tesselate + push modifier on the collider's mesh to get a
                // better approximation, but the current Gizmo system is a bit limited and because
                // everything is dynamic in Unity and can be changed at anytime, it's hard to keep
                // track of changes in an elegant way (which we'd need to implement a nice cache
                // system for generated volume meshes).
                switch (collider)
                {
                    case BoxCollider c:
                        Gizmos.DrawCube(c.center, c.size);
                        break;
                    case SphereCollider c:
                        // For sphere the only scale that is used is the transform.x
                        Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one * scale.x);
                        Gizmos.DrawSphere(c.center, c.radius);
                        break;
                    case MeshCollider c:
                        // Only convex mesh m_Colliders are allowed
                        if (!c.convex)
                            c.convex = true;

                        // Mesh pivot should be centered or this won't work
                        Gizmos.DrawMesh(c.sharedMesh);
                        break;
                    default:
                        // Nothing for capsule (DrawCapsule isn't exposed in Gizmo), terrain, wheel and
                        // other m_Colliders...
                        break;
                }
            }

            colliders.Clear();
        }//  函数完__

#endif
    }
}
