using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.Scripting.APIUpdating;

#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
#endif

namespace UnityEngine.Rendering.Universal
{
    
    /*
        本类实例专门用来存储 "ScriptableRenderer" 类实例的 数据;

        在 urp 中, ForwardRendererData, Renderer2DData 继承本类;
    */
    [MovedFrom("UnityEngine.Rendering.LWRP")]
    public abstract class ScriptableRendererData//ScriptableRendererData__RR
        : ScriptableObject
    {

        internal bool isInvalidated { get; set; } // 是否为: "是无效的"


        /*
            创建一个 "ScriptableRenderer" 类实例;
            派生类 必须实现之;
            可到 ForwardRendererData, Renderer2DData 中寻找;
        */
        protected abstract ScriptableRenderer Create();
        

        [SerializeField] internal List<ScriptableRendererFeature> m_RendererFeatures = new List<ScriptableRendererFeature>(10);
        [SerializeField] internal List<long> m_RendererFeatureMap = new List<long>(10);


       
        /*
            可以向 "ScriptableRenderer" 实例绑定数个 RendererFeature;
            --
            猜测: 沿用 ForwardRenderer inspector 配置数据;
        */
        public List<ScriptableRendererFeature> rendererFeatures
        {
            get => m_RendererFeatures;
        }

        
        /*
            当改变本类中的 settings 时, 使用本函数;
            It will rebuild the "render passes" with the new data.
        */
        public new void SetDirty()
        {
            isInvalidated = true;
        }


        internal ScriptableRenderer InternalCreateRenderer()//  读完__
        {
            isInvalidated = false;
            return Create();
        }


        protected virtual void OnValidate()
        {
            SetDirty();
#if UNITY_EDITOR
            if (m_RendererFeatures.Contains(null))
                ValidateRendererFeatures();
#endif
        }

        protected virtual void OnEnable()
        {
            SetDirty();
        }

#if UNITY_EDITOR
        internal virtual Material GetDefaultMaterial(DefaultMaterialType materialType)
        {
            return null;
        }

        internal virtual Shader GetDefaultShader()
        {
            return null;
        }

        internal bool ValidateRendererFeatures()
        {
            // Get all Subassets
            var subassets = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(this));
            var linkedIds = new List<long>();
            var loadedAssets = new Dictionary<long, object>();
            var mapValid = m_RendererFeatureMap != null && m_RendererFeatureMap?.Count == m_RendererFeatures?.Count;
            var debugOutput = $"{name}\nValid Sub-assets:\n";

            // Collect valid, compiled sub-assets
            foreach (var asset in subassets)
            {
                if (asset == null || asset.GetType().BaseType != typeof(ScriptableRendererFeature)) continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long localId);
                loadedAssets.Add(localId, asset);
                debugOutput += $"-{asset.name}\n--localId={localId}\n";
            }

            // Collect assets that are connected to the list
            for (var i = 0; i < m_RendererFeatures?.Count; i++)
            {
                if (!m_RendererFeatures[i]) continue;
                if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_RendererFeatures[i], out var guid, out long localId))
                {
                    linkedIds.Add(localId);
                }
            }

            var mapDebug = mapValid ? "Linking" : "Map missing, will attempt to re-map";
            debugOutput += $"Feature List Status({mapDebug}):\n";

            // Try fix missing references
            for (var i = 0; i < m_RendererFeatures?.Count; i++)
            {
                if (m_RendererFeatures[i] == null)
                {
                    if (mapValid && m_RendererFeatureMap[i] != 0)
                    {
                        var localId = m_RendererFeatureMap[i];
                        loadedAssets.TryGetValue(localId, out var asset);
                        m_RendererFeatures[i] = (ScriptableRendererFeature)asset;
                    }
                    else
                    {
                        m_RendererFeatures[i] = (ScriptableRendererFeature)GetUnusedAsset(ref linkedIds, ref loadedAssets);
                    }
                }

                debugOutput += m_RendererFeatures[i] != null ? $"-{i}:Linked\n" : $"-{i}:Missing\n";
            }

            UpdateMap();

            if (!m_RendererFeatures.Contains(null))
                return true;

            Debug.LogError($"{name} is missing RendererFeatures\nThis could be due to missing scripts or compile error.", this);
            return false;
        }


        internal bool DuplicateFeatureCheck(Type type)
        {
            var isSingleFeature = type.GetCustomAttribute(typeof(DisallowMultipleRendererFeature));
            return isSingleFeature != null && m_RendererFeatures.Select(renderFeature => renderFeature.GetType()).Any(t => t == type);
        }


        private static object GetUnusedAsset(ref List<long> usedIds, ref Dictionary<long, object> assets)
        {
            foreach (var asset in assets)
            {
                var alreadyLinked = usedIds.Any(used => asset.Key == used);

                if (alreadyLinked)
                    continue;

                usedIds.Add(asset.Key);
                return asset.Value;
            }

            return null;
        }


        private void UpdateMap()
        {
            if (m_RendererFeatureMap.Count != m_RendererFeatures.Count)
            {
                m_RendererFeatureMap.Clear();
                m_RendererFeatureMap.AddRange(new long[m_RendererFeatures.Count]);
            }

            for (int i = 0; i < rendererFeatures.Count; i++)
            {
                if (m_RendererFeatures[i] == null) continue;
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(m_RendererFeatures[i], out var guid, out long localId)) continue;

                m_RendererFeatureMap[i] = localId;
            }
        }

#endif
    }
}
