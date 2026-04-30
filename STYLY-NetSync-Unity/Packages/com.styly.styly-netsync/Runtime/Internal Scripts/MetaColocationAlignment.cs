using System;
using System.Reflection;
using UnityEngine;

namespace Styly.NetSync
{
    internal sealed class MetaColocationAlignment
    {
        private const float ResolveIntervalSeconds = 2f;

        private static readonly string[] CandidateTypeNames =
        {
            "Meta.XR.Colocation.MetaColocation",
            "Meta.XR.Colocation.ColocationManager",
            "Meta.XR.Colocation.ColocationCoordinator",
            "MetaColocation"
        };

        private static readonly string[] CandidateTransformMembers =
        {
            "AlignedTransform",
            "AlignmentTransform",
            "ColocationOrigin",
            "ColocationTransform",
            "SharedOrigin",
            "OriginTransform",
            "AnchorTransform"
        };

        private readonly NetSyncManager _manager;
        private Transform _alignmentTransform;
        private bool _isMetaQuestPlatform;
        private float _nextResolveTime;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private bool _hasLastPose;

        internal MetaColocationAlignment(NetSyncManager manager)
        {
            _manager = manager;
        }

        internal void Initialize()
        {
            if (!_manager.EnableMetaColocation)
            {
                return;
            }

            _isMetaQuestPlatform = MetaQuestPlatformDetector.IsMetaQuestPlatformSelected();
            if (!_isMetaQuestPlatform)
            {
                return;
            }

            ResolveAlignmentTransform(true);
            ApplyAlignmentIfChanged(true);
        }

        internal void Tick()
        {
            if (!_manager.EnableMetaColocation)
            {
                return;
            }

            if (!_isMetaQuestPlatform)
            {
                return;
            }

            ResolveAlignmentTransform(false);
            ApplyAlignmentIfChanged(false);
        }

        private void ResolveAlignmentTransform(bool force)
        {
            if (!force && _alignmentTransform != null)
            {
                return;
            }

            if (!force && Time.time < _nextResolveTime)
            {
                return;
            }

            _nextResolveTime = Time.time + ResolveIntervalSeconds;

            Transform overrideTransform = _manager.MetaColocationOriginOverride;
            if (overrideTransform != null)
            {
                _alignmentTransform = overrideTransform;
                return;
            }

            foreach (var typeName in CandidateTypeNames)
            {
                var type = FindTypeByName(typeName);
                if (type == null)
                {
                    continue;
                }

                var obj = UnityEngine.Object.FindFirstObjectByType(type);
                if (obj == null)
                {
                    continue;
                }

                var transform = ExtractAlignmentTransform(obj);
                if (transform != null)
                {
                    _alignmentTransform = transform;
                    _manager.DebugLogMetaColocation($"MetaColocation を検出しました: {type.Name}");
                    return;
                }
            }
        }

        private void ApplyAlignmentIfChanged(bool force)
        {
            if (_alignmentTransform == null)
            {
                return;
            }

            var position = _alignmentTransform.position;
            var rotation = _alignmentTransform.rotation;

            if (!force && _hasLastPose && position == _lastPosition && rotation == _lastRotation)
            {
                return;
            }

            _lastPosition = position;
            _lastRotation = rotation;
            _hasLastPose = true;

            _manager.UpdatePhysicalOffset(position, rotation.eulerAngles);
        }

        private static Transform ExtractAlignmentTransform(UnityEngine.Object obj)
        {
            if (obj == null)
            {
                return null;
            }

            var component = obj as Component;
            if (component == null)
            {
                return null;
            }

            foreach (var memberName in CandidateTransformMembers)
            {
                var transform = GetTransformFromMember(component, memberName);
                if (transform != null)
                {
                    return transform;
                }
            }

            return component.transform;
        }

        private static Transform GetTransformFromMember(object source, string memberName)
        {
            var type = source.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && typeof(Transform).IsAssignableFrom(property.PropertyType))
            {
                return property.GetValue(source) as Transform;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null && typeof(Transform).IsAssignableFrom(field.FieldType))
            {
                return field.GetValue(source) as Transform;
            }

            return null;
        }

        private static Type FindTypeByName(string typeName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }

        private static class MetaQuestPlatformDetector
        {
            private const string OpenXrSettingsTypeName = "UnityEngine.XR.OpenXR.OpenXRSettings";
            private const string MetaQuestFeatureTypeName = "UnityEngine.XR.OpenXR.Features.Meta.MetaQuestFeature";

            internal static bool IsMetaQuestPlatformSelected()
            {
                if (Application.platform != RuntimePlatform.Android)
                {
                    return false;
                }

                var openXrSettingsType = FindTypeByName(OpenXrSettingsTypeName);
                var metaQuestFeatureType = FindTypeByName(MetaQuestFeatureTypeName);
                if (openXrSettingsType == null || metaQuestFeatureType == null)
                {
                    return true;
                }

                var instanceProperty = openXrSettingsType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                if (instanceProperty == null)
                {
                    return true;
                }

                var settingsInstance = instanceProperty.GetValue(null);
                if (settingsInstance == null)
                {
                    return true;
                }

                var featuresProperty = openXrSettingsType.GetProperty("features", BindingFlags.Public | BindingFlags.Instance);
                if (featuresProperty == null)
                {
                    return true;
                }

                var featuresValue = featuresProperty.GetValue(settingsInstance);
                var featureEnumerable = featuresValue as System.Collections.IEnumerable;
                if (featureEnumerable == null)
                {
                    return true;
                }

                foreach (var feature in featureEnumerable)
                {
                    if (feature == null)
                    {
                        continue;
                    }

                    if (!metaQuestFeatureType.IsInstanceOfType(feature))
                    {
                        continue;
                    }

                    var enabledProperty = metaQuestFeatureType.GetProperty("enabled", BindingFlags.Public | BindingFlags.Instance);
                    if (enabledProperty == null)
                    {
                        return true;
                    }

                    var enabledValue = enabledProperty.GetValue(feature);
                    if (enabledValue is bool enabled)
                    {
                        return enabled;
                    }
                }

                return true;
            }
        }
    }
}
