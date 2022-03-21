using System.Collections.Generic;
using UnityEngine;

namespace LordAshes
{
    public class Arc_PathBuilder
    {
        public static List<Vector3> MakePath(Vector3 source, Vector3 destination, string parameters, int frames)
        {
            float[] raise = new float[frames];
            float elevation = 0;
            float elevationDelta = (parameters==null | parameters=="") ? 0.25f : float.Parse(parameters);
            for (int p = 0; p < (frames / 2); p++)
            {
                elevation = elevation + elevationDelta;
                elevationDelta = (elevationDelta * 0.9f);
                raise[p] = elevation;
                raise[frames - p - 1] = elevation;
            }
            List<Vector3> path = new List<Vector3>();
            Vector3 delta = (destination - source) / frames;
            for (int f = 0; f < frames; f++)
            {
                Vector3 step = source + (f * delta);
                step.y = step.y + raise[f];
                path.Add(step);
            }
            return path;
        }
    }
}
