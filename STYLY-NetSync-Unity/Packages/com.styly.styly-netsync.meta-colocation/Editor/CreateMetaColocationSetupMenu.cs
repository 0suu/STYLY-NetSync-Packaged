// CreateMetaColocationSetupMenu.cs
// Editor helper: creates a minimal setup similar to "dropping" a Meta colocation prefab.

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using Unity.XR.CoreUtils;

namespace Styly.NetSync.MetaColocation.Editor
{
    internal static class CreateMetaColocationSetupMenu
    {
        [MenuItem("GameObject/STYLY NetSync/Meta Colocation Setup", false, 10)]
        private static void Create(MenuCommand menuCommand)
        {
            // ARSession (required for ARAnchorManager subsystem)
            if (Object.FindFirstObjectByType<ARSession>() == null)
            {
                var arSessionGo = new GameObject("AR Session");
                arSessionGo.AddComponent<ARSession>();
                Undo.RegisterCreatedObjectUndo(arSessionGo, "Create AR Session");
            }

            // Ensure XROrigin exists (STYLY XR Rig usually provides it)
            var xrOrigin = Object.FindFirstObjectByType<XROrigin>();
            if (xrOrigin == null)
            {
                Debug.LogWarning("[MetaColocation] No XROrigin found. Create/enable your XR rig (STYLY XR Rig) first, then re-run this menu.");
            }
            else
            {
                if (xrOrigin.GetComponent<ARAnchorManager>() == null)
                {
                    Undo.AddComponent<ARAnchorManager>(xrOrigin.gameObject);
                }
            }

            var go = new GameObject("STYLY NetSync Meta Colocation");
            Undo.RegisterCreatedObjectUndo(go, "Create STYLY NetSync Meta Colocation");
            go.AddComponent<NetSyncMetaColocationController>();

            GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
            Selection.activeObject = go;
        }

        [MenuItem("GameObject/STYLY NetSync/Meta Colocation Setup", true)]
        private static bool ValidateCreate()
        {
            return true;
        }
    }
}
#endif

