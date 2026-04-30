using System;
using System.Collections;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Styly.NetSync;

namespace Styly.NetSync.MetaColocation
{
    /// <summary>
    /// Drop-in component that mimics the typical "Meta colocation prefab" flow:
    /// - Host creates a Shared Anchors Group ID and broadcasts it via NetSync.
    /// - Host creates + shares an anchor, then aligns its XROrigin so the anchor becomes the world origin.
    /// - Clients receive the group ID, load shared anchors, and align their XROrigin the same way.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetSyncMetaColocationController : MonoBehaviour
    {
        [Header("Role")]
        [SerializeField] private MetaColocationRoleMode _role = MetaColocationRoleMode.AutoByClientNo;
        [SerializeField, Tooltip("If AutoByClientNo, clientNo==1 becomes host.")]
        private int _hostClientNo = 1;

        [Header("NetSync Keys")]
        [SerializeField] private MetaColocationNetSyncKeys _keys = new MetaColocationNetSyncKeys();

        [Header("Anchor")]
        [SerializeField] private MetaColocationAlignmentMode _alignment = MetaColocationAlignmentMode.PositionYawAndHeight;
        [SerializeField, Tooltip("How often clients retry loading shared anchors until one is found.")]
        private float _loadRetrySeconds = 2.0f;

        [Header("References (auto-resolved if empty)")]
        [SerializeField] private NetSyncManager _netSync;
        [SerializeField] private XROrigin _xrOrigin;
        [SerializeField] private ARAnchorManager _anchorManager;

        private bool _started;
        private bool _aligned;

        private readonly SharedAnchorTracker _tracker = new SharedAnchorTracker();

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();

            if (_anchorManager != null)
            {
                _anchorManager.trackablesChanged.AddListener(OnAnchorsTrackablesChanged);
            }
        }

        private void OnDestroy()
        {
            if (_anchorManager != null)
            {
                _anchorManager.trackablesChanged.RemoveListener(OnAnchorsTrackablesChanged);
            }
            if (_netSync != null)
            {
                _netSync.OnReady.RemoveListener(OnNetSyncReady);
            }
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (_netSync != null)
            {
                _netSync.OnReady.AddListener(OnNetSyncReady);
            }
        }

        private void OnDisable()
        {
            if (_netSync != null)
            {
                _netSync.OnReady.RemoveListener(OnNetSyncReady);
            }
        }

        private void ResolveReferences()
        {
            if (_netSync == null) { _netSync = FindFirstObjectByType<NetSyncManager>(); }
            if (_anchorManager == null) { _anchorManager = FindFirstObjectByType<ARAnchorManager>(); }

            if (_xrOrigin == null)
            {
                _xrOrigin = _anchorManager != null
                    ? _anchorManager.GetComponent<XROrigin>()
                    : FindFirstObjectByType<XROrigin>();
            }
        }

        private void OnNetSyncReady()
        {
            if (_started) { return; }
            _started = true;
            StartCoroutine(RunFlow());
        }

        private IEnumerator RunFlow()
        {
            ResolveReferences();

            if (_netSync == null)
            {
                Debug.LogError("[MetaColocation] NetSyncManager not found.");
                yield break;
            }
            if (_anchorManager == null)
            {
                Debug.LogError("[MetaColocation] ARAnchorManager not found. Add it to your XROrigin.");
                yield break;
            }

            // Wait until the subsystem is up.
            yield return new WaitUntil(() => _anchorManager.subsystem != null);
            yield return null;

            var api = new MetaSharedAnchorsApi(_anchorManager);
            if (!api.TryGetMetaSubsystem(out _))
            {
                Debug.LogError("[MetaColocation] MetaOpenXRAnchorSubsystem not available. " +
                               "Enable OpenXR feature 'AR Foundation Meta Anchor'.");
                yield break;
            }

            var role = ResolveRole();
            Debug.Log($"[MetaColocation] Starting flow. role={role}, clientNo={_netSync.ClientNo}");

            if (role == MetaColocationRoleMode.Host)
            {
                yield return HostRoutine(api);
                yield break;
            }

            yield return ClientRoutine(api);
        }

        private MetaColocationRoleMode ResolveRole()
        {
            if (_role == MetaColocationRoleMode.Host || _role == MetaColocationRoleMode.Client)
            {
                return _role;
            }

            if (_netSync != null && _netSync.ClientNo == _hostClientNo)
            {
                return MetaColocationRoleMode.Host;
            }

            return MetaColocationRoleMode.Client;
        }

