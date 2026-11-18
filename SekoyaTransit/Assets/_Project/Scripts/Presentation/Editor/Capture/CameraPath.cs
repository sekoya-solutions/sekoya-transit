#nullable enable
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SekoyaTransit.Presentation.Editor.Capture
{
    /// <summary>
    /// The camera view at any time of a shot (ADR 0036): before the first keyframe and after the last the camera holds
    /// still; between two keyframes it moves with an ease-in-ease-out curve, so every move starts and stops gently.
    /// The zoom changes evenly in scale (it is interpolated on its logarithm), so a zoom from 40 to 10 feels as steady
    /// as one from 10 to 2.5.
    /// </summary>
    public static class CameraPath
    {
        /// <summary>
        /// Gets the view at <paramref name="time"/> seconds.
        /// </summary>
        public static CameraKey Evaluate(IReadOnlyList<CameraKey> keys, float time)
        {
            if (keys == null)
            {
                throw new ArgumentNullException(nameof(keys));
            }

            if (keys.Count == 0)
            {
                throw new ArgumentException("A camera path needs at least one keyframe.", nameof(keys));
            }

            if (time <= keys[0].Time)
            {
                return keys[0];
            }

            for (int index = 1; index < keys.Count; index++)
            {
                CameraKey to = keys[index];
                if (time < to.Time)
                {
                    CameraKey from = keys[index - 1];
                    float linear = (time - from.Time) / (to.Time - from.Time);
                    float eased = linear * linear * (3f - (2f * linear));
                    float zoom = Mathf.Exp(Mathf.Lerp(Mathf.Log(from.Zoom), Mathf.Log(to.Zoom), eased));
                    return new CameraKey(
                        time,
                        Mathf.Lerp(from.TileX, to.TileX, eased),
                        Mathf.Lerp(from.TileY, to.TileY, eased),
                        zoom);
                }
            }

            return keys[keys.Count - 1];
        }
    }
}
