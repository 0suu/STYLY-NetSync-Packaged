using UnityEngine;

namespace Styly.NetSync.MetaColocation
{
    public enum MetaColocationRoleMode
    {
        AutoByClientNo,
        Host,
        Client,
    }

    public enum MetaColocationAlignmentMode
    {
        /// <summary>Align X/Z + yaw. Keep height as-is.</summary>
        PositionAndYaw,
        /// <summary>Align position (including height) + yaw.</summary>
        PositionYawAndHeight,
        /// <summary>Align full 6DoF (position + full rotation).</summary>
        Full6Dof,
    }

    [System.Serializable]
    public sealed class MetaColocationNetSyncKeys
    {
        [Tooltip("Global variable key used to broadcast the shared anchors group ID (GUID string).")]
        public string GroupIdGlobalVar = "meta_colocation_group_id";

        [Tooltip("Global variable key used as a flag to indicate whether the host already shared an anchor.")]
        public string AnchorSharedGlobalVar = "meta_colocation_anchor_shared";
    }
}