        private IEnumerator HostRoutine(MetaSharedAnchorsApi api)
        {
            // 1) Create a per-session group id and broadcast it via NetSync.
            var groupId = Guid.NewGuid();
            var groupIdStr = groupId.ToString();

            if (!api.TrySetSharedAnchorsGroupId(groupId, out var error))
            {
                Debug.LogError("[MetaColocation] " + error);
                yield break;
            }

            _netSync.SetGlobalVariable(_keys.GroupIdGlobalVar, groupIdStr);
            _netSync.SetGlobalVariable(_keys.AnchorSharedGlobalVar, "0");
            Debug.Log($"[MetaColocation] Host set group id: {groupIdStr}");

            // 2) Create an anchor at the host's current XR Origin pose and share it.
            var originT = _xrOrigin != null ? _xrOrigin.transform : null;
            var pose = originT != null
                ? new Pose(originT.position, originT.rotation)
                : new Pose(Vector3.zero, Quaternion.identity);

            var createDone = false;
            UnityEngine.XR.ARSubsystems.Result<ARAnchor> createResult = default;
            api.TryCreateAnchorAsync(pose, r => { createResult = r; createDone = true; });
            yield return new WaitUntil(() => createDone);

            if (!createResult.status.IsSuccess() || createResult.value == null)
            {
                Debug.LogError($"[MetaColocation] Failed to create anchor. status={createResult.status}");
                yield break;
            }

            var anchor = createResult.value;

            var shareDone = false;
            UnityEngine.XR.ARSubsystems.XRResultStatus shareStatus = default;
            api.TryShareAnchorAsync(anchor, s => { shareStatus = s; shareDone = true; });
            yield return new WaitUntil(() => shareDone);

            if (shareStatus.IsError())
            {
                Debug.LogError($"[MetaColocation] Failed to share anchor. status={shareStatus}" +
                               " (If nativeStatusCode=-1000169004, enable Enhanced Spatial Services on Quest.)");
                yield break;
            }

            XrOriginAligner.AlignToAnchor(_xrOrigin, anchor.transform, _alignment);
            _aligned = true;

            _netSync.SetGlobalVariable(_keys.AnchorSharedGlobalVar, "1");
            Debug.Log("[MetaColocation] Host shared anchor successfully.");
        }

        private IEnumerator ClientRoutine(MetaSharedAnchorsApi api)
        {
            // 1) Wait for host to publish the group id.
            string groupIdStr = null;
            while (string.IsNullOrEmpty(groupIdStr))
            {
                groupIdStr = _netSync.GetGlobalVariable(_keys.GroupIdGlobalVar);
                yield return null;
            }

            if (!Guid.TryParse(groupIdStr, out var groupId))
            {
                Debug.LogError($"[MetaColocation] Invalid group id string: {groupIdStr}");
                yield break;
            }

            if (!api.TrySetSharedAnchorsGroupId(groupId, out var error))
            {
                Debug.LogError("[MetaColocation] " + error);
                yield break;
            }

            Debug.Log($"[MetaColocation] Client set group id: {groupIdStr}");

            // 2) Try loading shared anchors until we get one.
            while (!_aligned)
            {
                var sharedFlag = _netSync.GetGlobalVariable(_keys.AnchorSharedGlobalVar, "0");
                if (sharedFlag != "1")
                {
                    yield return new WaitForSeconds(_loadRetrySeconds);
                    continue;
                }

                var loadDone = false;
                UnityEngine.XR.ARSubsystems.XRResultStatus loadStatus = default;
                api.TryLoadAllSharedAnchorsAsync(
                    s => { loadStatus = s; loadDone = true; },
                    _tracker.OnIncrementalSharedAnchorsLoaded);

                yield return new WaitUntil(() => loadDone);

                if (loadStatus.IsError())
                {
                    Debug.LogWarning($"[MetaColocation] TryLoadAllSharedAnchorsAsync failed. status={loadStatus}" +
                                     " (If nativeStatusCode=-1000169004, enable Enhanced Spatial Services on Quest.)");
                }

                if (_aligned) { break; }
                yield return new WaitForSeconds(_loadRetrySeconds);
            }
        }

        private void OnAnchorsTrackablesChanged(ARTrackablesChangedEventArgs<ARAnchor> args)
        {
            if (_aligned) { return; }

            if (_tracker.TryPickFromTrackablesChanged(args, out var anchor))
            {
                XrOriginAligner.AlignToAnchor(_xrOrigin, anchor.transform, _alignment);
                _aligned = true;
                Debug.Log("[MetaColocation] Aligned to shared anchor.");
            }
        }
    }
}
