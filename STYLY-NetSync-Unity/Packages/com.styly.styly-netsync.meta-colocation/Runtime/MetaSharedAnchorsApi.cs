using System;
using System.Collections.Generic;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.OpenXR.Features.Meta;
using ArSerializableGuid = UnityEngine.XR.ARSubsystems.SerializableGuid;

namespace Styly.NetSync.MetaColocation
{
    internal sealed class MetaSharedAnchorsApi
    {
        private readonly ARAnchorManager _anchorManager;

        public MetaSharedAnchorsApi(ARAnchorManager anchorManager)
        {
            _anchorManager = anchorManager;
        }

        public bool TryGetMetaSubsystem(out MetaOpenXRAnchorSubsystem metaSubsystem)
        {
            metaSubsystem = null;

            if (_anchorManager == null) { return false; }
            if (_anchorManager.subsystem == null) { return false; }

            metaSubsystem = _anchorManager.subsystem as MetaOpenXRAnchorSubsystem;
            return metaSubsystem != null;
        }

        public bool TrySetSharedAnchorsGroupId(Guid groupId, out string error)
        {
            error = null;

            if (!TryGetMetaSubsystem(out var metaSubsystem))
            {
                error = "MetaOpenXRAnchorSubsystem not available. Ensure OpenXR feature 'AR Foundation Meta Anchor' is enabled and ARAnchorManager is active.";
                return false;
            }

            metaSubsystem.sharedAnchorsGroupId = new ArSerializableGuid(groupId);
            return true;
        }

        public async void TryCreateAnchorAsync(Pose worldPose, Action<Result<ARAnchor>> callback)
        {
            try
            {
                var result = await _anchorManager.TryAddAnchorAsync(worldPose);
                callback?.Invoke(result);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                callback?.Invoke(new Result<ARAnchor>(new XRResultStatus(XRResultStatus.StatusCode.UnknownError), null));
            }
        }

        public async void TryShareAnchorAsync(ARAnchor anchor, Action<XRResultStatus> callback)
        {
            try
            {
                // Meta extension method (throws if subsystem is not MetaOpenXRAnchorSubsystem).
                var status = await _anchorManager.TryShareAnchorAsync(anchor);
                callback?.Invoke(status);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                callback?.Invoke(new XRResultStatus(XRResultStatus.StatusCode.UnknownError));
            }
        }

        public async void TryLoadAllSharedAnchorsAsync(
            Action<XRResultStatus> callback,
            Action<ReadOnlyListSpan<XRAnchor>> incrementalResultsCallback)
        {
            try
            {
                var loaded = new List<XRAnchor>();
                var status = await _anchorManager.TryLoadAllSharedAnchorsAsync(
                    loaded,
                    incrementalResultsCallback);
                callback?.Invoke(status);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                callback?.Invoke(new XRResultStatus(XRResultStatus.StatusCode.UnknownError));
            }
        }
    }
}
