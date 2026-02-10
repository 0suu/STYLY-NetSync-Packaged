using Unity.XR.CoreUtils;
using UnityEngine;

namespace Styly.NetSync.MetaColocation
{
    internal static class XrOriginAligner
    {
        public static void AlignToAnchor(XROrigin xrOrigin, Transform anchorTransform, MetaColocationAlignmentMode mode)
        {
            if (xrOrigin == null)
            {
                Debug.LogError("[MetaColocation] XROrigin is null.");
                return;
            }
            if (anchorTransform == null)
            {
                Debug.LogError("[MetaColocation] Anchor transform is null.");
                return;
            }

            var anchorPos = anchorTransform.position;
            var anchorRot = anchorTransform.rotation;

            Quaternion deltaRot;
            Vector3 deltaPos;

            switch (mode)
            {
                case MetaColocationAlignmentMode.Full6Dof:
                    deltaRot = Quaternion.Inverse(anchorRot);
                    deltaPos = -(deltaRot * anchorPos);
                    break;
                case MetaColocationAlignmentMode.PositionYawAndHeight:
                    var yawRot = ExtractYaw(anchorRot);
                    deltaRot = Quaternion.Inverse(yawRot);
                    deltaPos = -(deltaRot * anchorPos);
                    break;
                default:
                    var yaw = ExtractYaw(anchorRot);
                    deltaRot = Quaternion.Inverse(yaw);
                    var xz = new Vector3(anchorPos.x, 0f, anchorPos.z);
                    deltaPos = -(deltaRot * xz);
                    break;
            }

            var originT = xrOrigin.transform;
            originT.SetPositionAndRotation(
                deltaRot * originT.position + deltaPos,
                deltaRot * originT.rotation);
        }

        private static Quaternion ExtractYaw(Quaternion rotation)
        {
            var forward = rotation * Vector3.forward;
            forward.y = 0f;

            if (forward.sqrMagnitude < 1e-6f)
            {
                forward = rotation * Vector3.right;
                forward.y = 0f;
            }

            forward.Normalize();
            return Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}

