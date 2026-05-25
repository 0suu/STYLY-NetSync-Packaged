using System.Collections.Generic;
using Unity.XR.CoreUtils.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Styly.NetSync.MetaColocation
{
    internal sealed class SharedAnchorTracker
    {
        private readonly HashSet<TrackableId> _sharedTrackableIds = new HashSet<TrackableId>();
        public ARAnchor SharedAnchor { get; private set; }

        public bool HasAnchor => SharedAnchor != null;

        public void OnIncrementalSharedAnchorsLoaded(ReadOnlyListSpan<XRAnchor> xrAnchors)
        {
            for (int i = 0; i < xrAnchors.Count; i++)
            {
                _sharedTrackableIds.Add(xrAnchors[i].trackableId);
            }
        }

        public bool TryPickFromTrackablesChanged(ARTrackablesChangedEventArgs<ARAnchor> args, out ARAnchor anchor)
        {
            anchor = null;
            if (SharedAnchor != null)
            {
                anchor = SharedAnchor;
                return true;
            }

            foreach (var a in args.added)
            {
                if (a == null) { continue; }

                // If we have IDs from the incremental callback, prefer only those anchors.
                if (_sharedTrackableIds.Count > 0 && !_sharedTrackableIds.Contains(a.trackableId))
                {
                    continue;
                }

                SharedAnchor = a;
                anchor = a;
                return true;
            }

            return false;
        }
    }
}

